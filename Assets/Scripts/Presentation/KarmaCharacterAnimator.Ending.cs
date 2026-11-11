using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
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
