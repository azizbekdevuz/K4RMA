using UnityEngine;
using System.Collections.Generic;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        readonly Dictionary<string, Sprite[]> playerSequences = new Dictionary<string, Sprite[]>();
        string playerClip;
        float playerClipAt, jumpVisualUntil;
        internal void BeginJumpVisual() => jumpVisualUntil = Time.time + 0.125f;
        const float playerSampleRate = KarmaBootstrap.FramesPerSecond;

        void LoadPlayerClips()
        {
            playerSequences.Clear(); playerClip = null;
            if (sheet != "Player") return;
            var material = KarmaArtResource.Load<Material>("Shared/Rendering/KarmaPlayer12");
            if (material != null && picture != null) picture.sharedMaterial = material;
            SamplePlayerClip("Idle", true);
        }
        // Animation clips are authored from this same timeline. Live playback needs only its drawings and loop flag.
        Sprite[] LoadNativePlayerPose(string name)
        {
            if (playerSequences.TryGetValue(name, out var sequence)) return sequence;
            var motion = KarmaPlayerTimeline.Find(name);
            if (motion == null) return System.Array.Empty<Sprite>();
            if (!KarmaMotionArt.TryPreparedPlayer(motion.Source, out sequence)) sequence = motion.Load();
            playerSequences[name] = sequence;
            return sequence;
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
            actionUntil = poseUntil = hurtUntil = playerExplicitRecoveryEnd = 0;
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
        void SamplePlayerClip(string name, bool restart = false)
        {
            if (picture == null) return;
            playerStatusPose = null;
            if (name == "Riposte" && SampleParrySuccess(restart)) return;
            var sequence = LoadNativePlayerPose(name);
            if (sequence.Length == 0) return;
            if (restart || playerClip != name) { playerClip = name; playerClipAt = Time.time; }
            float elapsed = Mathf.Max(0, Time.time - playerClipAt);
            var motion = KarmaPlayerTimeline.Find(name);
            float sample = motion.Loop ? elapsed % motion.Duration : elapsed;
            // Quantize visuals only. Physics, damage and input windows keep their original clocks.
            picture.sprite = sequence[Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(sample * playerSampleRate + 0.00001f))];
            picture.flipX = false;
            picture.transform.localScale = pictureScale;
            CurrentPose = name;
        }

    }
}
