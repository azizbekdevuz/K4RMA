#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run in Play Mode against the authored scene. Does not save scene or configuration changes.
public static class KarmaGuardBasicDodgeChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static async Task<string> Run(bool baseline = false)
    {
        Check(Application.isPlaying, "Play Mode required");
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        Check(game != null, "K4RMA scene required");
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        var results = new List<string>();
        Keyboard keyboard = null;
        var orders = new[] {
            new[] { Essence.Guard },
            new[] { Essence.Guard, Essence.SwordWave },
            new[] { Essence.Guard, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash },
            new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave }
        };
        try
        {
            int frame = Time.frameCount;
            await Task.Delay(100);
            Check(Time.frameCount > frame, "Player loop must advance during validation");
            foreach (var order in orders)
            foreach (int side in new[] { -1, 1 })
            foreach (bool jump in new[] { false, true })
            foreach (bool shielded in new[] { true, false })
            {
                if (baseline && (order.Length != 1 || side != 1)) continue;
                game.StartRun();
                foreach (var essence in order) { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
                typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
                Call(game, "LoadRoom", true);
                var enemy = game.Enemies[0];
                var body = game.Player.GetComponent<Rigidbody2D>();
                bool liveControls = !baseline && order.Length == 1 && side == 1 && jump;
                if (liveControls && keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>();
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                body.bodyType = liveControls ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
                Action<Vector2> place = position => { body.position = position; game.Player.transform.position = position; };
                enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
                place(new Vector2(20 + side * 6, KarmaSpace.PlayerHalfHeight));
                Set(game.Player, "invulnerableUntil", 0f);
                Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
                Set(enemy, "nextShield", float.MaxValue);
                Call(enemy, "InterruptPattern");
                if (!shielded) Set(enemy, "shieldUntil", 0f);
                Set(enemy, "selected", EnemyAttack.GroundStrike); Set(enemy, "direction", (float)side);
                Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
                float health = game.Player.Health;
                var animator = ((Transform)Get(enemy, "art")).GetComponent<KarmaCharacterAnimator>();
                enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
                float started = enemy.FightSeconds;
                bool dodged = false, sawSwing = false, sawRecovery = false;
                bool bufferedSecondJump = false;
                float recoverFrom = 0;
                string scenario = string.Join(">", order) + " / " + side + " / " + (jump ? "jump" : "range")
                    + " / shield=" + shielded + (liveControls ? " / player physics" : "");
                double deadline = EditorApplication.timeSinceStartup + 6;
                while ((bool)Get(enemy, "running"))
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Missed basic attack must finish without timing out");
                    float dodgeAt = liveControls && jump ? 0.52f : 0.25f;
                    if (!dodged && enemy.FightSeconds - started > dodgeAt)
                    {
                        if (liveControls && jump)
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                            InputSystem.Update();
                            Set(game.Player, "jumpBufferedUntil", Time.time + 0.12f);
                        }
                        else if (liveControls) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                        else place(new Vector2(20 + side * (jump ? 6 : 18), jump ? 12 : KarmaSpace.PlayerHalfHeight));
                        dodged = true;
                    }
                    if (liveControls && jump && sawSwing && !bufferedSecondJump && enemy.FightSeconds - started > 1.4f)
                    {
                        Set(game.Player, "jumpBufferedUntil", Time.time + 0.12f);
                        bufferedSecondJump = true;
                    }
                    string state = Get(enemy, "state").ToString();
                    bool activeHit = ((Transform)Get(enemy, "effectsRoot")).GetComponentsInChildren<KarmaBossAttack>().Length > 0;
                    if (state == "Attack" && activeHit)
                    {
                        if (liveControls && jump && !sawSwing)
                            Check(game.Player.Airborne, "Buffered jump must launch the player before the first swing: " + scenario);
                        sawSwing = true;
                        Check(animator.CurrentPose == "Thrust", "Active guard basic attack must show Thrust, not a frozen windup: " + animator.CurrentPose);
                    }
                    if (!sawRecovery && state == "Recover") { sawRecovery = true; recoverFrom = enemy.FightSeconds; }
                    Check(!enemy.Stunned && !game.Player.Stunned, "Dodging basic attacks must not create a stun: " + scenario);
                    Check(game.Player.Health == health, "Basic attack contacted player during " + scenario
                        + " at t=" + (enemy.FightSeconds - started).ToString("F3") + ", player=" + game.Player.Position + ", boss=" + enemy.Position);
                    await Task.Delay(10);
                }
                Check(sawSwing && sawRecovery, "Basic attack must show its swing and normal recovery");
                Check(enemy.AutoShieldActive == shielded, "Missing a basic attack preserves the initial GD01 state");
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                float recovered = enemy.FightSeconds;
                Vector2 chaseStart = enemy.Position;
                place(new Vector2(chaseStart.x + side * 12, KarmaSpace.PlayerHalfHeight));
                string chasePose = null;
                int movedFrame = -1;
                deadline = EditorApplication.timeSinceStartup + 2;
                while (!(bool)Get(enemy, "running") && EditorApplication.timeSinceStartup < deadline)
                {
                    if (Mathf.Abs(enemy.Position.x - chaseStart.x) > 0.2f && chasePose == null)
                    {
                        if (movedFrame < 0) movedFrame = Time.frameCount;
                        if (Time.frameCount <= movedFrame) { await Task.Delay(10); continue; }
                        chasePose = animator.CurrentPose;
                        if (!baseline && shielded)
                        {
                            var damageSprite = (Sprite)Get(animator, "guardianAppearance")
                                ?? ((Dictionary<string, Sprite>)Get(animator, "poses"))["Guard"];
                            float expectedWidth = damageSprite.bounds.size.x * Mathf.Abs(animator.transform.lossyScale.x)
                                * ((Vector3)Get(animator, "pictureScale")).x;
                            Check(Mathf.Abs(enemy.DamageBounds.size.x - expectedWidth) < 0.01f,
                                "Moving with GD01 keeps the original shielded damage silhouette");
                        }
                    }
                    Check(!enemy.Stunned, "Recovery must return to movement without stun");
                    await Task.Delay(10);
                }
                Check((bool)Get(enemy, "running"), "Boss must resume its normal decisions after evasion");
                if (!baseline) Check(chasePose == "Move", "Guard must show movement on chase with or without GD01: " + chasePose + " / " + scenario);
                results.Add("PASS " + scenario + " / chase=" + chasePose + " / recovery=" + (recovered - recoverFrom).ToString("F3")
                    + " / next=" + (enemy.FightSeconds - recovered).ToString("F3"));
            }
            string result = "PASS: " + results.Count + " guard basic attack evasion cases\n" + string.Join("\n", results);
            Directory.CreateDirectory("output/validation");
            File.WriteAllText("output/validation/" + (baseline ? "guard-basic-shield-before.txt" : "week11-basic-dodge-validation.txt"), result);
            return result;
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            game.ReturnToTitle();
            Application.runInBackground = background;
        }
    }
}
#endif
