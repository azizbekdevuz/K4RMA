#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Runs only in a disposable validation project. Captures real production combat,
// with a stationary, damageable player; it is not a player-skill demonstration.
[InitializeOnLoad]
public static class KarmaRisingCombatReview
{
    const string Key = "K4RMA.RisingCombatReview";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static bool started;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static string error;
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static KarmaRisingCombatReview() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && !started && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunCandidateBatch() { SessionState.SetInt(Key + ".variant", 1); RunBatch(); }
    public static void RunStreakBatch() { SessionState.SetInt(Key + ".variant", 2); RunBatch(); }
    static async Task Frame()
    {
        int frame = Time.frameCount;
        double deadline = EditorApplication.timeSinceStartup + 60;
        while (Time.frameCount == frame)
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Player loop stalled");
            await Task.Delay(1);
        }
    }
    static void Capture(string path)
    {
        var camera = game.GameCamera;
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(960, 540, 24);
        var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(texture);
        }
    }
    static async void Run()
    {
        int exit = 0;
        string folder = "output/rising-review";
        Directory.CreateDirectory(folder);
        try
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 60;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            var config = UnityEngine.Object.Instantiate(game.Config);
            config.playerHealth = 100000;
            Set(game, "config", config);
            int variant = SessionState.GetInt(Key + ".variant", 0);
            var settings = game.Config.BossTuning.rising;
            var combo = settings.GetType().GetField("basicVariant");
            if (combo != null) combo.SetValue(settings, Enum.ToObject(combo.FieldType, variant > 0 ? 1 : 0));
            var trail = settings.GetType().GetField("trailStyle");
            if (trail != null) trail.SetValue(settings, Enum.ToObject(trail.FieldType, variant == 2 ? 1 : 0));
            Application.logMessageReceived += (m, s, t) => { if (t == LogType.Exception || t == LogType.Error) error = m; };
            game.StartRun();
            foreach (var essence in new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard })
            { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
            Call(game, "LoadRoom", true);
            enemy = game.Enemies[0];
            enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
            var body = game.Player.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.position = game.Player.transform.position = new Vector2(30, KarmaSpace.PlayerHalfHeight + 0.02f);
            Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
            // Let the normal game camera settle; no showcase zoom.
            for (int i = 0; i < 45; i++) await Frame();
            enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
            Call(enemy, "InterruptPattern");
            Set(enemy, "selected", EnemyAttack.GroundStrike); Set(enemy, "direction", 1f);
            Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
            enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
            float health = game.Player.Health;
            var timeline = new System.Text.StringBuilder("frame,time,x,y,facing,pose,hitboxes,playerHealth\n");
            for (int i = 0; i < 120; i++)
            {
                await Frame();
                Capture(Path.Combine(folder, i.ToString("D4") + ".png"));
                timeline.AppendLine(string.Join(",", i, Time.time, enemy.Position.x, enemy.Position.y, enemy.Facing,
                    enemy.GetComponentInChildren<KarmaCharacterAnimator>().CurrentPose,
                    ((Transform)Get(enemy, "effectsRoot")).GetComponentsInChildren<KarmaBossAttack>().Length, game.Player.Health));
            }
            File.WriteAllText(Path.Combine(folder, "timeline.csv"), timeline.ToString());
            if (error != null) throw new Exception(error);
            File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: real Play Mode capture; player damage=" + (health - game.Player.Health));
        }
        catch (Exception e) { exit = 1; File.WriteAllText(Path.Combine(folder, "result.txt"), "FAIL: " + e); }
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); SessionState.SetInt(Key + ".variant", 0); EditorApplication.Exit(exit);
    }
}
#endif
