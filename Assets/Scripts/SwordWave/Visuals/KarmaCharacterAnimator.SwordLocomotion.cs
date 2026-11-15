using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        Sprite[] swordWalkFrames;
        bool swordWalkAllowed, swordWasWalking;
        float swordWalkPhase, swordWalkSampleAge, swordWalkStopAge;
        int swordWalkFacing, swordWalkFrame;
        float swordStride;
        const float WalkSampleSeconds = 1f / 24;

        void ConfigureSwordLocomotion()
        {
            if (!poses.TryGetValue("Idle", out var idle)) return;
            // Exact own-form resource only; no base-costume fallback.
            var loaded = Resources.LoadAll<Sprite>("SwordWave/Locomotion/" + idle.texture.name + "_Walk");
            if (loaded.Length != 8) return;
            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < loaded.Length; i++)
                if (loaded[i].name != "Walk" + i.ToString("00")) return;
            swordWalkFrames = loaded;
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
                swordWasWalking = false; swordWalkSampleAge = swordWalkStopAge = 0;
                swordWalkFacing = facing;
                return false;
            }
            bool moving = Mathf.Abs(travel) > 0.00001f;
            bool turned = swordWalkFacing != 0 && facing != swordWalkFacing;
            swordWalkFacing = facing;
            if (!moving && !swordWasWalking) return false;
            if (moving)
            {
                swordWalkPhase = AdvanceSwordGait(swordWalkPhase, travel, stride, facing, turned);
                swordWalkSampleAge += dt;
                if (!swordWasWalking || turned || swordWalkSampleAge >= WalkSampleSeconds)
                {
                    swordWalkFrame = Mathf.Min(7, Mathf.FloorToInt(swordWalkPhase * 8));
                    swordWalkSampleAge = Mathf.Repeat(swordWalkSampleAge, WalkSampleSeconds);
                }
                swordWalkStopAge = 0;
            }
            else
            {
                if (turned) swordWalkPhase = AdvanceSwordGait(swordWalkPhase, 0, stride, facing, true);
                swordWalkStopAge += dt;
                // Settle on the nearest supporting contact for at most two
                // authored samples. No walking continues against a wall.
                swordWalkFrame = Mathf.Repeat(swordWalkPhase + 0.25f, 1) < 0.5f ? 0 : 4;
                if (swordWalkStopAge >= 2 * WalkSampleSeconds)
                { swordWasWalking = false; return false; }
            }
            swordWasWalking = true;
            picture.sprite = swordWalkFrames[swordWalkFrame];
            picture.transform.localScale = pictureScale;
            // New import pivots are calibrated to each support sole. Existing
            // sheet normalization and incoming-damage registration stay intact.
            SwordFoot = Vector3.zero;
            SwordVisualHome = pictureHome + Vector3.up * swordReferenceSole;
            picture.transform.localPosition = SwordVisualHome;
            return true;
        }
    }
}
