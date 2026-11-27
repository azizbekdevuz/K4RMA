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
            DrawAimedFrame();
            return true;
        }

        internal bool SwordCarriesShield => motionForm == "WaveWard" || motionForm == "WaveFinal";

        void DrawAimedFrame()
        {
            if (DrawSwordCastArt(EnemyAttack.SW02)) return;
            if (sw02DisplayFrame < 6) DrawMotion(motionForm, "SW02", sw02DisplayFrame * KarmaBootstrap.Step);
            else DrawMotion(motionForm, "FollowThrough", (sw02DisplayFrame - 5) * KarmaBootstrap.Step);
            RegisterSwordAppearance();
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
            // The pattern attaches its charge to the current blade immediately after this call.
            if (motionForm == "Wave" && SwordBodyCanOverride("Telegraph", EnemyAttack.SW02))
                DrawSwordCastArt(EnemyAttack.SW02);
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
            // Release and recovery use the same clock, including when recovery
            // starts between two 12 fps samples.
            sw02VisualPhase = SW02VisualPhase.Recovery;
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
                    // The last preparation drawing leads into the real release.
                    // The full strike cannot appear before the projectile event.
                    frame = elapsed < SwordFrameTime(sw02PhaseSeconds - KarmaBootstrap.Step) ? 1 : 2; break;
                case SW02VisualPhase.Released:
                case SW02VisualPhase.Recovery:
                    // Let the support hand leave the hilt and the point pass the knee before standing.
                    if (sw02VisualPhase == SW02VisualPhase.Recovery
                        && elapsed >= (motionForm == "Wave" ? WaveDowncutEnd : 5 * KarmaBootstrap.Step)) return;
                    frame = Mathf.Min(sw02VisualPhase == SW02VisualPhase.Recovery ? 7 : 5,
                        3 + Mathf.RoundToInt(elapsed * KarmaBootstrap.FramesPerSecond));
                    break;
                default: return;
            }
            sw02DisplayFrame = frame;
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
        float swordBodyAt, swordBodyReleasedAt = float.NegativeInfinity;
        float swordPreviousRelease = float.NegativeInfinity;
        internal static float SwordFrameTime(float seconds) => Mathf.Floor(Mathf.Max(0, seconds) * KarmaBootstrap.FramesPerSecond) * KarmaBootstrap.Step;

        internal void PrepareSwordBody(string action, float seconds, int strike = 0)
        {
            if (sheet != "Wave" || deathAt >= 0) return;
            if (action != "Counter") swordWalkAllowed = false;
            swordPreviousRelease = action == "Volley" && SwordBodyAction == "Volley" && strike == SwordStrikeIndex + 1
                ? swordBodyReleasedAt : float.NegativeInfinity;
            SwordBodyAction = action; SwordBodyDuration = seconds; SwordStrikeIndex = strike;
            swordBodyAt = Time.time; swordBodyReleasedAt = float.NegativeInfinity;
            if (action == "UltimateAscent") SwordAscentProgress = 0;
            if (action == "UltimateWindup") SwordGroundProgress = 0;
        }
        internal void ReleaseSwordBody(string action, int strike = 0)
        {
            if (sheet != "Wave" || deathAt >= 0 || Time.time < hurtUntil) return;
            SwordBodyAction = action; SwordStrikeIndex = strike;
            swordBodyAt = swordBodyReleasedAt = Time.time;
            if (action == "UltimateCommand") swordRainCutAt = swordBodyReleasedAt;
            swordPreviousRelease = float.NegativeInfinity;
            WaveMotionRevision++;
            // Real coroutine release is authoritative, even between Update and
            // LateUpdate. Show the contact drawing in the event's render frame.
            var attack = sw02Owner != null ? (EnemyAttack)sw02Owner.CurrentPattern :
                action == ShieldRaiseAction ? EnemyAttack.SW04 :
                action == "Heavy" ? EnemyAttack.SW02 : action == "Volley" ? EnemyAttack.SW01 : EnemyAttack.SWBasic;
            if (SwordBodyCanOverride(action == "Counter" ? "Approach" : "Attack", attack))
            {
                if (action == "Heavy")
                {
                    sw02DisplayFrame = 3;
                    SW02UsesIntermediateFrame = true;
                    PresentSwordIntermediate(EnemyAttack.SW02);
                }
                else ShowSwordPatternMotion(attack);
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
            HideSwordShieldRaise();
            swordGroundDrawing = -1;
            RegisterSwordGroundArt();
            swordCastDrawing = -1;
            swordRainCutAt = float.NegativeInfinity;
            if (swordCastBlade != null) swordCastBlade.enabled = false;
            swordBasicDrawing = -1;
            if (swordBasicBlade != null) swordBasicBlade.enabled = false;
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
            if (SwordBodyAction != "Counter" && SwordBodyAction != ShieldRaiseAction) return;
            HideSwordShieldRaise();
            SwordBodyAction = null; SwordBodyVisible = false; swordBodyReleasedAt = float.NegativeInfinity;
        }
        bool SwordBodyCanOverride(string action, EnemyAttack attack)
        {
            if (action == "Stunned" || Time.time < hurtUntil || deathAt >= 0) return false;
            if (SwordBodyAction == ShieldRaiseAction)
                return attack == EnemyAttack.SW04 && (action == "Telegraph" || action == "Attack"
                    || action == "Recover" && SwordBodyReleased);
            if (SwordBodyAction == "Volley")
                return attack == EnemyAttack.SW01 && (action == "Telegraph" || action == "Attack" ||
                    action == "Recover" && SwordBodyReleased);
            if (SwordBodyAction == "Heavy")
                return attack == EnemyAttack.SW02 && sw02VisualPhase != SW02VisualPhase.None &&
                    (action == "Telegraph" || action == "Attack" || action == "Recover");
            if (SwordBodyAction != null && SwordBodyAction.StartsWith("Ultimate"))
                return attack == EnemyAttack.SW05 && (action == "Telegraph" || action == "Attack" || action == "Recover");
            // Persistent SW03/SW04 effects cannot take over another cast. Mobile
            // spacing and evasion always win over the short summoning gesture.
            if (SwordBodyAction == "Summon")
                return attack == EnemyAttack.SW03 && (action == "Telegraph" || action == "Attack"
                    || action == "Approach" && CurrentPose != "Move" && CurrentPose != "Backstep"
                        && SwordReleaseAge < SwordGroundPlantSeconds + 2 * KarmaBootstrap.Step);
            if (SwordBodyAction == "Counter")
            {
                bool gestureActive = SwordBodyReleased ? SwordReleaseAge < 3 * KarmaBootstrap.Step
                    : SwordBodyAge <= SwordBodyDuration + KarmaBootstrap.Step;
                return gestureActive && CurrentPose != "Backstep" &&
                    (action == "Approach" || action == "Recover" || attack == EnemyAttack.SW04);
            }
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
            if (SwordBodyVisible && !(attack == EnemyAttack.SW02 && SwordBodyAction == "Heavy"))
                ShowSwordPatternMotion(attack, action);
        }

        string SwordPatternMotion(EnemyAttack attack)
        {
            return SwordBodyAction == "UltimateRecover" ? "FollowThrough"
                : SwordBodyAction == "Counter" ? "SW04" : SwordBodyAction == "Summon" ? "SW03"
                : attack == EnemyAttack.SWBasic && RangedBasicBody ? "SWBasicRanged" : attack.ToString();
        }

        void ShowSwordPatternMotion(EnemyAttack attack, string action = "Attack")
        {
            if (attack == EnemyAttack.SW01 && SwordBodyAction == "Volley") { DrawSwordVolley(action); return; }
            if (SwordBodyAction == ShieldRaiseAction) { DrawSwordShieldRaise(); return; }
            if (DrawSwordGroundArt(attack)) return;
            if (DrawSwordCastArt(attack)) return;
            if (attack == EnemyAttack.SWBasic && BasicBodyActive && swordBasicArt != null)
            { DrawSwordBasicArt(attack); return; }
            string motion = SwordPatternMotion(attack);
            if (!KarmaMotionArt.Has(motionForm, motion)) return;
            if (motion == "FollowThrough")
            {
                PlayBody(motion);
                RegisterSwordAppearance();
                return;
            }
            var frames = motionArt.Frames(motionForm, motion);
            int last = frames.Length - 1;
            int raised = Mathf.Max(0, last - 1);
            string drawing = motion;
            int frame;
            switch (SwordBodyAction)
            {
                case "BasicMelee":
                case "BasicRanged":
                    // Both basic modes share the same two-handed downcut.
                    drawing = "SWBasic";
                    if (!SwordBodyReleased)
                    {
                        if (SwordBodyAge < KarmaBootstrap.Step)
                        { drawing = "SW02"; frame = 0; }
                        else frame = 0;
                    }
                    else if (SwordReleaseAge < KarmaBootstrap.Step) frame = 1;
                    else if (SwordReleaseAge < 3 * KarmaBootstrap.Step) frame = 2;
                    else
                    {
                        int basicSettle = Mathf.FloorToInt(SwordReleaseAge * KarmaBootstrap.FramesPerSecond) - 3;
                        drawing = basicSettle < 2 ? "FollowThrough" : "Idle";
                        frame = basicSettle < 2 ? basicSettle + 1 : 0;
                    }
                    break;
                case "Summon":
                case "UltimateWindup":
                    // Each skill has its own lift, loaded hold, drive and impact drawings.
                    float remaining = (1 - SwordGroundProgress) * SwordBodyDuration;
                    if (SwordBodyReleased) frame = 3;
                    else if (SwordBodyAction == "UltimateWindup" && SwordStrikeIndex > 0 && SwordBodyAge < KarmaBootstrap.Step) frame = 3;
                    else if (SwordGroundProgress < 0.25f) frame = 0;
                    else frame = remaining > 1.5f * KarmaBootstrap.Step ? 1 : 2;
                    break;
                case "UltimateStart":
                    drawing = "Guard";
                    frame = SwordBodyAge < SwordBodyDuration * 0.5f ? 0 : 1;
                    break;
                case "UltimateAscent":
                case "UltimateHold":
                    // Carry the sword up to its ready position with the ascent.
                    // Each of the three volleys owns its overhead lift and slam.
                    frame = 0;
                    break;
                case "UltimateCharge":
                case "UltimateCommand":
                case "UltimateFinish":
                    // Hold the third point through the detonation. Only a new
                    // group of falling swords starts another downstroke.
                    frame = 3;
                    break;
                case "Counter":
                    frame = SwordBodyReleased ? Mathf.Min(last, raised + Mathf.RoundToInt(SwordReleaseAge * KarmaBootstrap.FramesPerSecond))
                        : SwordBodyAge < KarmaBootstrap.Step ? 0 : 1;
                    break;
                default:
                    frame = SwordBodyReleased ? Mathf.Min(last, 1 + Mathf.RoundToInt(SwordReleaseAge * KarmaBootstrap.FramesPerSecond)) : 0;
                    break;
            }
            displayForm = motionForm; displayMotion = motion;
            DrawMotion(motionForm, drawing, frame * KarmaBootstrap.Step);
            RegisterSwordAppearance();
        }

        internal float SwordChargeBodyHeight => standingDamageBounds.size.y * Mathf.Abs(pictureScale.y * transform.lossyScale.y);
        // The new combat sheets reserve room around the idle-scale body for its full blade arc.
        const float CombatSheetPacking = 0.67f;
        static float SwordStandingHeight(string form) => form == "WaveWard" ? 2.79f : form == "WaveFinal" ? 2.77f : 2.68f;

        float SwordInheritedDrawingScale(Texture2D texture)
        {
            if (motionForm != "WaveFinal" || texture == null) return 1;
            // Final wears its own gear on the Rise ground strike and the Ward shield lift.
            if (texture.name == "GroundPlunge" && swordGroundArt != null)
            {
                // Fit the whole rigid pose to the final blade. Its painted guard is
                // exactly one blade length above the soles on the contact drawing.
                return swordGroundArt.bladeLength / ShieldLocalPoint(swordGroundArt.poses[4], swordGroundArt.sockets[4]).y;
            }
            string source = texture.name switch {
                "ShieldRaise" => "WaveWard",
                _ => null
            };
            return source == null ? 1 : SwordStandingHeight(motionForm) / SwordStandingHeight(source);
        }
        Texture2D registeredSwordTexture;
        float swordSheetScale = 1;
        internal void RegisterSwordAppearance()
        {
            RegisterSwordBasicArt();
            RegisterSwordCastArt();
            RegisterSwordGroundArt();
            RegisterSwordShieldRaise();
            if (picture == null) return;
            var texture = picture.sprite != null ? picture.sprite.texture : null;
            if (texture != registeredSwordTexture)
            {
                registeredSwordTexture = texture;
                swordSheetScale = texture != null && texture.name is
                    ("SWBasic" or "SWBasicRanged" or "SW01" or "SW02" or "SW03" or "SW04" or "SW05"
                    or "Guard" or "Windup" or "FollowThrough") ? 1 / CombatSheetPacking : 1;
                swordSheetScale *= SwordInheritedDrawingScale(texture);
            }
            float scale = IsVolleyDrawing(picture.sprite) ? volleyDrawingScale : swordSheetScale;
            picture.transform.localScale = Vector3.Scale(pictureScale, new Vector3(scale, scale, 1));
        }
        void ApplySwordBodyColor(Color color)
        {
            picture.color = color;
            ApplyDamageFlash();
        }
    }
}
