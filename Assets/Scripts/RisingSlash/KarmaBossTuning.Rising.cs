using System;
namespace KarmaPrototype
{
    public sealed partial class KarmaBossTuning
    {
        [NonSerialized] KarmaRisingBossSettings risingSettings;
        public KarmaRisingBossSettings rising
        { get { return risingSettings ?? (risingSettings = KarmaRisingBossProfile.LoadSettings()); } }
    }
}
