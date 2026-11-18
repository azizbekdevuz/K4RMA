using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;

// Included in the standalone validation build. Launch with -karmaFrameReport <file>.
public sealed class KarmaFramePacingProbe : MonoBehaviour
{
    static string report;
    int ticks;
    readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-karmaFrameReport");
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
        var game = FindAnyObjectByType<KarmaGame>();
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
