using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Included in the standalone validation build. Launch with -karmaFrameReport <file>.
public sealed class KarmaFramePacingProbe : MonoBehaviour
{
    static string report;
    static bool activeCombat;
    int ticks;
    readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-karmaFrameReport");
        activeCombat = Array.IndexOf(args, "-karmaCombatReport") >= 0;
        if (activeCombat) index = Array.IndexOf(args, "-karmaCombatReport");
        if (index < 0 || index + 1 >= args.Length) return;
        report = args[index + 1];
        new GameObject("Standalone frame pacing probe").AddComponent<KarmaFramePacingProbe>();
    }

    readonly HashSet<string> masterMotions = new HashSet<string>();
    SpriteRenderer endingMaster;
    void ObserveMaster()
    {
        if (endingMaster == null || !endingMaster.enabled || endingMaster.sprite == null) return;
        masterMotions.Add(endingMaster.sprite.texture.name);
    }

    void FixedUpdate() => ticks++;

    IEnumerator Start()
    {
        Application.runInBackground = true;
        Debug.Log($"Display probe: focused={Application.isFocused}, refresh={Screen.currentResolution.refreshRateRatio}, " +
            $"vSync={QualitySettings.vSyncCount}, targetFrameRate={Application.targetFrameRate}, fixedDeltaTime={Time.fixedDeltaTime}");
        var game = FindAnyObjectByType<KarmaGame>();
        if (activeCombat)
        {
            yield return MeasureActiveCombat(game);
            yield break;
        }
        File.WriteAllText(report, "phase,frame,real_seconds,interval_ms,fixed_updates,time_scale\n");
        yield return Measure(game, RunPhase.Title, "TitleCold");
        yield return Measure(game, RunPhase.Title);
        game.StartRun();
        game.Player.ToggleTestInvincibility();
        yield return Measure(game, RunPhase.Combat);
        // Enter the ending through final-room defeat so the guardian mesh and camera are present.
        foreach (var essence in new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash })
        {
            game.Player.Essences.Preview(essence);
            game.Player.Essences.Confirm(out _);
        }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        typeof(KarmaGame).GetMethod("LoadRoom", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true });
        var guardian = game.Enemies[0];
        game.EnemyDefeated(guardian, guardian.transform.Find("Artwork"));
        var ending = FindAnyObjectByType<KarmaEndingPresentation>();
        endingMaster = ending.transform.Find("Master").GetComponent<SpriteRenderer>();
        yield return Measure(game, RunPhase.Ending);
        double deadline = Time.realtimeSinceStartupAsDouble + 90;
        while (!game.EndingCompleted && Time.realtimeSinceStartupAsDouble < deadline)
        {
            yield return endOfFrame;
            ObserveMaster();
        }
        bool finished = game.EndingCompleted;
        var failures = new List<string>();
        var results = new List<string>();
        foreach (var motion in new[] {"Breath", "MasterLift", "MasterTeach", "Reply", "Farewell"})
        {
            results.Add("Master/" + motion + " visible " + masterMotions.Contains(motion));
            if (!masterMotions.Contains(motion)) failures.Add("Missing ending master motion " + motion);
        }
        if (!finished) failures.Add("Ending timeline did not finish");
        foreach (var name in KarmaPlayerTimeline.Motions.Select(m => m.Name).Concat(new[] { "Dash", "AirSlash", "Transition" }))
        {
            var motion = KarmaPlayerTimeline.Find(name);
            var actual = motion.Load();
            var library = Resources.Load<KarmaSpriteLibrary>(KarmaPlayerTimeline.Clips + "Frames");
            var clip = Resources.Load<AnimationClip>(KarmaPlayerTimeline.Clips + name);
            bool pass = clip != null && clip.frameRate == 12 && actual.SequenceEqual(library.All(name));
            if (!pass) failures.Add(name);
            results.Add(name + " " + actual.Length + " samples " + (pass ? "OK" : "FAIL"));
        }
        int oldResources = Resources.LoadAll("Player/Animation24").Length + Resources.LoadAll("Player/Native24").Length;
        if (oldResources != 0) failures.Add("Old Resources still present");
        int missing = 0;
        var libraries = Resources.LoadAll<KarmaSpriteLibrary>("BossArtwork").Concat(new[] {
            KarmaArtwork.Settings.spriteLibrary, Resources.Load<KarmaSpriteLibrary>("Player/Animation/Frames"),
            Resources.Load<KarmaSpriteLibrary>(KarmaPlayerTimeline.Clips + "Frames")
        });
        foreach (var library in libraries)
            foreach (var sheet in library.sheets)
                for (int i = 0; i < sheet.sprites.Length; i++)
                    if (sheet.sprites[i] == null) { missing++; failures.Add(library.name + "/" + sheet.id + "/" + i); }
        foreach (string form in new[] { "WaveRise", "WaveWard", "RiseWave", "RiseWard", "WardRise", "WardWave", "WaveFinal", "RiseFinal", "WardFinal" })
        {
            var poses = KarmaArtwork.InheritedPoses(form);
            if (poses.Length != 16) failures.Add(form);
            results.Add(form + " inherited poses " + poses.Length);
        }
        results.Add("Old Resources " + oldResources + ", null registered sprites " + missing + ", ending completed " + finished);
        results.AddRange(failures);
        results.Add(failures.Count == 0 ? "PASS" : "FAIL");
        File.WriteAllLines(Path.ChangeExtension(report, ".checks.txt"), results);
        Application.Quit(failures.Count == 0 ? 0 : 1);
    }

    IEnumerator MeasureActiveCombat(KarmaGame game)
    {
        // Include the operation and its first presented frame, not just the steady state afterwards.
        var rows = new List<string>();
        rows.Add("section,frame,real_seconds,interval_ms,fixed_updates,time_scale,player_x,guardian_x,player_pose,guardian_pose,fight_seconds");
        var keyboard = InputSystem.AddDevice<Keyboard>();
        bool finished = false;
        try
        {
            yield return endOfFrame;
            double previous = Time.realtimeSinceStartupAsDouble;
            int previousTicks = ticks;
            game.StartRun();
            game.Player.ToggleTestInvincibility();
            yield return endOfFrame;
            void Record(string section, int frame, KarmaCharacterAnimator actor, KarmaCharacterAnimator boss)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                var enemy = game.Enemies[0];
                rows.Add(FormattableString.Invariant($"{section},{frame},{now:F5},{(now-previous)*1000:F3},{ticks-previousTicks},{Time.timeScale:F3},{game.Player.Position.x:F4},{enemy.Position.x:F4},{actor.CurrentPose},{boss.CurrentPose},{enemy.FightSeconds:F4}"));
                previous = now;
                previousTicks = ticks;
            }
            var player = game.Player.GetComponentInChildren<KarmaCharacterAnimator>(true);
            var guardian = game.Enemies[0].GetComponentInChildren<KarmaCharacterAnimator>(true);
            Record("StartRun", 0, player, guardian);
            foreach (var essence in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard })
            {
                game.Player.Essences.Preview(essence);
                game.Player.Essences.Confirm(out _);
            }
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
            typeof(KarmaGame).GetMethod("LoadRoom", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true });
            guardian = game.Enemies[0].GetComponentInChildren<KarmaCharacterAnimator>(true);
            yield return endOfFrame;
            Record("EnterWaveFinal", 0, player, guardian);
            double combatStart = previous;
            for (int i = 0; previous - combatStart < 25; i++)
            {
                if (game.Phase != RunPhase.Combat || game.Enemies.Count == 0) throw new InvalidOperationException("Combat ended before capture");
                bool approach = game.Enemies[0].Position.x >= game.Player.Position.x;
                int sample = (int)((previous - combatStart) * 12);
                var move = (sample % 48 < 32) == approach ? Key.D : Key.A;
                int step = sample % 36;
                var keys = step < 3 ? new KeyboardState(move, Key.Space)
                    : step < 15 ? new KeyboardState(move, Key.J) : new KeyboardState(move);
                InputSystem.QueueStateEvent(keyboard, keys);
                yield return endOfFrame;
                Record("Combat", i + 1, player, guardian);
            }
            var args = Environment.GetCommandLineArgs();
            int captureIndex = Array.IndexOf(args, "-karmaSkillFrames");
            if (captureIndex >= 0 && captureIndex + 1 < args.Length)
            {
                string directory = args[captureIndex + 1];
                Directory.CreateDirectory(directory);
                game.Enemies[0].gameObject.SetActive(false);
                game.Player.ResetForRoom(KarmaStage.Spawn, true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int i = 0; i < 6; i++) yield return endOfFrame;
                var capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                float previousCaptureStep = Time.captureDeltaTime;
                Time.captureDeltaTime = KarmaBootstrap.Step;
                for (int i = 0; i < 32; i++)
                {
                    var keys = i == 3 ? new KeyboardState(Key.Space)
                        : i == 23 ? new KeyboardState(Key.L) : new KeyboardState();
                    InputSystem.QueueStateEvent(keyboard, keys);
                    yield return endOfFrame;
                    capture.ReadPixels(new Rect(0, 0, capture.width, capture.height), 0, 0, false);
                    File.WriteAllBytes(Path.Combine(directory, i.ToString("D3") + ".jpg"), capture.EncodeToJPG(85));
                }
                Destroy(capture);
                Time.captureDeltaTime = previousCaptureStep;
            }
            finished = true;
        }
        finally
        {
            File.WriteAllLines(report, rows);
            InputSystem.RemoveDevice(keyboard);
            Application.Quit(finished ? 0 : 1);
        }
    }

    IEnumerator Measure(KarmaGame game, RunPhase expected, string label = null)
    {
        // Start on a completed render. Count later renders and actual FixedUpdate calls
        // over the same monotonic wall-clock window, including GC and resource loads.
        yield return endOfFrame;
        double start = Time.realtimeSinceStartupAsDouble, previous = start;
        int initialTicks = ticks;
        var rows = new List<string>();
        do
        {
            yield return endOfFrame;
            if (expected == RunPhase.Ending) ObserveMaster();
            double now = Time.realtimeSinceStartupAsDouble;
            if (game.Phase != expected) throw new InvalidOperationException("Unexpected phase " + game.Phase);
            rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F6},{3:F3},{4},{5:F3}",
                label ?? expected.ToString(), rows.Count + 1, now - start, (now - previous) * 1000, ticks - initialTicks, Time.timeScale));
            previous = now;
        }
        while (previous - start < 10);
        File.AppendAllLines(report, rows);
    }
}
