using System;
namespace KarmaPrototype
{
    public sealed partial class KarmaBossTuning
    {
        [NonSerialized] KarmaGuardBossSettings guardSettings;
        public KarmaGuardBossSettings guard
        { get { return guardSettings ?? (guardSettings = KarmaGuardBossProfile.LoadSettings()); } }
    }
}
