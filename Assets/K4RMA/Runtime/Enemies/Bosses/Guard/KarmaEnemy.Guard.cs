using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        float shieldUntil, nextShield, shieldWarningUntil;
        bool frontalGuard;
        readonly KarmaTrialState trial = new KarmaTrialState();
        internal static float TrialRadius { get { return GD05Pattern.TrialRadius; } }
        internal static float TrialCenter { get { return GD05Pattern.TrialCenter; } }
        public bool TrialActive { get { return trial.Active; } }
        public bool HeavyParryWindowOpen { get { return behavior is GuardBossBehavior guard && guard.Heavy.ParryWindowOpen; } }
    }
}
