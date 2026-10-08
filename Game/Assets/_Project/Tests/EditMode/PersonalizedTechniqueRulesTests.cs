using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace K4RMA.Tests
{
    public class PersonalizedTechniqueRulesTests
    {
        [Test]
        public void PreviewDefaults_LeaveOriginalTechniquesInPlace()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();

            Assert.IsFalse(tuning.previewPiercingSlash);
            Assert.IsFalse(tuning.previewAirHover);
            Assert.IsFalse(tuning.previewCounter);
            Assert.IsTrue(PersonalizedMechanicRouting.SwordWaveOwnsAbility(false, false, true));
            Assert.IsTrue(PersonalizedMechanicRouting.RisingSlashOwnsInput(false));
            Assert.IsTrue(PersonalizedMechanicRouting.GuardOwnsDefense(false));
            Assert.IsFalse(PersonalizedMechanicRouting.PiercingSlashOwnsAbility(false, false));
            Assert.IsFalse(PersonalizedMechanicRouting.AirHoverOwnsAerialAttack(false));
            Assert.IsFalse(PersonalizedMechanicRouting.CounterOwnsDefense(false));

            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void SavedPlayerTuning_PreviewFlagsAreOff()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(
                "Assets/_Project/Data/Prototype/PlayerTuning.asset");

            Assert.IsNotNull(tuning);
            Assert.IsFalse(tuning.previewPiercingSlash);
            Assert.IsFalse(tuning.previewAirHover);
            Assert.IsFalse(tuning.previewCounter);
        }

        [Test]
        public void PreviewRouting_LeavesRunProgressionUntouched()
        {
            var run = RunProgressionState.CreateNewRun();
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            tuning.previewPiercingSlash = true;
            tuning.previewAirHover = true;
            tuning.previewCounter = true;

            Assert.IsTrue(PersonalizedMechanicRouting.PiercingSlashOwnsAbility(tuning.previewPiercingSlash, false));
            Assert.IsFalse(PersonalizedMechanicRouting.SwordWaveOwnsAbility(tuning.previewPiercingSlash, false, true));
            Assert.IsTrue(PersonalizedMechanicRouting.AirHoverOwnsAerialAttack(tuning.previewAirHover));
            Assert.IsFalse(PersonalizedMechanicRouting.RisingSlashOwnsInput(tuning.previewAirHover));
            Assert.IsTrue(PersonalizedMechanicRouting.CounterOwnsDefense(tuning.previewCounter));
            Assert.IsFalse(PersonalizedMechanicRouting.GuardOwnsDefense(tuning.previewCounter));
            Assert.IsTrue(PersonalizedMechanicRouting.LegacyDeflectOwnsAbility(true));
            Assert.IsFalse(PersonalizedMechanicRouting.PiercingSlashOwnsAbility(tuning.previewPiercingSlash, true));

            Assert.AreEqual(1, run.CurrentStage);
            Assert.AreEqual(0, run.PersonalizationOrder.Count);
            Assert.AreEqual(GuardianArchetype.None, run.MainArchetype);
            Assert.IsFalse(run.IsRunComplete);
            Assert.AreEqual(PlayerTechniqueState.Original, run.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Original, run.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Original, run.GetTechniqueState(TechniqueId.Guard));

            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void Routing_LegacyDeflectKeepsAbilityWhenPiercingPreviewIsOn()
        {
            Assert.IsTrue(PersonalizedMechanicRouting.LegacyDeflectOwnsAbility(true));
            Assert.IsFalse(PersonalizedMechanicRouting.PiercingSlashOwnsAbility(true, true));
            Assert.IsFalse(PersonalizedMechanicRouting.SwordWaveOwnsAbility(true, true, true));
        }

        [Test]
        public void Routing_PiercingPreviewReplacesSwordWaveOnly()
        {
            Assert.IsTrue(PersonalizedMechanicRouting.PiercingSlashOwnsAbility(true, false));
            Assert.IsFalse(PersonalizedMechanicRouting.SwordWaveOwnsAbility(true, false, true));
            Assert.IsTrue(PersonalizedMechanicRouting.RisingSlashOwnsInput(false));
            Assert.IsTrue(PersonalizedMechanicRouting.GuardOwnsDefense(false));
        }

        [Test]
        public void Routing_AirHoverPreviewReplacesRisingSlashOnly()
        {
            Assert.IsFalse(PersonalizedMechanicRouting.RisingSlashOwnsInput(true));
            Assert.IsTrue(PersonalizedMechanicRouting.AirHoverOwnsAerialAttack(true));
            Assert.IsTrue(PersonalizedMechanicRouting.SwordWaveOwnsAbility(false, false, true));
            Assert.IsTrue(PersonalizedMechanicRouting.GuardOwnsDefense(false));
        }

        [Test]
        public void Routing_CounterPreviewReplacesGuardOnly()
        {
            Assert.IsFalse(PersonalizedMechanicRouting.GuardOwnsDefense(true));
            Assert.IsTrue(PersonalizedMechanicRouting.CounterOwnsDefense(true));
            Assert.IsFalse(PersonalizedMechanicRouting.LegacyDeflectOwnsAbility(false));
            Assert.IsTrue(PersonalizedMechanicRouting.SwordWaveOwnsAbility(false, false, true));
        }

        [Test]
        public void PiercingSlash_ReadyIdleCanActivate()
        {
            Assert.IsTrue(PiercingSlashRules.CanActivate(true, false, 0f));
        }

        [Test]
        public void PiercingSlash_DeadActiveOrCoolingCannotActivate()
        {
            Assert.IsFalse(PiercingSlashRules.CanActivate(false, false, 0f));
            Assert.IsFalse(PiercingSlashRules.CanActivate(true, true, 0f));
            Assert.IsFalse(PiercingSlashRules.CanActivate(true, false, 0.2f));
            Assert.IsTrue(PiercingSlashRules.CanActivate(true, false, 0f));
        }

        [Test]
        public void PiercingSlash_OneActivationClaimsOneTarget()
        {
            Assert.IsTrue(PiercingSlashRules.TryClaimTarget(false));
            Assert.IsFalse(PiercingSlashRules.TryClaimTarget(true));
        }

        [Test]
        public void PiercingSlash_FarSideClearsTheBodyInsideTheArena()
        {
            var exit = PiercingSlashRules.Resolve(0f, 1, 2f, 0.5f, 0.4f, 0.1f, -10f, 10f);

            Assert.IsTrue(exit.Found);
            Assert.IsTrue(exit.Crossed);
            Assert.AreEqual(3f, exit.X, 0.001f);
            Assert.IsTrue(exit.X > 2f);
            Assert.GreaterOrEqual(Mathf.Abs(exit.X - 2f) + 0.0001f, 0.5f + 0.4f + 0.1f);
        }

        [Test]
        public void PiercingSlash_NegativeFacingMirrorsTheFarSide()
        {
            var exit = PiercingSlashRules.Resolve(0f, -1, -2f, 0.5f, 0.4f, 0.1f, -10f, 10f);

            Assert.IsTrue(exit.Found);
            Assert.IsTrue(exit.Crossed);
            Assert.AreEqual(-3f, exit.X, 0.001f);
        }

        [Test]
        public void PiercingSlash_WallBlocksTheFarSideAndKeepsAClearNearSide()
        {
            var exit = PiercingSlashRules.Resolve(8f, 1, 9.7f, 0.5f, 0.5f, 0.2f, -10f, 10f);

            Assert.IsTrue(exit.Found);
            Assert.IsFalse(exit.Crossed);
            Assert.AreEqual(8.5f, exit.X, 0.001f);
            Assert.GreaterOrEqual(Mathf.Abs(exit.X - 9.7f) + 0.0001f, 1.2f);
            Assert.Less(exit.X + 0.5f, 9.7f - 0.5f);
        }

        [Test]
        public void PiercingSlash_NoExitWhenTheBodyFillsTheArena()
        {
            var exit = PiercingSlashRules.Resolve(0f, 1, 0f, 20f, 0.5f, 0.2f, -10f, 10f);

            Assert.IsFalse(exit.Found);
            Assert.IsFalse(exit.Crossed);
        }

        [Test]
        public void PiercingSlash_MissTravelStaysShortAndInsideTheArena()
        {
            float miss = PiercingSlashRules.MissEndX(0f, 1, 1.35f, 0.45f, -8.4f, 8.4f);
            float edge = PiercingSlashRules.MissEndX(7.9f, 1, 1.35f, 0.45f, -8.4f, 8.4f);
            float outside = PiercingSlashRules.MissEndX(8.2f, 1, 1.35f, 0.45f, -8.4f, 8.4f);

            Assert.AreEqual(1.35f, miss, 0.001f);
            Assert.AreEqual(7.95f, edge, 0.001f);
            Assert.AreEqual(7.95f, outside, 0.001f);
            Assert.LessOrEqual(edge, 8.4f - 0.45f);
        }

        [Test]
        public void PiercingSlash_OpenSweepReachesTheRequestedExit()
        {
            var player = CreateSweepBody(new Vector3(0f, 1f, 0f), out CapsuleCollider capsule);
            try
            {
                Physics.SyncTransforms();
                bool clear = PiercingSlashClearance.TryMove(capsule, player.transform.position, 1.4f, null, out float safeX);

                Assert.IsTrue(clear);
                Assert.AreEqual(1.4f, safeX, 0.02f);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PiercingSlash_SolidWallStopsTheSweepBeforeContact()
        {
            var player = CreateSweepBody(new Vector3(0f, 1f, 0f), out CapsuleCollider capsule);
            var wall = CreateSweepWall(new Vector3(2.2f, 1.2f, 0f), new Vector3(0.5f, 3f, 2f));
            try
            {
                Physics.SyncTransforms();
                bool clear = PiercingSlashClearance.TryMove(capsule, player.transform.position, 6f, null, out float safeX);

                Assert.IsFalse(clear);
                Assert.Greater(safeX, 0.2f);
                Assert.Less(safeX + capsule.radius, wall.bounds.min.x);
                Assert.IsFalse(SweepPoseHits(capsule, safeX, wall));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(wall.gameObject);
            }
        }

        [Test]
        public void PiercingSlash_BlockedFarSideUsesAClearNearSide()
        {
            var player = CreateSweepBody(new Vector3(0f, 1f, 0f), out CapsuleCollider capsule);
            var wall = CreateSweepWall(new Vector3(2.2f, 1.2f, 0f), new Vector3(0.5f, 3f, 2f));
            try
            {
                Physics.SyncTransforms();
                float exit = PiercingSlashClearance.ChooseExit(
                    capsule,
                    player.transform.position,
                    5f,
                    true,
                    -1.6f,
                    true,
                    null,
                    out bool found,
                    out bool crossed);

                Assert.IsTrue(found);
                Assert.IsFalse(crossed);
                Assert.AreEqual(-1.6f, exit, 0.02f);
                Assert.IsFalse(SweepPoseHits(capsule, exit, wall));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(wall.gameObject);
            }
        }

        [Test]
        public void PiercingSlash_WallsOnBothSidesStopOutsideTheGeometry()
        {
            var player = CreateSweepBody(new Vector3(0f, 1f, 0f), out CapsuleCollider capsule);
            var farWall = CreateSweepWall(new Vector3(2.2f, 1.2f, 0f), new Vector3(0.5f, 3f, 2f));
            var nearWall = CreateSweepWall(new Vector3(-2.2f, 1.2f, 0f), new Vector3(0.5f, 3f, 2f));
            try
            {
                Physics.SyncTransforms();
                float exit = PiercingSlashClearance.ChooseExit(
                    capsule,
                    player.transform.position,
                    5f,
                    true,
                    -5f,
                    true,
                    null,
                    out bool found,
                    out bool crossed);

                Assert.IsFalse(found);
                Assert.IsFalse(crossed);
                Assert.Greater(exit, 0.2f);
                Assert.Greater(exit - capsule.radius, nearWall.bounds.max.x);
                Assert.Less(exit + capsule.radius, farWall.bounds.min.x);
                Assert.IsFalse(SweepPoseHits(capsule, exit, farWall));
                Assert.IsFalse(SweepPoseHits(capsule, exit, nearWall));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(farWall.gameObject);
                Object.DestroyImmediate(nearWall.gameObject);
            }
        }

        [Test]
        public void PiercingSlash_IgnoredGuardianDoesNotBlockAndAWallStillDoes()
        {
            var player = CreateSweepBody(new Vector3(0f, 1f, 0f), out CapsuleCollider capsule);
            var guardian = new GameObject("SweepGuardian");
            var wall = CreateSweepWall(new Vector3(3.6f, 1.2f, 0f), new Vector3(0.5f, 3f, 2f));
            try
            {
                guardian.transform.position = new Vector3(1.3f, 1f, 0f);
                var guardianBody = guardian.AddComponent<CapsuleCollider>();
                guardianBody.height = 2f;
                guardianBody.radius = 0.5f;
                Physics.SyncTransforms();

                bool pastGuardian = PiercingSlashClearance.TryMove(
                    capsule,
                    player.transform.position,
                    2.3f,
                    guardianBody,
                    out float pastX);
                bool throughWall = PiercingSlashClearance.TryMove(
                    capsule,
                    player.transform.position,
                    6f,
                    guardianBody,
                    out float stoppedX);

                Assert.IsTrue(pastGuardian);
                Assert.AreEqual(2.3f, pastX, 0.02f);
                Assert.IsFalse(throughWall);
                Assert.Greater(stoppedX, 2.3f);
                Assert.Less(stoppedX + capsule.radius, wall.bounds.min.x);
                Assert.IsFalse(SweepPoseHits(capsule, stoppedX, wall));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(guardian);
                Object.DestroyImmediate(wall.gameObject);
            }
        }

        static GameObject CreateSweepBody(Vector3 position, out CapsuleCollider capsule)
        {
            var player = new GameObject("SweepPlayer");
            player.transform.position = position;
            capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.4f;
            capsule.center = Vector3.zero;
            return player;
        }

        static BoxCollider CreateSweepWall(Vector3 position, Vector3 size)
        {
            var wall = new GameObject("SweepWall");
            wall.transform.position = position;
            var box = wall.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        static bool SweepPoseHits(CapsuleCollider body, float x, Collider solid)
        {
            var position = body.transform.position;
            position.x = x;
            float half = Mathf.Max(0f, body.height * 0.5f - body.radius);
            Vector3 center = position + body.center;
            var overlaps = Physics.OverlapCapsule(
                center + Vector3.up * half,
                center - Vector3.up * half,
                body.radius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] == solid)
                    return true;
            }

            return false;
        }

        [Test]
        public void AirHover_AirborneArmedCanStartOnce()
        {
            var state = NewHover();

            Assert.IsTrue(state.TryBegin(true, true));
            Assert.IsTrue(state.Hovering);
            Assert.IsFalse(state.Armed);
            Assert.IsFalse(state.TryBegin(true, true));
        }

        [Test]
        public void AirHover_GroundedCannotStart()
        {
            var state = NewHover();

            Assert.IsFalse(state.TryBegin(true, false));
            Assert.IsFalse(state.Hovering);
            Assert.IsTrue(state.Armed);
        }

        [Test]
        public void AirHover_DeadCannotStart()
        {
            Assert.IsFalse(NewHover().TryBegin(false, true));
        }

        [Test]
        public void AirHover_WindowEndsAndDoesNotRestartUntilLandingAndCooldown()
        {
            var state = NewHover();
            Assert.IsTrue(state.TryBegin(true, true));

            state.Tick(0.2f, false, false, true);

            Assert.IsFalse(state.Hovering);
            Assert.IsFalse(state.Armed);
            Assert.IsFalse(state.TryBegin(true, true));

            state.Tick(0.1f, true, false, true);
            Assert.IsTrue(state.Armed);
            Assert.IsFalse(state.TryBegin(true, true));

            state.Tick(0.25f, true, false, true);
            Assert.AreEqual(0f, state.CooldownRemaining, 0.0001f);
            Assert.IsFalse(state.TryBegin(true, false));
            Assert.IsTrue(state.TryBegin(true, true));
        }

        [Test]
        public void AirHover_KnockbackClearsHoverUntilLanding()
        {
            var state = NewHover();
            Assert.IsTrue(state.TryBegin(true, true));

            state.Tick(0.05f, false, true, true);

            Assert.IsFalse(state.Hovering);
            Assert.IsFalse(state.Armed);
            Assert.Greater(state.CooldownRemaining, 0f);
            Assert.IsFalse(state.TryBegin(true, true));
        }

        [Test]
        public void AirHover_DeathClearsHoverAndCooldown()
        {
            var state = NewHover();
            Assert.IsTrue(state.TryBegin(true, true));

            state.Tick(0.05f, false, false, false);

            Assert.IsFalse(state.Hovering);
            Assert.AreEqual(0f, state.CooldownRemaining);
            Assert.IsTrue(state.Armed);
            Assert.IsFalse(state.TryBegin(false, true));
        }

        [Test]
        public void Counter_OpenWindowRetaliatesOnceThenCloses()
        {
            var timing = NewCounter();
            Assert.IsTrue(timing.TryOpen());

            Assert.IsTrue(timing.TryReceiveHit(out bool firstRetaliate));
            Assert.IsTrue(firstRetaliate);
            Assert.IsFalse(timing.WindowOpen);
            Assert.IsTrue(timing.LastCloseWasSuccess);
            Assert.AreEqual(0.6f, timing.CooldownRemaining, 0.0001f);

            Assert.IsFalse(timing.TryReceiveHit(out bool secondRetaliate));
            Assert.IsFalse(secondRetaliate);
        }

        [Test]
        public void Counter_SuccessCooldownIsNotReplacedByWhiff()
        {
            var timing = NewCounter();
            timing.TryOpen();
            timing.TryReceiveHit(out _);

            timing.Tick(0.1f);

            Assert.AreEqual(0.5f, timing.CooldownRemaining, 0.0001f);
            Assert.IsTrue(timing.LastCloseWasSuccess);
            Assert.IsFalse(timing.TryOpen());
        }

        [Test]
        public void Counter_WhiffStartsCooldownAndDoesNotReceive()
        {
            var timing = NewCounter();
            Assert.IsTrue(timing.TryOpen());

            timing.Tick(0.2f);

            Assert.IsFalse(timing.WindowOpen);
            Assert.IsFalse(timing.LastCloseWasSuccess);
            Assert.AreEqual(0.4f, timing.CooldownRemaining, 0.0001f);
            Assert.IsFalse(timing.TryReceiveHit(out bool retaliate));
            Assert.IsFalse(retaliate);
            Assert.IsFalse(timing.TryOpen());
        }

        [Test]
        public void Counter_CooldownThenAllowsAnotherWindow()
        {
            var timing = NewCounter();
            timing.TryOpen();
            timing.Tick(0.2f);

            timing.Tick(0.4f);

            Assert.AreEqual(0f, timing.CooldownRemaining, 0.0001f);
            Assert.IsTrue(timing.TryOpen());
            Assert.IsTrue(timing.WindowOpen);
        }

        [Test]
        public void Counter_ResetClearsTheWindow()
        {
            var timing = NewCounter();
            timing.TryOpen();
            timing.TryReceiveHit(out _);

            timing.Reset();

            Assert.IsFalse(timing.WindowOpen);
            Assert.AreEqual(0f, timing.CooldownRemaining);
            Assert.IsFalse(timing.LastCloseWasSuccess);
        }

        [Test]
        public void Counter_BlocksPlayerDamageOnceAndRetaliatesOnce()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            tuning.previewCounter = true;
            tuning.counterDamage = 9;
            tuning.counterWindowSeconds = 0.3f;
            var player = new GameObject("CounterPlayer");
            var attacker = new GameObject("CounterAttacker");
            try
            {
                var playerHealth = player.AddComponent<Health>();
                playerHealth.ConfigureMax(100);
                var counter = player.AddComponent<PlayerCounter>();
                counter.Configure(tuning, playerHealth, null);
                var attackerHealth = attacker.AddComponent<Health>();
                attackerHealth.ConfigureMax(40);

                Assert.IsTrue(counter.TryOpenWindow());
                Assert.IsFalse(playerHealth.ApplyDamage(12, attacker));
                Assert.AreEqual(100, playerHealth.Current);
                Assert.AreEqual(31, attackerHealth.Current);

                Assert.IsTrue(playerHealth.ApplyDamage(12, attacker));
                Assert.AreEqual(88, playerHealth.Current);
                Assert.AreEqual(31, attackerHealth.Current);
            }
            finally
            {
                DestroyTestObjects(player, attacker, tuning);
            }
        }

        [Test]
        public void Counter_PreviewOffDoesNotBlock()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            tuning.previewCounter = false;
            var player = new GameObject("OriginalGuardPlayer");
            var attacker = new GameObject("OriginalGuardAttacker");
            try
            {
                var playerHealth = player.AddComponent<Health>();
                playerHealth.ConfigureMax(100);
                var counter = player.AddComponent<PlayerCounter>();
                counter.Configure(tuning, playerHealth, null);
                var attackerHealth = attacker.AddComponent<Health>();
                attackerHealth.ConfigureMax(40);

                Assert.IsFalse(counter.TryOpenWindow());
                Assert.IsTrue(playerHealth.ApplyDamage(5, attacker));
                Assert.AreEqual(95, playerHealth.Current);
                Assert.AreEqual(40, attackerHealth.Current);
            }
            finally
            {
                DestroyTestObjects(player, attacker, tuning);
            }
        }

        [Test]
        public void CounterStagger_DoesNotDamageAndIsNotDeflectPunish()
        {
            var bossObject = new GameObject("CounterStaggerBoss");
            try
            {
                var health = bossObject.AddComponent<Health>();
                health.ConfigureMax(50);
                var boss = bossObject.AddComponent<BossController>();
                var healthField = typeof(BossController).GetField(
                    "health",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(healthField);
                healthField.SetValue(boss, health);

                boss.ApplyCounterStagger(0.45f);

                Assert.AreEqual(BossState.Hurt, boss.State);
                Assert.AreEqual(50, health.Current);

                boss.ApplyDeflectPunish(8, 0.55f);

                Assert.AreEqual(42, health.Current);
                Assert.AreEqual(BossState.Hurt, boss.State);
            }
            finally
            {
                Object.DestroyImmediate(bossObject);
            }
        }

        [Test]
        public void Counter_DoesNotProtectEnemyHealth()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            tuning.previewCounter = true;
            tuning.counterDamage = 9;
            var player = new GameObject("CounterOwner");
            var enemy = new GameObject("UnprotectedEnemy");
            try
            {
                var playerHealth = player.AddComponent<Health>();
                var counter = player.AddComponent<PlayerCounter>();
                counter.Configure(tuning, playerHealth, null);
                Assert.IsTrue(counter.TryOpenWindow());

                var enemyHealth = enemy.AddComponent<Health>();
                enemyHealth.ConfigureMax(50);
                Assert.IsTrue(enemyHealth.ApplyDamage(10, player));
                Assert.AreEqual(40, enemyHealth.Current);
            }
            finally
            {
                DestroyTestObjects(player, enemy, tuning);
            }
        }

        static AirHoverState NewHover()
        {
            return new AirHoverState
            {
                Duration = 0.2f,
                CooldownDuration = 0.5f
            };
        }

        static CounterTiming NewCounter()
        {
            return new CounterTiming
            {
                WindowDuration = 0.2f,
                WhiffCooldown = 0.4f,
                SuccessCooldown = 0.6f
            };
        }

        static void DestroyTestObjects(GameObject first, GameObject second, PlayerTuning tuning)
        {
            var sparks = Object.FindObjectsByType<SparkBurst>(FindObjectsSortMode.None);
            for (int i = 0; i < sparks.Length; i++)
            {
                if (sparks[i] != null)
                    Object.DestroyImmediate(sparks[i].gameObject);
            }

            if (first != null)
                Object.DestroyImmediate(first);
            if (second != null)
                Object.DestroyImmediate(second);
            if (tuning != null)
                Object.DestroyImmediate(tuning);
        }
    }
}
