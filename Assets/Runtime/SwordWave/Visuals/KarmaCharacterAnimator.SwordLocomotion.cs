using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        Sprite[] swordWalkFrames;
        bool swordWalkAllowed, swordWasWalking;
        float swordWalkPhase, swordWalkStopAge;
        int swordWalkFacing, swordWalkFrame;
        float swordStride;
        const float WalkSampleSeconds = KarmaBootstrap.Step;

        void ConfigureSwordLocomotion()
        {
            var loaded = swordWalkFrames = motionArt.Frames(motionForm, "Move");
            // Authored contact-to-contact foot travel, in artwork-local units.
            // It is converted to world space at sampling, never applied to root.
            swordStride = loaded[0].rect.height / loaded[0].pixelsPerUnit * Mathf.Abs(pictureScale.x) * 0.82f;
        }

        internal static float AdvanceSwordGait(float phase, float travel, float stride, int facing, bool turned)
        {
            // Mirroring exchanges the supporting legs, not a restart at frame 0.
            if (turned) phase = Mathf.Repeat(phase + 0.5f, 1);
            return Mathf.Repeat(phase + travel * facing / Mathf.Max(0.001f, stride), 1);
        }

        internal bool SampleSwordLocomotion(float travel, float dt, int facing, bool grounded)
        {
            bool allowed = swordWalkAllowed && grounded && deathAt < 0 && Time.time >= hurtUntil
                && (CurrentPose == "Idle" || CurrentPose == "Move") && swordWalkFrames != null;
            float stride = swordStride * Mathf.Abs(transform.lossyScale.x);
            // A teleport, scripted relocation or long paused frame cannot wind
            // a walking cycle many times. Attack/air/evade never call ShowPose.
            if (!allowed || dt <= 0 || Mathf.Abs(travel) > stride * 0.75f)
            {
                swordWasWalking = false; swordWalkStopAge = 0;
                swordWalkFacing = facing;
                return false;
            }
            bool moving = Mathf.Abs(travel) > 0.00001f;
            bool turned = swordWalkFacing != 0 && facing != swordWalkFacing;
            swordWalkFacing = facing;
            if (!moving && !swordWasWalking) return false;
            if (moving)
            {
                // One phase clock. Sampling distance with a second fixed clock
                // used to repeat one contact and skip the opposite passing foot.
                float maxTravel = stride * (dt + swordWalkStopAge) / (swordWalkFrames.Length * WalkSampleSeconds);
                swordWalkPhase = AdvanceSwordGait(swordWalkPhase, Mathf.Clamp(travel, -maxTravel, maxTravel), stride, facing, turned);
                swordWalkFrame = Mathf.Min(swordWalkFrames.Length - 1, (int)(swordWalkPhase * swordWalkFrames.Length));
                swordWalkStopAge = 0;
            }
            else
            {
                if (turned)
                {
                    swordWalkPhase = AdvanceSwordGait(swordWalkPhase, 0, stride, facing, true);
                    swordWalkFrame = (int)(swordWalkPhase * swordWalkFrames.Length);
                }
                swordWalkStopAge += dt;
                // Retain the actual planted foot across a missed movement tick.
                if (swordWalkStopAge >= 2 * WalkSampleSeconds)
                { swordWasWalking = false; return false; }
            }
            swordWasWalking = true;
            picture.sprite = swordWalkFrames[swordWalkFrame];
            picture.transform.localScale = pictureScale;
            // New import pivots are calibrated to each support sole. Existing
            // sheet normalization and incoming-damage registration stay intact.
            picture.transform.localPosition = pictureHome;
            return true;
        }
    }
}
