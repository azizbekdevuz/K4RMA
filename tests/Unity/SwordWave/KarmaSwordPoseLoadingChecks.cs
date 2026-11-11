#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Run baseline and updated builds in the same disposable project. The baseline
// comes from the user's working tree, not HEAD, so existing local work is covered.
[InitializeOnLoad]
public static class KarmaSwordPoseLoadingChecks
{
    const string Key = "K4RMA.SwordPoseLoading";
    const string Folder = "output/sword-pose-loading";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static bool started;
    static int assertions;
    static KarmaGame game;
    static readonly List<string> results = new List<string>();
    [Serializable] public sealed class Sample
    {
        public string key, pose, sprite;
        public Vector3 center, size;
        public List<Vector2> closest = new List<Vector2>();
        public List<bool> overlaps = new List<bool>(), paths = new List<bool>();
    }
    [Serializable] public sealed class Samples { public List<Sample> samples = new List<Sample>(); }
    static KarmaSwordPoseLoadingChecks() { EditorApplication.update += Pump; }
    public static void RunBaseline() { Begin(true); }
    public static void RunBatch() { Begin(false); }
    static void Begin(bool baseline)
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Baseline", baseline);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    static object Get(object target, string name) => Field(target, name).GetValue(target);
    static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static FieldInfo Field(object target, string name)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F); if (field != null) return field; }
        throw new MissingFieldException(name);
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
    static KarmaEnemy Reset(Essence[] order)
    {
        game.StartRun();
        foreach (var essence in order) { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length == 3 ? 3 : order.Length);
        Call(game, "LoadRoom", true);
        var enemy = game.Enemies[0];
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "running", true);
        return enemy;
    }
    static void ResetPose(KarmaCharacterAnimator animator, KarmaEnemy enemy)
    {
        Set(animator, "actionUntil", 0f); Set(animator, "hurtUntil", 0f); Set(animator, "poseUntil", 0f);
        Set(animator, "waveSwingAt", float.PositiveInfinity); Set(animator, "wavePhase", null);
        Set(animator, "wavePhaseSwingAt", float.PositiveInfinity); Set(animator, "waveReleasedUntil", 0f);
        Set(animator, "guardianPreviousPosition", enemy.transform.position);
    }
    static void Apply(KarmaCharacterAnimator animator, KarmaEnemy enemy, string scenario)
    {
        ResetPose(animator, enemy);
        var attack = EnemyAttack.SW02; string action = "Approach"; bool armor = false;
        switch (scenario)
        {
            case "Move": Set(animator, "guardianPreviousPosition", enemy.transform.position - Vector3.right * 0.2f); break;
            case "GuardOutside": armor = true; break;
            case "Windup": action = "Telegraph"; attack = EnemyAttack.SW01; animator.PrepareWaveAttack(0.7f, false); break;
            case "Guard": action = "Telegraph"; animator.PrepareWaveAttack(0.9f, true); break;
            case "Slash":
                action = "Telegraph"; animator.PrepareWaveAttack(0.9f, true);
                Set(animator, "waveSwingAt", Time.time - 0.01f); break;
            case "Wave": action = "Attack"; animator.Attack(Vector2.right); break;
            case "FollowThrough": action = "Recover"; break;
            case "Hurt": action = "Stunned"; break;
            case "ReservedHurt": animator.Hurt(); break;
            case "PhaseWindup": action = "Telegraph"; attack = EnemyAttack.SW03; animator.PlayWavePhase("Windup", 0.9f); break;
            case "PhaseRising": action = "Attack"; attack = EnemyAttack.SW03; animator.PlayWavePhase("Rising"); break;
            case "PhaseRelease": action = "Attack"; attack = EnemyAttack.SW03; animator.PlayWavePhase("Rising"); animator.ReleaseWavePhase(); break;
            case "Spacing": armor = true; break;
            case "Backstep": armor = true; break;
        }
        animator.GuardianPose(action, attack, false, armor);
        if (scenario == "Spacing") animator.ShowGuardianSpacing();
        if (scenario == "Backstep") Call(animator, "ShowSwordRetreat");
    }
    static string Expected(string scenario)
    {
        switch (scenario)
        {
            case "GuardOutside": return "Guard";
            case "ReservedHurt": return "Hurt";
            case "PhaseWindup": return "Windup";
            case "PhaseRising": return "Rising";
            case "PhaseRelease": return "Wave";
            case "Spacing": return "Move";
            default: return scenario;
        }
    }
    static Sample Capture(KarmaEnemy enemy, string key)
    {
        var animator = (KarmaCharacterAnimator)Get(enemy, "animator");
        var bounds = enemy.DamageBounds;
        var sample = new Sample { key = key, pose = animator.CurrentPose,
            sprite = ((SpriteRenderer)Get(animator, "picture")).sprite.name, center = bounds.center, size = bounds.size };
        // Probe the original boundary, not just the center: an accidentally
        // enlarged/shrunken sprite would change these public gameplay queries.
        foreach (Vector2 point in new[] { (Vector2)bounds.center, (Vector2)bounds.min, (Vector2)bounds.max,
            new Vector2(bounds.min.x - 0.01f, bounds.center.y), new Vector2(bounds.max.x + 0.01f, bounds.center.y),
            new Vector2(bounds.center.x, bounds.max.y + 0.01f) })
        {
            sample.closest.Add(enemy.ClosestDamagePoint(point));
            sample.overlaps.Add(enemy.OverlapsDamageArea(point, Vector2.one * 0.005f));
            sample.paths.Add(enemy.IntersectsDamagePath(point + Vector2.up * 0.001f, point - Vector2.up * 0.001f, 0.001f));
        }
        return sample;
    }
    static void Compare(Sample before, Sample after)
    {
        Check(before.key == after.key, "matching sample key");
        Check(Vector3.Distance(before.center, after.center) < 0.0001f && Vector3.Distance(before.size, after.size) < 0.0001f,
            after.key + " original DamageBounds: " + before.center + "/" + before.size + " -> " + after.center + "/" + after.size);
        for (int i = 0; i < before.closest.Count; i++)
        {
            Check(Vector2.Distance(before.closest[i], after.closest[i]) < 0.0001f, after.key + " ClosestDamagePoint " + i);
            Check(before.overlaps[i] == after.overlaps[i], after.key + " OverlapsDamageArea " + i);
            Check(before.paths[i] == after.paths[i], after.key + " IntersectsDamagePath " + i);
        }
    }
    static void Run()
    {
        bool baseline = SessionState.GetBool(Key + ".Baseline", false); int exit = 0;
        Directory.CreateDirectory(Folder);
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); Check(game != null, "real game scene");
            var all = new Samples(); var before = baseline ? null : JsonUtility.FromJson<Samples>(File.ReadAllText(Folder + "/baseline.json"));
            var forms = new[] { new[] { Essence.SwordWave }, new[] { Essence.SwordWave, Essence.RisingSlash },
                new[] { Essence.SwordWave, Essence.Guard }, new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard } };
            string[] names = { "Wave", "WaveRise", "WaveWard", "WaveFinal" };
            string[] scenarios = { "Idle", "Move", "GuardOutside", "Windup", "Guard", "Slash", "Wave", "FollowThrough", "Hurt",
                "ReservedHurt", "PhaseWindup", "PhaseRising", "PhaseRelease", "Spacing", "Backstep", "Idle" };
            for (int form = 0; form < forms.Length; form++)
            {
                var enemy = Reset(forms[form]); var animator = (KarmaCharacterAnimator)Get(enemy, "animator");
                all.samples.Add(Capture(enemy, names[form] + "/Spawn"));
                if (!baseline) Compare(before.samples[all.samples.Count - 1], all.samples[all.samples.Count - 1]);
                var poses = (Dictionary<string, Sprite>)Get(animator, "poses");
                Check(KarmaArtwork.Settings.spriteLibrary.All("Wave").Length == 0, "shared library has no Wave poses");
                Check(KarmaArtwork.Sprites("Wave").Length == 12, "owned library supplies twelve real Wave poses");
                Check(poses.Count == (form == 0 ? baseline ? 0 : 12 : 16), names[form] + " expected loaded pose count");
                var expectedSprites = new Dictionary<string, Sprite>();
                foreach (var sprite in form == 0 ? KarmaArtwork.Sprites("Wave") : KarmaArtwork.InheritedPoses(names[form])) expectedSprites[sprite.name] = sprite;
                foreach (int facing in new[] { -1, 1 }) foreach (float scale in new[] { 1f, 0.85f })
                {
                    enemy.transform.position = new Vector3(20 + facing * 2, KarmaSpace.GuardianGroundY + (scale == 1 ? 0 : 2), 0);
                    var art = (Transform)Get(enemy, "art"); art.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale * scale, KarmaSpace.GuardianArtScale * scale, 1);
                    foreach (string scenario in scenarios)
                    {
                        Apply(animator, enemy, scenario);
                        string key = names[form] + "/" + facing + "/" + scale + "/" + scenario;
                        var sample = Capture(enemy, key); all.samples.Add(sample);
                        if (!baseline) Compare(before.samples[all.samples.Count - 1], sample);
                        Call(art.GetComponent<KarmaWaveMotion>(), "AdvanceMotion", 1f / 30);
                        Compare(sample, Capture(enemy, key));
                        if (!baseline || form > 0)
                        {
                            string expected = Expected(scenario); Check(animator.CurrentPose == expected, key + " logical pose " + sample.pose);
                            string displayed = expected == "Backstep" || scenario == "GuardOutside" && form > 0 ? form > 0 ? "Idle" : "Guard" : expected;
                            var picture = (SpriteRenderer)Get(animator, "picture");
                            Check(picture.sprite == expectedSprites[displayed], key + " exact own-form Sprite identity");
                            Check(picture.sprite.texture == expectedSprites["Idle"].texture, key + " no cross-form/body substitution");
                        }
                    }
                }
                if (!baseline)
                {
                    var last = Capture(enemy, names[form] + "/repeat-configure");
                    Call(animator, "ConfigureSwordPresentation");
                    Apply(animator, enemy, "Idle");
                    Compare(last, Capture(enemy, last.key));
                }
                results.Add("PASS: " + names[form] + " " + (baseline && form == 0 ? "reproduced empty pose dictionary / static Idle" : "pose transitions and exact own-form sprites") + "; spawn + 64 bounds/contact samples");
            }
            if (!baseline) Check(all.samples.Count == before.samples.Count, "all baseline samples compared");
            File.WriteAllText(Folder + (baseline ? "/baseline.json" : "/updated.json"), JsonUtility.ToJson(all, true));
            results.Insert(0, "PASS: " + assertions + " assertions; " + all.samples.Count + " samples; " + (baseline ? "pre-fix reproduction" : "four-form poses and unchanged damage queries"));
        }
        catch (Exception e) { results.Insert(0, "FAIL: " + e); Debug.LogException(e); exit = 1; }
        File.WriteAllLines(Folder + (baseline ? "/baseline-result.txt" : "/result.txt"), results);
        SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
