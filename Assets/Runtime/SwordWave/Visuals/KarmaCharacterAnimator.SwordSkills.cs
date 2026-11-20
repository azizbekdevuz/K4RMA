using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        float basicLinkAt = float.NegativeInfinity, basicLinkSeconds;
        internal float SwordGroundProgress { get; private set; }
        internal float BasicLinkAge => Mathf.Max(0, Time.time - basicLinkAt);
        internal float BasicLinkDuration => basicLinkSeconds;
        internal bool BasicLinkActive => BasicBodyActive && BasicStrikeIndex == 0 && !float.IsNegativeInfinity(basicLinkAt);

        internal void BeginBasicLink(float seconds)
        {
            if (!BasicBodyActive || BasicStrikeIndex != 0) return;
            basicLinkAt = Time.time; basicLinkSeconds = Mathf.Max(1f / 24, seconds);
        }
        internal void BeginSwordGroundStrike(float warning)
        {
            PrepareSwordBody("Summon", warning);
            SwordGroundProgress = 0;
        }
        internal void SampleSwordGroundStrike(float progress)
        {
            if (SwordBodyAction == "Summon" && !SwordBodyReleased)
                SwordGroundProgress = Mathf.Clamp01(progress);
        }
        internal void ReleaseSwordGroundStrike()
        {
            if (sheet != "Wave" || deathAt >= 0 || Time.time < hurtUntil) return;
            SwordGroundProgress = 1;
            ReleaseSwordBody("Summon");
        }
    }
}
