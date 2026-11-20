using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        string playerStatusPose;
        float playerStatusAt;
        internal void ResetPlayerStatus() { playerStatusPose = null; }
        internal bool PresentPlayerStatus(string pose, float elapsed, bool recovery = false, bool reverse = false)
        {
            if (sheet != "Player" || picture == null || deathAt >= 0 || Time.time < empowermentUntil) return false;
            if (recovery && (Time.time < actionUntil || Time.time < poseUntil || Time.time < hurtUntil)) return false;
            var sequence = LoadNativePlayerPose(pose);
            if (sequence.Length == 0) return false;
            if (playerStatusPose != pose) { playerStatusPose = pose; playerStatusAt = Time.time; }
            if (pose.StartsWith("Airborne", System.StringComparison.Ordinal)) elapsed = Time.time - playerStatusAt;
            int frame = Mathf.FloorToInt(elapsed * KarmaBootstrap.FramesPerSecond);
            playerClip = null;
            picture.flipX = reverse;
            picture.sprite = sequence[KarmaPlayerTimeline.Find(pose).Loop ? frame % sequence.Length : Mathf.Min(frame, sequence.Length - 1)];
            picture.transform.localScale = pictureScale;
            picture.transform.localPosition = pictureHome; picture.transform.localRotation = Quaternion.identity;
            picture.color = pictureColor; transform.localRotation = Quaternion.identity;
            CurrentPose = pose;
            return true;
        }
    }
}
