namespace K4RMA
{
    /// <summary>
    /// Pure sacrifice rules for the prototype. Only proto.projectile has a boss transfer.
    /// </summary>
    public static class SacrificeRules
    {
        public static bool CanSacrifice(RunState state, string abilityId)
        {
            if (state == null || string.IsNullOrEmpty(abilityId))
                return false;
            if (state.Phase != RunPhase.Altar)
                return false;
            if (!string.IsNullOrEmpty(state.SacrificedAbilityId))
                return false;
            if (abilityId != PrototypeIds.Projectile)
                return false;
            return state.OwnedAbilityIds != null && state.OwnedAbilityIds.Contains(abilityId);
        }

        public static bool TrySacrifice(RunState state, string abilityId)
        {
            if (!CanSacrifice(state, abilityId))
                return false;

            state.OwnedAbilityIds.Remove(abilityId);
            state.SacrificedAbilityId = abilityId;
            state.BossInheritedAbilityId = abilityId;
            state.ActiveAbilityId = null;
            state.CounterAbilityId = PrototypeIds.Deflect;
            state.StageIndex = 2;
            state.Phase = RunPhase.Fight;
            return true;
        }
    }
}
