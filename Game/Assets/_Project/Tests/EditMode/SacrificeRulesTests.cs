using NUnit.Framework;

namespace K4RMA.Tests
{
    public class SacrificeRulesTests
    {
        [Test]
        public void PrototypeStart_HasNoInheritedBossAbility()
        {
            var state = RunState.CreatePrototypeStart();

            Assert.AreEqual(1, state.StageIndex);
            Assert.AreEqual(RunPhase.Fight, state.Phase);
            Assert.IsNull(state.BossInheritedAbilityId);
            Assert.AreEqual(PrototypeIds.Projectile, state.ActiveAbilityId);
            Assert.IsTrue(state.OwnedAbilityIds.Contains(PrototypeIds.Dash));
            Assert.IsTrue(state.OwnedAbilityIds.Contains(PrototypeIds.Projectile));
            Assert.IsTrue(state.OwnedAbilityIds.Contains(PrototypeIds.Guard));
            Assert.IsNull(state.CounterAbilityId);
        }

        [Test]
        public void FightPhase_RejectsSacrifice()
        {
            var state = RunState.CreatePrototypeStart();

            Assert.IsFalse(SacrificeRules.TrySacrifice(state, PrototypeIds.Projectile));
            Assert.AreEqual(PrototypeIds.Projectile, state.ActiveAbilityId);
            Assert.IsNull(state.BossInheritedAbilityId);
        }

        [Test]
        public void Altar_TransfersProjectile_AndUnlocksDeflect()
        {
            var state = RunState.CreatePrototypeStart();
            state.Phase = RunPhase.Altar;

            Assert.IsTrue(SacrificeRules.TrySacrifice(state, PrototypeIds.Projectile));
            Assert.IsFalse(state.OwnedAbilityIds.Contains(PrototypeIds.Projectile));
            Assert.AreEqual(PrototypeIds.Projectile, state.SacrificedAbilityId);
            Assert.AreEqual(PrototypeIds.Projectile, state.BossInheritedAbilityId);
            Assert.IsNull(state.ActiveAbilityId);
            Assert.AreEqual(PrototypeIds.Deflect, state.CounterAbilityId);
            Assert.AreEqual(2, state.StageIndex);
            Assert.AreEqual(RunPhase.Fight, state.Phase);
        }

        [Test]
        public void SecondSacrifice_IsRejected()
        {
            var state = RunState.CreatePrototypeStart();
            state.Phase = RunPhase.Altar;
            SacrificeRules.TrySacrifice(state, PrototypeIds.Projectile);
            state.Phase = RunPhase.Altar;

            Assert.IsFalse(SacrificeRules.TrySacrifice(state, PrototypeIds.Dash));
            Assert.AreEqual(PrototypeIds.Projectile, state.SacrificedAbilityId);
            Assert.AreEqual(PrototypeIds.Deflect, state.CounterAbilityId);
        }

        [Test]
        public void UnwiredAbilities_CannotBeSacrificed()
        {
            var state = RunState.CreatePrototypeStart();
            state.Phase = RunPhase.Altar;

            Assert.IsFalse(SacrificeRules.CanSacrifice(state, PrototypeIds.Dash));
            Assert.IsFalse(SacrificeRules.CanSacrifice(state, PrototypeIds.Guard));
            Assert.IsFalse(SacrificeRules.CanSacrifice(state, "final.fireball"));
            Assert.IsTrue(SacrificeRules.CanSacrifice(state, PrototypeIds.Projectile));
        }
    }
}
