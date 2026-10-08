namespace K4RMA
{
    /// <summary>
    /// Prototype-only routing. These flags are not run progression.
    /// Legacy Deflect keeps the ability button even when the piercing preview is on.
    /// </summary>
    public static class PersonalizedMechanicRouting
    {
        public static bool LegacyDeflectOwnsAbility(bool hasLegacyDeflect)
        {
            return hasLegacyDeflect;
        }

        public static bool PiercingSlashOwnsAbility(bool previewPiercingSlash, bool hasLegacyDeflect)
        {
            return previewPiercingSlash && !hasLegacyDeflect;
        }

        public static bool SwordWaveOwnsAbility(bool previewPiercingSlash, bool hasLegacyDeflect, bool hasActiveProjectile)
        {
            return hasActiveProjectile && !hasLegacyDeflect && !previewPiercingSlash;
        }

        public static bool RisingSlashOwnsInput(bool previewAirHover)
        {
            return !previewAirHover;
        }

        public static bool AirHoverOwnsAerialAttack(bool previewAirHover)
        {
            return previewAirHover;
        }

        public static bool GuardOwnsDefense(bool previewCounter)
        {
            return !previewCounter;
        }

        public static bool CounterOwnsDefense(bool previewCounter)
        {
            return previewCounter;
        }
    }
}
