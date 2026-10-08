using System;

namespace K4RMA
{
    public enum PersonalizedTechniqueId
    {
        PiercingSlash,
        AirHover,
        Counter
    }

    /// <summary>
    /// Mapping data for one original technique. Display text is addressed by localization keys.
    /// Ownership lives on <see cref="RunProgressionState"/>.
    /// </summary>
    public sealed class TechniqueDefinition
    {
        public const int Count = 3;

        static readonly TechniqueDefinition[] catalog = new TechniqueDefinition[Count];

        static TechniqueDefinition()
        {
            Register(new TechniqueDefinition(
                TechniqueId.SwordWave,
                "technique.sword_wave.name",
                "technique.sword_wave.description",
                "technique.piercing_slash.name",
                "technique.piercing_slash.description",
                PersonalizedTechniqueId.PiercingSlash,
                GuardianArchetype.SwordWave));
            Register(new TechniqueDefinition(
                TechniqueId.RisingSlash,
                "technique.rising_slash.name",
                "technique.rising_slash.description",
                "technique.air_hover.name",
                "technique.air_hover.description",
                PersonalizedTechniqueId.AirHover,
                GuardianArchetype.RisingSlash));
            Register(new TechniqueDefinition(
                TechniqueId.Guard,
                "technique.guard.name",
                "technique.guard.description",
                "technique.counter.name",
                "technique.counter.description",
                PersonalizedTechniqueId.Counter,
                GuardianArchetype.Guard));
        }

        TechniqueDefinition(
            TechniqueId id,
            string originalNameKey,
            string originalDescriptionKey,
            string personalizedNameKey,
            string personalizedDescriptionKey,
            PersonalizedTechniqueId personalizedId,
            GuardianArchetype guardianArchetype)
        {
            Id = id;
            OriginalNameKey = originalNameKey;
            OriginalDescriptionKey = originalDescriptionKey;
            PersonalizedNameKey = personalizedNameKey;
            PersonalizedDescriptionKey = personalizedDescriptionKey;
            PersonalizedId = personalizedId;
            GuardianArchetype = guardianArchetype;
        }

        public TechniqueId Id { get; }
        public string OriginalNameKey { get; }
        public string OriginalDescriptionKey { get; }
        public string PersonalizedNameKey { get; }
        public string PersonalizedDescriptionKey { get; }
        public PersonalizedTechniqueId PersonalizedId { get; }
        public GuardianArchetype GuardianArchetype { get; }

        public static TechniqueDefinition Get(TechniqueId technique)
        {
            return catalog[IndexOf(technique)];
        }

        public static int IndexOf(TechniqueId technique)
        {
            switch (technique)
            {
                case TechniqueId.SwordWave:
                    return 0;
                case TechniqueId.RisingSlash:
                    return 1;
                case TechniqueId.Guard:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(technique));
            }
        }

        static void Register(TechniqueDefinition definition)
        {
            catalog[IndexOf(definition.Id)] = definition;
        }
    }
}
