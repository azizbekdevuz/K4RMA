using UnityEngine;
using System.Collections.Generic;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        readonly Dictionary<string, AnimationClip> playerClips = new Dictionary<string, AnimationClip>();
        readonly Dictionary<string, Sprite[]> playerSequences = new Dictionary<string, Sprite[]>();
        string playerClip;
        float playerClipAt, jumpVisualUntil;

        void LoadPlayerClips()
        {
            playerClips.Clear(); playerSequences.Clear(); playerClip = null;
            if (sheet != "Player") return;
            foreach (var clip in Resources.LoadAll<AnimationClip>("PlayerAnimation")) playerClips[clip.name] = clip;
            var library = Resources.Load<KarmaSpriteLibrary>("PlayerAnimation/Frames");
            if (library != null) foreach (var entry in library.sheets) playerSequences[entry.id] = entry.sprites;
        }
        void SamplePlayerClip(string name, bool restart = false)
        {
            if (picture == null) return;
            if (!playerClips.TryGetValue(name, out var clip) || !playerSequences.TryGetValue(name, out var sequence) || sequence.Length == 0) { ShowPose(name); return; }
            if (restart || playerClip != name) { playerClip = name; playerClipAt = Time.time; }
            float elapsed = Mathf.Max(0, Time.time - playerClipAt);
            float duration = sequence.Length / 12f;
            float sample = clip.isLooping ? elapsed % duration : Mathf.Min(elapsed, duration - 0.0001f);
            // Quantize visuals only. Physics, damage and input windows keep their original clocks.
            picture.sprite = sequence[Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(sample * 12))];
            picture.transform.localScale = pictureScale;
            CurrentPose = name;
        }
    }
}
