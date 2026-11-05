#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaPlayerAirChecks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static KarmaPlayerAirChecks() { EditorApplication.update += PumpLiveChecks; }
    static void PumpLiveChecks()
    {
        if (!SessionState.GetBool("K4RMA.FeatureChecks.Live", false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true; Time.maximumDeltaTime = 1f / 30f;
        EditorApplication.isPaused = false;
    }
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Place(KarmaPlayer p, Vector2 point, Vector2 velocity)
    {
        p.transform.position = point; var body = p.GetComponent<Rigidbody2D>();
        body.position = point; body.linearVelocity = velocity; Physics2D.SyncTransforms();
    }
    static float GroundJumpApex(KarmaPlayer player, Keyboard keyboard, bool held, KarmaGame game)
    {
        var body = player.GetComponent<Rigidbody2D>();
        player.ResetForRoom(new Vector2(8, KarmaSpace.PlayerHalfHeight), true);
        Place(player, new Vector2(8, KarmaSpace.PlayerHalfHeight), Vector2.zero);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
        Set(player, "jumpBufferedUntil", Time.time + 1); Call(player, "FixedUpdate");
        Check(body.linearVelocity.y == game.Config.jumpSpeed, "Original ground-jump launch speed");
        Physics2D.Simulate(Time.fixedDeltaTime);
        if (!held) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); }
        float apex = body.position.y;
        for (int i = 0; i < 60; i++)
        {
            float vy = body.linearVelocity.y; Call(player, "FixedUpdate");
            Check(Mathf.Abs(body.linearVelocity.y - vy) < 0.01f, "Ground jump has no release cut or frozen frame");
            Check(body.gravityScale == game.Config.gravityScale, "Original ground-jump gravity");
            Physics2D.Simulate(Time.fixedDeltaTime); apex = Mathf.Max(apex, body.position.y);
            if (i > 5 && (bool)Call(player, "Grounded") && body.linearVelocity.y <= 0.1f) break;
        }
        return apex;
    }
    public static string Run()
    {
        checks = 0; Check(Application.isPlaying, "Play Mode required");
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); game.StartRun();
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mode = Physics2D.simulationMode;
        game.enabled = player.enabled = false; Physics2D.simulationMode = SimulationMode2D.Script;
        try
        {
            Check(game.Config.jumpSpeed == 26 && game.Config.gravityScale == 6, "Original saved jump tuning restored");
            float held = GroundJumpApex(player, keyboard, true, game), tap = GroundJumpApex(player, keyboard, false, game);
            Check(Mathf.Abs(held - tap) < 0.01f && tap > KarmaSpace.PlayerHalfHeight + 5, "Tap and held jumps preserve original height");
            Check(Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Player/AerialActions").Length == 0, "Replaced aerial sheet is removed");
            var animator = (KarmaCharacterAnimator)Get(player, "animator");
            animator.Pose(0, true, -4);
            Check(((SpriteRenderer)Get(animator, "picture")).sprite == KarmaArtwork.Sprite("Player", "Fall"), "Original falling sprite is connected");
            Place(player, new Vector2(20, 14), new Vector2(0, -3));
            Set(player, "airSlashBufferedUntil", Time.time + 1); Set(player, "nextSword", 0f); Call(player, "Attack");
            Check(!(bool)Get(player, "airSlashRunning"), "Leap slash is locked before Rising personalization");
            player.Essences.Preview(Essence.RisingSlash); player.Essences.Confirm(out _);
            Set(player, "jumpBufferedUntil", Time.time + 1); Call(player, "FixedUpdate");
            Check(body.linearVelocity.y == -3, "Space in air does not add a double jump");
            Set(player, "jumpBufferedUntil", 0f);
            var tuning = game.Config.Techniques;
            Check(tuning.airSlashDistance == 2.4f && tuning.airSlashDuration == 0.12f
                && tuning.airSlashRecovery == 0.16f && tuning.airSlashJumpScale == 0.9f
                && tuning.airSlashHorizontalBoost == 1.5f, "Original personalized leap-slash tuning restored");
            var keys = new[] { new[] { Key.A }, new[] { Key.D }, new[] { Key.W }, new[] { Key.S }, new[] { Key.W, Key.D } };
            var directions = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down, new Vector2(1, 1).normalized };
            for (int i = 0; i < keys.Length; i++)
            {
                player.ResetForRoom(new Vector2(20, KarmaSpace.PlayerHalfHeight), true);
                Place(player, new Vector2(20, 14), Vector2.zero);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i])); InputSystem.Update();
                Set(player, "airSlashBufferedUntil", Time.time + 1); Set(player, "nextSword", 0f); Call(player, "Attack");
                Check((bool)Get(player, "airSlashRunning"), "Personalized directional leap slash starts: " + i);
                Check(animator.CurrentPose == "AirSlash", "Original AirSlash pose is used");
                Vector2 from = body.position; int frames = 0;
                while ((bool)Get(player, "airSlashRunning"))
                {
                    Check(++frames < 30, "Leap slash completes within its original duration"); Call(player, "FixedUpdate");
                    if ((bool)Get(player, "airSlashRunning")) Physics2D.Simulate(Time.fixedDeltaTime);
                }
                Check(Vector2.Distance(body.position - from, directions[i] * tuning.airSlashDistance) < 0.03f,
                    "Original directional slash travel: " + i + " actual=" + (body.position-from) + " expected=" + (directions[i] * tuning.airSlashDistance) + " direction=" + Get(player, "airSlashDirection"));
                Check(Mathf.Abs(body.linearVelocity.y - game.Config.jumpSpeed * tuning.airSlashJumpScale) < 0.01f,
                    "Original upward launch follows every slash direction");
                Check(Mathf.Abs(body.linearVelocity.x - directions[i].x * game.Config.moveSpeed * tuning.airSlashHorizontalBoost) < 0.01f,
                    "Original horizontal launch is preserved");
                Set(player, "airSlashBufferedUntil", Time.time + 1); Set(player, "nextSword", 0f); Call(player, "Attack");
                Check(!(bool)Get(player, "airSlashRunning"), "Only one leap slash per airborne flight");
                Place(player, new Vector2(8, KarmaSpace.PlayerHalfHeight), Vector2.zero); Call(player, "FixedUpdate");
                Place(player, new Vector2(20, 14), Vector2.zero);
                Set(player, "airSlashBufferedUntil", Time.time + 1); Set(player, "nextSword", 0f); Call(player, "Attack");
                Check((bool)Get(player, "airSlashRunning"), "Landing restores the leap slash");
            }
            player.Stun(0.2f); Call(player, "FixedUpdate");
            Check(!(bool)Get(player, "airSlashRunning") && body.gravityScale == game.Config.gravityScale, "Stun ends leap slash and restores gravity");
            return "PASS: " + checks + " original jump/trajectory, no-double-jump and personalized leap-slash assertions";
        }
        finally
        {
            Physics2D.simulationMode = mode; InputSystem.RemoveDevice(keyboard);
            game.enabled = player.enabled = true; game.StartRun();
        }
    }
}
#endif
