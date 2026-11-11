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
        float playerSampleRate = 12;
        bool nativePlayer24;

        void LoadPlayerClips()
        {
            playerClips.Clear(); playerSequences.Clear(); playerClip = null;
            if (sheet != "Player") return;
            nativePlayer24 = Resources.Load<TextAsset>("PlayerAnimation24/Ready") != null;
            playerSampleRate = nativePlayer24 ? 24 : 12;
            if (nativePlayer24)
            {
                var material = Resources.Load<Material>("Materials/KarmaPlayer24");
                if (material != null && picture != null) picture.sharedMaterial = material;
                LoadNativePlayerPose("Idle");
                if (picture != null && playerSequences.TryGetValue("Idle", out var idle) && idle.Length > 0)
                { picture.sprite = idle[0]; playerClip = "Idle"; playerClipAt = Time.time; CurrentPose = "Idle"; }
                return;
            }
            foreach (var clip in Resources.LoadAll<AnimationClip>("PlayerAnimation")) playerClips[clip.name] = clip;
            var library = Resources.Load<KarmaSpriteLibrary>("PlayerAnimation/Frames");
            if (library != null) foreach (var entry in library.sheets) playerSequences[entry.id] = entry.sprites;
        }
        // Load a new sheet only when its action is first requested. CC/ending art does not inflate normal startup.
        void LoadNativePlayerPose(string name)
        {
            if (!nativePlayer24 || playerSequences.ContainsKey(name)) return;
            string artName = name == "AirSlash" ? "AirHorizontal" : name == "Dash" ? "Pierce"
                : name == "Transition" ? "PersonalizeWave" : name;
            var clip = Resources.Load<AnimationClip>("PlayerAnimation24/" + name);
            if (clip == null) return;
            var sequence = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Player/Native24/" + artName);
            System.Array.Sort(sequence, (a,b) => string.CompareOrdinal(a.name,b.name));
            if (sequence.Length == 0) return;
            playerClips[name] = clip; playerSequences[name] = sequence;
        }
        float playerExplicitRecoveryEnd;
        internal bool UsesNativePlayer24 => nativePlayer24;
        internal void PlayPlayerAction(string nativePose, string legacyPose, float seconds)
        {
            if (nativePlayer24) { playerExplicitRecoveryEnd = Time.time + seconds; hurtUntil = 0; }
            PlayPose(nativePlayer24 ? nativePose : legacyPose, seconds);
        }
        internal void CancelPlayerActionForControl()
        {
            if (!nativePlayer24) return;
            actionUntil = poseUntil = hurtUntil = playerExplicitRecoveryEnd = playerTurnUntil = 0;
            explicitPose = null;
        }
        bool PlayerRecovering(float movement, bool airborne)
        {
            float end = Mathf.Max(actionUntil, playerExplicitRecoveryEnd);
            float duration = airborne ? 4f / 24 : Mathf.Abs(movement) > 0.1f ? 8f / 24 : 6f / 24;
            return nativePlayer24 && end > 0 && Time.time >= end && Time.time < end + duration;
        }
        string PlayerRecoveryPose(float movement, bool airborne)
            => airborne ? "AirRecovery" : Mathf.Abs(movement) > 0.1f ? "RunRecovery" : "Recovery";
        internal void ResetNativePlayerForRoom()
        {
            if (!nativePlayer24) return;
            CancelPlayerActionForControl(); ResetPlayerStatus();
            deathAt = -1; empowermentUntil = 0; playerClip = null;
            wasAirborne = false; landedUntil = jumpVisualUntil = 0;
            picture.transform.localPosition = pictureHome; picture.transform.localRotation = Quaternion.identity;
            picture.color = pictureColor; transform.localRotation = Quaternion.identity;
            SamplePlayerClip("Idle", true);
        }
        float playerTurnUntil;
        internal bool PlayerTurning => nativePlayer24 && Time.time < playerTurnUntil
            && Time.time >= actionUntil && Time.time >= poseUntil && Time.time >= hurtUntil;
        internal void BeginPlayerTurn() { if (nativePlayer24) playerTurnUntil = Time.time + 4f / 24; }
        void SamplePlayerClip(string name, bool restart = false)
        {
            if (picture == null) return;
            playerStatusPose = null;
            if (name == "Riposte" && SampleParrySuccess(restart)) return;
            LoadNativePlayerPose(name);
            if (!playerClips.TryGetValue(name, out var clip) || !playerSequences.TryGetValue(name, out var sequence) || sequence.Length == 0) { if (!nativePlayer24) ShowPose(name); return; }
            if (restart || playerClip != name) { playerClip = name; playerClipAt = Time.time; }
            float elapsed = Mathf.Max(0, Time.time - playerClipAt);
            float duration = sequence.Length / playerSampleRate;
            float sample = clip.isLooping ? elapsed % duration : Mathf.Min(elapsed, duration - 0.0001f);
            // Quantize visuals only. Physics, damage and input windows keep their original clocks.
            picture.sprite = sequence[Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(sample * playerSampleRate))];
            picture.flipX = false;
            picture.transform.localScale = pictureScale;
            CurrentPose = name;
        }
    }
}
