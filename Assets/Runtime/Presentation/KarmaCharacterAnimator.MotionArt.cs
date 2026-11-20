using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        string motionForm, previousMotionForm, displayForm, displayMotion;
        float motionStarted;
        Sprite displaySprite;
        Bounds displayDamage;
        internal string MotionForm => motionForm;

        void DrawMotion(string form, string motion, float elapsed, bool loop = false)
        {
            if (picture.sprite != null && picture.sprite != displaySprite)
            {
                var original = picture.sprite == displayedIdle ? standingDamageBounds : KarmaBodyPoses.HitBounds(picture.sprite);
                var ratio = new Vector3(picture.transform.localScale.x / pictureScale.x,
                    picture.transform.localScale.y / pictureScale.y, 1);
                displayDamage = new Bounds(Vector3.Scale(original.center, ratio), Vector3.Scale(original.size, ratio));
            }
            picture.sprite = displaySprite = motionArt.Sample(form, motion, elapsed, loop);
            picture.transform.localScale = pictureScale;
        }

        void PlayBody(string motion, string form = null, bool loop = false)
        {
            form ??= motionForm;
            if (displayForm != form || displayMotion != motion)
            {
                displayForm = form; displayMotion = motion; motionStarted = Time.time;
            }
            DrawMotion(form, motion, Time.time - motionStarted, loop);
        }

        void PresentCombatBody(string action, EnemyAttack attack, bool moved)
        {
            string form = motionForm;
            string motion;
            if (sheet == "Ward") motion = GuardBodyMotion(action, attack, moved, ref form);
            else if (action == "Stunned") motion = "Stun";
            else if (Time.time < hurtUntil) motion = "Hurt";
            else if (sheet == "Wave") motion = SwordBodyMotion(action, attack, moved);
            else if (sheet == "Rise")
            {
                if (PresentRisingCrossing(action, attack)) return;
                motion = RisingBodyMotion(action, attack, moved);
            }
            else motion = action == "Attack" ? "GroundStrike" : action == "Recover" ? "Recovery"
                : action == "Telegraph" ? "Windup" : moved ? "Move" : "Idle";
            if (sheet == "Wave" && PresentSwordIntermediate(attack)) return;
            // State-specific poses are selected before this final drawing pass.
            // Keep the authored pose if this form has no matching replacement.
            if (!KarmaMotionArt.Has(form, motion)) return;
            if (sheet == "Wave" && SwordBodyVisible && SwordBodyAction != null && displayMotion == motion) return;
            PlayBody(motion, form, motion == "Move" || motion == "Dash" || motion == "Stun" || motion == "Guard");
        }
    }
}
