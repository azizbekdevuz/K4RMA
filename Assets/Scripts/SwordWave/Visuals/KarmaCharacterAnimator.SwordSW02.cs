using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        enum SW02VisualPhase { None, Tracking, Locked, Released, Recovery }
        SW02VisualPhase sw02VisualPhase;
        Sprite[] sw02Frames;
        KarmaEnemy sw02Owner;
        float sw02PhaseAt, sw02PhaseSeconds;
        bool sw02RecoveryRequested;
        internal bool SW02UsesIntermediateFrame { get; private set; }

        void ConfigureSW02Frames()
        {
            // Inherited Idle sprites retain their source texture identity. Load
            // only that form's complete set; never fall back to the base body.
            string group = "WaveSW02";
            if (guardianAppearance != null)
            {
                switch (guardianAppearance.texture.name)
                {
                    case "WaveRise": group = "WaveRiseSW02"; break;
                    case "WaveWard": group = "WaveWardSW02"; break;
                    case "WaveFinal": group = "WaveFinalSW02"; break;
                    default: return;
                }
            }
            sw02Owner = GetComponentInParent<KarmaEnemy>();
            string[] names = { "Prepare01", "Prepare02", "Swing01", "Swing02", "Recover01", "Recover02" };
            var frames = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                frames[i] = KarmaArtwork.Sprite(group, names[i]);
                if (frames[i] == null) return;
            }
            sw02Frames = frames;
        }
        internal void BeginSW02Presentation(float trackingSeconds)
        {
            CancelSW02Presentation();
            if (sw02Frames == null) return;
            sw02VisualPhase = SW02VisualPhase.Tracking;
            sw02PhaseAt = Time.time; sw02PhaseSeconds = trackingSeconds;
        }
        internal void LockSW02Presentation(float warningSeconds)
        {
            if (sw02VisualPhase == SW02VisualPhase.None) return;
            sw02VisualPhase = SW02VisualPhase.Locked;
            sw02PhaseAt = Time.time; sw02PhaseSeconds = warningSeconds;
        }
        internal void ReleaseSW02Presentation()
        {
            if (sw02VisualPhase == SW02VisualPhase.None) return;
            sw02VisualPhase = SW02VisualPhase.Released; sw02PhaseAt = Time.time;
        }
        internal void RecoverSW02Presentation()
        { sw02RecoveryRequested = sw02VisualPhase != SW02VisualPhase.None; }
        void EndSW02PresentationAction()
        {
            // Recovery calls EndGuardianAction at its actual start. Death and
            // any other end clear immediately rather than replaying recovery.
            if (!sw02RecoveryRequested || sw02Owner == null || !sw02Owner.Alive)
            { CancelSW02Presentation(); return; }
            sw02RecoveryRequested = false;
            sw02VisualPhase = SW02VisualPhase.Recovery; sw02PhaseAt = Time.time;
        }
        internal void CancelSW02Presentation()
        {
            if (SW02UsesIntermediateFrame && picture != null && CurrentPose != null
                && poses.TryGetValue(CurrentPose, out var original)) picture.sprite = original;
            sw02VisualPhase = SW02VisualPhase.None; sw02RecoveryRequested = false;
            SW02UsesIntermediateFrame = false;
        }
        void ApplySW02Presentation(string action, EnemyAttack attack)
        {
            SW02UsesIntermediateFrame = false;
            if (attack != EnemyAttack.SW02 || action == "Stunned" || Time.time < hurtUntil
                || action == "Approach")
            { CancelSW02Presentation(); return; }
            if (sw02Frames == null || sw02VisualPhase == SW02VisualPhase.None) return;
            float elapsed = Mathf.Max(0, Time.time - sw02PhaseAt);
            int frame;
            switch (sw02VisualPhase)
            {
                case SW02VisualPhase.Tracking:
                    frame = elapsed < sw02PhaseSeconds * 0.5f ? 0 : 1; break;
                case SW02VisualPhase.Locked:
                    // Keep the existing final 0.12-second swing lead-in. No
                    // warning, coroutine wait, release or hit timer is changed.
                    float swing = sw02PhaseSeconds - elapsed;
                    frame = swing > 0.12f ? 1 : swing > 0.06f ? 2 : 3; break;
                case SW02VisualPhase.Released:
                    frame = 3; break;
                default:
                    // Both recovery frames finish inside the existing minimum
                    // 0.38-second recovery, then retain the base ready picture.
                    if (elapsed >= 0.28f) { picture.sprite = poses["Idle"]; return; }
                    frame = elapsed < 0.13f ? 4 : 5; break;
            }
            picture.sprite = sw02Frames[frame];
            SW02UsesIntermediateFrame = true;
        }
    }
}
