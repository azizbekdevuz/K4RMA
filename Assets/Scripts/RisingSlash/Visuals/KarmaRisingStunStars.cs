using System;
using UnityEngine;

namespace KarmaPrototype
{
    // Animates the host-owned cue; HideStunCue still owns stun/interrupt/death cleanup.
    public sealed class KarmaRisingStunStars : MonoBehaviour
    {
        const int FrameCount = 48;
        const float FramesPerSecond = 24;
        // Approved preview: 160px cue canvas / 250px crouched body;
        // the orbit centre is 15px above the body's top after lowering by 25px.
        const float CanvasHeightRatio = 160f / 250f;
        const float HeadGapRatio = 15f / 250f;
        readonly Sprite[] frames = new Sprite[FrameCount];
        KarmaGame game;
        SpriteRenderer picture, mark;
        Func<Transform> getCue;
        Transform cue;
        float elapsed;
        int displayedFrame = -1;

        public void Initialize(KarmaGame director, SpriteRenderer body, Func<Transform> cueSource)
        {
            game = director;
            picture = body;
            getCue = cueSource;
            for (int i = 0; i < FrameCount; i++)
            {
                frames[i] = KarmaArtwork.Sprite("RisingStunStars", "Frame" + i.ToString("00"));
                if (frames[i] == null)
                    throw new InvalidOperationException("상승베기 기절 별 프레임 누락: " + i);
            }
        }

        void LateUpdate()
        {
            Transform current = getCue != null ? getCue() : null;
            if (current == null || picture == null)
            {
                cue = null; mark = null; elapsed = 0; displayedFrame = -1;
                return;
            }
            if (current != cue)
            {
                cue = current;
                mark = cue.GetComponent<SpriteRenderer>();
                elapsed = 0; displayedFrame = -1;
            }
            else if (game != null && !game.Paused)
                elapsed = (elapsed + Time.deltaTime) % (FrameCount / FramesPerSecond);
            if (mark == null) return;

            int frame = Mathf.FloorToInt(elapsed * FramesPerSecond) % FrameCount;
            if (frame != displayedFrame)
            {
                mark.sprite = frames[frame];
                displayedFrame = frame;
            }
            Bounds body = picture.bounds;
            float height = body.size.y;
            cue.position = new Vector3(transform.position.x, body.max.y + height * HeadGapRatio,
                transform.position.z);
            cue.localRotation = Quaternion.identity;
            float fit = height * CanvasHeightRatio / Mathf.Max(0.01f, frames[0].bounds.size.y);
            // The cue is a direct child of the host, not the mirrored character artwork.
            Vector3 parentScale = transform.lossyScale;
            cue.localScale = new Vector3(fit / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                fit / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1);
        }
    }
}
