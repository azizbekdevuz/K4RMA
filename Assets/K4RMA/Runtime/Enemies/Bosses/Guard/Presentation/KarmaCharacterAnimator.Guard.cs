using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        KarmaHeavyStrikeMotion heavyStrike;
        Sprite guardStagger;
        Vector3 staggerScale;
        public Vector2 HeavyHandPosition { get { return heavyStrike.HandPosition; } }
        public void ConfigureHeavyStrike(IReadOnlyList<Essence> order)
        {
            guardianPose = GuardGuardianPose;
            onActionEnded = EndHeavyStrike;
            heavyStrike = new KarmaHeavyStrikeMotion(picture, pictureHome, pictureScale, order);
            Sprite reference = null;
            foreach (var sprite in KarmaArtwork.Sprites("GuardStagger" + heavyStrike.FormKey))
            {
                if (sprite.name == "Stagger") guardStagger = sprite;
                else if (sprite.name == "Reference") reference = sprite;
            }
            // Calibrate with a standing figure drawn at the same anatomical scale.
            // Using crouched height here would inflate the boss during every parry.
            if (guardStagger == null || reference == null)
                throw new System.InvalidOperationException("경직 스프라이트가 없습니다: " + heavyStrike.FormKey);
            staggerScale = pictureScale * (picture.sprite.bounds.size.y / reference.bounds.size.y);
        }
        void GuardGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
            if (action == "Stunned" && guardStagger != null)
            {
                picture.sprite = guardStagger;
                picture.transform.localPosition = pictureHome;
                picture.transform.localScale = staggerScale;
                picture.transform.localRotation = Quaternion.identity;
                picture.color = new Color(pictureColor.r * 0.82f, pictureColor.g * 0.82f, pictureColor.b * 0.82f, pictureColor.a);
                CurrentPose = "Stagger/" + heavyStrike.FormKey;
                guardianPreviousPosition = transform.position;
                return;
            }
            if (heavyStrike != null && heavyStrike.Show(attack))
            { CurrentPose = heavyStrike.CurrentPose; return; }
            string pose = Time.time < hurtUntil ? "Hurt"
                : action == "Stunned" ? "Crouch"
                : action == "Recover" ? "Recover"
                : action == "Attack" ? attack == EnemyAttack.GD02 ? "ShieldBash" : attack == EnemyAttack.GD03 ? "Thrust" : "Windup"
                : DefaultGuardianPose(action, airborne, armored);
            ApplyGuardianPose(pose);
        }
        public void PrepareHeavyStrike() { heavyStrike.Prepare(); }
        public void SwingHeavyStrike() { heavyStrike.Swing(); }
        public void EndHeavyStrike() { if (heavyStrike != null) heavyStrike.Stop(); }
    }
}
