#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using KarmaPrototype;
using KarmaPrototype.EditorTools;

public static partial class KarmaGuardPlayChecks
{
    const string SlamReviewKey = "K4RMA.Guard.GD04.FeedbackReview";
    const string SlamReviewOutput = "output/gd04-review";
    static bool slamReviewRunning;
    [InitializeOnLoadMethod]
    static void InstallSlamReview() { EditorApplication.update += PumpSlamReview; }
    [MenuItem("K4RMA/방어형/GD04 피드백/변경 전 실행 비교")]
    static void RequestSlamBefore() { RequestSlamReview("before"); }
    [MenuItem("K4RMA/방어형/GD04 피드백/변경 후 영향 검사와 영상")]
    static void RequestSlamAfter() { RequestSlamReview("after"); }
    [MenuItem("K4RMA/방어형/GD04 피드백/짧은 프레임과 메모리 비교")]
    static void RequestSlamSampling() { RequestSlamReview("sample"); }
    static void RequestSlamReview(string mode)
    {
        if (slamReviewRunning) return;
        KarmaBossPractice.Stop();
        SessionState.SetString(SlamReviewKey, mode);
        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
    }
    static void PumpSlamReview()
    {
        string mode = SessionState.GetString(SlamReviewKey, "");
        if (string.IsNullOrEmpty(mode) || slamReviewRunning) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || Time.frameCount < 2) return;
        SessionState.EraseString(SlamReviewKey);
        ReviewSlam(mode == "after", mode == "sample");
    }
    static async void ReviewSlam(bool verify, bool sampleOnly = false)
    {
        slamReviewRunning = true;
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        var original = game.Config;
        var config = UnityEngine.Object.Instantiate(original);
        Set(game, "config", config); config.playerHealth = 100000;
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        assertions = 0; runtimeError = null;
        Application.logMessageReceived += CaptureError;
        Directory.CreateDirectory(SlamReviewOutput);
        string mode = sampleOnly ? "sample" : verify ? "after" : "before";
        try
        {
            EditorApplication.isPaused = false;
            int frame = Time.frameCount;
            await Task.Delay(100);
            Check(Time.frameCount > frame, "GD04 runs on a live player loop");
            if (sampleOnly)
            {
                string samples = await SampleSlamFrames();
                Check(runtimeError == null, "GD04 short sampling has no runtime errors: " + runtimeError);
                File.WriteAllText(SlamReviewOutput + "/sample-result.txt", samples); Debug.Log(samples);
                return;
            }
            flightTarget = new RenderTexture(1280, 720, 24);
            flightPixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            flightEncoder = new MediaEncoder(SlamReviewOutput + "/GD04-" + mode + ".mp4",
                new VideoTrackAttributes { width = 1280, height = 720, frameRate = new MediaRational(24), includeAlpha = false });
            recordedFlightFrames = 0; largestCaptureGap = 0;
            flightRecordingAt = lastCaptureAt = Time.realtimeSinceStartup;
            recordingFlight = true; game.StartCoroutine(RecordFlightFrames());
            var orders = new[] {
                new[] { Essence.Guard, Essence.RisingSlash },
                new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash },
                new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave }
            };
            foreach (var order in orders)
            foreach (int facing in new[] { -1, 1 })
            {
                Reset(order);
                Place(new Vector2(20 + facing * 8, KarmaSpace.PlayerHalfHeight));
                var player = game.Player;
                var body = player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Dynamic;
                Set(player, "invulnerableUntil", 0f);
                await Seconds(0.04f);
                var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
                var playerAnimator = Get<Transform>(player, "art").GetComponent<KarmaCharacterAnimator>();
                string label = (order.Length == 2 ? "WardRise" : order[1] == Essence.SwordWave ? "Final-wave" : "Final-rise") + "-" + facing;
                float health = player.Health; int parries = player.ParryCount;
                Force(EnemyAttack.GD04, facing);
                await Seconds(0.15f); SlamReviewShot(mode + "-" + label + "-tell");
                await Until(() => SlamFan() != null, 2);
                await Seconds(0.06f); SlamReviewShot(mode + "-" + label + "-rise");
                player.BeginCounter();
                await Until(() => player.Health < health, 1);
                Check(player.Stunned && player.Airborne && body.linearVelocity.y > 0 && player.ParryCount == parries,
                    "GD04 launches and stuns without consuming a parry: " + label);
                await Seconds(0.22f);
                if (verify) Check(player.Stunned && player.Airborne, "existing physical airborne state remains active: " + label);
                SlamReviewShot(mode + "-" + label + "-airborne");
                await Until(() => !Get<bool>(enemy, "running"), 3);
                Set(enemy, "timer", float.MaxValue);
                Check(SlamFan() == null, "GD04 finishes the entire rise-hold-lower sequence: " + label);
                await Until(() => !player.Airborne && !player.Stunned, 3);
                await Seconds(0.04f);
                if (verify) Check(playerAnimator.CurrentPose != "Hurt", "landing returns to the existing locomotion poses: " + label);
                SlamReviewShot(mode + "-" + label + "-landed");
            }
            StopFlightRecording();
            if (verify) await CheckExtendedSlamReach();
            Check(runtimeError == null, "GD04 review emits no runtime errors: " + runtimeError);
            string result = "PASS: " + assertions + " GD04 live review assertions; recorded frames=" + recordedFlightFrames
                + "; largest capture interval=" + largestCaptureGap.ToString("F3") + "s";
            if (verify) result += "\nPlayer airborne pose connection deferred: common files were not approved.\n" + await RunGD04Only();
            File.WriteAllText(SlamReviewOutput + "/" + mode + "-result.txt", result);
            Debug.Log(result);
        }
        catch (Exception error)
        { File.WriteAllText(SlamReviewOutput + "/" + mode + "-failure.txt", error.ToString()); Debug.LogException(error); }
        finally
        {
            StopFlightRecording();
            if (flightTarget != null) UnityEngine.Object.Destroy(flightTarget);
            if (flightPixels != null) UnityEngine.Object.Destroy(flightPixels);
            Application.logMessageReceived -= CaptureError;
            if (game.Paused) game.TogglePause();
            game.ReturnToTitle(); Set(game, "config", original); UnityEngine.Object.Destroy(config);
            Application.runInBackground = background;
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
            slamReviewRunning = false;
        }
    }
    static async Task<string> SampleSlamFrames()
    {
        var tuning = game.Config.BossTuning.guard;
        float range = tuning.slamRange, height = tuning.slamHeight, final = tuning.slamFinalHeight;
        string result = "Short Editor samples with all three slabs raised; no video encoder; not a standalone benchmark.\n";
        try
        {
            foreach (bool enlarged in new[] { false, true })
            {
                tuning.slamRange = enlarged ? range : 20;
                tuning.slamHeight = enlarged ? height : 2.4f;
                tuning.slamFinalHeight = enlarged ? final : 7.2f;
                Reset(new[] { Essence.Guard, Essence.RisingSlash });
                Place(new Vector2(34, 12)); Force(EnemyAttack.GD04, 1);
                await Until(() => SlamFan() != null, 2);
                await Seconds(0.66f);
                int frame = Time.frameCount, count = 0;
                float total = 0, maximum = 0, end = enemy.FightSeconds + 0.12f;
                await Until(() => {
                    if (Time.frameCount != frame)
                    { frame = Time.frameCount; count++; total += Time.unscaledDeltaTime; maximum = Mathf.Max(maximum, Time.unscaledDeltaTime); }
                    return enemy.FightSeconds >= end;
                }, 1);
                Check(count > 1 && SlamFan() != null, "sample observes live raised ground");
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(KarmaArtwork.Sprite("GuardSlamFan", "Impact").texture);
                result += (enlarged ? "expanded" : "original size") + ": samples=" + count + "; mean-ms=" + (1000 * total / count).ToString("F2")
                    + "; max-ms=" + (1000 * maximum).ToString("F2") + "; texture-bytes=" + bytes + "\n";
                Call(enemy, "InterruptPattern");
            }
        }
        finally { tuning.slamRange = range; tuning.slamHeight = height; tuning.slamFinalHeight = final; }
        return result;
    }
    static async Task CheckExtendedSlamReach()
    {
        foreach (int facing in new[] { -1, 1 })
        foreach (bool inside in new[] { true, false })
        {
            Reset(new[] { Essence.Guard, Essence.RisingSlash });
            float bossX = facing < 0 ? 35 : 15;
            enemy.transform.position = new Vector3(bossX, KarmaSpace.GuardianGroundY, 0);
            Place(new Vector2(bossX + facing * (inside ? 22 : 26), KarmaSpace.PlayerHalfHeight));
            Set(game.Player, "invulnerableUntil", 0f);
            float health = game.Player.Health;
            Force(EnemyAttack.GD04, facing);
            await Until(() => SlamFan() != null, 2);
            var slab = SlamFan();
            await Seconds(0.10f);
            float floorY = (slab.sprite.rect.height * 0.15f - slab.sprite.pivot.y) / slab.sprite.pixelsPerUnit;
            Check(Mathf.Abs(slab.transform.TransformPoint(new Vector3(0, floorY, 0)).y - 0.05f) < 0.03f,
                "GD04 vertical rise reaches the authored floor alignment within 0.10s");
            await Until(() => !Get<bool>(enemy, "running"), 3);
            Check(inside ? game.Player.Health < health && game.Player.Stunned : game.Player.Health == health,
                "GD04 expanded range hits beyond the old range and misses beyond the new range: " + facing + "/" + inside);
        }
    }
    static void SlamReviewShot(string label)
    { RenderFlightFrame(); File.WriteAllBytes(SlamReviewOutput + "/" + label + ".png", flightPixels.EncodeToPNG()); }
}
#endif
