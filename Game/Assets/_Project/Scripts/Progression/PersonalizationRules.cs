using System;

namespace K4RMA
{
    /// <summary>
    /// Pure personalization rules. One original technique changes per successful call.
    /// </summary>
    public static class PersonalizationRules
    {
        public static bool CanPersonalize(RunProgressionState state, TechniqueId technique)
        {
            if (state == null || state.IsRunComplete)
                return false;
            if (!Enum.IsDefined(typeof(TechniqueId), technique))
                return false;
            if (state.PersonalizationOrder.Count >= TechniqueDefinition.Count)
                return false;
            return state.GetTechniqueState(technique) == PlayerTechniqueState.Original;
        }

        public static bool TryPersonalize(RunProgressionState state, TechniqueId technique)
        {
            if (!CanPersonalize(state, technique))
                return false;
            state.RecordPersonalization(technique);
            return true;
        }
    }
}
