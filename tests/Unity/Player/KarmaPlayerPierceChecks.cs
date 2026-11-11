#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaPlayerPierceChecks
{
    const string Key = "K4RMA.PlayerPierceChecks";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool started;
    static int checks;
    static KarmaGame game;
    static KarmaPlayer player;
    static Rigidbody2D body;
    static KarmaPlayerPierceChecks() { EditorApplication.update += Pump; }
    [MenuItem("K4RMA/Player/Validate original art and teleport slash")]
    public static void RunInEditor() { Begin(false); }
    public static void RunBatch() { Begin(true); }
    static void Begin(bool batch)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
        SessionState.SetBool(Key + ".Batch", batch);
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetBool(Key, true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Set(string name, object value) => typeof(KarmaPlayer).GetField(name, Private).SetValue(player, value);
    static float Immunity => (float)typeof(KarmaPlayer).GetField("invulnerableUntil", Private).GetValue(player);
    static void Ready(Vector2 position)
    {
        player.ResetForRoom(position, true);
        player.enabled = false;
        body.gravityScale = 0;
        Physics2D.SyncTransforms();
    }
    static void Slash(Vector2 direction, float distance)
    {
        typeof(KarmaPlayer).GetMethod("BeginDirectionalSlash", Private).Invoke(player,
            new object[] { direction, distance, Essence.SwordWave });
    }
    static async Task Frames(int count)
    {
        int end = Time.frameCount + count;
        double timeout = EditorApplication.timeSinceStartup + 10;
        while (Time.frameCount < end)
        {
            if (EditorApplication.timeSinceStartup > timeout) throw new TimeoutException("Player loop stopped advancing.");
            await Task.Delay(10);
        }
    }
    static async void Run()
    {
        string verdict;
        float previousCapture = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60;
        GameObject hazard = null, wall = null;
        try
        {
            KarmaPrototype.EditorTools.KarmaPlayerAnimationAssets.Validate();
            Check(Resources.Load<TextAsset>("PlayerAnimation24/Ready") == null, "24 FPS activation marker removed");
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            game.StartRun();
            foreach (var essence in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard })
            { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
            typeof(KarmaGame).GetMethod("LoadRoom", Private).Invoke(game, new object[] { true });
            player = game.Player; body = player.GetComponent<Rigidbody2D>();
            if (player.TestInvincible) player.ToggleTestInvincibility();
            foreach (var enemy in game.Enemies) enemy.gameObject.SetActive(false);
            var animator = player.GetComponentInChildren<KarmaCharacterAnimator>();
            Check(!(bool)typeof(KarmaCharacterAnimator).GetField("nativePlayer24", Private).GetValue(animator), "Live player uses original art");
            Check((float)typeof(KarmaCharacterAnimator).GetField("playerSampleRate", Private).GetValue(animator) == 12, "Live playback uses 12 FPS");
            var picture = (SpriteRenderer)typeof(KarmaCharacterAnimator).GetField("picture", Private).GetValue(animator);
            var sample = typeof(KarmaCharacterAnimator).GetMethod("SamplePlayerClip", Private);
            var clock = typeof(KarmaCharacterAnimator).GetField("playerClipAt", Private);
            var library = Resources.Load<KarmaSpriteLibrary>("PlayerAnimation/Frames");
            foreach (var sheet in library.sheets)
            {
                sample.Invoke(animator, new object[] { sheet.id, true });
                for (int i = 0; i < sheet.sprites.Length; i++)
                {
                    clock.SetValue(animator, Time.time - (i + 0.1f) / 12f);
                    sample.Invoke(animator, new object[] { sheet.id, false });
                    Check(picture.sprite == sheet.sprites[i] && !AssetDatabase.GetAssetPath(picture.sprite).Contains("/Native24/"), sheet.id + " original frame " + i);
                }
            }
            Vector2 origin = new Vector2(25, KarmaSpace.Units(7));
            float distance = game.Config.Techniques.pierceDistance;
            foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                new Vector2(1, 1).normalized, new Vector2(-1, -1).normalized })
            {
                Ready(origin); Slash(direction, distance);
                Check(Vector2.Distance(player.Position, origin + direction * distance) < 0.01f, "Atomic teleport " + direction);
                Check(Vector2.Distance(body.position, player.Position) < 0.001f, "Collision body reaches destination immediately");
                Check(Immunity == 0, "Teleport adds no immunity");
                float hp = player.Health;
                player.TakeDamage(1);
                Check(player.Health == hp - 1, "Arrival damage applies during slash visuals");
            }
            Ready(origin); Set("invulnerableUntil", Time.time + 1);
            float previous = Immunity; Slash(Vector2.right, distance);
            Check(Immunity == previous, "Existing hit protection is neither extended nor removed");
            Ready(origin); Slash(Vector2.right, 0);
            Check(player.Position == origin && Immunity == 0, "Blocked/zero movement grants no immunity");
            Ready(origin);
            wall = new GameObject("Teleport solid wall check");
            wall.transform.position = origin + Vector2.right * (distance * 0.5f);
            wall.AddComponent<KarmaSurface>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 8);
            Physics2D.SyncTransforms(); Slash(Vector2.right, distance);
            Check(player.Position.x > origin.x && player.Position.x < wall.transform.position.x - KarmaSpace.PlayerHalfWidth, "Teleport respects solid walls");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            Ready(origin);
            hazard = new GameObject("Teleport transit hazard check");
            hazard.transform.position = origin + Vector2.right * (distance * 0.5f);
            var collider = hazard.AddComponent<BoxCollider2D>(); collider.size = new Vector2(0.2f, 5); collider.isTrigger = true;
            var probe = hazard.AddComponent<KarmaPlayerPierceHazard>();
            Physics2D.SyncTransforms(); await Frames(3);
            float before = player.Health;
            Slash(Vector2.right, distance); await Frames(4);
            Check(probe.Contacts == 0 && player.Health == before, "Teleport skips actual trigger damage along transit");
            hazard.transform.position = player.Position; Physics2D.SyncTransforms(); await Frames(4);
            Check(probe.Contacts > 0 && player.Health == before - 1, "Destination trigger still hits during the 0.22-second slash pose");
            verdict = "PASS: " + checks + " original player art, 12 FPS, teleport, walls, transit evasion and vulnerable arrival checks";
        }
        catch (Exception ex) { verdict = "FAIL: " + ex; Debug.LogException(ex); }
        finally
        {
            if (hazard != null) UnityEngine.Object.DestroyImmediate(hazard);
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            Time.captureDeltaTime = previousCapture;
        }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/player-pierce.txt", verdict);
        Debug.Log(verdict); SessionState.SetBool(Key, false);
        Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
        if (SessionState.GetBool(Key + ".Batch", false)) EditorApplication.Exit(verdict.StartsWith("PASS:") ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}
public sealed class KarmaPlayerPierceHazard : MonoBehaviour
{
    public int Contacts;
    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<KarmaPlayer>();
        if (player == null) return;
        Contacts++; player.TakeDamage(1);
    }
}
#endif
