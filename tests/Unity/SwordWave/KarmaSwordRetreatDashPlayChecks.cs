#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

// Uses the real scene, normal boss Update/coroutines and player MoveSlash collision path.
[InitializeOnLoad]
public static class KarmaSwordRetreatDashPlayChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const string Key = "K4RMA.DashRetreatChecks";
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started, updated;
    static int assertions;
    static string error;
    static readonly List<string> results = new List<string>();
    static readonly List<string> trace = new List<string>();
    static string Output { get { return "output/dash-" + (updated ? "updated" : "baseline"); } }
    static KarmaSwordRetreatDashPlayChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!started && EditorApplication.isPlaying) EditorApplication.isPaused = false;
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2)
        { started = true; updated = SessionState.GetBool(Key + ".Updated", false); Run(); }
    }
    public static void RunBaseline() { Begin(false); }
    public static void RunUpdated() { Begin(true); }
    static void Begin(bool revised)
    {
        SessionState.SetBool(Key + ".Updated", revised);
        if (revised) typeof(KarmaSwordPlayChecks).GetMethod("CompilePlayerScripts", Flags).Invoke(null, null);
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) return field; }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    static object Get(object target, string name) { return Field(target, name).GetValue(target); }
    static void Set(object target, string name, object value) { Field(target, name).SetValue(target, value); }
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    static object Behavior { get { return Get(enemy, "behavior"); } }
    static object Movement { get { return Get(Behavior, "movement"); } }
    static bool Retreating { get { return (float)Get(Movement, "swordRetreatRemaining") > 0; } }
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static void Log(string message) { results.Add(message); Debug.Log(message); }
    static void Place(float x)
    {
        var position = new Vector2(x, KarmaSpace.PlayerHalfHeight + 0.02f);
        game.Player.transform.position = position; game.Player.GetComponent<Rigidbody2D>().position = position;
        Physics2D.SyncTransforms();
    }
    static void Reset(int facing = 1, float gap = 3.5f)
    {
        typeof(KarmaSwordPlayChecks).GetMethod("Reset", Flags).Invoke(null, new object[] { Essence.SwordWave });
        enemy = (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", Flags).GetValue(null);
        enemy.transform.position = new Vector3(25, KarmaSpace.GuardianGroundY, 0);
        Set(enemy, "direction", (float)facing);
        ((Transform)Get(enemy, "art")).localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
        Place(25 + facing * gap);
    }
    static async Task Frames(int count)
    { int end = Time.frameCount + count; while (Time.frameCount < end) await Task.Delay(5); }
    static async Task Wait(Func<bool> done, double seconds = 300)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!done()) { if (EditorApplication.timeSinceStartup > end) throw new Exception("Play Mode timeout"); await Task.Delay(5); }
    }
    static async void Run()
    {
        int exit = 0;
        Directory.CreateDirectory(Output);
        try
        {
            Application.runInBackground = true; Application.targetFrameRate = 120;
            Time.captureDeltaTime = 1f / 60;
            Application.logMessageReceived += (message, stack, type) =>
            { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) error = message + "\n" + stack; };
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); Check(game != null, "real game scene");
            typeof(KarmaSwordPlayChecks).GetField("game", Flags).SetValue(null, game);
            var config = UnityEngine.Object.Instantiate(game.Config); Set(game, "config", config); config.playerHealth = 1000000;
            UnityEngine.Random.InitState(6101);
            await CheckDash();
            if (updated)
            {
                CheckDistanceCurves();
                await CheckRendered(1); await CheckRendered(-1);
                CheckBlocked();
            }
            await Campaign();
            if (updated)
            {
                foreach (string check in new[] { "CheckBasics", "CheckForcedInterruption" })
                { await (Task)typeof(KarmaSwordPlayChecks).GetMethod(check, Flags).Invoke(null, null); Log("PASS: existing " + check); }
                File.Copy("output/validation/sword-player-compilation.txt", Output + "/player-compilation.txt", true);
            }
            Check(error == null, "no runtime errors: " + error);
            results.Insert(0, "PASS: " + assertions + " " + (updated ? "updated" : "baseline") + " assertions");
        }
        catch (Exception failure) { results.Insert(0, "FAIL: " + failure); Debug.LogException(failure); exit = 1; }
        Time.captureDeltaTime = 0;
        File.WriteAllLines(Output + "/result.txt", results);
        File.WriteAllLines(Output + "/trace.csv", trace);
        SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
    [Serializable] sealed class DashMetrics
    { public float configuredDistance, rightDistance, leftDistance, solidWallDistance, boundaryDistance, airSlashDistance, airSlashDuration; public string motion = "atomic MoveSlash, not speed * duration; Pierce pose 0.22s"; }
    static async Task CheckDash()
    {
        Reset(); await Frames(3);
        var metrics = new DashMetrics { configuredDistance = game.Config.Techniques.pierceDistance,
            airSlashDistance = game.Config.Techniques.airSlashDistance, airSlashDuration = game.Config.Techniques.airSlashDuration };
        foreach (int facing in new[] { -1, 1 })
        {
            Place(20); Vector2 from = game.Player.Position;
            Call(game.Player, "BeginDirectionalSlash", Vector2.right * facing, metrics.configuredDistance, Essence.SwordWave);
            float distance = Mathf.Abs(game.Player.Position.x - from.x);
            if (facing > 0) metrics.rightDistance = distance; else metrics.leftDistance = distance;
            Check(Mathf.Abs(distance - metrics.configuredDistance) < 0.001f, "actual directional dash reaches configured distance in open arena");
        }
        var wall = new GameObject("Dash distance solid terrain fixture");
        wall.transform.position = new Vector3(22.5f, 3, 0);
        wall.AddComponent<KarmaSurface>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 6);
        Place(20); Call(game.Player, "BeginDirectionalSlash", Vector2.right, metrics.configuredDistance, Essence.SwordWave);
        metrics.solidWallDistance = game.Player.Position.x - 20;
        Check(metrics.solidWallDistance > 0 && metrics.solidWallDistance < metrics.configuredDistance, "dash stops before solid wall");
        UnityEngine.Object.DestroyImmediate(wall);
        Place(KarmaStage.ArenaRight - KarmaSpace.PlayerHalfWidth - 1);
        float start = game.Player.Position.x;
        Call(game.Player, "BeginDirectionalSlash", Vector2.right, metrics.configuredDistance, Essence.SwordWave);
        metrics.boundaryDistance = game.Player.Position.x - start;
        Check(metrics.boundaryDistance <= 1.001f, "dash obeys arena boundary");
        File.WriteAllText(Output + "/dash.json", JsonUtility.ToJson(metrics, true));
        Log("PASS: actual player dash " + metrics.rightDistance + "/" + metrics.leftDistance + " units; solid terrain and boundary truncate travel");
    }
    static void ClockTick(float dt)
    {
        typeof(KarmaEnemy).GetProperty("FightSeconds").SetValue(enemy, enemy.FightSeconds + dt);
        Call(Behavior, "TickMovement", dt, game.Player.Position.x - enemy.Position.x);
    }
    static void CheckDistanceCurves()
    {
        float dash = game.Config.Techniques.pierceDistance;
        foreach (float dt in new[] { 1f / 30, 1f / 60, 1f / 120 })
        foreach (int facing in new[] { -1, 1 })
        foreach (float gap in new[] { 3.5f, 8f })
        {
            Reset(facing, gap); float start = enemy.Position.x;
            ClockTick(dt); Check(enemy.Position.x == start && !Retreating, "first attack remains available before retreat");
            Call(Behavior, "OnCompleted"); float lastSpeed = 0, clock = 0;
            do
            {
                float before = enemy.Position.x; ClockTick(dt); clock += dt;
                float speed = Mathf.Abs(enemy.Position.x - before) / dt;
                Check(speed <= 16.03f, "extended retreat retains 16 unit/s cap");
                Check(speed - lastSpeed <= 180 * dt + 0.04f, "extended retreat accelerates continuously");
                Check(lastSpeed - speed <= 160 * dt + 0.06f, "extended retreat brakes continuously");
                Check((enemy.Position.x - before) * facing <= 0.001f && enemy.Facing == facing, "backward movement with player-facing posture");
                lastSpeed = speed;
                Check(clock < 3, "extended retreat finishes within feasible duration");
            } while (Retreating);
            float travel = Mathf.Abs(enemy.Position.x - start);
            Check(travel >= dash * 1.2f - 0.01f, "actual open retreat exceeds dash by 20% at both sides and all frame rates: " + travel);
            Check(travel <= dash * 1.2f + 0.02f, "open retreat reaches requested endpoint without overshoot");
            float stopped = enemy.Position.x;
            for (int frame = 0; frame < Mathf.FloorToInt(0.5f / dt); frame++) ClockTick(dt);
            Check(enemy.Position.x == stopped && !Retreating, "post-retreat attack opportunity prevents immediate chase or repeated retreat");
        }
        // The goal follows changed player tuning, rather than a fixed 9.6 constant.
        Reset(); float original = game.Config.Techniques.pierceDistance;
        game.Config.Techniques.pierceDistance = 12; Call(Behavior, "OnCompleted");
        float initial = enemy.Position.x;
        do { ClockTick(1f / 60); } while (Retreating);
        Check(Mathf.Abs(enemy.Position.x - initial) >= 14.39f, "retreat follows a 12-unit dash to at least 14.4 units");
        game.Config.Techniques.pierceDistance = original;
        Log("PASS: open retreat >= dash * 1.2 at 30/60/120 FPS, both directions and gaps 3.5/8; cap/braking/attack window/dynamic tuning");
    }
    static void CheckBlocked()
    {
        foreach (int facing in new[] { -1, 1 })
        {
            Reset(facing);
            float start = facing > 0 ? KarmaStage.BossLeftWall + 1.5f : KarmaStage.BossRightWall - 1.5f;
            enemy.transform.position = new Vector3(start, KarmaSpace.GuardianGroundY, 0); Place(start + facing * 3.5f);
            Call(Behavior, "OnCompleted"); do { ClockTick(1f / 60); } while (Retreating);
            Check(Mathf.Abs(enemy.Position.x - start) <= 1.01f, "edge retreat is limited to available travel");
            Check(enemy.Position.x >= KarmaStage.BossLeftWall && enemy.Position.x <= KarmaStage.BossRightWall, "edge position stays inside arena");
            float stopped = enemy.Position.x;
            for (int frame = 0; frame < 60; frame++) ClockTick(1f / 60);
            Check(Mathf.Abs(enemy.Position.x - stopped) < 0.001f && !Retreating, "blocked retreat cannot repeat without a completed attack");
        }
        Reset(); var wall = new GameObject("Long retreat wall fixture");
        wall.transform.position = new Vector3(22.5f, 3, 0); wall.AddComponent<KarmaSurface>();
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 6); Physics2D.SyncTransforms();
        Call(Behavior, "OnCompleted"); do { ClockTick(1f / 60); } while (Retreating);
        Check(enemy.Position.x >= 23.54f, "long retreat respects swept body clearance before solid wall");
        UnityEngine.Object.DestroyImmediate(wall); Log("PASS: both arena ends and solid terrain; no repeated blocked retreat");
    }
    static async Task CheckRendered(int facing)
    {
        Reset(facing); await Frames(3);
        var images = enemy.GetComponentInChildren<KarmaSwordRetreatAfterimages>();
        var pool = (Array)Get(images, "pool");
        var sources = (Transform[])Get(images, "sources");
        int artworkIndex = Array.IndexOf(sources, images.transform);
        var probe = new GameObject("Extended retreat render observer").AddComponent<KarmaSwordDashFrameProbe>();
        float startX = enemy.Position.x, start = enemy.FightSeconds, nextCapture = start;
        int count = 0, peak = 0; bool moved = false, hit = false;
        probe.After = () =>
        {
            try
            {
                int active = 0;
                foreach (var echo in pool)
                {
                    if (!(bool)Get(echo, "active")) continue; active++;
                    if (Retreating)
                    {
                        var ghosts = (Transform[])Get(echo, "transforms");
                        Check((ghosts[artworkIndex].position.x - enemy.Position.x) * facing > 0, "extended retreat echoes stay on player-facing side");
                    }
                }
                peak = Mathf.Max(peak, active); moved |= Mathf.Abs(enemy.Position.x - startX) > 0.1f;
                if (Retreating) Check(enemy.Facing == facing, "rendered long slide faces player");
                if (!hit && active >= 3) { hit = true; enemy.TakeDamage(0.1f); }
                if (enemy.FightSeconds >= nextCapture && count < 26)
                { Snapshot((facing > 0 ? "facing-right-" : "facing-left-") + count.ToString("D2")); count++; nextCapture = enemy.FightSeconds + 0.05f; }
                trace.Add("render," + facing + "," + (enemy.FightSeconds - start).ToString("F4") + "," + enemy.Position.x.ToString("F4") + "," + active);
            }
            catch (Exception failure) { error = failure.ToString(); probe.After = null; }
        };
        Call(Behavior, "OnCompleted"); Set(enemy, "running", false); Set(enemy, "timer", 100f);
        await Wait(() => enemy.FightSeconds - start > 1.4f);
        probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        Check(error == null && moved && peak >= 3 && peak <= 6, "long retreat renders 3-6 cyan silhouettes: " + error);
        Log("PASS: rendered " + (facing > 0 ? "right-facing/left-travel" : "left-facing/right-travel") + ", peak " + peak + " echoes, damage coexistence, " + count + " continuous captures");
    }
    [Serializable] sealed class CampaignMetrics
    {
        public string phase, scenario = "60 game seconds at 60Hz; collision-checked 14 unit/s pursuer; gap 3.5; 0.1 damage pressure every 0.6s; flank only after a retreat near an arena end; seed 6101";
        public float seconds = 60, dashDistance, configuredCooldown, meanActualRetreatDistance, minimumRetreatInterval = 999, meanCompletedRetreatDistance;
        public int retreatStarts, completedRetreats, partialRetreats, attackStarts, meleeStarts, waveBasics, maximumRetreatsWithoutAttack;
        public List<float> actualDistances = new List<float>();
    }
    static async Task Campaign()
    {
        UnityEngine.Random.InitState(6101); Reset(); await Frames(3);
        var metrics = new CampaignMetrics { phase = updated ? "updated" : "baseline", dashDistance = game.Config.Techniques.pierceDistance,
            configuredCooldown = game.Config.BossTuning.sword.pressureRetreatCooldown };
        float start = enemy.FightSeconds, nextPressure = start + 0.6f, lastPattern = -1, lastRetreat = -100, retreatX = 0;
        float completedTravel = 0, beforeX = enemy.Position.x;
        int flank = 1, retreatsWithoutAttack = 0; bool wasRetreating = false;
        var probe = new GameObject("60-second normal AI observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.Before = () =>
        {
            beforeX = enemy.Position.x;
            if (!Retreating)
            {
                if (enemy.Position.x < KarmaStage.BossLeftWall + 10.5f) flank = -1;
                else if (enemy.Position.x > KarmaStage.BossRightWall - 10.5f) flank = 1;
            }
            float target = Mathf.Clamp(enemy.Position.x + flank * 3.5f, KarmaStage.ArenaLeft + 1, KarmaStage.ArenaRight - 1);
            float step = Mathf.MoveTowards(game.Player.Position.x, target, game.Config.moveSpeed * Time.deltaTime) - game.Player.Position.x;
            if (Mathf.Abs(step) > 0.00001f) Call(game.Player, "MoveSlash", Vector2.right * Mathf.Sign(step), Mathf.Abs(step));
            if (enemy.FightSeconds >= nextPressure) { enemy.TakeDamage(0.1f); nextPressure += 0.6f; }
        };
        probe.After = () =>
        {
            try
            {
                float pattern = (float)Get(enemy, "patternStarted");
                if ((bool)Get(enemy, "running") && pattern != lastPattern)
                {
                    lastPattern = pattern; metrics.attackStarts++; retreatsWithoutAttack = 0;
                    if ((EnemyAttack)Get(enemy, "selected") == EnemyAttack.SWBasic)
                    {
                        if ((bool)Behavior.GetType().GetProperty("BasicMeleeSelected").GetValue(Behavior)) metrics.meleeStarts++;
                        else metrics.waveBasics++;
                    }
                }
                bool moving = Retreating;
                if (moving && !wasRetreating)
                {
                    metrics.retreatStarts++; retreatX = beforeX; retreatsWithoutAttack++;
                    metrics.maximumRetreatsWithoutAttack = Mathf.Max(metrics.maximumRetreatsWithoutAttack, retreatsWithoutAttack);
                    if (lastRetreat > 0) metrics.minimumRetreatInterval = Mathf.Min(metrics.minimumRetreatInterval, enemy.FightSeconds - lastRetreat);
                    lastRetreat = enemy.FightSeconds;
                }
                if (!moving && wasRetreating)
                { float distance = Mathf.Abs(enemy.Position.x - retreatX); metrics.actualDistances.Add(distance); completedTravel += distance; metrics.completedRetreats++; }
                wasRetreating = moving;
                Check(enemy.Position.x >= KarmaStage.BossLeftWall && enemy.Position.x <= KarmaStage.BossRightWall, "60-second pursuit stays inside arena");
                trace.Add("campaign," + (enemy.FightSeconds - start).ToString("F4") + "," + enemy.Position.x.ToString("F4") + "," + game.Player.Position.x.ToString("F4") + "," + moving + "," + Get(enemy, "running") + "," + Get(enemy, "selected"));
            }
            catch (Exception failure) { error = failure.ToString(); probe.Before = probe.After = null; }
        };
        Set(enemy, "running", false); Set(enemy, "timer", 0f);
        await Wait(() => enemy.FightSeconds - start >= 60 || error != null);
        probe.Before = probe.After = null; UnityEngine.Object.Destroy(probe.gameObject);
        if (wasRetreating) { metrics.actualDistances.Add(Mathf.Abs(enemy.Position.x - retreatX)); metrics.partialRetreats = 1; }
        float sum = 0; foreach (float distance in metrics.actualDistances) sum += distance;
        metrics.meanActualRetreatDistance = metrics.actualDistances.Count > 0 ? sum / metrics.actualDistances.Count : 0;
        metrics.meanCompletedRetreatDistance = metrics.completedRetreats > 0 ? completedTravel / metrics.completedRetreats : 0;
        File.WriteAllText(Output + "/campaign.json", JsonUtility.ToJson(metrics, true));
        Check(error == null, "campaign runtime: " + error);
        Check(metrics.retreatStarts > 0 && metrics.meleeStarts > 0, "normal AI mixes retreats with melee basics");
        Check(metrics.maximumRetreatsWithoutAttack <= 1, "no retreat chain without an intervening attack");
        Check(metrics.minimumRetreatInterval >= (updated ? 1.59f : 2.38f), "campaign respects cooldown");
        if (updated)
        {
            var baseline = JsonUtility.FromJson<CampaignMetrics>(File.ReadAllText("output/dash-baseline/campaign.json"));
            Check(metrics.retreatStarts > baseline.retreatStarts, "60-second retreat frequency increases: " + metrics.retreatStarts + " vs " + baseline.retreatStarts);
            Check(metrics.meanActualRetreatDistance > metrics.dashDistance, "mean actual retreat exceeds the measured player dash");
            Check(metrics.meanCompletedRetreatDistance >= metrics.dashDistance * 1.2f - 0.01f,
                "completed pursuit retreats reach the 20% distance margin: " + metrics.meanCompletedRetreatDistance);
        }
        Log("PASS: " + metrics.phase + " 60s: " + metrics.retreatStarts + " retreats, mean actual " + metrics.meanActualRetreatDistance.ToString("F3")
            + ", " + metrics.meleeStarts + " melee / " + metrics.attackStarts + " attacks, minimum interval " + metrics.minimumRetreatInterval.ToString("F3"));
        Call(enemy, "InterruptPattern"); Set(enemy, "running", true);
    }
    static void Snapshot(string name)
    {
        var camera = Camera.main; Check(camera != null, "Game camera present");
        var target = new RenderTexture(1600, 700, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var picture = new Texture2D(1600, 700, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; picture.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0); picture.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", picture.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; UnityEngine.Object.Destroy(picture); UnityEngine.Object.Destroy(target); }
    }
}
[DefaultExecutionOrder(500)]
public sealed class KarmaSwordDashFrameProbe : MonoBehaviour
{
    public Action Before, After;
    void Awake() { gameObject.AddComponent<KarmaSwordDashBeforeProbe>().Observer = this; }
    void LateUpdate() { After?.Invoke(); }
}
[DefaultExecutionOrder(-1000)]
public sealed class KarmaSwordDashBeforeProbe : MonoBehaviour
{
    public KarmaSwordDashFrameProbe Observer;
    void Update() { if (Observer != null) Observer.Before?.Invoke(); }
}
#endif
