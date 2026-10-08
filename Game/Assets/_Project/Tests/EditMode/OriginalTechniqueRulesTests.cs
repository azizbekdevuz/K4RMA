using NUnit.Framework;

namespace K4RMA.Tests
{
    public class OriginalTechniqueRulesTests
    {
        [Test]
        public void Guard_HoldStartsProtection()
        {
            var timing = NewGuard();

            timing.Tick(0.01f, true);

            Assert.IsTrue(timing.IsGuarding);
            Assert.Greater(timing.ActiveRemaining, 0f);
            Assert.AreEqual(0f, timing.CooldownRemaining);
        }

        [Test]
        public void Guard_ReleaseEndsAndStartsCooldown()
        {
            var timing = NewGuard();
            timing.Tick(0.01f, true);

            timing.Tick(0.05f, false);

            Assert.IsFalse(timing.IsGuarding);
            Assert.AreEqual(timing.CooldownDuration, timing.CooldownRemaining);
        }

        [Test]
        public void Guard_ActiveWindowEndsWhileHeld()
        {
            var timing = NewGuard();
            timing.Tick(0.01f, true);

            timing.Tick(timing.ActiveDuration, true);

            Assert.IsFalse(timing.IsGuarding);
            Assert.Greater(timing.CooldownRemaining, 0f);
        }

        [Test]
        public void Guard_HoldDuringCooldownDoesNotRestart()
        {
            var timing = NewGuard();
            timing.Tick(0.01f, true);
            timing.Tick(timing.ActiveDuration, true);

            timing.Tick(0.05f, true);

            Assert.IsFalse(timing.IsGuarding);
            Assert.Greater(timing.CooldownRemaining, 0f);
        }

        [Test]
        public void Guard_AfterCooldownHoldCanStartAgain()
        {
            var timing = NewGuard();
            timing.Tick(0.01f, true);
            timing.Tick(timing.ActiveDuration, true);
            float cooldown = timing.CooldownRemaining;

            timing.Tick(cooldown, true);
            Assert.IsFalse(timing.IsGuarding);

            timing.Tick(0.01f, true);
            Assert.IsTrue(timing.IsGuarding);
        }

        [Test]
        public void Guard_ResetClearsActiveState()
        {
            var timing = NewGuard();
            timing.Tick(0.01f, true);
            timing.Tick(timing.ActiveDuration, true);

            timing.Reset();

            Assert.IsFalse(timing.IsGuarding);
            Assert.AreEqual(0f, timing.ActiveRemaining);
            Assert.AreEqual(0f, timing.CooldownRemaining);
        }

        [Test]
        public void RisingSlash_GroundedIdleCanActivate()
        {
            Assert.IsTrue(RisingSlashRules.CanActivate(true, true, false, 0f));
        }

        [Test]
        public void RisingSlash_AirborneCannotActivate()
        {
            Assert.IsFalse(RisingSlashRules.CanActivate(true, false, false, 0f));
        }

        [Test]
        public void RisingSlash_ActiveSlashCannotActivate()
        {
            Assert.IsFalse(RisingSlashRules.CanActivate(true, true, true, 0f));
        }

        [Test]
        public void RisingSlash_CooldownCannotBeBypassedByRepeatedChecks()
        {
            Assert.IsFalse(RisingSlashRules.CanActivate(true, true, false, 0.2f));
            Assert.IsFalse(RisingSlashRules.CanActivate(true, true, false, 0.01f));
            Assert.IsTrue(RisingSlashRules.CanActivate(true, true, false, 0f));
        }

        [Test]
        public void RisingSlash_DeadCannotActivate()
        {
            Assert.IsFalse(RisingSlashRules.CanActivate(false, true, false, 0f));
        }

        [Test]
        public void AttackPriority_GuardInputBlocksBeforeGuardStateTicks()
        {
            var choice = OriginalAttackPriority.Choose(
                true, false, false, false, true, true, true);

            Assert.AreEqual(OriginalAttackStart.None, choice);
        }

        [Test]
        public void AttackPriority_SameFrameRisingSlashBeatsMeleeAndSwordWave()
        {
            var withMelee = OriginalAttackPriority.Choose(false, false, false, false, true, true, false);
            var withAbility = OriginalAttackPriority.Choose(false, false, false, false, true, false, true);

            Assert.AreEqual(OriginalAttackStart.RisingSlash, withMelee);
            Assert.AreEqual(OriginalAttackStart.RisingSlash, withAbility);
        }

        [Test]
        public void AttackPriority_SameFrameMeleeBeatsAbility()
        {
            var choice = OriginalAttackPriority.Choose(false, false, false, false, false, true, true);

            Assert.AreEqual(OriginalAttackStart.Melee, choice);
        }

        [Test]
        public void AttackPriority_MeleeSwingBlocksRisingSlash()
        {
            var choice = OriginalAttackPriority.Choose(false, false, false, true, true, false, false);

            Assert.AreEqual(OriginalAttackStart.None, choice);
        }

        [Test]
        public void AttackPriority_RisingSlashBlocksMeleeAndAbility()
        {
            var melee = OriginalAttackPriority.Choose(false, false, true, false, false, true, false);
            var ability = OriginalAttackPriority.Choose(false, false, true, false, false, false, true);

            Assert.AreEqual(OriginalAttackStart.None, melee);
            Assert.AreEqual(OriginalAttackStart.None, ability);
        }

        [Test]
        public void AttackPriority_AbilityRemainsAvailableAlone()
        {
            var choice = OriginalAttackPriority.Choose(false, false, false, false, false, false, true);

            Assert.AreEqual(OriginalAttackStart.Ability, choice);
        }

        static GuardTiming NewGuard()
        {
            return new GuardTiming
            {
                ActiveDuration = 0.8f,
                CooldownDuration = 0.55f
            };
        }
    }
}
