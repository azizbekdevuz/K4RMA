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
        System.Action<float> presentGuardianEndingDefeat;

        internal void BeginEndingPoses(params string[] names)
        {
            if (picture == null || presentingEnding) return;
            presentingEnding = true;
            endingOriginalMaterial = picture.sharedMaterial;
            endingOriginalSprite = picture.sprite;
            endingFrames["Draw"] = motionArt.Frames("Player", "Draw");
            endingFrames["Sheathe"] = motionArt.Frames("Player", "Sheathe");
            foreach (var name in names)
            {
                var motion = KarmaPlayerTimeline.Find(name);
                if (motion != null) endingFrames.Add(name, motion.Load());
            }
            var material = KarmaArtResource.Load<Material>("Shared/Rendering/KarmaPlayer12");
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
            if (endingFrames.TryGetValue(name, out var sequence))
            {
                int last = sequence.Length - 1;
                int frame;
                if (draw || sheathe)
                    frame = Mathf.RoundToInt(last * Mathf.Clamp01(elapsed));
                else
                {
                    float sampleTime = name == "Attack" ? elapsed * 0.68f : elapsed;
                    frame = Mathf.FloorToInt(Mathf.Max(0, sampleTime) * KarmaBootstrap.FramesPerSecond + 0.0001f);
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
            if (presentGuardianEndingDefeat != null)
            {
                presentGuardianEndingDefeat(elapsed);
                return;
            }
            PresentEndingDefeatDrawing(elapsed, elapsed);
        }

        void PresentEndingDefeatDrawing(float elapsed, float poseElapsed)
        {
            // The final guardian accepts the result while still present. Keep a
            // grounded kneel and lower the head instead of reviving into combat.
            deathAt = -1;
            picture.transform.localPosition = pictureHome;
            picture.transform.localScale = pictureScale;
            picture.transform.localRotation = Quaternion.identity;
            transform.localRotation = Quaternion.identity;
            DrawMotion(motionForm, "FinalDefeat", poseElapsed);
            picture.color = Color.Lerp(new Color(0.72f, 0.72f, 0.76f), pictureColor,
                KarmaGame.EndingEase(0.55f, 1.15f, elapsed));
            CurrentPose = elapsed < 2.9f ? "Ending/Reflect" : "Ending/Acknowledge";
        }
    }
}
