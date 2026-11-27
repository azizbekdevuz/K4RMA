using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        internal const float SwordGroundPlantSeconds = 1f;
        internal float SwordGroundProgress { get; private set; }
        // The final steel socket is 94.5% along the blade. Extend its last segment to the point.
        internal float SwordGroundTipX
        {
            get
            {
                if (SwordGroundBladePoints(out _, out _, out var exact)) return exact.x;
                return KarmaSwordWaveAnimation.BladePoints(picture, out _, out var bend, out var tip)
                    ? tip.x + (tip.x - bend.x) * (0.055f / 0.445f) : transform.position.x;
            }
        }
        internal void BeginSwordGroundStrike(float warning)
        {
            PrepareSwordBody("Summon", warning);
            SwordGroundProgress = 0;
        }
        internal void SampleSwordGroundStrike(float progress)
        {
            if ((SwordBodyAction == "Summon" || SwordBodyAction == "UltimateWindup") && !SwordBodyReleased)
                SwordGroundProgress = Mathf.Clamp01(progress);
        }
        internal bool ReleaseSwordGroundStrike()
        {
            if (sheet != "Wave" || deathAt >= 0 || Time.time < hurtUntil) return false;
            SwordGroundProgress = 1;
            ReleaseSwordBody("Summon");
            return SwordGroundBladePoints(out _, out _, out _);
        }
    }
}
