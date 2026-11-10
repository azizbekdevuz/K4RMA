#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using KarmaPrototype;

public static partial class KarmaGuardPlayChecks
{
    const string FlightReviewKey = "K4RMA.Guard.GD03.PresentationReview";
    const string FlightOutput = "output/gd03-review";
    static bool flightReviewRunning, recordingFlight;
    static MediaEncoder flightEncoder;
    static RenderTexture flightTarget;
    static Texture2D flightPixels;
    static float flightRecordingAt, largestCaptureGap, lastCaptureAt;
    static int recordedFlightFrames;

    [InitializeOnLoadMethod]
    static void InstallFlightReview() { EditorApplication.update += PumpFlightReview; }
    [MenuItem("K4RMA/방어형/GD03 외형/기존 외형 비교 캡처")]
    public static void RequestFlightBaseline() { RequestFlightReview("before"); }
    [MenuItem("K4RMA/방어형/GD03 외형/변경 후 영향 검사와 영상")]
    public static void RequestFlightValidation() { RequestFlightReview("after"); }
    [MenuItem("K4RMA/방어형/GD03 외형/최종형 정리와 프레임 측정")]
    public static void RequestFlightLifecycle() { RequestFlightReview("lifecycle"); }
    static void RequestFlightReview(string mode)
    {
        if (flightReviewRunning) return;
        SessionState.SetString(FlightReviewKey, mode);
        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
    }
    static void PumpFlightReview()
    {
        string mode = SessionState.GetString(FlightReviewKey, "");
        if (string.IsNullOrEmpty(mode) || flightReviewRunning) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || Time.frameCount < 2) return;
        SessionState.EraseString(FlightReviewKey);
        if (mode == "lifecycle") ReviewFlightLifecycle();
        else ReviewFlight(mode == "after");
    }
    static async void ReviewFlight(bool verify)
    {
        flightReviewRunning = true;
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        var original = game.Config;
        var config = UnityEngine.Object.Instantiate(original);
        Set(game, "config", config);
        config.playerHealth = 100000;
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        assertions = 0; runtimeError = null;
        Application.logMessageReceived += CaptureError;
        Directory.CreateDirectory(FlightOutput);
        string mode = verify ? "after" : "before";
        try
        {
            EditorApplication.isPaused = false;
            int firstFrame = Time.frameCount;
            await Task.Delay(80);
            Check(Time.frameCount > firstFrame, "live player loop advances");
            flightTarget = new RenderTexture(1280, 720, 24);
            flightPixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            if (verify)
            {
                flightEncoder = new MediaEncoder(FlightOutput + "/GD03-hand-continuity.mp4",
                    new VideoTrackAttributes { width = 1280, height = 720, frameRate = new MediaRational(24), includeAlpha = false });
                recordedFlightFrames = 0; largestCaptureGap = 0;
                flightRecordingAt = lastCaptureAt = Time.realtimeSinceStartup;
                recordingFlight = true;
                game.StartCoroutine(RecordFlightFrames());
            }
            var orders = new[] {
                new[] { Essence.Guard, Essence.SwordWave },
                new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash },
                new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave }
            };
            foreach (var order in orders)
            foreach (int facing in verify ? new[] { -1, 1 } : new[] { 1 })
            {
                Reset(order);
                string form = order.Length == 2 ? "WardWave" : "WardFinal";
                string label = form + "-" + (order[1] == Essence.SwordWave ? "wave-first" : "rise-first") + "-" + facing;
                Place(new Vector2(20 + facing * 12, 12));
                Force(EnemyAttack.GD03, facing);
                var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
                var picture = Get<SpriteRenderer>(animator, "picture");
                await Seconds(0.08f);
                if (verify) Check(!animator.CurrentPose.StartsWith("GD03/ShieldAway"), "shield stays in hand during tell: " + label);
                FlightShot(mode + "-" + label + "-tell");
                await Until(() => WholeShield() != null, 2);
                // Reproduce the original hand appearance without changing the flight rules.
                if (!verify) animator.SetGuardThrownShield(null);
                float shieldStartedX = WholeShield().transform.position.x;
                await Seconds(0.05f);
                if (verify)
                {
                    Check((WholeShield().transform.position.x - shieldStartedX) * facing > 0, "outbound shield moves in the visible facing direction: " + label);
                    CheckFlightPose(animator, picture, form, "ShieldBash");
                }
                FlightShot(mode + "-" + label + "-flight");
                if (!verify) continue;
                Vector3 scale = picture.transform.localScale, position = picture.transform.localPosition;
                enemy.TakeDamage(1, enemy.Position + Vector2.left * facing * 3);
                await Seconds(0.03f);
                CheckFlightPose(animator, picture, form, "Hurt");
                FlightShot(mode + "-" + label + "-hurt");
                Check(scale == picture.transform.localScale && position == picture.transform.localPosition, "hurt preserves picture transform");
                await Until(() => WholeShield() != null && WholeShield().name.EndsWith("복귀"), 3);
                CheckFlightPose(animator, picture, form, "ShieldBash");
                await Until(() => !Get<bool>(enemy, "running"), 4);
                Check(Get<SpriteRenderer>(animator, "guardThrownShield") == null && !animator.CurrentPose.StartsWith("GD03/ShieldAway"), "normal catch restores hand: " + label);
                FlightShot(mode + "-" + label + "-catch");

                Place(new Vector2(20 + facing * 12, KarmaSpace.PlayerHalfHeight));
                Force(EnemyAttack.GD03, facing);
                await Until(() => WholeShield() != null, 2);
                await Until(() => Mathf.Abs(WholeShield().transform.position.x - game.Player.Position.x) < 4, 3);
                Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
                await Until(() => WholeShield() != null && WholeShield().name.EndsWith("반사"), 1);
                CheckFlightPose(animator, picture, form, "ShieldBash");
                Check(!enemy.AutoShieldActive, "parry still breaks GD01 immediately");
                FlightShot(mode + "-" + label + "-reflected");
                await Until(() => enemy.Stunned, 2);
                await Seconds(0.03f);
                Check(Get<SpriteRenderer>(animator, "guardThrownShield") == null && animator.CurrentPose.StartsWith("Stagger/"), "impact restores the existing stagger form");
                FlightShot(mode + "-" + label + "-stun");
                Call(enemy, "InterruptPattern");
                Check(Get<SpriteRenderer>(animator, "guardThrownShield") == null, "interrupt is idempotent after impact");
            }
            if (verify)
            {
                StopFlightRecording();
                Check(recordedFlightFrames > 24, "actual gameplay video contains advancing frames");
                Check(runtimeError == null, "GD03 presentation emits no combat runtime errors: " + runtimeError);
                string appearanceResult = "PASS: " + assertions + " GD03 presentation assertions; video frames=" + recordedFlightFrames
                    + "; largest capture interval=" + largestCaptureGap.ToString("F3") + "s";
                File.WriteAllText(FlightOutput + "/presentation-result.txt", appearanceResult);
                string contacts = await RunGD03Only();
                File.WriteAllText(FlightOutput + "/result.txt", appearanceResult + "\n" + contacts + "\n");
                Debug.Log(appearanceResult + "\n" + contacts);
            }
            else { File.WriteAllText(FlightOutput + "/baseline-result.txt", "PASS: original hand appearance reproduced in live GD03 tell and flight captures for both inherited forms and both inheritance orders.\n"); Debug.Log("PASS: GD03 baseline captures complete"); }
            Check(runtimeError == null, "GD03 presentation emits no combat runtime errors: " + runtimeError);
        }
        catch (Exception error)
        { File.WriteAllText(FlightOutput + "/" + mode + "-failure.txt", error.ToString()); Debug.LogException(error); }
        finally
        {
            recordingFlight = false; StopFlightRecording();
            if (flightTarget != null) UnityEngine.Object.Destroy(flightTarget);
            if (flightPixels != null) UnityEngine.Object.Destroy(flightPixels);
            Application.logMessageReceived -= CaptureError;
            if (game.Paused) game.TogglePause();
            game.ReturnToTitle(); Set(game, "config", original); UnityEngine.Object.Destroy(config);
            Application.runInBackground = background;
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
            flightReviewRunning = false;
        }
    }
    static async void ReviewFlightLifecycle()
    {
        flightReviewRunning = true;
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        var original = game.Config;
        var config = UnityEngine.Object.Instantiate(original);
        Set(game, "config", config); config.playerHealth = 100000;
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        assertions = 0; runtimeError = null;
        Application.logMessageReceived += CaptureError;
        try
        {
            EditorApplication.isPaused = false;
            int initialFrame = Time.frameCount;
            await Task.Delay(100);
            Check(Time.frameCount > initialFrame, "live player loop advances before sampling");
            string measurements = "Live Editor sampling; no video encoder; not a standalone-build benchmark.\n";
            foreach (var order in new[] { new[] { Essence.Guard, Essence.SwordWave }, new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash } })
            foreach (bool shieldless in new[] { false, true })
            {
                Reset(order); Place(new Vector2(34, 12)); Force(EnemyAttack.GD03, 1);
                await Until(() => WholeShield() != null, 2);
                var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
                if (!shieldless) animator.SetGuardThrownShield(null);
                await Seconds(0.06f);
                int frame = Time.frameCount, samples = 0;
                float total = 0, maximum = 0, end = enemy.FightSeconds + 0.6f;
                await Until(() => {
                    if (Time.frameCount != frame)
                    { frame = Time.frameCount; samples++; total += Time.unscaledDeltaTime; maximum = Mathf.Max(maximum, Time.unscaledDeltaTime); }
                    return enemy.FightSeconds >= end;
                }, 3);
                string form = order.Length == 2 ? "WardWave" : "WardFinal";
                long textureBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(KarmaArtwork.Sprite("GuardShieldless" + form, "ShieldBash").texture);
                Check(samples > 1 && WholeShield() != null, "performance sample observes real moving GD03 frames");
                measurements += form + (shieldless ? " shieldless" : " original") + ": samples=" + samples + "; mean-ms=" + (1000 * total / samples).ToString("F2")
                    + "; max-ms=" + (1000 * maximum).ToString("F2") + "; sibling-texture-bytes=" + textureBytes + "\n";
            }
            foreach (var order in new[] { new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash }, new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave } })
                await CheckShieldCancellation(order);
            File.WriteAllText(FlightOutput + "/frame-measurements.txt", measurements);
            Check(runtimeError == null, "final-form cleanup emits no combat runtime errors: " + runtimeError);
            string result = "PASS: " + assertions + " final-form interruption/death and frame sampling assertions\n" + measurements;
            File.WriteAllText(FlightOutput + "/lifecycle-result.txt", result);
            Debug.Log(result);
        }
        catch (Exception error)
        { File.WriteAllText(FlightOutput + "/lifecycle-failure.txt", error.ToString()); Debug.LogException(error); }
        finally
        {
            Application.logMessageReceived -= CaptureError;
            if (game.Paused) game.TogglePause();
            game.ReturnToTitle(); Set(game, "config", original); UnityEngine.Object.Destroy(config);
            Application.runInBackground = background;
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
            flightReviewRunning = false;
        }
    }
    static void CheckFlightPose(KarmaCharacterAnimator animator, SpriteRenderer picture, string form, string pose)
    {
        // Check the rendered world-space direction, not just the requested attack direction.
        float visibleHeading = picture.transform.TransformVector(Vector3.right).x * (picture.flipX ? -1 : 1);
        Check(visibleHeading * enemy.Facing > 0, "visible shieldless facing matches throw direction: " + form + "/" + pose);
        Sprite expected = KarmaArtwork.Sprite("GuardShieldless" + form, pose);
        Check(expected != null && picture.sprite == expected && animator.CurrentPose == "GD03/ShieldAway/" + form + "/" + pose, "visible shieldless " + form + "/" + pose);
        var original = Array.Find(KarmaArtwork.InheritedPoses(form), sprite => sprite.name == pose);
        Check(expected.rect == original.rect && expected.pivot == original.pivot && Mathf.Approximately(expected.pixelsPerUnit, original.pixelsPerUnit), "slice, pivot and PPU preserved: " + form + "/" + pose);
        Check(expected.bounds == original.bounds, "source pose bounds preserved");
        var damagePose = Array.Find(KarmaArtwork.InheritedPoses(form), sprite => sprite.name == (pose == "Hurt" ? "Hurt" : "Thrust"));
        var actualDamageBounds = enemy.DamageBounds;
        // Compare against Unity's renderer bounds for the original gameplay pose, without advancing a frame.
        picture.sprite = damagePose;
        var originalDamageBounds = picture.bounds;
        picture.sprite = expected;
        Check((actualDamageBounds.center - originalDamageBounds.center).sqrMagnitude < 0.000001f
            && (actualDamageBounds.size - originalDamageBounds.size).sqrMagnitude < 0.000001f,
            "old GD03 damage bounds preserved while the shield arm is shown");
    }
    static void RenderFlightFrame()
    {
        var camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = flightTarget; camera.Render(); RenderTexture.active = flightTarget;
            flightPixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); flightPixels.Apply();
        }
        finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; }
    }
    static void FlightShot(string label)
    { RenderFlightFrame(); File.WriteAllBytes(FlightOutput + "/" + label + ".png", flightPixels.EncodeToPNG()); }
    static IEnumerator RecordFlightFrames()
    {
        float nextAt = Time.realtimeSinceStartup;
        while (recordingFlight)
        {
            yield return new WaitForEndOfFrame();
            float now = Time.realtimeSinceStartup;
            if (now < nextAt || !recordingFlight) continue;
            nextAt = now + 1f / 24;
            RenderFlightFrame();
            flightEncoder.AddFrame(flightPixels, new MediaTime((long)((now - flightRecordingAt) * 1000000), 1000000, 1));
            largestCaptureGap = Mathf.Max(largestCaptureGap, now - lastCaptureAt); lastCaptureAt = now;
            recordedFlightFrames++;
        }
    }
    static void StopFlightRecording()
    { recordingFlight = false; if (flightEncoder != null) { flightEncoder.Dispose(); flightEncoder = null; } }
}
#endif