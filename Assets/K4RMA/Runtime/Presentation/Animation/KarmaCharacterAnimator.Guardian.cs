using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        // Bosses install their own pose policy; the animator owns sprite application.
        System.Action<string, EnemyAttack, bool, bool> guardianPose;
        System.Action onPoseRequested, onActionEnded;
        bool deferExplicitPose;
        public void EndGuardianAction() { onActionEnded?.Invoke(); }
        public void SetGuardianAppearance(Sprite sprite, string form = null)
        {
            if (sprite == null || picture == null) return;
            guardianAppearance = sprite;
            appearanceScale = picture.sprite.bounds.size.y / Mathf.Max(0.01f, sprite.bounds.size.y);
            if (string.IsNullOrEmpty(form)) return;
            var inherited = KarmaArtwork.InheritedPoses(form);
            if (inherited.Length != 16) return;
            foreach (var pose in inherited) poses[pose.name] = pose;
            guardianAppearance = poses["Idle"];
            appearanceScale = 1;
            revivalFrames = LoadRevivalFrames(form);
        }
        public void GuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            if (picture == null || deathAt >= 0) return;
            if (EmpowermentPose()) return;
            if (guardianPose != null) { guardianPose(action, attack, airborne, armored); return; }
            ApplyGuardianPose(DefaultGuardianPose(action, airborne, armored));
        }
        string DefaultGuardianPose(string action, bool airborne, bool armored)
        {
            if (Time.time < hurtUntil) return "Hurt";
            if (action == "Stunned") return "Kneel";
            if (action == "Recover") return "Recovery";
            if (action == "Telegraph") return armored && poses.ContainsKey("Guard") ? "Guard" : "Windup";
            if (action == "Attack") return "Slash";
            return armored && poses.ContainsKey("Guard") ? "Guard"
                : (transform.position - guardianPreviousPosition).sqrMagnitude > 0.00001f ? "Move" : "Idle";
        }
        void ApplyGuardianPose(string pose)
        {
            if (!poses.ContainsKey(pose)) pose = "Idle";
            ShowPose(pose);
            bool composed = guardianAppearance != null && (pose == "Idle" || pose == "Guard");
            if (composed) picture.sprite = guardianAppearance;
            picture.transform.localScale = pictureScale * (composed ? appearanceScale : 1);
            guardianPreviousPosition = transform.position;
            picture.transform.localPosition = pictureHome;
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
        }
    }
}
