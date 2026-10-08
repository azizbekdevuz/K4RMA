using System;
using System.Collections.Generic;

namespace K4RMA
{
    /// <summary>
    /// Pure run progression. The personalization list is the only ordered record.
    /// Inheritance and the main archetype are derived from it.
    /// </summary>
    public sealed class RunProgressionState
    {
        public const int MinimumStage = 1;
        public const int FinalStage = 4;

        readonly PlayerTechniqueState[] techniqueStates = new PlayerTechniqueState[TechniqueDefinition.Count];
        readonly List<TechniqueId> personalizationOrder = new List<TechniqueId>(TechniqueDefinition.Count);
        readonly IReadOnlyList<TechniqueId> personalizationOrderView;

        public RunProgressionState()
        {
            personalizationOrderView = personalizationOrder.AsReadOnly();
            Reset();
        }

        public static RunProgressionState CreateNewRun()
        {
            return new RunProgressionState();
        }

        public int CurrentStage => personalizationOrder.Count + MinimumStage;
        public bool IsRunComplete { get; private set; }
        public IReadOnlyList<TechniqueId> PersonalizationOrder => personalizationOrderView;

        public GuardianArchetype MainArchetype
        {
            get
            {
                if (personalizationOrder.Count == 0)
                    return GuardianArchetype.None;
                return TechniqueDefinition.Get(personalizationOrder[0]).GuardianArchetype;
            }
        }

        public GuardianInheritanceState Inheritance =>
            new GuardianInheritanceState(personalizationOrderView, MainArchetype);

        public PlayerTechniqueState GetTechniqueState(TechniqueId technique)
        {
            return techniqueStates[TechniqueDefinition.IndexOf(technique)];
        }

        public void Reset()
        {
            for (int i = 0; i < techniqueStates.Length; i++)
                techniqueStates[i] = PlayerTechniqueState.Original;
            personalizationOrder.Clear();
            IsRunComplete = false;
        }

        public bool TryCompleteRun()
        {
            if (IsRunComplete || CurrentStage != FinalStage)
                return false;
            IsRunComplete = true;
            return true;
        }

        internal void RecordPersonalization(TechniqueId technique)
        {
            int index = TechniqueDefinition.IndexOf(technique);
            if (techniqueStates[index] != PlayerTechniqueState.Original)
                throw new InvalidOperationException("Technique is already personalized.");
            techniqueStates[index] = PlayerTechniqueState.Personalized;
            personalizationOrder.Add(technique);
        }
    }
}
