#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaPlayerCrowdControlChecks
{
    const string Key = "K4RMA.PlayerCrowdControlChecks";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool started;
    static int checks;
    static KarmaGame game;
    static KarmaPlayer player;
    static Rigidbody2D body;
    static KarmaPlayerCrowdControlChecks() { EditorApplication.update += Pump; }
    static void Set(string name, object value) => typeof(KarmaPlayer).GetField(name, F).SetValue(player, value);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && !started && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Ready()
    {
        player.ClearStatus();
        Set("invulnerableUntil", 0f); Set("guardUntil", 0f);
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
    }
    static async Task Wait(Func<bool> done, float timeout = 5)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!done())
        {
            if (EditorApplication.timeSinceStartup > end) throw new Exception("CC test timed out");
            await Task.Delay(10);
        }
    }
    static async void Run()
    {
        string verdict;
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            game.StartRun();
            // Skip the tutorial and isolate the player from boss AI.
            foreach (var essence in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard })
            { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
            typeof(KarmaGame).GetMethod("LoadRoom", F).Invoke(game, new object[] { true });
            player = game.Player; body = player.GetComponent<Rigidbody2D>();
            foreach (var enemy in game.Enemies) enemy.gameObject.SetActive(false);
            player.ResetForRoom(new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.05f), true);
            await Task.Delay(100);
            Ready();
            Check(!player.ApplyShock(float.NaN) && !player.ApplyShock(0), "Invalid shock rejected");
            Check(!player.ApplyAirborne(Vector2.down), "Invalid launch rejected");
            Check(player.ApplyShock(0.25f) && player.Shocked && player.ControlLocked && !player.Stunned,
                "Shock has a distinct state and locks controls");
            Set("move", 1f); Set("jumpBufferedUntil", Time.time + 1);
            typeof(KarmaPlayer).GetMethod("FixedUpdate", F).Invoke(player, null);
            Check(Mathf.Abs(body.linearVelocity.x) < 0.01f, "Shock blocks horizontal movement");
            Check(!(bool)typeof(KarmaPlayer).GetMethod("CanSword", F).Invoke(player, null), "Shock blocks sword");
            float cooldown = player.Essences.Cooldown(Essence.Guard);
            player.Essences.TryCounter();
            Check(player.Essences.Cooldown(Essence.Guard) == cooldown, "Shock blocks counter");
            Check(player.ApplyShock(0.01f), "Shock may refresh without shortening its end");
            await Task.Delay(80); Check(player.Shocked, "Short refresh preserves longer shock");
            await Wait(() => !player.Shocked);
            Ready();
            player.BeginGuard(1);
            float hp = player.Health;
            Check(!player.TakeCrowdControlHit(1, KarmaPlayer.CrowdControlKind.Shock, 1)
                && player.Health == hp && !player.Shocked, "Guard blocks damage and CC");
            Ready(); player.BeginCounter();
            Check(!player.TakeCrowdControlHit(1, KarmaPlayer.CrowdControlKind.Shock, 1)
                && player.Health == hp && !player.Shocked, "Counter blocks damage and CC");
            Ready(); Set("invulnerableUntil", Time.time + 1);
            Check(!player.TakeCrowdControlHit(1, KarmaPlayer.CrowdControlKind.Airborne, 1, Vector2.up * 12)
                && !player.KnockedAirborne && player.Health == hp, "Invulnerability blocks damage and launch");
            Ready();
            Check(player.TakeCrowdControlHit(1, KarmaPlayer.CrowdControlKind.Shock, 0.2f)
                && player.Health == hp - 1 && player.Shocked, "Confirmed damage applies shock despite new hit immunity");
            Ready();
            Vector2 launch = KarmaSpace.Units(new Vector2(2, 12));
            Check(player.ApplyAirborne(launch) && player.KnockedAirborne && body.linearVelocity == launch,
                "Airborne sets world-space launch velocity");
            float startY = player.Position.y;
            await Task.Delay(80);
            Check(player.Position.y > startY && player.ControlLocked, "Real physics lifts player while controls are locked");
            Check(player.ApplyShock(0.12f) && player.KnockedAirborne, "Shock does not cancel airborne motion");
            await Wait(() => !player.KnockedAirborne);
            Check(player.Position.y < startY + 0.3f, "Airborne unlocks on landing");
            Ready();
            Check(player.ApplyAirborne(Vector2.up * 12, 0.1f), "Airborne safety cap accepted");
            await Wait(() => !player.KnockedAirborne);
            Check(!player.ControlLocked, "Safety cap releases controls");
            Ready(); player.ApplyShock(2); player.ApplyAirborne(Vector2.up * 12);
            player.ResetForRoom(new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.05f), true);
            Check(!player.ControlLocked && !player.Shocked && !player.KnockedAirborne, "Room reset clears all CC");
            Ready(); player.Stun(0.1f);
            Check(player.Stunned && player.ControlLocked && !player.Shocked, "Legacy stun remains distinct");
            player.ClearStatus();
            verdict = "PASS: " + checks + " player crowd-control assertions";
        }
        catch (Exception ex) { verdict = "FAIL: " + ex; }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/player-cc.txt", verdict);
        SessionState.SetBool(Key, false);
        Debug.Log(verdict);
        EditorApplication.Exit(verdict.StartsWith("PASS:") ? 0 : 1);
    }
}
#endif
