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
        Application.runInBackground = true;
        Time.maximumDeltaTime = 1f / 30f;
        EditorApplication.isPaused = false;
    }
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Place(KarmaPlayer player, Vector2 p, Vector2 velocity)
    {
        player.transform.position = p; player.GetComponent<Rigidbody2D>().position = p;
        player.GetComponent<Rigidbody2D>().linearVelocity = velocity; Physics2D.SyncTransforms();
    }
    public static string Run()
    {
        checks = 0;
        Check(Application.isPlaying, "Player checks require Play Mode");
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); game.StartRun();
        var player = game.Player; var enemy = game.Enemies[0]; var body = player.GetComponent<Rigidbody2D>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mode = Physics2D.simulationMode;
        game.enabled = player.enabled = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        try
        {
            var sprites = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Player/AerialActions");
            Check(sprites.Length == 9, "Nine aerial sprites import through their runtime Resources path");
            var animator = (KarmaCharacterAnimator)Get(player, "animator");
            foreach (var sprite in sprites)
            {
                animator.PlayPose(sprite.name, 0.1f);
                Check(((SpriteRenderer)Get(animator, "picture")).sprite == sprite, "Player pose is connected: " + sprite.name);
                Check(sprite.pixelsPerUnit == 155 && sprite.rect.width > 0 && sprite.rect.height > 0, "Consistent aerial sprite import: " + sprite.name);
            }
            // A missed downward slash retains gravity, horizontal control and downward momentum.
            Place(player, new Vector2(8, 12), new Vector2(0, -5)); Set(player, "move", 0.5f);
            Call(player, "BeginAirSlash", Vector2.down);
            Check(body.linearVelocity.y == -5 && body.gravityScale == game.Config.gravityScale, "Starting a slash creates no suspension or impulse");
            float firstY = player.Position.y;
            for (int i = 0; i < 15; i++)
            {
                Call(player, "FixedUpdate"); Physics2D.Simulate(Time.fixedDeltaTime);
                Check(body.linearVelocity.y < 0 && body.gravityScale == game.Config.gravityScale, "Missed slash keeps falling: " + i);
                Check(Mathf.Abs(body.linearVelocity.x - game.Config.moveSpeed * 0.5f) < 0.01f, "Horizontal control survives aerial slash: " + i);
            }
            Check(player.Position.y < firstY && !(bool)Get(player, "airSlashRunning"), "A missed slash ends without a forced jump");
            // No double jump before RisingSlash personalization.
            Place(player, new Vector2(10, 12), new Vector2(0, -3));
            Set(player, "jumpBufferedUntil", Time.time + 1); Call(player, "ConsumeJumpBuffer");
            Check((float)Get(player, "doubleJumpDelay") == 0 && body.linearVelocity.y == -3, "Double jump stays locked before personalization");
            player.Essences.Preview(Essence.RisingSlash); player.Essences.Confirm(out _);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
            Call(player, "ConsumeJumpBuffer");
            Check((float)Get(player, "doubleJumpDelay") > 0, "Unlocked double jump has a brief anticipation");
            for (int i = 0; i < 3; i++) { Call(player, "FixedUpdate"); Physics2D.Simulate(Time.fixedDeltaTime); }
            Check(body.linearVelocity.y > game.Config.jumpSpeed * 0.65f && body.linearVelocity.y < game.Config.jumpSpeed, "Second jump rises naturally below full ground-jump speed: v=" + body.linearVelocity.y + " jump=" + game.Config.jumpSpeed + " held=" + KarmaInput.Held(KarmaAction.Jump) + " dt=" + Time.fixedDeltaTime + " delay=" + Get(player, "doubleJumpDelay"));
            Set(player, "jumpBufferedUntil", Time.time + 1); Call(player, "ConsumeJumpBuffer");
            Check((float)Get(player, "doubleJumpDelay") == 0, "A third jump is unavailable");
            float beforeRelease = body.linearVelocity.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); Call(player, "FixedUpdate");
            Check(body.linearVelocity.y <= beforeRelease * 0.6f, "Releasing jump shortens ascent");
            // A confirmed down hit, and only that hit, grants a short hang and modest bounce.
            Set(player, "jumpBufferedUntil", 0f); Set(player, "jumpCutAvailable", false);
            Place(player, new Vector2(enemy.DamageBounds.center.x, enemy.DamageBounds.max.y + 0.7f), new Vector2(0, -4));
            float hp = enemy.Health;
            Call(player, "BeginAirSlash", Vector2.down);
            Call(player, "DamageAirSlash");
            Check(enemy.Health == hp, "Downward windup has no premature contact damage");
            for (int i = 0; i < 4; i++) Call(player, "AdvanceAirSlash");
            Call(player, "DamageAirSlash");
            Check(enemy.Health < hp && (float)Get(player, "airHangLeft") > 0, "Confirmed downward hit starts the short hang");
            hp = enemy.Health; Call(player, "DamageAirSlash");
            Check(enemy.Health == hp, "A slash damages each enemy once");
            var techniques = (KarmaTechniqueState)Get(player, "techniques");
            Check(techniques.TryAirJump(), "Successful pogo refreshes the double jump");
            for (int i = 0; i < 3; i++) { Call(player, "FixedUpdate"); Physics2D.Simulate(Time.fixedDeltaTime); }
            Check(body.linearVelocity.y > 0 && body.linearVelocity.y < game.Config.jumpSpeed * 0.5f,
                "Short hang releases into a modest pogo, not a full jump");
            Check(body.gravityScale == game.Config.gravityScale && (float)Get(player, "airHangLeft") == 0, "Gravity resumes after the bounded hang");
            game.TogglePause(); float velocity = body.linearVelocity.y; Call(player, "FixedUpdate");
            Check(body.linearVelocity.y == velocity, "Pause does not advance aerial movement"); game.TogglePause();
            player.ResetForRoom(new Vector2(8, KarmaSpace.PlayerHalfHeight), true);
            Check(!(bool)Get(player, "airSlashRunning") && (float)Get(player, "doubleJumpDelay") == 0
                && body.gravityScale == game.Config.gravityScale, "Room reset clears aerial states and restores gravity");
            return "PASS: " + checks + " player aerial sprite, unlock, double jump, release, miss, pogo and reset assertions";
        }
        finally
        {
            Physics2D.simulationMode = mode; InputSystem.RemoveDevice(keyboard);
            game.enabled = player.enabled = true;
            player.ResetForRoom(new Vector2(8, KarmaSpace.PlayerHalfHeight), true);
        }
    }
}
#endif
