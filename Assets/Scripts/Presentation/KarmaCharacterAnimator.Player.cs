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
        internal void BeginJumpVisual() => jumpVisualUntil = Time.time + 0.125f;
        const float playerSampleRate = KarmaBootstrap.FramesPerSecond;

        void LoadPlayerClips()
        {
            playerClips.Clear(); playerSequences.Clear(); playerClip = null;
            if (sheet != "Player") return;
            var material = Resources.Load<Material>("Shared/Rendering/KarmaPlayer12");
            if (material != null && picture != null) picture.sharedMaterial = material;
            SamplePlayerClip("Idle", true);
        }
        // Load a new sheet only when its action is first requested. CC/ending art does not inflate normal startup.
        void LoadNativePlayerPose(string name)
        {
            if (playerSequences.ContainsKey(name)) return;
            var motion = KarmaPlayerTimeline.Find(name);
            if (motion == null) return;
            var clip = Resources.Load<AnimationClip>(KarmaPlayerTimeline.Clips + name);
            if (clip == null) throw new System.InvalidOperationException("Missing player clip " + name);
            var sequence = motion.Load();
            playerClips[name] = clip; playerSequences[name] = sequence;
        }
        float playerExplicitRecoveryEnd;
        internal bool PlayerActionPlaying => Time.time < actionUntil || Time.time < poseUntil;
        internal void PlayPlayerAction(string pose, float seconds)
        {
            playerExplicitRecoveryEnd = Time.time + seconds; hurtUntil = 0;
            PlayPose(pose, seconds);
        }
        internal void CancelPlayerActionForControl()
        {
            if (sheet != "Player") return;
            actionUntil = poseUntil = hurtUntil = playerExplicitRecoveryEnd = playerTurnUntil = 0;
            explicitPose = null;
        }
        bool PlayerRecovering(float movement, bool airborne)
        {
            float end = Mathf.Max(actionUntil, playerExplicitRecoveryEnd);
            float duration = airborne ? 2 * KarmaBootstrap.Step : Mathf.Abs(movement) > 0.1f ? 4 * KarmaBootstrap.Step : 3 * KarmaBootstrap.Step;
            return end > 0 && Time.time >= end && Time.time < end + duration;
        }
        string PlayerRecoveryPose(float movement, bool airborne)
            => airborne ? "AirRecovery" : Mathf.Abs(movement) > 0.1f ? "RunRecovery" : "Recovery";
        internal void ResetNativePlayerForRoom()
        {
            if (sheet != "Player") return;
            EndPersonalizationProof();
            CancelPlayerActionForControl(); ResetPlayerStatus();
            deathAt = -1; empowermentUntil = damageFlashUntil = 0; playerClip = null;
            wasAirborne = false; landedUntil = jumpVisualUntil = 0;
            picture.transform.localPosition = pictureHome; picture.transform.localRotation = Quaternion.identity;
            picture.color = pictureColor; transform.localRotation = Quaternion.identity;
            SamplePlayerClip("Idle", true);
        }
        float playerTurnUntil;
        internal bool PlayerTurning => Time.time < playerTurnUntil
            && Time.time >= actionUntil && Time.time >= poseUntil && Time.time >= hurtUntil;
        internal void BeginPlayerTurn() { playerTurnUntil = Time.time + 2 * KarmaBootstrap.Step; }
        void SamplePlayerClip(string name, bool restart = false)
        {
            if (picture == null) return;
            playerStatusPose = null;
            if (name == "Riposte" && SampleParrySuccess(restart)) return;
            LoadNativePlayerPose(name);
            if (!playerClips.TryGetValue(name, out var clip) || !playerSequences.TryGetValue(name, out var sequence) || sequence.Length == 0) return;
            if (restart || playerClip != name) { playerClip = name; playerClipAt = Time.time; }
            float elapsed = Mathf.Max(0, Time.time - playerClipAt);
            float sampleRate = playerSampleRate;
            float duration = KarmaPlayerTimeline.Find(name).Duration;
            float sample = clip.isLooping ? elapsed % duration : elapsed;
            // Quantize visuals only. Physics, damage and input windows keep their original clocks.
            picture.sprite = sequence[Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(sample * sampleRate + 0.00001f))];
            picture.flipX = false;
            picture.transform.localScale = pictureScale;
            CurrentPose = name;
        }

    }
}
