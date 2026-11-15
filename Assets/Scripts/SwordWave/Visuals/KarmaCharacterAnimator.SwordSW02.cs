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
            sw02Owner = GetComponentInParent<KarmaEnemy>();
            if (!poses.TryGetValue("Idle", out var idle)) return;
            // Load the own-form motion. Registration is keyed by the displayed
            // sprite, independent of which skill happens to select it.
            var loaded = Resources.LoadAll<Sprite>("SwordWave/Forms/" + idle.texture.name + "_Combat");
            string[] names = { "Prepare01", "Prepare02", "Swing01", "Swing02", "Recover01", "Recover02" };
            var frames = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                foreach (var sprite in loaded) if (sprite.name == names[i]) frames[i] = sprite;
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
                    frame = elapsed < sw02PhaseSeconds * 0.5f ? 0 : 1; break;
                case SW02VisualPhase.Locked:
                    // Two authored 24fps samples lead into the real release.
                    // The full strike cannot appear before the projectile event.
                    float swing = sw02PhaseSeconds - elapsed;
                    frame = swing > 2f / 24 ? 1 : 2; break;
                case SW02VisualPhase.Released:
                    frame = 3; break;
                default:
                    // Both recovery frames finish inside the existing minimum
                    // 0.38-second recovery, then retain the base ready picture.
                    if (elapsed >= 8f / 24) { picture.sprite = poses["Idle"]; return; }
                    frame = elapsed < 3f / 24 ? 4 : 5; break;
            }
            picture.sprite = sw02Frames[frame];
            SW02UsesIntermediateFrame = true;
        }

        // Absolute own-form Idle registration. Measure the body, not weapon
        // reach, wing tips, sprite rectangles or transparent source cells.
        float swordReferenceSole;
        internal Vector3 SwordVisualHome { get; private set; }
        internal Vector3 SwordAuthoredHome => pictureHome;
        internal Vector3 SwordFoot { get; private set; }
        internal string SwordBodyAction { get; private set; }
        internal int SwordStrikeIndex { get; private set; }
        bool SwordPreviousReleaseVisible => Time.time - swordPreviousRelease < 3f / 24;
        internal bool SwordBodyReleased => !float.IsNegativeInfinity(swordBodyReleasedAt) || SwordPreviousReleaseVisible;
        internal int SwordMotionStrike => SwordPreviousReleaseVisible ? swordPreviousStrike : SwordStrikeIndex;
        internal float SwordBodyAge => SwordFrameTime(Time.time - swordBodyAt);
        internal float SwordBodySmoothAge => Mathf.Max(0, Time.time - swordBodyAt);
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
        float swordRecoveryAt = float.NegativeInfinity;
        float swordPreviousRelease = float.NegativeInfinity;
        int swordPreviousStrike;
        internal static float SwordFrameTime(float seconds) => Mathf.Floor(Mathf.Max(0, seconds) * 24) / 24;

        void ConfigureSwordBodyCoordinates()
        {
            SwordVisualHome = pictureHome;
            if (!poses.TryGetValue("Idle", out var idle) || sw02Frames == null) return;
            // Body dimensions belong to the authored sheet and its fixed PPU.
            // Imported pivots put each supporting sole at the same origin.
            // Base Idle's sole is 8px above its rect bottom, pivot at 10px.
            // Inherited sheets put both sole and pivot 2px above the bottom.
            swordReferenceSole = idle.texture.name == "Wave" ? -2f / idle.pixelsPerUnit * pictureScale.y : 0;
        }

        internal void PrepareSwordBody(string action, float seconds, int strike = 0)
        {
            if (sheet != "Wave" || deathAt >= 0) return;
            if (action != "Summon" && action != "Counter") swordWalkAllowed = false;
            swordPreviousRelease = action == "Volley" && SwordBodyAction == "Volley" ? swordBodyReleasedAt : float.NegativeInfinity;
            swordPreviousStrike = SwordStrikeIndex;
            SwordBodyAction = action; SwordBodyDuration = seconds; SwordStrikeIndex = strike;
            swordBodyAt = Time.time; swordBodyReleasedAt = float.NegativeInfinity;
            swordRecoveryAt = float.NegativeInfinity;
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
            { if (BasicBodyActive) ApplyBasicBodyFrame(BasicBodyFrame()); else ApplySwordBodyFrame(strike % 2 == 0 ? 3 : 2); SwordBodyVisible = true; GuardianPhaseOverridesPose = true; }
        }
        void EndSwordBodyAction()
        {
            EndSW02PresentationAction();
            if (sw02Owner == null || !sw02Owner.Alive) { CancelSwordBodyPresentation(); return; }
            swordRecoveryAt = Time.time;
        }
        internal void CancelSwordBodyPresentation()
        {
            SwordBodyAction = null; swordBodyReleasedAt = swordRecoveryAt = float.NegativeInfinity;
            SwordBodyVisible = false; swordPreviousRelease = float.NegativeInfinity;
            wavePhase = null; waveReleasedUntil = 0; waveSwingAt = wavePhaseSwingAt = float.PositiveInfinity;
            basicPresentationActive = basicReleaseStarted = false;
            CancelSW02Presentation();
            SwordVisualHome = pictureHome; SwordFoot = Vector3.up * swordReferenceSole;
            if (picture != null && poses.ContainsKey("Idle"))
            { ShowPose("Idle"); RegisterSwordBodySprite(); GuardianPhaseOverridesPose = true; }
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
                return action == "Approach" && CurrentPose != "Move" && CurrentPose != "Backstep" && SwordBodyAge < 9f / 24;
            if (SwordBodyAction == "Counter")
                return CurrentPose != "Backstep" && (action == "Approach" || action == "Recover" || attack == EnemyAttack.SW04);
            return action == "Telegraph" || action == "Attack" || action == "Recover";
        }
        void ApplySwordBodyPresentation(string action, EnemyAttack attack)
        {
            SwordBodyVisible = false;
            if (action == "Stunned" || Time.time < hurtUntil)
            { SwordBodyAction = null; swordBodyReleasedAt = float.NegativeInfinity; RegisterSwordBodySprite(); return; }
            if (sw02Frames == null) { RegisterSwordBodySprite(); return; }
            if (attack == EnemyAttack.SW02 && SW02UsesIntermediateFrame) { SwordBodyVisible = true; RegisterSwordBodySprite(); return; }
            if (BasicBodyActive && attack == EnemyAttack.SWBasic && SwordBodyCanOverride(action, attack))
            {
                SwordBodyVisible = true;
                ApplyBasicBodyFrame(BasicBodyFrame());
                return;
            }
            int frame = -1;
            bool owns = SwordBodyCanOverride(action, attack);
            if (owns && SwordBodyAction != null)
            {
                SwordBodyVisible = true;
                float age = SwordBodyAge, release = SwordReleaseAge;
                if (action == "Recover" && SwordBodyAction != "Counter")
                {
                    float recovery = SwordFrameTime(Time.time - swordRecoveryAt);
                    frame = recovery < 3f / 24 && SwordBodyAction != "UltimateRecover" ? 4 : recovery < 8f / 24 ? 5 : -1;
                    if (frame < 0) picture.sprite = poses["Idle"];
                }
                else if (SwordBodyAction == "Volley" && Time.time - swordPreviousRelease < 3f / 24)
                    frame = swordPreviousStrike % 2 == 0 ? 3 : 2;
                else if (SwordBodyAction == "Summon") frame = age < 3f / 24 ? 1 : age < 5f / 24 ? 3 : 4;
                // Keep SW05's windup -> raised ascent -> aerial guard distinct.
                // Use the registered, own-form frames so the old phase poses
                // cannot undo the Idle size/palette correction.
                else if (SwordBodyAction == "UltimateStart") frame = age < 3f / 24 ? 0 : 1;
                else if (SwordBodyAction == "UltimateAscent") frame = 1;
                else if (SwordBodyAction == "UltimateHold") frame = 0;
                // Finish has already passed Recover01 before descent starts.
                else if (SwordBodyAction == "UltimateRecover") frame = 5;
                else if (SwordBodyReleased)
                {
                    float strike = SwordBodyAction == "BasicRanged" ? 2f / 24 : 3f / 24;
                    frame = release < strike ? (SwordStrikeIndex % 2 == 0 ? 3 : 2)
                        : release < 6f / 24 ? 4 : SwordBodyAction == "Counter" || SwordBodyAction == "UltimateCommand" ? 0 : 5;
                }
                else frame = SwordBodyAction == "BasicRanged" || SwordBodyAction == "Counter" ? 0
                    : age < Mathf.Min(3f / 24, SwordBodyDuration * 0.35f) ? 0 : 1;
            }
            else if (CurrentPose == "Guard" || CurrentPose == "Backstep") frame = 0;
            else if (CurrentPose == "FollowThrough") picture.sprite = poses["Idle"];
            if (frame >= 0) ApplySwordBodyFrame(frame); else RegisterSwordBodySprite();
        }
        void ApplySwordBodyFrame(int frame)
        {
            if (sw02Frames != null) picture.sprite = sw02Frames[frame];
            RegisterSwordBodySprite();
        }

        // Source-pixel calibration: crown-to-supporting-sole (not sword bounds),
        // then median wood RGB. Each row belongs to its own approved Idle form.
        // Combat 0..5, Basic LinkLow/ReturnCross/ReturnFinish 6..8.
        static readonly Vector4[,] swordBodySamples = {
            { new Vector4(359.0f,173.0f,136.0f,110.0f), new Vector4(353.0f,171.0f,135.0f,110.0f), new Vector4(359.0f,174.0f,137.0f,112.0f), new Vector4(356.0f,173.0f,137.0f,111.0f), new Vector4(328.0f,174.0f,137.0f,111.0f), new Vector4(340.0f,174.0f,137.0f,111.0f), new Vector4(510.0f,166.0f,131.0f,111.0f), new Vector4(510.0f,166.0f,132.0f,111.0f), new Vector4(510.0f,167.0f,132.0f,111.0f) },
            { new Vector4(373.0f,186.0f,138.0f,104.0f), new Vector4(373.0f,184.0f,136.0f,103.0f), new Vector4(368.0f,185.0f,137.0f,103.0f), new Vector4(367.0f,185.0f,137.0f,103.0f), new Vector4(366.0f,185.0f,136.0f,102.0f), new Vector4(367.0f,187.0f,138.0f,104.0f), new Vector4(655.0f,186.0f,140.0f,116.0f), new Vector4(652.0f,185.0f,139.0f,115.0f), new Vector4(651.0f,186.0f,140.0f,116.0f) },
            { new Vector4(367.0f,172.0f,134.0f,106.0f), new Vector4(366.0f,170.0f,132.0f,105.0f), new Vector4(367.0f,169.0f,131.0f,105.0f), new Vector4(368.0f,172.0f,134.0f,106.0f), new Vector4(362.0f,176.0f,136.0f,107.0f), new Vector4(367.0f,170.0f,132.0f,105.0f), new Vector4(632.0f,167.0f,129.0f,106.0f), new Vector4(632.0f,172.0f,131.0f,107.0f), new Vector4(633.0f,170.0f,131.0f,106.0f) },
            { new Vector4(388.0f,183.0f,130.0f,92.0f), new Vector4(382.0f,182.0f,129.0f,92.0f), new Vector4(375.0f,182.0f,130.0f,92.0f), new Vector4(373.0f,184.0f,130.0f,93.0f), new Vector4(366.0f,184.0f,131.0f,93.0f), new Vector4(375.0f,185.0f,133.0f,95.0f), new Vector4(629.0f,181.0f,128.0f,96.0f), new Vector4(627.0f,182.0f,128.0f,96.0f), new Vector4(626.0f,182.0f,128.0f,96.0f) }
        };
        static readonly float[] swordIdleHeights = {331,257,280,253};
        static readonly Vector3[] swordIdleWood = { new Vector3(178.0f,138.0f,106.0f), new Vector3(188.0f,146.0f,112.0f), new Vector3(174.0f,130.0f,94.0f), new Vector3(185.0f,139.0f,101.0f) };
        Material swordBaseMaterial, swordBodyMaterial;
        MaterialPropertyBlock swordBaseProperties, swordBodyProperties;

        static readonly int SwordWoodGain = Shader.PropertyToID("_WoodGain");
        internal float SwordChargeBodyHeight
        {
            get
            {
                if (!poses.TryGetValue("Idle", out var idle)) return 2;
                int form;
                switch (idle.texture.name)
                { case "WaveRise": form=1; break; case "WaveWard": form=2; break; case "WaveFinal": form=3; break; default: form=0; break; }
                return swordIdleHeights[form] / idle.pixelsPerUnit * Mathf.Abs(pictureScale.y * transform.lossyScale.y);
            }
        }
        void ConfigureSwordAppearance()
        {
            swordBaseMaterial = picture.sharedMaterial;
            // ConfigureSprites ran before the final-form spawn tint. Capture it
            // once as the animator baseline, including Idle and interruption.
            pictureColor = picture.color;
            swordBaseProperties = new MaterialPropertyBlock(); picture.GetPropertyBlock(swordBaseProperties);
            swordBodyProperties = new MaterialPropertyBlock(); picture.GetPropertyBlock(swordBodyProperties);
            swordBodyMaterial = Resources.Load<Material>("SwordWave/Shared/SwordBodyConsistent");
        }
        internal void RegisterSwordAppearance()
        {
            if (swordBaseProperties == null || picture == null) return;
            // Absolute assignment: never compound a previous pose correction.
            picture.transform.localScale = pictureScale;
            int form = -1, frame = -1;
            if (poses.TryGetValue("Idle", out var idle))
            {
                switch (idle.texture.name)
                { case "Wave": form=0; break; case "WaveRise": form=1; break; case "WaveWard": form=2; break; case "WaveFinal": form=3; break; }
                if (sw02Frames != null) frame = System.Array.IndexOf(sw02Frames, picture.sprite);
                if (frame < 0 && basicReturnFrames != null)
                { int index = System.Array.IndexOf(basicReturnFrames, picture.sprite); if (index >= 0) frame = index + 6; }
                if (form >= 0 && frame >= 0 && swordBodyMaterial != null)
                {
                    Vector4 sample = swordBodySamples[form,frame];
                    float referenceHeight = swordIdleHeights[form] / idle.pixelsPerUnit;
                    // Calibrate each authored sheet ONCE against its ready pose.
                    // A lower crown in a bent-knee cut is motion, not a smaller
                    // body. Normalising every pose's height enlarged the torso
                    // by up to 359/328 before shrinking again on return to Idle.
                    int referenceFrame = frame < 6 ? 0 : 6;
                    Sprite referenceSprite = frame < 6 ? sw02Frames[0] : basicReturnFrames[0];
                    float sheetBodyHeight = swordBodySamples[form,referenceFrame].x / referenceSprite.pixelsPerUnit;
                    picture.transform.localScale = pictureScale * (referenceHeight / sheetBodyHeight);
                    Vector3 reference = swordIdleWood[form];
                    swordBodyProperties.SetVector(SwordWoodGain, new Vector4(reference.x/sample.y, reference.y/sample.z, reference.z/sample.w, 1));
                    picture.sharedMaterial = swordBodyMaterial; picture.SetPropertyBlock(swordBodyProperties);
                    return;
                }
            }
            if (picture.sharedMaterial == swordBodyMaterial)
            { picture.sharedMaterial = swordBaseMaterial; picture.SetPropertyBlock(swordBaseProperties); }
        }

        void ApplySwordBodyColor(Color color)
        {
            picture.color = color;
        }
        void RegisterSwordBodySprite()
        {
            SwordVisualHome = pictureHome;
            SwordFoot = Vector3.up * swordReferenceSole;
            if (picture == null || picture.sprite == null) return;
            int frame = sw02Frames == null ? -1 : System.Array.IndexOf(sw02Frames, picture.sprite);
            if (frame >= 0)
            {
                SwordFoot = Vector3.zero;
                SwordVisualHome += Vector3.up * swordReferenceSole;
            }
            else if (poses.TryGetValue("Idle", out var idle) && picture.sprite.texture == idle.texture)
            {
                // Measured sole coordinates (pixels from rect bottom). These
                // are ground anchors, not per-pose size normalisation.
                float solePixel = 2;
                if (idle.texture.name == "Wave") solePixel = picture.sprite.name == "Move" ? 6 : picture.sprite.name == "Hurt" ? 9 : 8;
                else if (idle.texture.name == "WaveWard" && picture.sprite.name == "Move") solePixel = 1;
                else if (idle.texture.name == "WaveRise" && picture.sprite.name == "Hurt") solePixel = 1;
                float sole = (solePixel - picture.sprite.pivot.y) / picture.sprite.pixelsPerUnit * pictureScale.y;
                SwordFoot = Vector3.up * sole;
                SwordVisualHome += Vector3.up * (swordReferenceSole - sole);
            }
            picture.transform.localPosition = SwordVisualHome;
            RegisterSwordAppearance();
            ApplySwordBodyColor(Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor);
        }
    }
}
