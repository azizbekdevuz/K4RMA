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
    public static void RunCharacterBatch() { Begin(false); SessionState.SetBool(Key + ".CharacterOnly", true); }
    static void Begin(bool baseline)
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Baseline", baseline);
        SessionState.SetBool(Key + ".CharacterOnly", false);
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
    static object Property(object target, string name) => target.GetType().GetProperty(name, F).GetValue(target);
    static void CharacterChecks(KarmaEnemy enemy, string form)
    {
        var animator = (KarmaCharacterAnimator)Get(enemy, "animator");
        var picture = (SpriteRenderer)Get(animator, "picture");
        var art = (Transform)Get(enemy, "art");
        var motion = art.GetComponent<KarmaWaveMotion>();
        var poses = (Dictionary<string, Sprite>)Get(animator, "poses");
        var frames = (Sprite[])Get(animator, "sw02Frames");
        Check(frames != null && frames.Length == 6, form + " six approved motion keyframes");
        var rootScale = enemy.transform.localScale;
        var baseScale = (Vector3)Get(animator, "pictureScale");
        var idleColor = (Color)Get(animator, "pictureColor");
        var material = picture.sharedMaterial;
        var home = (Vector3)Get(animator, "pictureHome");
        float sole = (float)Get(animator, "swordReferenceSole");
        foreach (float x in new[] { KarmaStage.BossLeftWall, KarmaStage.ArenaCenter, KarmaStage.BossRightWall })
        foreach (int facing in new[] { -1, 1 })
        {
            enemy.transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0);
            art.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            Vector3 rootPosition = enemy.transform.position, artScale = art.localScale;
            Apply(animator, enemy, "Idle");
            Check(picture.sprite == poses["Idle"] && picture.transform.localScale == baseScale, form + " exact approved Idle");
            Check(picture.color == idleColor && picture.sharedMaterial == material, form + " authored Idle color and material");
            var originalDamage = Capture(enemy, "body-only");
            for (int i = 0; i < frames.Length; i++)
            {
                Call(animator, "ApplySwordBodyFrame", i);
                Check(picture.sprite == frames[i] && Vector3.Distance(picture.transform.localScale, baseScale) < 0.00001f,
                    form + " own-form body calibration " + i);
                var color = picture.color;
                Check(color == idleColor && color.a == idleColor.a && picture.sharedMaterial == material,
                    form + " Idle-referenced sheet palette without alpha/material changes " + i);
                Call(animator, "ApplySwordBodyFrame", i);
                Check(picture.color == color, form + " repeated sampling cannot accumulate tint " + i);
                var foot = (Vector3)Property(animator, "SwordFoot");
                var registered = (Vector3)Property(animator, "SwordVisualHome");
                Check(Mathf.Abs(registered.y + foot.y - home.y - sole) < 0.00001f, form + " exact registered sole " + i);
                Call(motion, "AdvanceMotion", 1f / 60);
                Vector3 planted = picture.transform.localPosition + picture.transform.localRotation * foot;
                Check(Mathf.Abs(planted.y - home.y - sole) < 0.0001f, form + " rotation preserves ground contact " + i);
                Compare(originalDamage, Capture(enemy, "body-only"));
            }
            foreach (string gesture in new[] { "BasicMelee", "BasicRanged", "Volley", "Heavy", "Counter", "UltimateStart",
                "UltimateHold", "UltimateCommand", "UltimateCharge", "UltimateFinish", "UltimateRecover" })
            {
                ResetPose(animator, enemy);
                Call(animator, "CancelSwordBodyPresentation");
                EnemyAttack attack = gesture.StartsWith("Ultimate") ? EnemyAttack.SW05 : gesture == "Counter" ? EnemyAttack.SW04
                    : gesture == "Volley" ? EnemyAttack.SW01 : gesture == "Heavy" ? EnemyAttack.SW02 : EnemyAttack.SWBasic;
                Set(enemy, "selected", attack);
                Call(animator, "PrepareSwordBody", gesture, 0.6f, 0);
                animator.GuardianPose("Telegraph", attack, false, false);
                Check(picture.color == idleColor && picture.sharedMaterial == material,
                    form + "/" + gesture + " preparation appearance");
                if (gesture == "UltimateStart" || gesture == "UltimateHold" || gesture == "UltimateCharge" || gesture == "UltimateRecover")
                {
                    Set(animator, "swordBodyAt", Time.time - 0.4f);
                    animator.GuardianPose("Telegraph", attack, false, false);
                    int phaseFrame = gesture == "UltimateHold" ? 0 : gesture == "UltimateRecover" ? 5 : 1;
                    Check(picture.sprite == frames[phaseFrame], form + "/" + gesture + " distinct sustained phase");
                    Call(animator, "CancelSwordBodyPresentation");
                    continue;
                }
                var before = Capture(enemy, "event");
                Call(animator, "ReleaseSwordBody", gesture, 0);
                Check(picture.sprite == frames[3], form + "/" + gesture + " release visible in the same call");
                Check(picture.color == idleColor, form + "/" + gesture + " synchronous release appearance");
                Compare(before, Capture(enemy, "event"));
                Call(animator, "ReleaseSwordBody", gesture, 1);
                Check(picture.sprite == frames[2], form + "/" + gesture + " alternating connected second strike");
                animator.GuardianPose("Attack", attack, false, false);
                Check(picture.sprite == frames[2], form + "/" + gesture + " release survives next pose update");
                Set(animator, "swordBodyReleasedAt", Time.time - 4.25f / 24);
                Set(animator, "swordBodyAt", Time.time - 4.25f / 24);
                animator.GuardianPose("Attack", attack, false, false);
                if (gesture != "UltimateHold" && gesture != "UltimateRecover")
                    Check(picture.sprite == frames[4], form + "/" + gesture + " sampled follow-through");
                Call(animator, "CancelSwordBodyPresentation");
                animator.GuardianPose("Approach", attack, false, false);
                Check(picture.sprite == poses["Idle"] && picture.transform.localScale == baseScale,
                    form + "/" + gesture + " interrupt restores approved baseline");
                Check(picture.color == idleColor && picture.sharedMaterial == material,
                    form + "/" + gesture + " return to exact Idle palette and material");
            }
            ResetPose(animator, enemy);
            Call(animator, "PrepareSwordBody", "Summon", 9f / 24, 0);
            animator.GuardianPose("Approach", EnemyAttack.SW03, false, false);
            Check(picture.sprite == frames[1], form + " brief SW03 command");
            animator.ShowGuardianSpacing();
            Check(picture.sprite == poses["Move"] && !(bool)Property(animator, "SwordBodyVisible"), form + " spacing overrides summon");
            Call(animator, "ShowSwordRetreat");
            Check(animator.CurrentPose == "Backstep" && picture.sprite == frames[0], form + " dodge overrides summon");
            Set(animator, "swordBodyAt", Time.time - 10f / 24);
            animator.GuardianPose("Approach", EnemyAttack.SW03, false, false);
            Check(picture.sprite == poses["Idle"], form + " summon expires without a gameplay wait");
            Call(animator, "PrepareSwordBody", "Counter", 0.6f, 0);
            Call(animator, "ReleaseSwordBody", "Counter", 0);
            animator.GuardianPose("Stunned", EnemyAttack.SW04, false, false);
            Check(Property(animator, "SwordBodyAction") == null && picture.sprite == poses["Hurt"], form + " parry stun cancels counter display");
            Call(animator, "CancelSwordBodyPresentation");
            animator.GuardianPose("Approach", EnemyAttack.SW04, false, false);
            Check(picture.sprite == poses["Idle"], form + " recovery after stun");
            Check(enemy.transform.position == rootPosition && enemy.transform.localScale == rootScale && art.localScale == artScale,
                form + " root travel, root scale and facing untouched at arena edge");
        }
        BasicAndAscentChecks(enemy, animator, picture, frames, form);
        LocomotionChecks(enemy, animator, picture, form);
        foreach (float seconds in new[] { 0f, 0.01f, 0.041f, 0.042f, 0.1f, 0.59f })
            Check(Mathf.Abs((float)Call(animator, "SwordFrameTime", seconds) * 24 - Mathf.Floor(seconds * 24)) < 0.00001f,
                "24fps sampling without changing simulation time");
        Call(animator, "PrepareSwordBody", "UltimateCharge", 0.6f, 0);
        typeof(KarmaEnemy).GetProperty("Health", F).SetValue(enemy, 0f);
        animator.Die();
        Check(Property(animator, "SwordBodyAction") == null && animator.CurrentPose == "Death"
            && picture.transform.localScale == baseScale, form + " death clears old body action and retains authored death artwork scale");
        results.Add("PASS: " + form + " registered six-frame family, all body actions, synchronous two strikes, SW03 mobility, interruption, both facings and arena edges");
    }
    static void LocomotionChecks(KarmaEnemy enemy, KarmaCharacterAnimator animator, SpriteRenderer picture, string form)
    {
        ResetPose(animator, enemy);
        animator.GuardianPose("Idle", EnemyAttack.SWBasic, false, false);
        var frames = (Sprite[])Get(animator, "swordWalkFrames");
        Check(frames != null && frames.Length == 8, form + " eight own-form walking drawings");
        var root = enemy.transform.position;
        var damage = Property(animator, "GuardianDamageSprite");
        var damageScale = Property(animator, "GuardianDamageScale");
        float stride = (float)Get(animator, "swordStride") * Mathf.Abs(animator.transform.lossyScale.x);
        foreach (int hz in new[] { 15, 24, 60, 144 })
        foreach (int facing in new[] { -1, 1 })
        {
            float phase = 0.137f;
            for (int i = 0; i < hz; i++)
                phase = (float)Call(animator, "AdvanceSwordGait", phase, facing * stride / hz, stride, facing, false);
            Check(Mathf.Abs(phase - 0.137f) < 0.0001f, form + " distance-driven gait " + hz);
            float turned = (float)Call(animator, "AdvanceSwordGait", phase, 0f, stride, -facing, true);
            Check(Mathf.Abs(turned - 0.637f) < 0.0001f, form + " mirrored supporting leg");
            float back = (float)Call(animator, "AdvanceSwordGait", turned, 0f, stride, facing, true);
            Check(Mathf.Abs(back - phase) < 0.0001f, form + " rapid double turn preserves phase");
        }
        for (int i = 0; i < 8; i++)
        {
            Set(animator, "swordWalkFacing", 1);
            Set(animator, "swordWalkPhase", i / 8f);
            Check((bool)Call(animator, "SampleSwordLocomotion", stride * 0.001f, 1f / 24, 1, true), form + " walking sample");
            Check(picture.sprite == frames[i], form + " distinct walking frame " + i);
            Check(Equals(damage, Property(animator, "GuardianDamageSprite")) && Equals(damageScale, Property(animator, "GuardianDamageScale")), form + " damage registration unchanged");
        }
        Check(!(bool)Call(animator, "SampleSwordLocomotion", stride, 1f / 24, 1, true), form + " relocation rejected");
        Check(!(bool)Call(animator, "SampleSwordLocomotion", 0.01f, 1f / 24, 1, false), form + " airborne yields");
        animator.GuardianPose("Attack", EnemyAttack.SW05, false, false);
        Check(!(bool)Call(animator, "SampleSwordLocomotion", 0.01f, 1f / 24, 1, true), form + " attack yields");
        Check(enemy.transform.position == root, form + " locomotion cannot move root");
    }
    static void BasicAndAscentChecks(KarmaEnemy enemy, KarmaCharacterAnimator animator,
        SpriteRenderer picture, Sprite[] frames, string form)
    {
        foreach (bool melee in new[] { true, false })
        foreach (int facing in new[] { -1, 1 })
        foreach (int hz in new[] { 30, 60, 120 })
        {
            ResetPose(animator, enemy);
            Call(animator, "CancelSwordBodyPresentation");
            Set(enemy, "selected", EnemyAttack.SWBasic);
            var art = (Transform)Get(enemy, "art");
            art.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            animator.PrepareBasicAttack(0.4f, melee);
            animator.GuardianPose("Telegraph", EnemyAttack.SWBasic, false, false);
            Check(picture.sprite == frames[0], form + " compact preparation");
            Set(animator, "swordBodyAt", Time.time - 0.375f);
            animator.GuardianPose("Telegraph", EnemyAttack.SWBasic, false, false);
            Check(picture.sprite == frames[melee ? 1 : 0], form + " distinct melee/ranged preparation");
            var damage = Capture(enemy, "basic-release");
            float active = melee ? 3f / 24 : 2f / 24;
            animator.BeginBasicRelease(active);
            Check(picture.sprite == frames[melee ? 2 : 3], form + " immediate real release");
            Compare(damage, Capture(enemy, "basic-release"));
            for (int sample = 0; sample <= hz; sample++)
            {
                // Avoid exact float subtraction boundaries; the active edge is
                // separately bracketed below rather than rounded by a test.
                float age = sample / (float)hz + 0.001f;
                Set(animator, "basicReleasedAt", Time.time - age);
                animator.GuardianPose("Attack", EnemyAttack.SWBasic, false, false);
                int expected = melee ? (age < 1f / 24 ? 2 : age < 2f / 24 ? 3 : 4) : age < active ? 3 : age < active + 3f / 24 ? 4 : 5;
                Check(picture.sprite == frames[expected], form + " event-relative frames " + hz + "Hz " + age);
            }
            foreach (float age in new[] { active - 0.001f, active + 0.001f, 0.8f })
            {
                Set(animator, "basicReleasedAt", Time.time - age);
                animator.GuardianPose("Attack", EnemyAttack.SWBasic, false, false);
                Check(picture.sprite == frames[melee ? (age < 1f / 24 ? 2 : age < 2f / 24 ? 3 : 4) : age < active ? 3 : age < active + 3f / 24 ? 4 : 5],
                    form + " contact edge and large frame step");
            }
            if (melee)
            {
                animator.BeginBasicRelease(active, 1);
                Check(picture.sprite == frames[4], form + " return starts at down-cut endpoint");
                foreach (float age in new[] {0.001f, 1.1f/24, 2.1f/24, 6.1f/24, 0.8f})
                {
                    Set(animator, "basicReleasedAt", Time.time-age);
                    animator.GuardianPose("Attack", EnemyAttack.SWBasic, false, false);
                    int expected = age < 1f/24 ? 4 : age < 2f/24 ? 3 : age < 5f/24 ? 2 : 0;
                    Check(picture.sprite == frames[expected], form + " return cut and ready recovery");
                }
            }
            // Recovery must not replay an already completed follow-through.
            animator.EndGuardianAction();
            animator.GuardianPose("Recover", EnemyAttack.SWBasic, false, false);
            Check(picture.sprite == frames[melee ? 0 : 5], form + " no recovery rewind");
            Call(animator, "CancelSwordBodyPresentation");
            var cancelled = picture.sprite;
            animator.BeginBasicRelease(active);
            Check(picture.sprite == cancelled && Property(animator, "SwordBodyAction") == null,
                form + " stale basic release rejected");
        }
        ResetPose(animator, enemy);
        Set(enemy, "selected", EnemyAttack.SW05);
        foreach (string phase in new[] { "UltimateStart", "UltimateAscent", "UltimateHold" })
        {
            Call(animator, "PrepareSwordBody", phase, 0.7f, 0);
            foreach (float progress in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                Set(animator, "swordBodyAt", Time.time - progress * 0.7f);
                Call(animator, "SampleSwordAscent", progress);
                animator.PlayWavePhase(phase == "UltimateStart" ? "Windup" : phase == "UltimateAscent" ? "Rising" : "Guard");
                animator.GuardianPose("Attack", EnemyAttack.SW05, true, false);
                int expected = phase == "UltimateHold" || phase == "UltimateStart" && progress == 0 ? 0 : 1;
                Check(picture.sprite == frames[expected], form + " SW05 phase survives legacy pose " + phase);
            }
        }
        Call(animator, "ReleaseSwordBody", "UltimateFinish", 0);
        Set(animator, "swordBodyReleasedAt", Time.time - 6.25f / 24);
        animator.GuardianPose("Attack", EnemyAttack.SW05, true, false);
        Check(picture.sprite == frames[5], form + " detonation settles before descent");
        Call(animator, "PrepareSwordBody", "UltimateRecover", 0.7f, 0);
        animator.PlayWavePhase("Rising");
        animator.GuardianPose("Attack", EnemyAttack.SW05, true, false);
        Check(picture.sprite == frames[5], form + " descent does not rewind detonation");
        Call(animator, "EndSwordBodyAction");
        animator.GuardianPose("Recover", EnemyAttack.SW05, false, false);
        Check(picture.sprite == frames[5], form + " landing does not rewind descent");
        animator.GuardianPose("Recover", EnemyAttack.SW05, false, false);
        var poses = (Dictionary<string, Sprite>)Get(animator, "poses");
        Check(picture.sprite == poses["Idle"], form + " SW05 recovery reaches Idle");
        animator.GuardianPose("Idle", EnemyAttack.SW05, false, false);
        Check(!(bool)Property(animator, "SwordBodyVisible"), form + " stale ultimate cannot override Idle");
        animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
        Check(!(bool)Property(animator, "SwordBodyVisible"), form + " stale ultimate cannot override another skill");
        Call(animator, "CancelSwordBodyPresentation");
    }
    static void Run()
    {
        bool baseline = SessionState.GetBool(Key + ".Baseline", false); int exit = 0;
        bool characterOnly = SessionState.GetBool(Key + ".CharacterOnly", false);
        Directory.CreateDirectory(Folder);
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); Check(game != null, "real game scene");
            var all = new Samples(); var before = baseline || characterOnly ? null : JsonUtility.FromJson<Samples>(File.ReadAllText(Folder + "/baseline.json"));
            var forms = new[] { new[] { Essence.SwordWave }, new[] { Essence.SwordWave, Essence.RisingSlash },
                new[] { Essence.SwordWave, Essence.Guard }, new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard } };
            string[] names = { "Wave", "WaveRise", "WaveWard", "WaveFinal" };
            string[] scenarios = { "Idle", "Move", "GuardOutside", "Windup", "Guard", "Slash", "Wave", "FollowThrough", "Hurt",
                "ReservedHurt", "PhaseWindup", "PhaseRising", "PhaseRelease", "Spacing", "Backstep", "Idle" };
            for (int form = 0; form < forms.Length; form++)
            {
                var enemy = Reset(forms[form]); var animator = (KarmaCharacterAnimator)Get(enemy, "animator");
                if (characterOnly) { CharacterChecks(enemy, names[form]); continue; }
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
                            bool registeredGuard = expected == "Backstep" || expected == "Guard";
                            var approved = registeredGuard ? ((Sprite[])Get(animator, "sw02Frames"))[0]
                                : expectedSprites[expected == "FollowThrough" ? "Idle" : displayed];
                            Check(picture.sprite == approved, key + " exact approved own-form Sprite identity");
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
            if (!baseline && !characterOnly) Check(all.samples.Count == before.samples.Count, "all baseline samples compared");
            File.WriteAllText(Folder + (baseline ? "/baseline.json" : "/updated.json"), JsonUtility.ToJson(all, true));
            results.Insert(0, "PASS: " + assertions + " assertions; " + all.samples.Count + " samples; " + (baseline ? "pre-fix reproduction" : "four-form poses and unchanged damage queries"));
        }
        catch (Exception e) { results.Insert(0, "FAIL: " + e); Debug.LogException(e); exit = 1; }
        File.WriteAllLines(Folder + (baseline ? "/baseline-result.txt" : "/result.txt"), results);
        SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
