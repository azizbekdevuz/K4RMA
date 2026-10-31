#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;

// Policy checks use the live config; one approach probe starts and cancels a pattern under a complete state snapshot.
public static class KarmaWavePolicyChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static int Run(KarmaGame game, KarmaEnemy enemy)
    {
        int assertions = 0;
        Action<bool, string> check = (ok, label) =>
        {
            assertions++;
            if (!ok) throw new Exception("Wave policy: " + label);
        };
        var tuning = game.Config.BossTuning;
        check(Mathf.Abs(tuning.swordDecisionMultiplier - 0.75f) < 0.00001f,
            "existing serialized config receives the new 0.75 decision multiplier default");
        check(Mathf.Abs(tuning.swordSelectionRetry - 0.1f) < 0.00001f,
            "existing serialized config receives the new 0.1 selection retry default");
        check(Mathf.Abs(tuning.ultimateCooldown - 20) < 0.00001f, "SW05 keeps its 20-second ultimate cooldown");

        string[] changed = { "<Health>k__BackingField", "<PrimaryType>k__BackingField", "attacks",
            "previous", "cycleIndex", "nextUltimate", "nextSwordBasic", "nextSwordRetreat" };
        var saved = new Dictionary<string, object>();
        foreach (string name in changed) saved.Add(name, Field(name).GetValue(enemy));
        object originalSelected = Field("selected").GetValue(enemy);
        object originalRunning = Field("running").GetValue(enemy);
        var playerBody = game.Player.GetComponent<Rigidbody2D>();
        Vector3 originalPlayer = game.Player.transform.position;
        Vector2 originalBody = playerBody.position;
        try
        {
            // These probes isolate inherited skill eligibility; the ordinary
            // attack and bounded retreat have their own live combat checks.
            Set(enemy, "nextSwordBasic", float.MaxValue);
            Set(enemy, "nextSwordRetreat", float.MaxValue);
            Set(enemy, "<PrimaryType>k__BackingField", (Essence?)Essence.SwordWave);
            var regular = new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04 };
            for (int phase = 0; phase < 2; phase++)
            {
                Set(enemy, "<Health>k__BackingField", enemy.MaxHealth * (phase == 0 ? 1 : 0.4f));
                foreach (var attack in regular)
                    check(Mathf.Abs(Delay(enemy, attack) - (phase == 0 ? 0.378f : 0.30996f)) < 0.00001f,
                        attack + " post-recovery idle is 25% shorter in phase " + phase);
                check(Mathf.Abs(Delay(enemy, EnemyAttack.SW05) - (phase == 0 ? 0.504f : 0.41328f)) < 0.00001f,
                    "SW05 post-recovery idle is unchanged in phase " + phase);
                foreach (var type in new[] { Essence.RisingSlash, Essence.Guard })
                {
                    Set(enemy, "<PrimaryType>k__BackingField", (Essence?)type);
                    check(Mathf.Abs(Delay(enemy, type == Essence.RisingSlash ? EnemyAttack.UP01 : EnemyAttack.GD02)
                        - (phase == 0 ? 0.504f : 0.41328f)) < 0.00001f,
                        type + " regular attack idle is unchanged in phase " + phase);
                    check(Mathf.Abs(Delay(enemy, type == Essence.RisingSlash ? EnemyAttack.UP05 : EnemyAttack.GD05)
                        - (phase == 0 ? 0.504f : 0.41328f)) < 0.00001f,
                        type + " ultimate attack idle is unchanged in phase " + phase);
                }
                Set(enemy, "<PrimaryType>k__BackingField", (Essence?)Essence.SwordWave);
            }

            Set(enemy, "<Health>k__BackingField", enemy.MaxHealth);
            var orders = new[]
            {
                new[] { Essence.SwordWave }, new[] { Essence.SwordWave, Essence.RisingSlash },
                new[] { Essence.SwordWave, Essence.Guard }, new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard }
            };
            var expected = new[]
            {
                new[] { EnemyAttack.SW01, EnemyAttack.SW02 },
                new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03 },
                new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW04 },
                new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 }
            };
            for (int stage = 0; stage < orders.Length; stage++)
            {
                Set(enemy, "attacks", KarmaPatternCatalog.BuildCycle(orders[stage]));
                Set(enemy, "cycleIndex", 0);
                Set(enemy, "previous", (EnemyAttack)(-1));
                Set(enemy, "nextUltimate", enemy.FightSeconds);
                for (int i = 0; i < expected[stage].Length * 2; i++)
                {
                    EnemyAttack next;
                    bool available = Select(game, enemy, KarmaSpace.Units(7), true, out next);
                    check(available && next == expected[stage][i % expected[stage].Length],
                        "stage variant " + stage + " preserves inherited cycle and unlocks at selection " + i);
                    Set(enemy, "previous", next);
                }
            }

            Set(enemy, "attacks", KarmaPatternCatalog.BuildCycle(orders[0]));
            Set(enemy, "cycleIndex", 0);
            Set(enemy, "previous", (EnemyAttack)(-1));
            EnemyAttack chosen;
            check(Select(game, enemy, KarmaSpace.Units(3.9f), true, out chosen) && chosen == EnemyAttack.SW02,
                "SW01 retains its minimum four-unit distance and SW02 remains available nearby");
            Set(enemy, "cycleIndex", 0);
            check(Select(game, enemy, KarmaSpace.Units(4), false, out chosen) && chosen == EnemyAttack.SW01,
                "SW01 still works at the exact minimum distance while SW02 requires sight");

            Set(enemy, "cycleIndex", 0);
            Set(enemy, "previous", EnemyAttack.SW01);
            check(Select(game, enemy, KarmaSpace.Units(7), true, out chosen) && chosen == EnemyAttack.SW02,
                "an eligible alternative prevents repeating the previous skill");
            Set(enemy, "cycleIndex", 0);
            Set(enemy, "previous", EnemyAttack.SW02);
            check(Select(game, enemy, KarmaSpace.Units(3.9f), true, out chosen) && chosen == EnemyAttack.SW02,
                "the sole eligible skill may repeat instead of forcing indefinite idle");
            Set(enemy, "cycleIndex", 0);
            check(!Select(game, enemy, KarmaSpace.Units(3.9f), false, out chosen),
                "close range plus blocked sight leaves no eligible Stage 2 skill");
            check(chosen != EnemyAttack.GroundStrike && Equals(Field("selected").GetValue(enemy), originalSelected)
                && Equals(Field("running").GetValue(enemy), originalRunning),
                "failed selection does not inject GroundStrike or start another pattern");

            Set(enemy, "attacks", KarmaPatternCatalog.BuildCycle(orders[3]));
            Set(enemy, "cycleIndex", 4);
            Set(enemy, "previous", EnemyAttack.SW04);
            Set(enemy, "nextUltimate", enemy.FightSeconds + tuning.ultimateCooldown);
            check(Select(game, enemy, KarmaSpace.Units(7), true, out chosen) && chosen == EnemyAttack.SW01,
                "a cooling-down SW05 yields to an unlocked regular attack");
            Set(enemy, "cycleIndex", 4);
            Set(enemy, "nextUltimate", enemy.FightSeconds);
            check(Select(game, enemy, KarmaSpace.Units(7), true, out chosen) && chosen == EnemyAttack.SW05,
                "SW05 becomes eligible when its existing cooldown expires");
            Set(enemy, "cycleIndex", 4);
            Set(enemy, "previous", EnemyAttack.SW05);
            check(Select(game, enemy, KarmaSpace.Units(7), true, out chosen) && chosen == EnemyAttack.SW01,
                "the existing ultimate repeat restriction remains effective even when ready");
            EngagementChecks(game, enemy, check);
        }
        finally
        {
            foreach (var pair in saved) Field(pair.Key).SetValue(enemy, pair.Value);
            playerBody.position = originalBody;
            game.Player.transform.position = originalPlayer;
        }
        Debug.Log("PASS: " + assertions + " Wave cooldown, inherited selection and repeat policy assertions");
        return assertions;
    }

    static FieldInfo Field(string name) => typeof(KarmaEnemy).GetField(name, Private);
    static void Set(KarmaEnemy enemy, string name, object value) => Field(name).SetValue(enemy, value);
    static float Delay(KarmaEnemy enemy, EnemyAttack attack)
        => (float)typeof(KarmaEnemy).GetMethod("DecisionDelay", Private).Invoke(enemy, new object[] { attack });
    static bool Select(KarmaGame game, KarmaEnemy enemy, float distance, bool sight, out EnemyAttack next)
    {
        PlacePlayer(game, new Vector2(enemy.Position.x + distance, KarmaSpace.PlayerHalfHeight));
        object[] args = { distance, sight, true, EnemyAttack.GroundStrike };
        bool available = (bool)typeof(KarmaEnemy).GetMethod("TrySelectSwordAttack", Private).Invoke(enemy, args);
        next = (EnemyAttack)args[3];
        return available;
    }
    static void PlacePlayer(KarmaGame game, Vector2 at)
    {
        game.Player.GetComponent<Rigidbody2D>().position = at;
        game.Player.transform.position = at;
    }
    static bool Reach(KarmaEnemy enemy, EnemyAttack attack, Vector2 target)
        => (bool)typeof(KarmaEnemy).GetMethod("SwordAttackCanReach", Private).Invoke(enemy, new object[] { attack, target });
    static bool Shot(KarmaEnemy enemy, Vector2 origin, Vector2 velocity, Vector2 size, float life, Vector2 target)
        => (bool)typeof(KarmaEnemy).GetMethod("SwordShotCanReach", Private).Invoke(enemy, new object[] { origin, velocity, size, life, target });
    static bool Available(KarmaEnemy enemy)
        => (bool)typeof(KarmaEnemy).GetMethod("HasSwordCandidate", Private).Invoke(enemy, new object[] { enemy.Position });

    static void EngagementChecks(KarmaGame game, KarmaEnemy enemy, Action<bool, string> check)
    {
        check(game.Phase == RunPhase.Combat && !game.Paused,
            "engagement checks run in actual unpaused combat");
        var enemyState = Snapshot(enemy);
        var animator = (KarmaCharacterAnimator)Field("animator").GetValue(enemy);
        var animatorState = Snapshot(animator);
        var transforms = enemy.GetComponentsInChildren<Transform>(true);
        var positions = new Vector3[transforms.Length];
        var rotations = new Quaternion[transforms.Length];
        var scales = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            positions[i] = transforms[i].localPosition;
            rotations[i] = transforms[i].localRotation;
            scales[i] = transforms[i].localScale;
        }
        var renderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
        var sprites = new Sprite[renderers.Length];
        var colors = new Color[renderers.Length];
        var enabled = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            sprites[i] = renderers[i].sprite;
            colors[i] = renderers[i].color;
            enabled[i] = renderers[i].enabled;
        }
        Transform player = game.Player.transform;
        var playerPosition = player.position;
        var body = game.Player.GetComponent<Rigidbody2D>();
        var bodyPosition = body.position;
        var bodyRotation = body.rotation;
        var bodyVelocity = body.linearVelocity;
        float bodyAngularVelocity = body.angularVelocity;
        try
        {
            Set(enemy, "<Health>k__BackingField", enemy.MaxHealth);
            Set(enemy, "state", Enum.Parse(Field("state").FieldType, "Approach"));
            foreach (string name in new[] { "stopped", "running", "pendingParry", "announcedEnrage",
                "fullShield", "frontalGuard", "swordGuard" }) Set(enemy, name, false);
            Set(enemy, "shieldUntil", 0f);
            Set(enemy, "shieldWarningUntil", 0f);
            Set(enemy, "stunnedUntil", 0f);
            Set(enemy, "selected", EnemyAttack.SW01);
            body.position = new Vector2(4, KarmaSpace.Units(1));
            player.position = new Vector3(4, KarmaSpace.Units(1), 0);
            var update = typeof(KarmaEnemy).GetMethod("Update", Private);
            var animatorPrevious = typeof(KarmaCharacterAnimator).GetField("guardianPreviousPosition", Private);
            for (int variant = 0; variant < 4; variant++)
            {
                Set(enemy, "<PrimaryType>k__BackingField", (Essence?)(variant == 1 ? Essence.RisingSlash
                    : variant == 2 ? Essence.Guard : Essence.SwordWave));
                Set(enemy, "engaged", false);
                Set(enemy, "<FightSeconds>k__BackingField", 0f);
                Set(enemy, "timer", 10f); // Exercise Update without ever reaching BeginAttack.
                enemy.transform.position = new Vector3(variant == 3 ? 30 : 10, KarmaSpace.GuardianGroundY, 0);
                animatorPrevious.SetValue(animator, animator.transform.position);
                update.Invoke(enemy, null);
                bool engaged = (bool)Field("engaged").GetValue(enemy);
                if (variant == 0)
                {
                    check(engaged && enemy.FightSeconds > 0,
                        "Wave guardian actually engages a nearby player at x=4 before the legacy x=12 gate");
                    check(!(bool)Field("running").GetValue(enemy) && (float)Field("timer").GetValue(enemy) > 0,
                        "nearby engagement respects the remaining idle and does not start a second pattern");
                }
                else check(!engaged && enemy.FightSeconds == 0,
                    variant == 3 ? "a distant Wave guardian keeps the original entry gate"
                        : "non-Wave type " + variant + " retains its original entry gate");
            }
            ReachAndApproachChecks(game, enemy, check);
        }
        finally
        {
            Restore(enemy, enemyState);
            Restore(animator, animatorState);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localPosition = positions[i];
                transforms[i].localRotation = rotations[i];
                transforms[i].localScale = scales[i];
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sprite = sprites[i];
                renderers[i].color = colors[i];
                renderers[i].enabled = enabled[i];
            }
            body.position = bodyPosition;
            body.rotation = bodyRotation;
            body.linearVelocity = bodyVelocity;
            body.angularVelocity = bodyAngularVelocity;
            player.position = playerPosition;
        }
    }

    static void ReachAndApproachChecks(KarmaGame game, KarmaEnemy enemy, Action<bool, string> check)
    {
        var tuning = game.Config.BossTuning;
        float originalSpeed = tuning.swordSpeed;
        int originalCount = tuning.aerialSwordCount;
        bool probeStarted = false;
        try
        {
            check(!(bool)Field("running").GetValue(enemy), "approach probe begins without another running pattern");
            Set(enemy, "<PrimaryType>k__BackingField", (Essence?)Essence.SwordWave);
            Set(enemy, "<Health>k__BackingField", enemy.MaxHealth);
            var size = new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), tuning.swordWidth),
                Mathf.Clamp(tuning.swordHeight, KarmaSpace.Units(0.2f), KarmaSpace.Units(0.8f)));
            float life = Mathf.Clamp(tuning.swordLifetime, 0.5f, 2);
            var origin = new Vector2(0, KarmaSpace.Units(1.05f));
            var velocity = Vector2.right * Mathf.Max(KarmaSpace.Scale, tuning.swordSpeed);
            float edge = origin.x + velocity.x * life + size.x / 2 + KarmaSpace.PlayerHalfWidth;
            check(Shot(enemy, origin, velocity, size, life, new Vector2(edge - 0.001f, KarmaSpace.PlayerHalfHeight))
                && !Shot(enemy, origin, velocity, size, life, new Vector2(edge + 0.001f, KarmaSpace.PlayerHalfHeight)),
                "reach boundary uses actual origin, speed, lifetime and expanded hit width");
            check(!Shot(enemy, new Vector2(0, KarmaSpace.Units(11.5f)), Vector2.up * tuning.swordSpeed,
                size, life, new Vector2(0, KarmaSpace.Units(14))), "arena exit limits the reachable segment");

            enemy.transform.position = new Vector3(-13, KarmaSpace.GuardianGroundY, 0);
            var far = new Vector2(53.5f, KarmaSpace.PlayerHalfHeight);
            check(!Reach(enemy, EnemyAttack.SW01, far) && Reach(enemy, EnemyAttack.SW02, far),
                "long range rejects unreachable alternating waves while retaining an aimed shot that can arrive");
            enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = enemy.Position.x + side * KarmaSpace.Units(7);
                check(Reach(enemy, EnemyAttack.SW01, new Vector2(x, KarmaSpace.PlayerHalfHeight))
                    && Reach(enemy, EnemyAttack.SW01, new Vector2(x, KarmaSpace.Units(3.3f))),
                    "both locked lanes remain reachable facing " + side);
                check(!Reach(enemy, EnemyAttack.SW01, new Vector2(x, KarmaSpace.Units(2.175f)))
                    && !Reach(enemy, EnemyAttack.SW03, new Vector2(x, KarmaSpace.Units(2.175f)))
                    && Reach(enemy, EnemyAttack.SW02, new Vector2(x, KarmaSpace.Units(2.175f))),
                    "airborne gap between horizontal lanes routes to the actual aimed trajectory facing " + side);
            }
            check(Reach(enemy, EnemyAttack.SW02, new Vector2(20.5f, KarmaSpace.PlayerHalfHeight)),
                "ultraclose horizontal crescent reaches the player on the aimed side of the boss");
            check(Reach(enemy, EnemyAttack.SW04, new Vector2(34, KarmaSpace.PlayerHalfHeight)),
                "counter ward keeps its real boss-center aimed reach");
            enemy.transform.position = new Vector3(KarmaStage.BossLeftWall, KarmaSpace.GuardianGroundY, 0);
            check(Reach(enemy, EnemyAttack.SW02, new Vector2(KarmaStage.ArenaLeft + KarmaSpace.PlayerHalfWidth, KarmaSpace.PlayerHalfHeight)),
                "wall-adjacent aimed origin retains immediate expanded-box overlap");
            enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
            check(Reach(enemy, EnemyAttack.SW05, new Vector2(40, KarmaSpace.PlayerHalfHeight)),
                "the random aerial pattern retains an actual full-life directly aimed first volley");
            tuning.swordSpeed = 1;
            check(!Reach(enemy, EnemyAttack.SW05, new Vector2(40, KarmaSpace.PlayerHalfHeight)),
                "SW05 selection rejects custom slow casts that cannot finish seven full-boundary volleys before the watchdog");
            tuning.swordSpeed = originalSpeed;
            var randomState = UnityEngine.Random.state;
            try
            {
                var seeds = new HashSet<int>();
                for (int sample = 0; sample < 32; sample++) seeds.Add(KarmaAerialSwordPlanner.ResolveSeed(0));
                check(seeds.Count == 32, "normal SW05 casts receive fresh seeds rather than a repeating scripted plan");
                check(KarmaAerialSwordPlanner.ResolveSeed(123456) == 123456,
                    "an explicit SW05 test seed remains fixed");
            }
            finally { UnityEngine.Random.state = randomState; }

            tuning.swordSpeed = KarmaSpace.Units(2);
            // Isolate the finite SW01 range. SW02 deliberately reaches the arena
            // boundary even with slow tuning, so it cannot serve this retry probe.
            Set(enemy, "attacks", new List<EnemyAttack> { EnemyAttack.SW01 });
            Set(enemy, "nextSwordBasic", float.MaxValue);
            Set(enemy, "previous", (EnemyAttack)(-1));
            Set(enemy, "cycleIndex", 0);
            Set(enemy, "engaged", true);
            Set(enemy, "timer", 0f);
            Set(enemy, "<FightSeconds>k__BackingField", 0f);
            // Cross the reach boundary between retry decisions: a blind close retreat
            // would push this target out of range again before the next decision.
            PlacePlayer(game, new Vector2(31.4f, KarmaSpace.PlayerHalfHeight));
            check(!Available(enemy), "short configured range has no usable attack at the approach target");
            int cycle = (int)Field("cycleIndex").GetValue(enemy);
            Available(enemy); Available(enemy);
            check((int)Field("cycleIndex").GetValue(enemy) == cycle, "movement availability queries never advance the pattern cycle");
            typeof(KarmaEnemy).GetMethod("BeginAttack", Private).Invoke(enemy, null);
            check(!(bool)Field("running").GetValue(enemy)
                && Mathf.Abs((float)Field("timer").GetValue(enemy) - Mathf.Max(0.05f, tuning.swordSelectionRetry)) < 0.00001f,
                "an unreachable selection retains the original retry interval without starting a pattern");
            Set(enemy, "timer", 0f);
            var update = typeof(KarmaEnemy).GetMethod("Update", Private);
            float beforeX = enemy.Position.x;
            update.Invoke(enemy, null);
            check(enemy.Position.x > beforeX && !(bool)Field("running").GetValue(enemy),
                "failed selection continues approaching within preferred distance without starting an unreachable pattern");
            check((float)Field("timer").GetValue(enemy) == 0,
                "needed approach owns the idle slot before attack selection and leaves its pending timer intact");
            float repositionedAt = -1;
            float preferred = Mathf.Clamp(tuning.swordPreferredDistance, 7, 9);
            for (int i = 0; i < 32 && !(bool)Field("running").GetValue(enemy); i++)
            {
                update.Invoke(enemy, null);
                bool repositioned = Mathf.Abs(game.Player.Position.x - enemy.Position.x) <= preferred + 0.00001f;
                if (Available(enemy) && repositioned && repositionedAt < 0) repositionedAt = enemy.FightSeconds;
            }
            probeStarted = (bool)Field("running").GetValue(enemy);
            check(probeStarted && repositionedAt >= 0, "approach reaches a usable attack and starts its existing coroutine within 32 frames");
            check(enemy.FightSeconds - repositionedAt <= Mathf.Max(0.05f, tuning.swordSelectionRetry) + Time.deltaTime + 0.00001f,
                "finishing needed spacing starts an attack within the existing retry plus one frame without retreat oscillation");
            check((EnemyAttack)Field("selected").GetValue(enemy) == EnemyAttack.SW01,
                "approach selects the isolated staggered wave when its real finite reach becomes available");
            Vector2 preparedAt = enemy.Position;
            int facing = enemy.Facing;
            PlacePlayer(game, new Vector2(preparedAt.x - KarmaSpace.Units(7), KarmaSpace.PlayerHalfHeight));
            update.Invoke(enemy, null);
            check(enemy.Position == preparedAt && enemy.Facing == facing,
                "attack preparation freezes boss movement and facing after a player crosses behind");
        }
        finally
        {
            tuning.swordSpeed = originalSpeed;
            tuning.aerialSwordCount = originalCount;
            if (probeStarted || (bool)Field("running").GetValue(enemy))
            {
                enemy.StopAllCoroutines();
                typeof(KarmaEnemy).GetMethod("ClearPatternObjects", Private).Invoke(enemy, null);
            }
        }
    }

    static Dictionary<FieldInfo, object> Snapshot(object target)
    {
        var saved = new Dictionary<FieldInfo, object>();
        foreach (var field in target.GetType().GetFields(Private | BindingFlags.DeclaredOnly))
            if (!field.IsInitOnly) saved.Add(field, field.GetValue(target));
        return saved;
    }
    static void Restore(object target, Dictionary<FieldInfo, object> state)
    {
        foreach (var pair in state) pair.Key.SetValue(target, pair.Value);
    }
}
#endif
