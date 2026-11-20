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
        int sw02DisplayFrame;
        bool sw02RecoveryRequested;
        internal bool SW02UsesIntermediateFrame { get; private set; }

        bool PresentSwordIntermediate(EnemyAttack attack)
        {
            if (attack != EnemyAttack.SW02) return false;
            if (!SW02UsesIntermediateFrame)
            {
                // Once Recover02 settles, the original cast holds its ready stance until recovery ends.
                if (sw02VisualPhase != SW02VisualPhase.Recovery) return false;
                PlayBody("Idle");
                return true;
            }
            displayForm = motionForm;
            displayMotion = "SW02";
            DrawMotion(motionForm, "SW02", sw02DisplayFrame * KarmaBootstrap.Step);
            return true;
        }

        void ConfigureSW02Frames()
        {
            sw02Owner = GetComponentInParent<KarmaEnemy>();
            sw02Frames = motionArt.Frames(motionForm, "SW02");
        }
        internal void BeginSW02Presentation(float trackingSeconds)
        {
            CancelSW02Presentation();
            if (sw02Frames == null) return;
            sw02VisualPhase = SW02VisualPhase.Tracking;
            sw02PhaseAt = Time.time; sw02PhaseSeconds = trackingSeconds;
            PrepareSwordBody("Heavy", trackingSeconds);
        }
        internal void LockSW02Presentation(float warningSeconds)
        {
            if (sw02VisualPhase == SW02VisualPhase.None) return;
            sw02VisualPhase = SW02VisualPhase.Locked;
            sw02PhaseAt = Time.time; sw02PhaseSeconds = warningSeconds;
            PrepareSwordBody("Heavy", warningSeconds);
        }
        internal void ReleaseSW02Presentation()
        {
            if (sw02VisualPhase == SW02VisualPhase.None) return;
            sw02VisualPhase = SW02VisualPhase.Released; sw02PhaseAt = Time.time;
            ReleaseSwordBody("Heavy");
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
            float elapsed = SwordFrameTime(Time.time - sw02PhaseAt);
            int frame;
            switch (sw02VisualPhase)
            {
                case SW02VisualPhase.Tracking:
                    frame = elapsed < sw02PhaseSeconds * 0.35f ? 0 : 1; break;
                case SW02VisualPhase.Locked:
                    // Two authored 24fps samples lead into the real release.
                    // The full strike cannot appear before the projectile event.
                    float swing = sw02PhaseSeconds - elapsed;
                    frame = swing > 2f / 24 ? 1 : 2; break;
                case SW02VisualPhase.Released:
                    // The blade keeps travelling after launch. Do not freeze
                    // the shoulders at the release pose for the whole wait.
                    frame = elapsed < 3f / 24 ? 3 : elapsed < 6f / 24 ? 4 : 5; break;
                default:
                    // Both recovery frames finish inside the existing minimum
                    // 0.38-second recovery, then retain the base ready picture.
                    if (elapsed >= 8f / 24) { picture.sprite = poses["Idle"]; return; }
                    frame = elapsed < 3f / 24 ? 4 : 5; break;
            }
            sw02DisplayFrame = frame;
            picture.sprite = sw02Frames[frame];
            SW02UsesIntermediateFrame = true;
        }

        // Absolute own-form Idle registration. Measure the body, not weapon
        // reach, wing tips, sprite rectangles or transparent source cells.
        internal Vector3 SwordVisualHome => pictureHome;
        internal Vector3 SwordAuthoredHome => pictureHome;
        internal Vector3 SwordFoot => Vector3.zero;
        internal string SwordBodyAction { get; private set; }
        internal int SwordStrikeIndex { get; private set; }
        bool SwordPreviousReleaseVisible => Time.time - swordPreviousRelease < 3f / 24;
        internal bool SwordBodyReleased => !float.IsNegativeInfinity(swordBodyReleasedAt) || SwordPreviousReleaseVisible;
        internal int SwordMotionStrike => SwordPreviousReleaseVisible ? swordPreviousStrike : SwordStrikeIndex;
        internal float SwordBodyAge => SwordFrameTime(Time.time - swordBodyAt);
        internal float SwordAscentProgress { get; private set; }
        internal void SampleSwordAscent(float progress)
        {
            if (SwordBodyAction == "UltimateAscent" && deathAt < 0)
                SwordAscentProgress = Mathf.Clamp01(progress);
        }
        internal float SwordReleaseAge => SwordFrameTime(Time.time - (SwordPreviousReleaseVisible ? swordPreviousRelease : swordBodyReleasedAt));
        internal float SwordBodyDuration { get; private set; }
        internal bool SwordBodyVisible { get; private set; }
        internal bool SwordHeavyLocked => sw02VisualPhase == SW02VisualPhase.Locked;
        float swordBodyAt, swordBodyReleasedAt = float.NegativeInfinity;
        float swordPreviousRelease = float.NegativeInfinity;
        int swordPreviousStrike;
        internal static float SwordFrameTime(float seconds) => Mathf.Floor(Mathf.Max(0, seconds) * KarmaBootstrap.FramesPerSecond) * KarmaBootstrap.Step;

        internal void PrepareSwordBody(string action, float seconds, int strike = 0)
        {
            if (sheet != "Wave" || deathAt >= 0) return;
            if (action != "Counter") swordWalkAllowed = false;
            swordPreviousRelease = action == "Volley" && SwordBodyAction == "Volley" ? swordBodyReleasedAt : float.NegativeInfinity;
            swordPreviousStrike = SwordStrikeIndex;
            SwordBodyAction = action; SwordBodyDuration = seconds; SwordStrikeIndex = strike;
            swordBodyAt = Time.time; swordBodyReleasedAt = float.NegativeInfinity;
            if (action == "UltimateAscent") SwordAscentProgress = 0;
        }
        internal void ReleaseSwordBody(string action, int strike = 0)
        {
            if (sheet != "Wave" || deathAt >= 0 || Time.time < hurtUntil) return;
            SwordBodyAction = action; SwordStrikeIndex = strike;
            swordBodyAt = swordBodyReleasedAt = Time.time;
            WaveMotionRevision++;
            // Real coroutine release is authoritative, even between Update and
            // LateUpdate. A 24fps sample may never move this event to a later tick.
            if (SwordBodyCanOverride(action == "Counter" ? "Approach" : "Attack",
                sw02Owner != null ? (EnemyAttack)sw02Owner.CurrentPattern : EnemyAttack.SWBasic))
            {
                ShowSwordPatternMotion(sw02Owner != null ? (EnemyAttack)sw02Owner.CurrentPattern : EnemyAttack.SWBasic);
                SwordBodyVisible = true; GuardianPhaseOverridesPose = true;
            }
        }
        void EndSwordBodyAction()
        {
            EndSW02PresentationAction();
            if (sw02Owner == null || !sw02Owner.Alive) CancelSwordBodyPresentation();
        }
        internal void CancelSwordBodyPresentation()
        {
            SwordBodyAction = null; swordBodyReleasedAt = float.NegativeInfinity;
            SwordBodyVisible = false; swordPreviousRelease = float.NegativeInfinity;
            wavePhase = null; waveReleasedUntil = 0; waveSwingAt = wavePhaseSwingAt = float.PositiveInfinity;
            basicPresentationActive = basicReleaseStarted = false;
            CancelSW02Presentation();
            if (picture != null && poses.ContainsKey("Idle"))
            { PlayBody("Idle"); GuardianPhaseOverridesPose = true; }
        }
        internal void EndSwordCounterBody()
        {
            if (SwordBodyAction != "Counter") return;
            SwordBodyAction = null; SwordBodyVisible = false; swordBodyReleasedAt = float.NegativeInfinity;
        }
        bool SwordBodyCanOverride(string action, EnemyAttack attack)
        {
            if (action == "Stunned" || Time.time < hurtUntil || deathAt >= 0) return false;
            if (SwordBodyAction != null && SwordBodyAction.StartsWith("Ultimate"))
                return attack == EnemyAttack.SW05 && (action == "Telegraph" || action == "Attack" || action == "Recover");
            // Persistent SW03/SW04 effects cannot take over another cast. Mobile
            // spacing and evasion always win over the short summoning gesture.
            if (SwordBodyAction == "Summon")
                return attack == EnemyAttack.SW03 && (action == "Telegraph" || action == "Attack"
                    || action == "Approach" && CurrentPose != "Move" && CurrentPose != "Backstep" && SwordReleaseAge < 5f / 24);
            if (SwordBodyAction == "Counter")
                return CurrentPose != "Backstep" && (action == "Approach" || action == "Recover" || attack == EnemyAttack.SW04);
            return action == "Telegraph" || action == "Attack" || action == "Recover";
        }
        void ApplySwordBodyPresentation(string action, EnemyAttack attack)
        {
            SwordBodyVisible = SwordBodyAction != null && SwordBodyCanOverride(action, attack);
            if (action == "Stunned" || Time.time < hurtUntil)
            {
                SwordBodyAction = null;
                swordBodyReleasedAt = float.NegativeInfinity;
            }
            if (SwordBodyVisible && attack != EnemyAttack.SW02) ShowSwordPatternMotion(attack);
        }

        void ShowSwordPatternMotion(EnemyAttack attack)
        {
            string motion = SwordBodyAction == "Counter" ? "SW04" : SwordBodyAction == "Summon" ? "SW03"
                : attack == EnemyAttack.SWBasic && RangedBasicBody ? "SWBasicRanged" : attack.ToString();
            if (!KarmaMotionArt.Has(motionForm, motion)) return;
            var frames = motionArt.Frames(motionForm, motion);
            // Hold the authored preparation until the combat event, then advance through contact and recovery.
            int frame = SwordBodyReleased ? Mathf.Min(frames.Length - 1, 1 + Mathf.FloorToInt(SwordReleaseAge * KarmaBootstrap.FramesPerSecond))
                : SwordBodyAction == "Summon" ? Mathf.FloorToInt(SwordGroundProgress * (frames.Length - 1))
                : SwordBodyAction == "UltimateAscent" ? Mathf.FloorToInt(SwordAscentProgress * (frames.Length - 1))
                : SwordBodyAction == "UltimateHold" || SwordBodyAction == "UltimateCharge" ? frames.Length - 1 : 0;
            displayForm = motionForm; displayMotion = motion;
            DrawMotion(motionForm, motion, frame * KarmaBootstrap.Step);
        }

        internal float SwordChargeBodyHeight => standingDamageBounds.size.y * Mathf.Abs(pictureScale.y * transform.lossyScale.y);
        internal void RegisterSwordAppearance()
        {
            // Each approved frame imports its own calibrated PPU and sole pivot.
            if (picture != null) picture.transform.localScale = pictureScale;
        }
        void ApplySwordBodyColor(Color color)
        {
            picture.color = color;
            ApplyDamageFlash();
        }
    }
}
