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
            if (endingFrames.TryGetValue(name, out var sequence))
            {
                int frame = Mathf.FloorToInt(Mathf.Max(0, elapsed) * 24f);
                picture.sprite = sequence[loop ? frame % sequence.Length : Mathf.Min(frame, sequence.Length - 1)];
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
