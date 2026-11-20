using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        internal Sprite GuardianDamageSprite { get; private set; }
        internal Vector3 GuardianDamageScale { get; private set; }
        internal bool GuardianPhaseOverridesPose { get; private set; }
        float waveSwingAt = float.PositiveInfinity;
        string wavePhase;
        float wavePhaseSwingAt = float.PositiveInfinity, waveReleasedUntil;
        internal int WaveMotionRevision { get; private set; }
        Dictionary<string, Sprite> swordDamagePoses;
        Sprite swordDamageFallback;
        bool swordPresentationConfigured;
        internal const float BasicSwingSeconds = 3f / 24;
        internal int BasicStrikeIndex { get; private set; }
        float basicReleasedAt = float.NegativeInfinity;
        bool basicPresentationActive, basicReleaseStarted;
        internal bool RangedBasicBody { get; set; }
        string SwordBodyMotion(string action, EnemyAttack attack, bool moved)
        {
            if (CurrentPose == "Backstep") return "Backstep";
            if (action == "Attack" || action == "Telegraph" && CurrentPose != "Windup" && CurrentPose != "Guard")
                return attack == EnemyAttack.SWBasic && RangedBasicBody ? "SWBasicRanged" : attack.ToString();
            if (action == "Recover") return "FollowThrough";
            if (action == "Telegraph") return "Windup";
            return moved || CurrentPose == "Move" ? "Move" : CurrentPose == "Guard" ? "Guard" : "Idle";
        }
        float basicStrikeSeconds = BasicSwingSeconds;
        internal bool BasicBodyActive => basicPresentationActive &&
            (SwordBodyAction == "BasicMelee" || SwordBodyAction == "BasicRanged");
        internal float BasicReleaseAge => Mathf.Max(0, Time.time - basicReleasedAt);
        public void PrepareBasicAttack(float warningSeconds, bool melee = false)
        {
            // Keep the original damage-pose policy and warning clock. Only the
            // Basic display below holds the loaded sword until the real event.
            PrepareWaveAttack(warningSeconds, false);
            basicPresentationActive = true; basicReleaseStarted = false;
            BasicStrikeIndex = 0; basicLinkAt = float.NegativeInfinity;
            PrepareSwordBody(melee ? "BasicMelee" : "BasicRanged", warningSeconds);
        }
        public void BeginBasicRelease(float strikeSeconds = BasicSwingSeconds, int strike = 0)
        {
            // A cancelled/replaced preparation cannot regain presentation ownership.
            if (!BasicBodyActive || deathAt >= 0 || Time.time < hurtUntil) return;
            basicStrikeSeconds = Mathf.Max(0, strikeSeconds);
            BasicStrikeIndex = strike;
            basicReleasedAt = Time.time; basicReleaseStarted = basicPresentationActive = true;
            WaveMotionRevision++;
            ReleaseSwordBody(SwordBodyAction, strike);
            // Coroutines can release after GuardianPose's Update. Show this
            // event now, preserving the damage silhouette already saved there.
            if (sheet == "Wave" && deathAt < 0 && Time.time >= hurtUntil && poses.ContainsKey("Slash"))
            { ShowPose("Slash"); ApplySwordBodyPresentation("Attack", EnemyAttack.SWBasic); GuardianPhaseOverridesPose = true; }
        }
        internal void ConfigureSwordPresentation()
        {
            if (sheet != "Wave" || swordPresentationConfigured) return;
            swordPresentationConfigured = true;
            // ConfigureSheet reads the common library, which has no base Wave
            // sheet. Keep its original damage policy BEFORE filling the display
            // poses; inherited forms already supplied their own sixteen poses.
            swordDamagePoses = new Dictionary<string, Sprite>(poses);
            swordDamageFallback = KarmaBodyPoses.Get("BossAppearance", "Wave");
            GuardianDamageSprite = picture.sprite; GuardianDamageScale = pictureScale;
            foreach (var sprite in KarmaArtwork.Sprites("Wave"))
                if (sprite != null && !poses.ContainsKey(sprite.name)) poses.Add(sprite.name, sprite);
            ConfigureSW02Frames();
            ConfigureSwordLocomotion();
            guardianPose = SwordGuardianPose;
            deferExplicitPose = true;
            onPoseRequested = () => waveSwingAt = float.PositiveInfinity;
            onActionEnded = EndSwordBodyAction;
        }
        public void PrepareWaveAttack(float warningSeconds, bool aimed)
        {
            PlayPose(aimed ? "Guard" : "Windup", warningSeconds);
            // This is a visual lead-in inside the existing warning, not an attack delay.
            waveSwingAt = poseUntil - Mathf.Min(0.12f, warningSeconds * 0.3f);
        }
        public void PlayWavePhase(string name, float warningSeconds = 0)
        {
            if (sheet != "Wave") return;
            wavePhase = name;
            wavePhaseSwingAt = warningSeconds > 0
                ? Time.time + warningSeconds - Mathf.Min(0.12f, warningSeconds * 0.3f)
                : float.PositiveInfinity;
        }
        public void ReleaseWavePhase()
        {
            if (sheet != "Wave") return;
            waveReleasedUntil = Time.time + 0.18f;
            WaveMotionRevision++;
        }
        public void ShowGuardianSpacing()
        {
            if (sheet != "Wave" || picture == null || deathAt >= 0 || Time.time < hurtUntil
                || !poses.ContainsKey("Move")) return;
            Sprite damageSprite = GuardianDamageSprite;
            Vector3 damageScale = GuardianDamageScale;
            ShowPose("Move");
            swordWalkAllowed = true;
            PlayBody("Move", loop: true);
            GuardianDamageSprite = damageSprite;
            GuardianDamageScale = damageScale;
            GuardianPhaseOverridesPose = true;
        }
        // A braced electrical display keeps the host's Hurt damage silhouette.
        // Only SW04's real parry stun requests this presentation.
        internal void ShowSwordElectrocutionPose()
        {
            if (picture == null || deathAt >= 0 || sheet != "Wave") return;
            ShowPose(poses.ContainsKey("Guard") ? "Guard" : "Hurt");
            PlayBody("Stun", loop: true);
            GuardianPhaseOverridesPose = true;
        }
        void SwordGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            swordWalkAllowed = (action == "Approach" || action == "Idle") && !airborne;
            // Poses follow the actual combat state, including long attacks and recovery.
            if (picture == null || deathAt >= 0) return;
            if (EmpowermentPose()) return;
            ApplySwordBodyColor(action == "Stunned" ? new Color(0.52f, 0.72f, 0.86f, pictureColor.a)
                : Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor);
            transform.localRotation = Quaternion.identity;
            bool basicWave = sheet == "Wave" && (attack == EnemyAttack.SWBasic || attack == EnemyAttack.SW01 || attack == EnemyAttack.SW02);
            bool waveAction = basicWave && (action == "Telegraph" || action == "Attack");
            bool phaseAction = sheet == "Wave" && (attack == EnemyAttack.SW03 || attack == EnemyAttack.SW04 || attack == EnemyAttack.SW05)
                && (action == "Telegraph" || action == "Attack");
            if (!phaseAction) { wavePhase = null; wavePhaseSwingAt = float.PositiveInfinity; waveReleasedUntil = 0; }
            if (sheet == "Wave" && !waveAction)
            {
                poseUntil = 0; waveSwingAt = float.PositiveInfinity;
            }
            string pose;
            if (Time.time < hurtUntil) pose = "Hurt";
            else if (action == "Stunned") pose = "Hurt";
            else if (action == "Recover") pose = "FollowThrough";
            else if (waveAction)
            {
                // A second warning can begin in the launch frame. Finish showing the
                // preceding release before starting its next windup, without extra waits.
                // Hold the prepared swing until Attack(), even if Wait resumes next frame.
                pose = Time.time < actionUntil ? "Wave"
                    : (Time.time < poseUntil || Time.time >= waveSwingAt) ? (Time.time >= waveSwingAt ? "Slash" : explicitPose)
                    : action == "Telegraph" ? "Windup" : "Guard";
            }
            else if (action == "Telegraph") pose = armored && poses.ContainsKey("Guard") ? "Guard" : "Windup";
            else if (action == "Attack")
            {
                pose = attack == EnemyAttack.SW01 || attack == EnemyAttack.SW02 ? "Wave" : airborne ? "Rising" : "Slash";
            }
            else pose = armored && poses.ContainsKey("Guard") ? "Guard"
                : (transform.position - guardianPreviousPosition).sqrMagnitude > 0.00001f
                    ? "Move" : "Idle";
            if (!poses.ContainsKey(pose)) pose = "Idle";
            // Compute the pre-loading damage silhouette before selecting the
            // additional presentation phases.
            // Base Wave previously kept its initial Idle cutout in every state.
            // The displayed sprite can now change, so it must never become the
            // fallback for incoming damage. Inherited pose bounds stay intact.
            string damagePose = swordDamagePoses.ContainsKey(pose) ? pose : "Idle";
            Sprite damageSprite = swordDamagePoses.TryGetValue(damagePose, out var original) ? original : swordDamageFallback;
            bool damageComposed = guardianAppearance != null && (damagePose == "Idle" || damagePose == "Guard");
            if (damageComposed) damageSprite = guardianAppearance;
            Vector3 damageScale = pictureScale * (damageComposed ? appearanceScale : 1);
            bool phaseOverridesPose = false;
            if (phaseAction && Time.time >= hurtUntil && wavePhase != null)
            {
                string phasePose = Time.time < waveReleasedUntil ? "Wave"
                    : Time.time >= wavePhaseSwingAt ? "Slash" : wavePhase;
                if (poses.ContainsKey(phasePose)) { phaseOverridesPose = phasePose != pose; pose = phasePose; }
            }
            // A live sword guard can accompany ordinary spacing. Show that
            // measured movement while retaining the Guard damage pose above.
            // Locked warnings, attacks and recovery keep their existing poses.
            if (sheet == "Wave" && action == "Approach" && Time.time >= hurtUntil
                && (transform.position - guardianPreviousPosition).sqrMagnitude > 0.00001f
                && poses.ContainsKey("Move"))
            {
                phaseOverridesPose |= pose != "Move";
                pose = "Move";
            }
            // Basic owns one display sequence: loaded windup -> event-aligned
            // slash -> follow-through. Latch through recovery instead of
            // expiring into the common Slash/Guard pose for one extra swing.
            // The original incoming-damage policy was saved above, unchanged.
            bool basicAction = attack == EnemyAttack.SWBasic
                && (action == "Telegraph" || action == "Attack" || action == "Recover");
            if (!basicAction) basicPresentationActive = basicReleaseStarted = false;
            if (basicAction && basicPresentationActive && Time.time >= hurtUntil)
            {
                string basicPose = action == "Telegraph" ? "Windup"
                    : action == "Attack" && basicReleaseStarted && BasicReleaseAge < basicStrikeSeconds
                        ? "Slash" : "FollowThrough";
                if (poses.ContainsKey(basicPose)) { phaseOverridesPose |= pose != basicPose; pose = basicPose; }
            }
            ShowPose(pose);
            // Inherited idle artwork remains intact. During a Wave attack its
            // single Guard key pose must stay on the same sheet/scale as the
            // following Windup/Slash, avoiding a body swap between each shot.
            bool attackGuard = sheet == "Wave" && pose == "Guard" && (waveAction || phaseAction);
            bool composed = guardianAppearance != null && (pose == "Idle" || pose == "Guard") && !attackGuard;
            if (composed) picture.sprite = guardianAppearance;
            picture.transform.localScale = pictureScale * (composed ? appearanceScale : 1);
            ApplySW02Presentation(action, attack);
            ApplySwordBodyPresentation(action, attack);
            if (sheet == "Wave")
            {
                GuardianDamageSprite = damageSprite; GuardianDamageScale = damageScale;
                GuardianPhaseOverridesPose = true; // Body registration never changes the saved damage silhouette.
            }
            // Secondary motion consumes this display pose; damage continues to
            // use the separately saved sprite, scale and authored foot transform.
            guardianPreviousPosition = transform.position;
            picture.transform.localPosition = SwordVisualHome;
            ApplySwordBodyColor(action == "Stunned" ? new Color(0.52f, 0.72f, 0.86f, pictureColor.a)
                : Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor);
            transform.localRotation = Quaternion.identity;
        }

        internal void SampleSwordBasicContactPose()
        {
            if (!BasicBodyActive || !SwordBodyVisible || deathAt >= 0 || Time.time < hurtUntil
                || sw02Owner == null || sw02Owner.Stunned
                || sw02Owner.CurrentPattern != (int)EnemyAttack.SWBasic) return;
            ShowSwordPatternMotion(EnemyAttack.SWBasic);
        }
    }
}
