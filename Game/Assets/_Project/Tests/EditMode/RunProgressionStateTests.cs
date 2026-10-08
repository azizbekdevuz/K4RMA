using System;
using NUnit.Framework;

namespace K4RMA.Tests
{
    public class RunProgressionStateTests
    {
        [Test]
        public void NewRun_StartsAtStage1()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.AreEqual(1, state.CurrentStage);
            Assert.IsFalse(state.IsRunComplete);
        }

        [Test]
        public void NewRun_AllTechniquesAreOriginal()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.Guard));
        }

        [Test]
        public void NewRun_HasNoInheritedTechniques()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.AreEqual(0, state.PersonalizationOrder.Count);
            Assert.AreEqual(0, state.Inheritance.InheritedTechniques.Count);
            Assert.IsFalse(state.Inheritance.HasTechnique(TechniqueId.SwordWave));
            Assert.IsFalse(state.Inheritance.HasTechnique(TechniqueId.RisingSlash));
            Assert.IsFalse(state.Inheritance.HasTechnique(TechniqueId.Guard));
        }

        [Test]
        public void NewRun_ArchetypeIsNone()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.AreEqual(GuardianArchetype.None, state.MainArchetype);
            Assert.AreEqual(GuardianArchetype.None, state.Inheritance.MainArchetype);
        }

        [Test]
        public void PersonalizeSwordWave_ChangesOnlySwordWave()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.Guard));
            CollectionAssert.AreEqual(new[] { TechniqueId.SwordWave }, state.PersonalizationOrder);
        }

        [Test]
        public void PersonalizeRisingSlash_ChangesOnlyRisingSlash()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.Guard));
            CollectionAssert.AreEqual(new[] { TechniqueId.RisingSlash }, state.PersonalizationOrder);
        }

        [Test]
        public void PersonalizeGuard_ChangesOnlyGuard()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.Guard));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.Guard));
            CollectionAssert.AreEqual(new[] { TechniqueId.Guard }, state.PersonalizationOrder);
        }

        [Test]
        public void Personalize_AppendsOriginalTechniqueToInheritance()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.RisingSlash));
            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.Guard));

            CollectionAssert.AreEqual(
                new[] { TechniqueId.RisingSlash, TechniqueId.Guard },
                state.Inheritance.InheritedTechniques);
            CollectionAssert.AreEqual(state.PersonalizationOrder, state.Inheritance.InheritedTechniques);
        }

        [Test]
        public void FirstPersonalization_SetsArchetype()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.RisingSlash));
            Assert.AreEqual(GuardianArchetype.RisingSlash, state.MainArchetype);
            Assert.AreEqual(GuardianArchetype.RisingSlash, state.Inheritance.MainArchetype);
        }

        [Test]
        public void SecondPersonalization_DoesNotReplaceArchetype()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.SwordWave));
            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.Guard));
            Assert.AreEqual(GuardianArchetype.SwordWave, state.MainArchetype);
            Assert.AreEqual(GuardianArchetype.SwordWave, state.Inheritance.MainArchetype);
            CollectionAssert.AreEqual(
                new[] { TechniqueId.SwordWave, TechniqueId.Guard },
                state.Inheritance.InheritedTechniques);
        }

        [Test]
        public void ThirdPersonalization_DoesNotReplaceArchetype()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.Guard));
            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.SwordWave));
            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.RisingSlash));
            Assert.AreEqual(GuardianArchetype.Guard, state.MainArchetype);
            Assert.AreEqual(GuardianArchetype.Guard, state.Inheritance.MainArchetype);
            Assert.IsFalse(state.IsRunComplete);
        }

        [Test]
        public void PersonalizedTechnique_CannotBePersonalizedAgain()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.SwordWave));
            Assert.IsFalse(PersonalizationRules.TryPersonalize(state, TechniqueId.SwordWave));
            Assert.AreEqual(2, state.CurrentStage);
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.Guard));
            CollectionAssert.AreEqual(new[] { TechniqueId.SwordWave }, state.PersonalizationOrder);
        }

        [Test]
        public void AllThreePersonalized_NoFourthPersonalizationAllowed()
        {
            var state = Personalize(TechniqueId.SwordWave, TechniqueId.RisingSlash, TechniqueId.Guard);

            Assert.IsFalse(PersonalizationRules.TryPersonalize(state, TechniqueId.SwordWave));
            Assert.IsFalse(PersonalizationRules.TryPersonalize(state, TechniqueId.RisingSlash));
            Assert.IsFalse(PersonalizationRules.TryPersonalize(state, TechniqueId.Guard));
            Assert.IsFalse(PersonalizationRules.TryPersonalize(state, (TechniqueId)99));
            Assert.AreEqual(4, state.CurrentStage);
            Assert.AreEqual(3, state.PersonalizationOrder.Count);
            CollectionAssert.AreEqual(
                new[] { TechniqueId.SwordWave, TechniqueId.RisingSlash, TechniqueId.Guard },
                state.PersonalizationOrder);
        }

        [Test]
        public void FirstPersonalization_AdvancesToStage2()
        {
            var state = RunProgressionState.CreateNewRun();

            Assert.IsTrue(PersonalizationRules.TryPersonalize(state, TechniqueId.Guard));
            Assert.AreEqual(2, state.CurrentStage);
        }

        [Test]
        public void SecondPersonalization_AdvancesToStage3()
        {
            var state = Personalize(TechniqueId.Guard, TechniqueId.SwordWave);

            Assert.AreEqual(3, state.CurrentStage);
            Assert.IsFalse(state.TryCompleteRun());
            Assert.IsFalse(state.IsRunComplete);
        }

        [Test]
        public void ThirdPersonalization_AdvancesToStage4()
        {
            var state = Personalize(TechniqueId.RisingSlash, TechniqueId.Guard, TechniqueId.SwordWave);

            Assert.AreEqual(4, state.CurrentStage);
            Assert.IsFalse(state.IsRunComplete);
            Assert.IsTrue(state.TryCompleteRun());
            Assert.IsTrue(state.IsRunComplete);
            Assert.IsFalse(state.TryCompleteRun());
            Assert.IsFalse(PersonalizationRules.TryPersonalize(state, TechniqueId.SwordWave));
        }

        [Test]
        public void Stage4_InheritsAllThreeOriginalTechniques()
        {
            var state = Personalize(TechniqueId.Guard, TechniqueId.RisingSlash, TechniqueId.SwordWave);

            Assert.AreEqual(4, state.CurrentStage);
            CollectionAssert.AreEqual(
                new[] { TechniqueId.Guard, TechniqueId.RisingSlash, TechniqueId.SwordWave },
                state.Inheritance.InheritedTechniques);
            Assert.IsTrue(state.Inheritance.HasTechnique(TechniqueId.SwordWave));
            Assert.IsTrue(state.Inheritance.HasTechnique(TechniqueId.RisingSlash));
            Assert.IsTrue(state.Inheritance.HasTechnique(TechniqueId.Guard));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.Guard));
        }

        [Test]
        public void Reset_ReturnsExactInitialState()
        {
            var state = Personalize(TechniqueId.Guard, TechniqueId.SwordWave, TechniqueId.RisingSlash);
            Assert.IsTrue(state.TryCompleteRun());

            state.Reset();

            AssertInitial(state);
        }

        [Test]
        public void PersonalizedResults_MapFromOriginalTechniques()
        {
            var swordWave = TechniqueDefinition.Get(TechniqueId.SwordWave);
            var risingSlash = TechniqueDefinition.Get(TechniqueId.RisingSlash);
            var guard = TechniqueDefinition.Get(TechniqueId.Guard);

            Assert.AreEqual(PersonalizedTechniqueId.PiercingSlash, swordWave.PersonalizedId);
            Assert.AreEqual(GuardianArchetype.SwordWave, swordWave.GuardianArchetype);
            Assert.AreEqual("technique.sword_wave.name", swordWave.OriginalNameKey);
            Assert.AreEqual("technique.sword_wave.description", swordWave.OriginalDescriptionKey);
            Assert.AreEqual("technique.piercing_slash.name", swordWave.PersonalizedNameKey);
            Assert.AreEqual("technique.piercing_slash.description", swordWave.PersonalizedDescriptionKey);

            Assert.AreEqual(PersonalizedTechniqueId.AirHover, risingSlash.PersonalizedId);
            Assert.AreEqual(GuardianArchetype.RisingSlash, risingSlash.GuardianArchetype);
            Assert.AreEqual("technique.rising_slash.name", risingSlash.OriginalNameKey);
            Assert.AreEqual("technique.rising_slash.description", risingSlash.OriginalDescriptionKey);
            Assert.AreEqual("technique.air_hover.name", risingSlash.PersonalizedNameKey);
            Assert.AreEqual("technique.air_hover.description", risingSlash.PersonalizedDescriptionKey);

            Assert.AreEqual(PersonalizedTechniqueId.Counter, guard.PersonalizedId);
            Assert.AreEqual(GuardianArchetype.Guard, guard.GuardianArchetype);
            Assert.AreEqual("technique.guard.name", guard.OriginalNameKey);
            Assert.AreEqual("technique.guard.description", guard.OriginalDescriptionKey);
            Assert.AreEqual("technique.counter.name", guard.PersonalizedNameKey);
            Assert.AreEqual("technique.counter.description", guard.PersonalizedDescriptionKey);
        }

        [Test]
        public void Inheritance_PublicListCannotBeMutated()
        {
            var state = Personalize(TechniqueId.SwordWave, TechniqueId.Guard);
            var exposed = state.Inheritance.InheritedTechniques;

            Assert.IsNull(exposed as TechniqueId[]);
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)exposed).Add(TechniqueId.RisingSlash));
            CollectionAssert.AreEqual(
                new[] { TechniqueId.SwordWave, TechniqueId.Guard },
                state.Inheritance.InheritedTechniques);
        }

        [TestCase(TechniqueId.SwordWave, TechniqueId.RisingSlash, TechniqueId.Guard)]
        [TestCase(TechniqueId.SwordWave, TechniqueId.Guard, TechniqueId.RisingSlash)]
        [TestCase(TechniqueId.RisingSlash, TechniqueId.SwordWave, TechniqueId.Guard)]
        [TestCase(TechniqueId.RisingSlash, TechniqueId.Guard, TechniqueId.SwordWave)]
        [TestCase(TechniqueId.Guard, TechniqueId.SwordWave, TechniqueId.RisingSlash)]
        [TestCase(TechniqueId.Guard, TechniqueId.RisingSlash, TechniqueId.SwordWave)]
        public void SixOrders_Stage4_PreservesOrderAndFirstArchetype(
            TechniqueId first,
            TechniqueId second,
            TechniqueId third)
        {
            var state = Personalize(first, second, third);
            var expected = new[] { first, second, third };

            Assert.AreEqual(4, state.CurrentStage);
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Personalized, state.GetTechniqueState(TechniqueId.Guard));
            CollectionAssert.AreEqual(expected, state.PersonalizationOrder);
            CollectionAssert.AreEqual(expected, state.Inheritance.InheritedTechniques);
            Assert.AreEqual(ArchetypeFor(first), state.MainArchetype);
            Assert.AreEqual(ArchetypeFor(first), state.Inheritance.MainArchetype);
            Assert.IsTrue(state.Inheritance.HasTechnique(TechniqueId.SwordWave));
            Assert.IsTrue(state.Inheritance.HasTechnique(TechniqueId.RisingSlash));
            Assert.IsTrue(state.Inheritance.HasTechnique(TechniqueId.Guard));
        }

        static RunProgressionState Personalize(params TechniqueId[] order)
        {
            var state = RunProgressionState.CreateNewRun();
            for (int i = 0; i < order.Length; i++)
                Assert.IsTrue(PersonalizationRules.TryPersonalize(state, order[i]));
            return state;
        }

        static void AssertInitial(RunProgressionState state)
        {
            Assert.AreEqual(1, state.CurrentStage);
            Assert.IsFalse(state.IsRunComplete);
            Assert.AreEqual(GuardianArchetype.None, state.MainArchetype);
            Assert.AreEqual(0, state.PersonalizationOrder.Count);
            Assert.AreEqual(GuardianArchetype.None, state.Inheritance.MainArchetype);
            Assert.AreEqual(0, state.Inheritance.InheritedTechniques.Count);
            Assert.IsFalse(state.Inheritance.HasTechnique(TechniqueId.SwordWave));
            Assert.IsFalse(state.Inheritance.HasTechnique(TechniqueId.RisingSlash));
            Assert.IsFalse(state.Inheritance.HasTechnique(TechniqueId.Guard));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.SwordWave));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.RisingSlash));
            Assert.AreEqual(PlayerTechniqueState.Original, state.GetTechniqueState(TechniqueId.Guard));
        }

        static GuardianArchetype ArchetypeFor(TechniqueId technique)
        {
            switch (technique)
            {
                case TechniqueId.SwordWave:
                    return GuardianArchetype.SwordWave;
                case TechniqueId.RisingSlash:
                    return GuardianArchetype.RisingSlash;
                case TechniqueId.Guard:
                    return GuardianArchetype.Guard;
                default:
                    throw new ArgumentOutOfRangeException(nameof(technique));
            }
        }
    }
}
