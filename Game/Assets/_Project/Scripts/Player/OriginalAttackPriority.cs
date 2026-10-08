namespace K4RMA
{
    public enum OriginalAttackStart
    {
        None,
        RisingSlash,
        Melee,
        Ability
    }

    /// <summary>
    /// Picks at most one new original attack for this frame.
    /// Guard input wins before PlayerGuard has ticked.
    /// </summary>
    public static class OriginalAttackPriority
    {
        public static OriginalAttackStart Choose(
            bool guardHeld,
            bool guardActive,
            bool risingSlashActive,
            bool meleeSwingActive,
            bool risingSlashPressed,
            bool attackPressed,
            bool abilityPressed)
        {
            if (guardHeld || guardActive)
                return OriginalAttackStart.None;
            if (!risingSlashActive && !meleeSwingActive && risingSlashPressed)
                return OriginalAttackStart.RisingSlash;
            if (!risingSlashActive && attackPressed)
                return OriginalAttackStart.Melee;
            if (!risingSlashActive && abilityPressed)
                return OriginalAttackStart.Ability;
            return OriginalAttackStart.None;
        }
    }
}
