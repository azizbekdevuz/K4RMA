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
