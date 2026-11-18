using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        bool growthActive, personalizationProofActive;
        readonly Dictionary<string, Sprite> growthPrevious = new Dictionary<string, Sprite>();

        internal void BeginGrowth(Sprite fallen, string previousForm)
        {
            EndGuardianAction();
            deathAt = -1; empowermentUntil = 0;
            growthActive = true;
            beforeInheritance = fallen;
            beforeRevivalFrames = LoadRevivalFrames(previousForm);
            growthPrevious.Clear();
            var previous = KarmaArtwork.InheritedPoses(previousForm);
            if (previous.Length == 0) previous = KarmaArtwork.Sprites(previousForm);
            foreach (var sprite in previous) if (sprite != null) growthPrevious[sprite.name] = sprite;
        }

        internal Sprite GrowthSprite(string pose, bool previous = false)
        {
            var source = previous ? growthPrevious : poses;
            if (source.TryGetValue(pose, out var sprite)) return sprite;
            return source.TryGetValue("Idle", out sprite) ? sprite : guardianAppearance ?? picture.sprite;
        }

        internal void GrowthRevive(float progress)
        {
            if (!growthActive || picture == null) return;
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * 5), 0, 5);
            picture.sprite = beforeRevivalFrames != null && beforeRevivalFrames.Length == 6
                ? beforeRevivalFrames[frame] : frame == 0 && beforeInheritance != null
                ? beforeInheritance : GrowthSprite(frame < 3 ? "Hurt" : "Idle", true);
            picture.transform.localScale = pictureScale;
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.identity;
            picture.color = Color.Lerp(new Color(0.55f, 0.55f, 0.6f), pictureColor, progress);
            CurrentPose = "Growth/Revive/" + frame;
        }

        internal void GrowthPose(string pose, bool previous, Vector2 offset, float angle = 0)
        {
            if (!growthActive || picture == null) return;
            picture.sprite = GrowthSprite(pose, previous);
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
                if (beforeRevivalFrames != null && beforeRevivalFrames.Length == 6)
                    picture.sprite = beforeRevivalFrames[0];
                picture.color = new Color(0.62f, 0.62f, 0.68f);
            }
            CurrentPose = "Growth/FailedCopy";
        }

        internal void EndGrowth()
        {
            if (!growthActive) return;
            growthActive = false; growthPrevious.Clear();
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.identity;
            picture.transform.localScale = pictureScale;
            picture.color = pictureColor;
            guardianPreviousPosition = transform.position;
            ApplyGuardianPose("Idle");
        }

        internal void GrowthKeyPose(Sprite keyPose, string pose, float relativeHeight, float lean = 0)
        {
            if (!growthActive || keyPose == null || picture == null) return;
            picture.sprite = keyPose;
            // Calibrate once against the old standing body, not the bounding box
            // of the last hurt/slash pose. The new art has a foot-aligned pivot.
            float height = GrowthSprite("Idle", true).bounds.size.y * relativeHeight;
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
                // The ending already samples these authored 24 FPS gestures
                // independently of the optional combat animation profile.
                BeginEndingPoses("Idle", "Pierce", "Recovery", "Jump", "AirDiagonal", "Land", "Parry", "Riposte");
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
        // optional Native24 profile. No Player, input, Rigidbody or hitbox is copied.
        internal void SampleGrowthSource(Essence essence, float seconds)
        {
            string pose = essence == Essence.SwordWave ? "SwordWave"
                : essence == Essence.RisingSlash ? "Rising" : "Guard";
            LoadNativePlayerPose(pose);
            if (playerSequences.TryGetValue(pose, out var sequence) && sequence.Length > 0)
                picture.sprite = sequence[Mathf.Clamp(Mathf.FloorToInt(seconds * playerSampleRate), 0, sequence.Length - 1)];
            else picture.sprite = KarmaArtwork.Sprite("Player", essence == Essence.SwordWave ? "Slash" : pose);
            picture.transform.localScale = pictureScale;
            picture.transform.localPosition = pictureHome;
            CurrentPose = "Memory/" + pose;
        }
    }
}
