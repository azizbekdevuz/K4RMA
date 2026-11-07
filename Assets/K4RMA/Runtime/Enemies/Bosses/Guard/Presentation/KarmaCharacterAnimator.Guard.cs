using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        KarmaHeavyStrikeMotion heavyStrike;
        Sprite guardStagger;
        Vector3 staggerScale;
        Sprite guardDamageSprite;
        Vector3 guardDamageScale;
        bool guardMoving;
        Sprite[] groundSlam;
        int groundSlamFrame = -1;
        public void ConfigureHeavyStrike(IReadOnlyList<Essence> order)
        {
            // Boss-local libraries also provide the base Ward poses after asset separation.
            // Preserve any inherited poses already selected for this body.
            foreach (var sprite in KarmaArtwork.Sprites("Ward"))
                if (sprite != null && !poses.ContainsKey(sprite.name)) poses[sprite.name] = sprite;
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
            string slamSheet = "GuardSlam" + heavyStrike.FormKey;
            groundSlam = new[] { KarmaArtwork.Sprite(slamSheet, "Windup"), KarmaArtwork.Sprite(slamSheet, "Descent"),
                KarmaArtwork.Sprite(slamSheet, "Contact"), KarmaArtwork.Sprite(slamSheet, "Rebound") };
        }
        void GuardGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            guardMoving = false;
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
            if (attack == EnemyAttack.GD04 && ShowGroundSlam()) return;
            string pose = Time.time < hurtUntil ? "Hurt"
                : action == "Stunned" ? "Crouch"
                : action == "Recover" ? "Recover"
                : action == "Attack" ? attack == EnemyAttack.GD02 ? "ShieldBash" : (attack == EnemyAttack.GroundStrike || attack == EnemyAttack.GD03) ? "Thrust" : "Windup"
                : DefaultGuardianPose(action, airborne, armored);
            if (action == "Approach" && Time.time >= hurtUntil
                && (transform.position - guardianPreviousPosition).sqrMagnitude > 0f
                && poses.ContainsKey("Move"))
            {
                guardDamageSprite = guardianAppearance != null && pose == "Guard" ? guardianAppearance : poses[pose];
                guardDamageScale = pictureScale * (guardDamageSprite == guardianAppearance ? appearanceScale : 1);
                guardMoving = true;
                pose = "Move";
            }
            ApplyGuardianPose(pose);
        }
        public void PrepareHeavyStrike() { heavyStrike.Prepare(); }
        public void HeatHeavyStrike() { heavyStrike.Heat(); }
        public void SwingHeavyStrike() { heavyStrike.Swing(); }
        public void EndHeavyStrike() { if (heavyStrike != null) heavyStrike.Stop(); }
        internal Bounds GuardDamageBounds(SpriteRenderer renderer)
        {
            if (renderer != picture || sheet != "Ward" || !guardMoving || guardDamageSprite == null)
                return renderer.bounds;
            // Approach only mirrors the unrotated guard artwork about its foot pivot.
            var local = guardDamageSprite.bounds;
            var root = picture.transform.parent;
            var scale = Vector3.Scale(root.lossyScale, guardDamageScale);
            var center = root.TransformPoint(pictureHome + Vector3.Scale(local.center, guardDamageScale));
            return new Bounds(center, Vector3.Scale(local.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
        }
        public void SetGroundSlamFrame(int frame)
        {
            groundSlamFrame = frame;
            if (frame >= 0) ShowGroundSlam();
        }
        public Vector2 GroundSlamContact
        {
            get { return picture.transform.TransformPoint(new Vector3(picture.sprite.bounds.max.x * 0.96f, 0, 0)); }
        }
        bool ShowGroundSlam()
        {
            if (groundSlam == null || groundSlamFrame < 0 || groundSlam[groundSlamFrame] == null) return false;
            picture.sprite = groundSlam[groundSlamFrame];
            picture.transform.localPosition = pictureHome;
            picture.transform.localScale = pictureScale;
            picture.transform.localRotation = Quaternion.identity;
            CurrentPose = "GD04/" + picture.sprite.name;
            return true;
        }
    }
}
