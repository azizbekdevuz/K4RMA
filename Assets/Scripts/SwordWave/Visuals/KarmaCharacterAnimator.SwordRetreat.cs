using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        // Sword-only presentation override. Keep the host's original damage pose,
        // just as ShowGuardianSpacing does, including inherited body proportions.
        internal void ShowSwordRetreat()
        {
            if (sheet != "Wave" || picture == null || deathAt >= 0 || Time.time < hurtUntil
                || !poses.ContainsKey("Guard")) return;
            Sprite damageSprite = GuardianDamageSprite;
            Vector3 damageScale = GuardianDamageScale;
            ShowPose("Guard");
            if (guardianAppearance != null)
            {
                picture.sprite = guardianAppearance;
                picture.transform.localScale = pictureScale * appearanceScale;
            }
            CurrentPose = "Backstep";
            GuardianDamageSprite = damageSprite;
            GuardianDamageScale = damageScale;
            GuardianPhaseOverridesPose = true;
        }
    }
}
