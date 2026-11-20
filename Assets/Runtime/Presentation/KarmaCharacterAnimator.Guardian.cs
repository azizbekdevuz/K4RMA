using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        // Bosses install their own pose policy; the animator owns sprite application.
        System.Action<string, EnemyAttack, bool, bool> guardianPose;
        System.Action onPoseRequested, onActionEnded;
        bool deferExplicitPose, hasInheritedPoseSet;
        Sprite stageIdle, shieldlessIdle, displayedIdle;
        Bounds standingDamageBounds;

        internal void ConfigureGuardianIdle(string form)
        {
            motionForm = form;
            stageIdle = KarmaArtwork.StageIdle(form);
            if (form == "WardFinal") shieldlessIdle = KarmaArtwork.StageIdle("WardFinal_NoShield");
            PlayBody("Idle");
            if (Application.isPlaying && isActiveAndEnabled) StartCoroutine(motionArt.PrepareGuardian(form));
        }

        internal void ShowStandingArt(bool shieldAway = false)
        {
            var sprite = shieldAway ? shieldlessIdle : stageIdle;
            if (sprite == null || picture.sprite == null || picture.sprite == sprite) return;
            if (picture.sprite != displayedIdle)
            {
                // The old sprite supplies the authored hit silhouette at its original scale.
                var ratio = new Vector3(picture.transform.localScale.x / pictureScale.x,
                    picture.transform.localScale.y / pictureScale.y, 1);
                var bounds = KarmaBodyPoses.HitBounds(picture.sprite);
                standingDamageBounds = new Bounds(Vector3.Scale(bounds.center, ratio), Vector3.Scale(bounds.size, ratio));
            }
            picture.sprite = displayedIdle = sprite;
            picture.transform.localScale = pictureScale;
        }

        internal Bounds DisplayDamageLocalBounds(SpriteRenderer renderer) =>
            renderer == picture && displaySprite != null && renderer.sprite == displaySprite ? displayDamage
                : renderer == picture && displayedIdle != null && renderer.sprite == displayedIdle
                ? standingDamageBounds : renderer.localBounds;

        internal Bounds DisplayDamageBounds(SpriteRenderer renderer)
        {
            if (renderer != picture || renderer.sprite != displaySprite && renderer.sprite != displayedIdle) return renderer.bounds;
            var bounds = DisplayDamageLocalBounds(renderer);
            var matrix = renderer.transform.localToWorldMatrix;
            var e = bounds.extents;
            var x = matrix.MultiplyVector(Vector3.right * e.x);
            var y = matrix.MultiplyVector(Vector3.up * e.y);
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center),
                2 * new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), 0));
        }
        public void EndGuardianAction() { onActionEnded?.Invoke(); }
        public void SetGuardianAppearance(Sprite sprite, string form = null)
        {
            if (sprite == null || picture == null) return;
            guardianAppearance = sprite;
            hasInheritedPoseSet = false;
            appearanceScale = KarmaBodyPoses.HitBounds(picture.sprite).size.y / Mathf.Max(0.01f, KarmaBodyPoses.HitBounds(sprite).size.y);
            if (string.IsNullOrEmpty(form)) return;
            var inherited = KarmaArtwork.InheritedPoses(form);
            if (inherited.Length != 16) return;
            hasInheritedPoseSet = true;
            foreach (var pose in inherited) poses[pose.name] = pose;
            guardianAppearance = poses["Idle"];
            appearanceScale = 1;
            revivalFrames = LoadRevivalFrames(form);
        }
        public void GuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            if (picture == null || deathAt >= 0) return;
            if (growthActive) return;
            if (!EmpowermentPose())
            {
                bool moved = (transform.position - guardianPreviousPosition).sqrMagnitude > 0.00001f;
                if (guardianPose != null) guardianPose(action, attack, airborne, armored);
                else ApplyGuardianPose(DefaultGuardianPose(action, airborne, armored));
                if ((CurrentPose == "Idle" || CurrentPose == "Guard" && action == "Approach") &&
                    (picture.sprite == guardianAppearance || poses.TryGetValue(CurrentPose, out var pose) && picture.sprite == pose))
                    ShowStandingArt();
                PresentCombatBody(action, attack, moved);
            }
            ApplyDamageFlash();
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
            bool composed = guardianAppearance != null && (pose == "Idle" || pose == "Guard" && !hasInheritedPoseSet);
            if (composed) picture.sprite = guardianAppearance;
            picture.transform.localScale = pictureScale * (composed ? appearanceScale : 1);
            guardianPreviousPosition = transform.position;
            picture.transform.localPosition = pictureHome;
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
        }
    }
}
