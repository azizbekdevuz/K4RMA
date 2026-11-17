using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        readonly Dictionary<string, Sprite[]> endingFrames = new Dictionary<string, Sprite[]>();
        Material endingOriginalMaterial;
        Sprite endingOriginalSprite;
        bool presentingEnding;

        internal void BeginEndingPoses(params string[] names)
        {
            if (picture == null || presentingEnding) return;
            presentingEnding = true;
            endingOriginalMaterial = picture.sharedMaterial;
            endingOriginalSprite = picture.sprite;
            foreach (var name in names)
            {
                var sprites = Resources.LoadAll<Sprite>("Player/Native24/" + name);
                Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
                if (sprites.Length > 0) endingFrames.Add(name, sprites);
            }
            var material = Resources.Load<Material>("Shared/Rendering/KarmaPlayer24");
            if (material != null) picture.sharedMaterial = material;
        }

        internal void EndEndingPoses()
        {
            if (!presentingEnding) return;
            if (picture != null)
            {
                picture.sharedMaterial = endingOriginalMaterial;
                picture.sprite = endingOriginalSprite;
                picture.color = pictureColor;
                picture.transform.localPosition = pictureHome;
                picture.transform.localScale = pictureScale;
                picture.transform.localRotation = Quaternion.identity;
            }
            endingFrames.Clear();
            endingOriginalMaterial = null; endingOriginalSprite = null;
            presentingEnding = false;
        }

        internal void PresentEndingPose(string name, float elapsed, bool loop, float glow)
        {
            if (picture == null) return;
            bool draw = name == "Draw";
            bool sheathe = name == "Sheathe";
            if (endingFrames.TryGetValue(draw || sheathe ? "Recovery" : name, out var sequence))
            {
                int last = sequence.Length - 1;
                int frame;
                if (draw)
                {
                    // Reverse the authored return to stance to raise the sword from the scabbard.
                    frame = Mathf.RoundToInt(last * (1 - Mathf.Clamp01(elapsed)));
                }
                else if (sheathe)
                {
                    // The follow-through already leaves the blade low, so continue from there.
                    frame = Mathf.RoundToInt(Mathf.Lerp(Mathf.Min(1, last), last, elapsed));
                }
                else
                {
                    float sampleTime = name == "Attack" ? elapsed * 0.68f : elapsed;
                    frame = Mathf.FloorToInt(Mathf.Max(0, sampleTime) * 24f + 0.0001f);
                    frame = loop ? frame % sequence.Length : Mathf.Min(frame, last);
                }
                picture.sprite = sequence[frame];
                CurrentPose = name;
            }
            else ShowPose(draw || sheathe ? "Idle" : name);
            picture.flipX = false;
            picture.transform.localScale = pictureScale;
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.identity;
            picture.color = Color.Lerp(pictureColor, new Color(1.25f, 1.14f, 0.8f, pictureColor.a), glow * 0.5f);
            transform.localRotation = Quaternion.identity;
        }

        internal void PresentEndingDefeat(float elapsed)
        {
            // The final guardian accepts the result while still present. Keep a
            // grounded kneel and lower the head instead of reviving into combat.
            deathAt = -1;
            picture.transform.localPosition = pictureHome;
            picture.transform.localScale = pictureScale;
            picture.transform.localRotation = Quaternion.identity;
            transform.localRotation = Quaternion.identity;
            if (elapsed < 0.32f)
            {
                ShowPose("Hurt");
                picture.color = new Color(1, 0.8f, 0.65f);
            }
            else
            {
                if (revivalFrames != null && revivalFrames.Length == 6)
                    picture.sprite = revivalFrames[2];
                else ShowPose(poses.ContainsKey("Kneel") ? "Kneel" : "Hurt");
                float bow = KarmaGame.EndingEase(0.55f, 1.15f, elapsed);
                float acknowledge = KarmaGame.EndingEase(2.65f, 3.15f, elapsed);
                picture.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-5 * bow, 1.5f, acknowledge));
                picture.color = Color.Lerp(new Color(0.72f, 0.72f, 0.76f), pictureColor, bow);
                CurrentPose = acknowledge < 0.5f ? "Ending/Reflect" : "Ending/Acknowledge";
            }
        }
    }
}
