using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        bool growthActive, personalizationProofActive;

        internal void BeginGrowth(Sprite fallen, string previousForm)
        {
            EndGuardianAction();
            deathAt = -1; empowermentUntil = 0;
            growthActive = true; previousMotionForm = previousForm;
        }

        internal Sprite GrowthSprite(string pose, bool previous = false)
        {
            string form = previous ? previousMotionForm : motionForm;
            string motion = KarmaMotionArt.Has(form, pose) ? pose : KarmaMotionArt.Has(form, "GrowthTechnique")
                ? "GrowthTechnique" : KarmaMotionArt.Has(form, "GrowthMastery") ? "GrowthMastery" : "Idle";
            return motionArt.Sample(form, motion, motion == "Idle" ? 0 : 1);
        }

        internal void GrowthRevive(float progress)
        {
            if (!growthActive || picture == null) return;
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * 5), 0, 5);
            string pose = frame == 0 ? "Death" : frame == 1 ? "RevivePush" : frame == 2 ? "ReviveKneel"
                : frame == 3 ? "ReviveRise" : frame == 4 ? "ReviveStand" : "Idle";
            PlayBody(pose, previousMotionForm);
            picture.transform.localScale = pictureScale;
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.identity;
            if (frame == 0 && previousMotionForm == "Rise") ShowFallenRisingGrowth();
            picture.color = Color.Lerp(new Color(0.55f, 0.55f, 0.6f), pictureColor, progress);
            CurrentPose = "Growth/Revive/" + frame;
        }

        internal void GrowthPose(string pose, bool previous, Vector2 offset, float angle = 0)
        {
            if (!growthActive || picture == null) return;
            string form = previous ? previousMotionForm : motionForm;
            PlayBody(KarmaMotionArt.Has(form, pose) ? pose : "Idle", form);
            picture.transform.localScale = pictureScale;
            // Offset is visual only: never move the combat body or its facing.
            picture.transform.localPosition = pictureHome + (Vector3)offset / KarmaSpace.GuardianArtScale;
            picture.transform.localRotation = Quaternion.Euler(0, 0, angle);
            picture.color = pictureColor;
            CurrentPose = "Growth/" + pose;
        }

        internal void GrowthStumble(string ready, int attempt, float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / 0.85f);
            float wobble = Mathf.Sin(t * Mathf.PI * 2) * (1 - t);
            GrowthPose(elapsed < 0.18f ? "Hurt" : ready, true,
                new Vector2(-Mathf.Sin(t * Mathf.PI) * (attempt == 1 ? 0.3f : 0.65f), 0),
                wobble * (attempt == 1 ? 4 : 8));
            DrawMotion(previousMotionForm, "GrowthStumble", elapsed);
            CurrentPose = "Growth/Unsteady/" + attempt;
        }

        internal void GrowthFalter(float elapsed)
        {
            if (elapsed < 0.4f)
            {
                float recoil = Mathf.Sin(Mathf.Clamp01(elapsed / 0.4f) * Mathf.PI);
                GrowthPose("Hurt", true, new Vector2(-recoil * 0.85f, 0), recoil * -10);
            }
            else
            {
                GrowthPose("Death", true, Vector2.zero);
                picture.color = new Color(0.62f, 0.62f, 0.68f);
            }
            DrawMotion(previousMotionForm, "GrowthFalter", elapsed);
            CurrentPose = "Growth/FailedCopy";
        }

        internal void EndGrowth()
        {
            if (!growthActive) return;
            growthActive = false;
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.identity;
            picture.transform.localScale = pictureScale;
            picture.color = pictureColor;
            guardianPreviousPosition = transform.position;
            ApplyGuardianPose("Idle");
            PlayBody("Idle");
        }

        internal void ShowGrowthMotion(string motion, bool previous, float elapsed)
        {
            DrawMotion(previous ? previousMotionForm : motionForm, motion, elapsed);
        }

        internal void GrowthKeyPose(Sprite keyPose, string pose, float relativeHeight, float lean = 0)
        {
            if (!growthActive || keyPose == null || picture == null) return;
            picture.sprite = keyPose;
            // Calibrate once against the old standing body, not the bounding box
            // of the last hurt/slash pose. The new art has a foot-aligned pivot.
            var standing = KarmaBodyPoses.Get(previousMotionForm, "Idle");
            float height = (standing != null ? KarmaBodyPoses.HitBounds(standing).size.y
                : GrowthSprite("Idle", true).bounds.size.y) * relativeHeight;
            picture.transform.localScale = pictureScale * (height / keyPose.bounds.size.y);
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.Euler(0, 0, lean);
            picture.color = pictureColor;
            CurrentPose = pose;
        }

        internal void PresentVictory(string pose, float elapsed)
        {
            if (picture == null) return;
            ApplyGuardianPose(elapsed < 0.65f ? "Idle" : pose);
            picture.transform.localRotation = Quaternion.Euler(0, 0,
                -3 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.7f, 1.5f, elapsed)));
            picture.color = pictureColor;
            DrawMotion(motionForm, "Victory", elapsed);
            CurrentPose = "Victory/" + pose;
        }

        internal void EndPersonalizationProof()
        {
            if (!personalizationProofActive) return;
            EndEndingPoses(); personalizationProofActive = false;
        }
        internal void SamplePersonalizationProof(Essence essence, float seconds)
        {
            if (!personalizationProofActive)
            {
                // The ending already samples these authored 12 FPS gestures
                // independently of the optional combat animation profile.
                BeginEndingPoses("Idle", "Pierce", "Recovery", "Jump", "AirDiagonal", "Fall", "Land", "Parry", "Riposte");
                personalizationProofActive = true;
            }
            string pose = "Idle"; float sample = seconds;
            Vector2 offset = Vector2.zero;
            float action = seconds - KarmaPersonalizationVFX.ProofAnticipation;
            if (action < 0)
            {
                // Raise the existing blade deliberately, then hold the ready pose.
                // Anticipation and recovery breathe; the release stays brisk.
                pose = "Draw";
                sample = Mathf.Clamp01(seconds / 0.55f);
            }
            else if (essence == Essence.SwordWave)
            {
                float advance = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.3f, action));
                offset.x = 3.2f * advance;
                pose = action < 0.95f ? "Pierce" : action < 1.65f ? "Recovery" : "Idle";
                sample = action < 0.95f ? Mathf.Min(action, 0.32f)
                    : action < 1.65f ? (action - 0.95f) * 0.6f : action - 1.65f;
            }
            else if (essence == Essence.RisingSlash)
            {
                float flight = Mathf.Clamp01(action / 1.1f);
                offset.y = Mathf.Sin(flight * Mathf.PI) * 2.8f;
                offset.x = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.3f, 0.64f, action)) * 2.2f;
                pose = action < 0.32f ? "Jump" : action < 0.7f ? "AirDiagonal" : action < 1.1f ? "Fall" : action < 1.85f ? "Land" : "Idle";
                sample = action - (action < 0.32f ? 0 : action < 0.7f ? 0.32f : action < 1.1f ? 0.7f : action < 1.85f ? 1.1f : 1.85f);
            }
            else
            {
                pose = action < 0.7f ? "Parry" : action < 1.03f ? "Riposte" : action < 1.85f ? "Recovery" : "Idle";
                sample = action < 0.7f ? Mathf.Min(action, 0.15f)
                    : action < 1.03f ? action - 0.7f : (action - 1.03f) * 0.6f;
            }
            // Use the exact existing clips and parry sampler. Rehearsal clocks
            // never call combat, consume a cooldown, or move the Rigidbody.
            PresentEndingPose(pose, Mathf.Max(0, sample), pose == "Idle", 0);
            float scale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
            picture.transform.localPosition = pictureHome + (Vector3)offset / scale;
            picture.color = pictureColor;
            CurrentPose = "Personalized/" + pose;
        }

        // Samples a display-only copy of the player's original skill, including the
        // equipped technique, independently of combat input and physics.
        internal void SampleGrowthSource(Essence essence, float seconds)
        {
            string pose = essence == Essence.SwordWave ? "SwordWave"
                : essence == Essence.RisingSlash ? "Rising" : "Guard";
            var sequence = LoadNativePlayerPose(pose);
            if (sequence.Length > 0)
                picture.sprite = sequence[Mathf.Clamp(Mathf.FloorToInt(seconds * playerSampleRate), 0, sequence.Length - 1)];
            else picture.sprite = KarmaArtwork.Sprite("Player", essence == Essence.SwordWave ? "Slash" : pose);
            picture.transform.localScale = pictureScale;
            picture.transform.localPosition = pictureHome;
            CurrentPose = "Memory/" + pose;
        }
    }
}
