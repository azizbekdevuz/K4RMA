#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;

// Called by the existing Play Mode driver. Every attack runs through the real
// player loop; selection, hit ledgers, cleanup and pause use production code.
public static class KarmaSwordCombatChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static KarmaEnemy enemy;
    static int scenario, phase, assertions;
    static float started, health, wardDeadline;
    static Transform ward;
    static readonly HashSet<KarmaBossAttack> ordinary = new HashSet<KarmaBossAttack>(), counters = new HashSet<KarmaBossAttack>();
    static readonly HashSet<string> poses = new HashSet<string>();
    static bool sawArmor, sawShield;
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    static void Check(bool ok, string label)
    { assertions++; if (!ok) throw new Exception("Sword combat scenario " + scenario + ": " + label); }
    static void PlayerAt(KarmaGame game, Vector2 position)
    {
        game.Player.GetComponent<Rigidbody2D>().position = position;
        game.Player.transform.position = position;
        Physics2D.SyncTransforms();
    }
    static void Launch(EnemyAttack attack)
    {
        Set(enemy, "selected", attack); Set(enemy, "running", true);
        Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Prepare(KarmaGame game)
    {
        Set(game.Player.Essences, "progression", new KarmaProgressionState());
        foreach (var e in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard }) game.Player.Essences.Sacrifice(e);
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Call(enemy, "InterruptPattern");
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
        Set(enemy, "nextSwordRetreat", float.MaxValue);
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        PlayerAt(game, new Vector2(24, KarmaSpace.PlayerHalfHeight));
        Set(enemy, "direction", 1f);
        ordinary.Clear(); counters.Clear(); poses.Clear();
        sawArmor = sawShield = false; phase = 1; started = enemy.FightSeconds;
    }
    public static bool Tick(KarmaGame game, out int count)
    {
        count = 0;
        if (scenario >= 9) { count = assertions; return true; }
        if (phase == 0)
        {
            Prepare(game);
            if (scenario == 0) RetreatChecks(game);
            if (scenario < 4)
            {
                // Normal/enraged, close/mid range, and both facing directions.
                typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * (scenario % 2 == 0 ? 1 : 0.45f));
                float side = scenario < 2 ? 1 : -1;
                PlayerAt(game, new Vector2(20 + side * (scenario % 2 == 0 ? 0.5f : 8), KarmaSpace.PlayerHalfHeight));
                health = game.Player.Health;
                Set(game.Player, "invulnerableUntil", -1f);
                Set(enemy, "swordBasicDue", true);
                Call(enemy, "BeginAttack");
                Check(Get<EnemyAttack>(enemy, "selected") == EnemyAttack.SWBasic, "selector chooses the ready filler/close attack");
                Check(Get<float>(enemy, "nextSwordBasic") > enemy.FightSeconds, "ordinary attack starts its cooldown");
            }
            else if (scenario < 7) Launch(EnemyAttack.SW04);
            else
            {
                enemy.transform.position = new Vector3(scenario == 7 ? KarmaStage.BossLeftWall : KarmaStage.BossRightWall,
                    KarmaSpace.GuardianGroundY, 0);
                Launch(EnemyAttack.SW05);
            }
            return false;
        }
        Check(enemy.FightSeconds - started < 25, "live scenario finishes within its watchdog");
        var effects = Get<Transform>(enemy, "effectsRoot");
        var animator = Get<KarmaCharacterAnimator>(enemy, "animator");
        poses.Add(animator.CurrentPose ?? "");
        foreach (var shot in effects.GetComponentsInChildren<KarmaBossAttack>())
        {
            if (ward != null && shot.transform.IsChildOf(ward))
            {
                if (counters.Add(shot))
                    Check(shot.GetComponentInChildren<SpriteRenderer>().sprite == KarmaArtwork.Sprite("ExtraWaveVFX", "SW04_CounterWave"),
                        "counter sprite remains SW04 while another pattern is selected");
            }
            else ordinary.Add(shot);
        }
        bool running = Get<bool>(enemy, "running");
        if (!running) Set(enemy, "timer", float.MaxValue);
        if (scenario < 4)
        {
            if (running) return false;
            Check(ordinary.Count == 1, "basic attack releases one short sword wave");
            Check(Mathf.Abs(health - game.Player.Health - game.Config.enemyDamage * game.Config.BossTuning.damageMultiplier * 0.5f) < 0.1f,
                "basic wave deals half regular skill damage exactly once at close and mid range");
            Check(poses.Contains("Windup") && poses.Contains("Slash") && poses.Contains("Wave") && poses.Contains("FollowThrough"),
                "prototype windup, swing, release and recovery are visible");
            Set(enemy, "swordBasicDue", true);
            Call(enemy, "BeginAttack");
            Check(Get<EnemyAttack>(enemy, "selected") != EnemyAttack.SWBasic, "basic cooldown yields to a skill");
            Call(enemy, "InterruptPattern");
        }
        else if (scenario < 7)
        {
            if (phase == 1)
            {
                if (running || !Get<bool>(enemy, "swordGuard")) return false;
                ward = Get<Transform>(enemy, "swordGuardRoot");
                wardDeadline = Get<float>(enemy, "swordGuardUntil");
                Check(ward != null && wardDeadline > enemy.FightSeconds, "formation frees the action slot while defense remains active");
                var other = scenario == 4 ? EnemyAttack.SWBasic : scenario == 5 ? EnemyAttack.SW01 : EnemyAttack.SW02;
                PlayerAt(game, new Vector2(28, KarmaSpace.PlayerHalfHeight));
                Launch(other);
                enemy.TakeDamage(1);
                // Re-entering the formation cannot stack or replace this ward.
                enemy.StartCoroutine((IEnumerator)Call(enemy, "CounterGuard"));
                Check(Get<Transform>(enemy, "swordGuardRoot") == ward, "duplicate ward activation keeps exactly one root");
                phase = 2;
                return false;
            }
            sawArmor |= running && Get<bool>(enemy, "swordGuard");
            if (Get<bool>(enemy, "swordGuard"))
            {
                Check(enemy.FightSeconds <= wardDeadline + Time.deltaTime, "concurrent attacks never extend defense duration");
                for (int i = 0; i < 10; i++) enemy.TakeDamage(0.1f);
                if (Get<bool>(enemy, "guardResponseRunning"))
                    Check(!Get<bool>(enemy, "guardResponsePending"), "multi-hit cannot queue another retaliation batch");
            }
            if (running || Get<Transform>(enemy, "swordGuardRoot") != null) return false;
            Check(sawArmor && ordinary.Count == (scenario == 5 ? 2 : 1), "ordinary attack overlaps the maintained ward and completes");
            Check(counters.Count == 4, "one hit batch produces exactly four independent counter shots");
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0 && !enemy.Armored,
                "both attack lifetimes end without hostile or armored residue");
            ward = null;
        }
        else
        {
            if (enemy.Invulnerable)
            {
                sawShield = true;
                Check(Vector2.Distance(enemy.Position, new Vector2(KarmaStage.ArenaCenter, KarmaSpace.Units(6))) < 0.001f,
                    "all aerial formation and release frames use the fixed central ceiling");
            }
            if (!sawShield) Check(ordinary.Count == 0, "no aerial swords launch before reaching the ceiling");
            if (running) return false;
            Check(sawShield && ordinary.Count == 105, "edge-origin cast keeps all seven fifteen-sword salvos");
            Check(Vector2.Distance(enemy.Position, new Vector2(KarmaStage.ArenaCenter, KarmaSpace.GuardianGroundY)) < 0.001f,
                "left/right edge cast lands at central ground before recovery ends");
        }
        Debug.Log("PASS: Sword combat live scenario " + scenario);
        scenario++; phase = 0;
        return false;
    }
    static void RetreatChecks(KarmaGame game)
    {
        Set(enemy, "nextSwordRetreat", 0f);
        float initial = enemy.Position.x, initialClock = enemy.FightSeconds;
        for (int i = 0; i < 12; i++)
        {
            Set(enemy, "<FightSeconds>k__BackingField", initialClock + i * 0.05f);
            Call(enemy, "RetreatSword", 0.05f, game.Player.Position.x - enemy.Position.x);
        }
        float travelled = initial - enemy.Position.x;
        Check(travelled > 0 && travelled <= KarmaSpace.Units(1.5f) + 0.001f, "retreat is a single bounded step away from the player");
        Check(!(bool)Call(enemy, "RetreatSword", 0.05f, 1f), "retreat cooldown prevents immediate repetition");
        Set(enemy, "<FightSeconds>k__BackingField", initialClock + 10);
        Check(!(bool)Call(enemy, "RetreatSword", 0.05f, 1f), "elapsed cooldown alone cannot replace the required intervening attack");
        enemy.transform.position = new Vector3(KarmaStage.BossLeftWall + 0.2f, KarmaSpace.GuardianGroundY, 0);
        Set(enemy, "swordAttackedSinceRetreat", true); Set(enemy, "nextSwordRetreat", 0f);
        Check(!(bool)Call(enemy, "RetreatSword", 0.05f, 1f), "wall-adjacent retreat immediately yields to an attack");
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        Set(enemy, "nextSwordRetreat", 0f);
        var block = new GameObject("Retreat blocking terrain probe");
        block.transform.position = new Vector3(18.8f, KarmaSpace.GuardianGroundY, 0);
        block.AddComponent<BoxCollider2D>().size = new Vector2(0.4f, 4);
        block.AddComponent<KarmaSurface>(); Physics2D.SyncTransforms();
        Check(!(bool)Call(enemy, "RetreatSword", 0.05f, 1f), "solid terrain blocks the retreat sweep");
        UnityEngine.Object.DestroyImmediate(block);
        Set(enemy, "<FightSeconds>k__BackingField", initialClock);
        Set(enemy, "nextSwordRetreat", float.MaxValue);
        Set(enemy, "swordRetreatRemaining", 0f);
    }
}
#endif
