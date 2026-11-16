#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

public static partial class KarmaGuardPlayChecks
{
    const string AppearanceKey = "K4RMA.Guard.AppearanceAudit";
    const string AppearanceOutput = "output/validation/guard-appearance";
    static bool appearanceRunning;
    static readonly List<AppearanceSample> appearanceSamples = new List<AppearanceSample>();
    static readonly List<string> appearanceIssues = new List<string>();
    static Camera appearanceCamera;
    static RenderTexture appearanceTarget;
    static Texture2D appearancePixels;
    static AppearanceAudit appearanceBaseline;
    static int appearanceBoundsChecks;
    static RenderTexture appearanceVideoTarget;
    static Texture2D appearanceVideoPixels;
    static bool appearanceDomeOnly, appearanceHeavyOnly;

    [Serializable] sealed class AppearanceSample
    {
        public string form, route, phase, label, pose, source, sprite, image;
        public int facing, frame;
        public float ppu;
        public Rect rect;
        public Vector2 pivot;
        public Vector3 localScale, worldScale, rootScale, localPosition, worldPosition, worldSize;
        public Color color;
        public Bounds damageBounds;
    }
    [Serializable] sealed class AppearanceAudit
    {
        public List<AppearanceSample> samples;
        public List<string> issues;
        public string coverage;
    }
    [InitializeOnLoadMethod]
    static void InstallAppearanceAudit() { EditorApplication.update += PumpAppearanceAudit; }
    public static void RunAppearanceAuditBatch()
    {
        SessionState.SetBool(AppearanceKey, true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        EditorApplication.EnterPlaymode();
    }
    public static void RunHeavyAppearanceBatch()
    {
        SessionState.SetBool(AppearanceKey + ".HeavyOnly", true);
        RunAppearanceAuditBatch();
    }
    public static void RunAppearanceGD05Batch()
    {
        SessionState.SetBool(AppearanceKey + ".DomeOnly", true);
        RunAppearanceAuditBatch();
    }
    public static void RunMoltenAppearanceBatch()
    {
        SessionState.SetBool(BatchKey + ".MoltenOnly", true);
        RunBatch();
    }
    static void PumpAppearanceAudit()
    {
        if (!SessionState.GetBool(AppearanceKey, false) || appearanceRunning) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || Time.frameCount < 2) return;
        SessionState.SetBool(AppearanceKey, false); appearanceRunning = true;
        AuditGuardAppearance();
    }
    static string AppearanceForm(Essence[] order) => order.Length >= 3 ? "WardFinal" : order.Length == 1 ? "Ward"
        : order[1] == Essence.SwordWave ? "WardWave" : "WardRise";
    static void CleanAppearance(KarmaCharacterAnimator animator, int facing)
    {
        Set(animator, "deathAt", -1f); Set(animator, "empowermentUntil", 0f); Set(animator, "hurtUntil", 0f);
        Set(animator, "guardThrownShield", null); Set(animator, "guardTrialShield", null); Set(animator, "groundSlamFrame", -1);
        animator.EndHeavyStrike();
        animator.transform.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
        animator.transform.localPosition = Vector3.zero;
        Set(animator, "guardianPreviousPosition", animator.transform.position);
        var picture = Get<SpriteRenderer>(animator, "picture");
        picture.transform.localScale = Get<Vector3>(animator, "pictureScale");
        picture.transform.localPosition = Get<Vector3>(animator, "pictureHome");
        picture.transform.localRotation = Quaternion.identity;
        picture.color = Get<Color>(animator, "pictureColor");
    }
    static void AppearanceShot(KarmaCharacterAnimator animator, string form, string route, string phase, string label, int facing, bool render)
    {
        var picture = Get<SpriteRenderer>(animator, "picture");
        var sprite = picture.sprite;
        if (sprite == null) { appearanceIssues.Add(route + "/" + label + ": missing sprite"); return; }
        var row = new AppearanceSample {
            form = form, route = route, phase = phase, label = label, facing = facing, frame = Time.frameCount,
            pose = animator.CurrentPose, source = AssetDatabase.GetAssetPath(sprite), sprite = sprite.name,
            rect = sprite.rect, pivot = sprite.pivot, ppu = sprite.pixelsPerUnit,
            localScale = picture.transform.localScale, worldScale = picture.transform.lossyScale,
            rootScale = animator.transform.localScale, localPosition = picture.transform.localPosition,
            worldPosition = picture.transform.position, worldSize = picture.bounds.size, color = picture.color
        };
        row.damageBounds = (Bounds)Call(animator, "GuardDamageBounds", picture);
        if (appearanceBaseline != null && phase == "Static" && (label.StartsWith("GD02_") || label.StartsWith("GD05_Heavy")))
        {
            var before = appearanceBaseline.samples.Find(s => s.route == route && s.facing == facing && s.phase == phase && s.label == label);
            if (before == null) appearanceIssues.Add(route + "/" + label + ": missing pre-change damage baseline");
            else
            {
                appearanceBoundsChecks++;
                if ((row.damageBounds.size - before.worldSize).sqrMagnitude > 0.00001f)
                    appearanceIssues.Add(route + "/" + label + ": original attack damage bounds changed");
            }
        }
        if (phase == "Static" && (label.StartsWith("GD02_") || label.StartsWith("GD05_Heavy")))
        {
            var idle = appearanceSamples.Find(s => s.route == route && s.phase == "Static" && s.label == "Idle" && s.facing == facing);
            if (idle == null || (row.localScale - idle.localScale * (row.ppu / (idle.ppu * 1.35f))).sqrMagnitude > 0.00001f)
                appearanceIssues.Add(route + "/" + label + ": heavy artwork pixel density differs from idle calibration");
        }
        if (Mathf.Abs(Mathf.Abs(row.worldScale.x) - Mathf.Abs(row.worldScale.y)) > 0.0001f)
            appearanceIssues.Add(route + "/" + label + ": nonuniform body transform " + row.worldScale);
        if (phase != "Revival" && form != "Ward" && row.source.EndsWith("/Forms/Ward.png"))
            appearanceIssues.Add(route + "/" + label + ": inherited form fell back to base Ward artwork");
        if (render)
        {
            row.image = route + "_" + label + ".png";
            int layer = picture.gameObject.layer;
            var previous = RenderTexture.active;
            try
            {
                picture.gameObject.layer = 31;
                appearanceCamera.transform.position = new Vector3(enemy.Position.x, 8, -10);
                appearanceCamera.Render(); RenderTexture.active = appearanceTarget;
                appearancePixels.ReadPixels(new Rect(0, 0, appearancePixels.width, appearancePixels.height), 0, 0); appearancePixels.Apply();
                File.WriteAllBytes(AppearanceOutput + "/" + row.image, appearancePixels.EncodeToPNG());
            }
            finally { picture.gameObject.layer = layer; RenderTexture.active = previous; }
        }
        appearanceSamples.Add(row);
    }
    static void StaticAppearancePass(Essence[] order, string route, int facing)
    {
        string form = AppearanceForm(order);
        var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
        var heavy = Get<object>(animator, "heavyStrike");
        bool render = facing > 0;
        void Pose(string label, string action, EnemyAttack attack, bool armored = false, bool move = false, bool hurt = false)
        {
            CleanAppearance(animator, facing);
            if (move) Set(animator, "guardianPreviousPosition", animator.transform.position - Vector3.right * 0.1f);
            if (hurt) animator.Hurt();
            animator.GuardianPose(action, attack, false, armored);
            AppearanceShot(animator, form, route, "Static", label, facing, render);
        }
        Pose("Idle", "Approach", EnemyAttack.GroundStrike);
        Pose("Guard", "Approach", EnemyAttack.GroundStrike, true);
        Pose("Move", "Approach", EnemyAttack.GroundStrike, false, true);
        Pose("MoveGuard", "Approach", EnemyAttack.GroundStrike, true, true);
        Pose("BasicTell", "Telegraph", EnemyAttack.GroundStrike);
        Pose("BasicTellGuard", "Telegraph", EnemyAttack.GroundStrike, true);
        Pose("BasicAttack", "Attack", EnemyAttack.GroundStrike);
        Pose("Recover", "Recover", EnemyAttack.GroundStrike);
        Pose("RecoverGuard", "Recover", EnemyAttack.GroundStrike, true);
        Pose("Hurt", "Approach", EnemyAttack.GroundStrike, false, false, true);
        Pose("Stunned", "Stunned", EnemyAttack.GroundStrike);
        float hp = enemy.Health;
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.25f);
        Pose("EnragedIdle", "Approach", EnemyAttack.GroundStrike);
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, hp);
        void Heavy(string prefix, bool away)
        {
            for (int heat = 0; heat < 2; heat++) for (int frame = 0; frame < 3; frame++)
            {
                CleanAppearance(animator, facing);
                GameObject dummy = null;
                if (away) { dummy = new GameObject("Audit flying trial shield"); Set(animator, "guardTrialShield", dummy.AddComponent<SpriteRenderer>()); }
                Set(heavy, "active", true); Set(heavy, "swinging", frame > 0); Set(heavy, "heated", heat > 0);
                Set(heavy, "swingAt", Time.time - (frame == 2 ? 0.1f : 0));
                animator.GuardianPose("Attack", away ? EnemyAttack.GD05 : EnemyAttack.GD02, false, false);
                AppearanceShot(animator, form, route, "Static", prefix + (heat > 0 ? "Hot" : "Cool") + frame, facing, render);
                if (dummy != null) UnityEngine.Object.DestroyImmediate(dummy);
            }
        }
        Heavy("GD02_", false);
        if (Array.IndexOf(order, Essence.SwordWave) >= 0)
        {
            Pose("GD03_Tell", "Telegraph", EnemyAttack.GD03);
            foreach (bool hurt in new[] { false, true })
            {
                CleanAppearance(animator, facing);
                var dummy = new GameObject("Audit thrown shield");
                animator.SetGuardThrownShield(dummy.AddComponent<SpriteRenderer>());
                if (hurt) animator.Hurt();
                animator.GuardianPose("Attack", EnemyAttack.GD03, false, false);
                AppearanceShot(animator, form, route, "Static", hurt ? "GD03_FlightHurt" : "GD03_Flight", facing, render);
                animator.SetGuardThrownShield(null);
                AppearanceShot(animator, form, route, "Static", hurt ? "GD03_CatchHurt" : "GD03_CatchImmediate", facing, render);
                UnityEngine.Object.DestroyImmediate(dummy);
            }
            Pose("GD03_CatchSettled", "Approach", EnemyAttack.GD03);
        }
        if (Array.IndexOf(order, Essence.RisingSlash) >= 0)
            for (int frame = 0; frame < 4; frame++)
            {
                CleanAppearance(animator, facing); animator.SetGroundSlamFrame(frame);
                AppearanceShot(animator, form, route, "Static", "GD04_" + frame, facing, render);
            }
        if (order.Length == 3)
        {
            foreach (string action in new[] { "Idle", "Move", "Hurt", "Stunned" })
            {
                CleanAppearance(animator, facing);
                var dummy = new GameObject("Audit trial shield away"); Set(animator, "guardTrialShield", dummy.AddComponent<SpriteRenderer>());
                if (action == "Hurt") animator.Hurt();
                if (action == "Move") Set(animator, "guardianPreviousPosition", animator.transform.position - Vector3.right * 0.1f);
                animator.GuardianPose(action == "Stunned" ? "Stunned" : "Approach", EnemyAttack.GD05, false, false);
                AppearanceShot(animator, form, route, "Static", "GD05_" + action, facing, render);
                UnityEngine.Object.DestroyImmediate(dummy);
            }
            Heavy("GD05_Heavy", true);
            Pose("GD05_ShieldReturned", "Approach", EnemyAttack.GD05);
        }
        CleanAppearance(animator, facing); animator.Die(); animator.Pose(0, false);
        AppearanceShot(animator, form, route, "Static", "Death", facing, render);
        string previousForm = order.Length == 1 ? "Neutral" : order.Length == 2 ? "Ward" : order[1] == Essence.SwordWave ? "WardWave" : "WardRise";
        var load = typeof(KarmaCharacterAnimator).GetMethod("LoadRevivalFrames", BindingFlags.Static | BindingFlags.NonPublic);
        var before = (Sprite[])load.Invoke(null, new object[] { previousForm });
        foreach (float t in new[] { 0.05f, 0.18f, 0.30f, 0.40f, 0.50f, 0.70f, 0.85f, 0.96f })
        {
            CleanAppearance(animator, facing);
            animator.Empower(order[order.Length - 1], 2, before.Length > 0 ? before[0] : null, previousForm == "Neutral", previousForm);
            Set(animator, "empowermentAt", Time.time - t * 2); Set(animator, "empowermentUntil", Time.time + (1 - t) * 2);
            animator.GuardianPose("Approach", EnemyAttack.GroundStrike, false, false);
            AppearanceShot(animator, form, route, "Revival", "Revive_" + Mathf.RoundToInt(t * 100).ToString("00"), facing, render);
        }
    }
    static async Task LiveAppearancePass(Essence[] order, string route, int facing)
    {
        var patterns = new List<EnemyAttack> { EnemyAttack.GroundStrike, EnemyAttack.GD02 };
        if (Array.IndexOf(order, Essence.SwordWave) >= 0) patterns.Add(EnemyAttack.GD03);
        if (Array.IndexOf(order, Essence.RisingSlash) >= 0) patterns.Add(EnemyAttack.GD04);
        if (order.Length == 3) patterns.Add(EnemyAttack.GD05);
        if (appearanceHeavyOnly) patterns.RemoveAll(pattern => pattern != EnemyAttack.GD02 && pattern != EnemyAttack.GD04 && pattern != EnemyAttack.GD05);
        if (appearanceDomeOnly) patterns.RemoveAll(pattern => pattern != EnemyAttack.GD05);
        foreach (var pattern in patterns)
        foreach (bool alternate in new[] { false, true })
        {
            Reset(order); Place(new Vector2(20 + facing * 6, KarmaSpace.PlayerHalfHeight));
            game.Player.BeginGuard(100);
            if (pattern == EnemyAttack.GroundStrike && alternate || pattern == EnemyAttack.GD02 && alternate)
                typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.3f);
            game.Config.BossTuning.guard.trialDuration = 2;
            if (pattern == EnemyAttack.GroundStrike && alternate) Call(enemy, "BreakAutoShield");
            Force(pattern, facing);
            var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>();
            var picture = Get<SpriteRenderer>(animator, "picture");
            string previous = null; bool hurt = false, failedTrial = false;
            bool video = facing > 0 && (route == "WardRise" && !alternate && (pattern == EnemyAttack.GD02 || pattern == EnemyAttack.GD04)
                || route == "WardFinal_waveFirst" && ((!alternate && pattern == EnemyAttack.GD02) || pattern == EnemyAttack.GD05));
            string videoDirectory = AppearanceOutput + "/live/" + route + "_" + pattern + (alternate ? "_failure" : "_normal");
            if (video) Directory.CreateDirectory(videoDirectory);
            int videoFrame = 0; float nextVideoAt = -1;
            double deadline = EditorApplication.timeSinceStartup + 15;
            float started = enemy.FightSeconds;
            while (Get<bool>(enemy, "running") && EditorApplication.timeSinceStartup < deadline)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (video && Time.unscaledTime >= nextVideoAt)
                {
                    CaptureAppearanceVideo(videoDirectory + "/" + (videoFrame++).ToString("D4") + ".png");
                    nextVideoAt = Time.unscaledTime + 1f / 24;
                }
                string key = (picture.sprite != null ? AssetDatabase.GetAssetPath(picture.sprite) + picture.sprite.name : "null") + animator.CurrentPose
                    + picture.transform.localScale + animator.transform.localScale + picture.color;
                if (key != previous)
                {
                    AppearanceShot(animator, AppearanceForm(order), route, "Live", pattern + (alternate ? "_alternate" : "_normal"), facing, false);
                    previous = key;
                }
                if (!hurt && enemy.FightSeconds - started > 0.4f && pattern != EnemyAttack.GD05)
                { enemy.TakeDamage(1, enemy.Position - Vector2.right * facing * 3); hurt = true; }
                if (pattern == EnemyAttack.GD05 && alternate && enemy.TrialActive && !failedTrial)
                {
                    var trial = Get<KarmaTrialState>(Pattern("GD05"), "trial");
                    for (int i = 0; i < 3; i++) trial.Hit(); failedTrial = true;
                }
                await Task.Delay(12);
            }
            if (Get<bool>(enemy, "running")) appearanceIssues.Add(route + "/" + pattern + ": live execution timed out");
            await Seconds(0.04f);
            AppearanceShot(animator, AppearanceForm(order), route, "Live", pattern + "_settled", facing, false);
            Call(enemy, "InterruptPattern");
        }
    }
    static void CaptureAppearanceVideo(string path)
    {
        var camera = game.GameCamera; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = appearanceVideoTarget; camera.Render(); RenderTexture.active = appearanceVideoTarget;
            appearanceVideoPixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); appearanceVideoPixels.Apply();
            File.WriteAllBytes(path, appearanceVideoPixels.EncodeToPNG());
        }
        finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; }
    }
    static async void AuditGuardAppearance()
    {
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        var original = game.Config; var config = UnityEngine.Object.Instantiate(original);
        Set(game, "config", config); config.playerHealth = 100000;
        bool background = Application.runInBackground; Application.runInBackground = true;
        Application.logMessageReceived += CaptureError; runtimeError = null;
        int code = 0;
        appearanceHeavyOnly = SessionState.GetBool(AppearanceKey + ".HeavyOnly", false);
        SessionState.EraseBool(AppearanceKey + ".HeavyOnly");
        appearanceDomeOnly = SessionState.GetBool(AppearanceKey + ".DomeOnly", false);
        SessionState.EraseBool(AppearanceKey + ".DomeOnly");
        Directory.CreateDirectory(AppearanceOutput);
        string baselinePath = Environment.GetEnvironmentVariable("K4RMA_GUARD_APPEARANCE_BASELINE");
        if (!string.IsNullOrEmpty(baselinePath)) appearanceBaseline = JsonUtility.FromJson<AppearanceAudit>(File.ReadAllText(baselinePath));
        var cameraNode = new GameObject("Guard appearance inspection camera");
        appearanceCamera = cameraNode.AddComponent<Camera>(); appearanceCamera.orthographic = true;
        appearanceCamera.orthographicSize = 12; appearanceCamera.clearFlags = CameraClearFlags.SolidColor;
        appearanceCamera.backgroundColor = Color.clear; appearanceCamera.cullingMask = int.MinValue;
        appearanceTarget = new RenderTexture(768, 896, 24); appearanceCamera.targetTexture = appearanceTarget;
        appearancePixels = new Texture2D(768, 896, TextureFormat.RGBA32, false);
        appearanceVideoTarget = new RenderTexture(960, 540, 24);
        appearanceVideoPixels = new Texture2D(960, 540, TextureFormat.RGBA32, false);
        var orders = new[] { new[] { Essence.Guard }, new[] { Essence.Guard, Essence.SwordWave }, new[] { Essence.Guard, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash }, new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave } };
        if (appearanceDomeOnly) orders = Array.FindAll(orders, order => order.Length == 3);
        try
        {
            EditorApplication.isPaused = false; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            int frame = Time.frameCount; await Task.Delay(100);
            if (Time.frameCount <= frame) throw new Exception("Appearance audit needs an advancing player loop");
            foreach (var order in orders)
            {
                Reset(order); await Seconds(0.04f); enemy.enabled = false;
                var animator = Get<Transform>(enemy, "art").GetComponent<KarmaCharacterAnimator>(); animator.enabled = false;
                string route = AppearanceForm(order) + (order.Length == 3 ? order[1] == Essence.SwordWave ? "_waveFirst" : "_riseFirst" : "");
                foreach (int facing in new[] { 1, -1 }) StaticAppearancePass(order, route, facing);
                Debug.Log("Guard appearance static captured: " + route);
            }
            foreach (var order in orders)
            {
                string route = AppearanceForm(order) + (order.Length == 3 ? order[1] == Essence.SwordWave ? "_waveFirst" : "_riseFirst" : "");
                foreach (int facing in new[] { 1, -1 }) await LiveAppearancePass(order, route, facing);
                Debug.Log("Guard appearance live sampled: " + route);
            }
            if (runtimeError != null) appearanceIssues.Add("Runtime: " + runtimeError);
            if (appearanceIssues.Count > 0) code = 1;
        }
        catch (Exception error) { code = 1; appearanceIssues.Add(error.ToString()); Debug.LogException(error); }
        finally
        {
            File.WriteAllText(AppearanceOutput + "/samples.json", JsonUtility.ToJson(new AppearanceAudit {
                samples = appearanceSamples, issues = appearanceIssues,
                coverage = (appearanceDomeOnly ? "Two final Guard inheritance routes; live GD05 only. " : appearanceHeavyOnly ? "Five Guard inheritance routes; live GD02/GD04/GD05 with full/low HP. " : "Five Guard inheritance routes; all live eligible attacks with full/low HP. ")
                    + "Both facings; static reachable poses, six heavy frames, shield-away, GD04, death/revival; GD05 success/failure. Live trial duration shortened to 2 seconds in a runtime config copy. Captures and sampling do not edit production assets."
            }, true));
            Debug.Log("Guard appearance audit completed: " + appearanceSamples.Count + " samples, " + appearanceBoundsChecks + " original damage-bound comparisons, " + appearanceIssues.Count + " structural findings. See samples.json and same-scale pose captures; unchanged transforms do not prove unchanged painted anatomy.");
            Application.logMessageReceived -= CaptureError;
            game.ReturnToTitle(); Set(game, "config", original); UnityEngine.Object.Destroy(config);
            Application.runInBackground = background;
            UnityEngine.Object.DestroyImmediate(cameraNode); UnityEngine.Object.DestroyImmediate(appearancePixels); UnityEngine.Object.DestroyImmediate(appearanceTarget);
            UnityEngine.Object.DestroyImmediate(appearanceVideoPixels); UnityEngine.Object.DestroyImmediate(appearanceVideoTarget);
            EditorApplication.Exit(code);
        }
    }
}
#endif
