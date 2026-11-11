using System;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        Sprite[] parryFrames, parryEffects;
        SpriteRenderer parryFlash;
        public bool ParrySuccessVisible => sheet == "Player" && explicitPose == "Riposte"
            && Time.time < poseUntil && Time.time >= hurtUntil && deathAt < 0;

        bool SampleParrySuccess(bool restart)
        {
            if (parryFrames == null)
            {
                parryFrames = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Player/ParrySuccess");
                parryEffects = Resources.LoadAll<Sprite>("K4RMAArtwork/Effects/ParrySuccess");
                Array.Sort(parryFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
                Array.Sort(parryEffects, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            if (parryFrames.Length != 8 || parryEffects.Length != 8) return false;
            if (restart || playerClip != "Riposte") { playerClip = "Riposte"; playerClipAt = Time.time; }
            // Seven full 24 FPS intervals, then a short final recovery frame.
            // The existing poseUntil (0.3 seconds) remains the authority for ending this pose.
            int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, Time.time - playerClipAt) * 24), 0, 7);
            picture.sprite = parryFrames[index];
            picture.flipX = false;
            picture.transform.localScale = pictureScale;
            CurrentPose = "Riposte";
            if (parryFlash == null)
            {
                var node = new GameObject("Parry success flash");
                node.transform.SetParent(picture.transform, false);
                parryFlash = node.AddComponent<SpriteRenderer>();
                KarmaVisuals.ApplyMaterial(parryFlash);
            }
            // Same relative position and scale as the approved composite preview.
            parryFlash.transform.localPosition = new Vector3(189f / 145, 50f / 145, 0);
            parryFlash.sortingLayerID = picture.sortingLayerID;
            parryFlash.sortingOrder = picture.sortingOrder + 1;
            parryFlash.sprite = parryEffects[index];
            parryFlash.enabled = true;
            return true;
        }

        void LateUpdate()
        {
            if (parryFlash != null && (!ParrySuccessVisible || CurrentPose != "Riposte"))
                parryFlash.enabled = false;
        }

        void OnDisable()
        {
            if (parryFlash != null) parryFlash.enabled = false;
        }
    }
}
