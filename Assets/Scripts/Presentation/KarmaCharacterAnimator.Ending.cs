using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        readonly System.Collections.Generic.Dictionary<string, Sprite[]> endingReviewFrames = new System.Collections.Generic.Dictionary<string, Sprite[]>();
        Material endingReviewMaterial;
        Sprite endingReviewSprite;
        bool reviewingEnding;
        internal void PresentEndingReviewPose(string name, float elapsed, bool loop)
        {
            if (picture == null) return;
            if (!reviewingEnding)
            {
                reviewingEnding = true;
                endingReviewMaterial = picture.sharedMaterial; endingReviewSprite = picture.sprite;
                var material = Resources.Load<Material>("Shared/Rendering/KarmaPlayer24");
                if (material != null) picture.sharedMaterial = material;
            }
            if (!endingReviewFrames.TryGetValue(name, out var sequence))
            {
                sequence = Resources.LoadAll<Sprite>("Player/Native24/" + name);
                System.Array.Sort(sequence, (a, b) => string.CompareOrdinal(a.name, b.name));
                endingReviewFrames.Add(name, sequence);
                if (sequence.Length == 0) Debug.LogError("Ending review pose missing: " + name);
            }
            if (sequence.Length == 0) return;
            int frame = Mathf.FloorToInt(Mathf.Max(0, elapsed) * 24);
            picture.sprite = sequence[loop ? frame % sequence.Length : Mathf.Min(frame, sequence.Length - 1)];
            picture.flipX = false; picture.color = pictureColor;
            picture.transform.localPosition = pictureHome;
            picture.transform.localScale = pictureScale;
            picture.transform.localRotation = Quaternion.identity;
            CurrentPose = name;
        }
        internal void EndEndingReviewPose()
        {
            if (!reviewingEnding || picture == null) return;
            picture.sharedMaterial = endingReviewMaterial; picture.sprite = endingReviewSprite;
            picture.color = pictureColor; picture.transform.localPosition = pictureHome;
            picture.transform.localScale = pictureScale; picture.transform.localRotation = Quaternion.identity;
            reviewingEnding = false; endingReviewFrames.Clear();
        }
        // Reuse authored key poses on the ending's 24 FPS clock, preserving original clip durations.
        // The shared player library includes authored intermediate frames at 24 FPS.
        internal void PresentEndingPose(string name, float elapsed, bool loop, float glow)
        {
            if (picture == null) return;
            LoadNativePlayerPose(name);
            float clock = Mathf.Floor(Mathf.Max(0, elapsed) * 24f) / 24f;
            if (playerSequences.TryGetValue(name, out var sequence) && sequence.Length > 0)
            {
                int sourceFrame = Mathf.FloorToInt(clock * playerSampleRate);
                picture.sprite = sequence[loop ? sourceFrame % sequence.Length : Mathf.Min(sourceFrame, sequence.Length - 1)];
                CurrentPose = name;
            }
            else ShowPose(name);
            picture.flipX = false;
            picture.transform.localScale = pictureScale;
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.identity;
            picture.color = Color.Lerp(pictureColor, new Color(1.25f, 1.14f, 0.8f, pictureColor.a), glow * 0.5f);
            transform.localRotation = Quaternion.identity;
        }
        internal void PresentEndingDefeat(float elapsed)
        {
            if (elapsed < 0.32f)
            {
                deathAt = -1;
                ShowPose("Hurt");
                picture.transform.localPosition = pictureHome;
                picture.color = new Color(1, 0.8f, 0.65f);
            }
            else
            {
                if (deathAt < 0) deathAt = Time.time;
                Pose(0, false);
            }
        }
    }
}
