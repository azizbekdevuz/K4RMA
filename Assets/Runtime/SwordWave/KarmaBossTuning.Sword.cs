using System;
namespace KarmaPrototype
{
    public sealed partial class KarmaBossTuning
    {
        [NonSerialized] KarmaSwordBossSettings swordSettings;
        public KarmaSwordBossSettings sword
        { get { return swordSettings ?? (swordSettings = KarmaSwordBossProfile.LoadSettings()); } }
    }
}
