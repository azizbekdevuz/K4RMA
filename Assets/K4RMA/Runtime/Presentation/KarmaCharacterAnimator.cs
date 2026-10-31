using UnityEngine;
using System.Collections.Generic;
namespace KarmaPrototype
{
    public sealed class KarmaCharacterAnimator : MonoBehaviour
    {
        Transform blade, leftBoot, rightBoot;
        SpriteRenderer picture;
        Sprite[] frames;
        Sprite[] runFrames;
        float runAt;
        Vector2 slashDirection = Vector2.right;
        Vector3 pictureHome;
        Color pictureColor;
        float actionUntil, hurtUntil, deathAt = -1;
        readonly Dictionary<string, Sprite> poses = new Dictionary<string, Sprite>();
        string sheet, explicitPose;
        float poseUntil, landedUntil;
        float waveSwingAt = float.PositiveInfinity;
        string wavePhase;
        float wavePhaseSwingAt = float.PositiveInfinity, waveReleasedUntil;
        bool wasAirborne;
        Vector3 guardianPreviousPosition;
        Sprite guardianAppearance;
        Sprite waveAppearance;
        Vector3 waveAppearanceScale;
        internal bool WaveFootAnchored { get { return waveAppearance != null; } }
        Vector3 pictureScale;
        float appearanceScale = 1;
        public string CurrentPose { get; private set; }
        // Preserve the pre-motion pose's incoming-hit bounds when secondary art
        // phases use a different image. These are not attack/collider settings.
        internal Sprite GuardianDamageSprite { get; private set; }
        internal Vector3 GuardianDamageScale { get; private set; }
        internal bool GuardianPhaseOverridesPose { get; private set; }
        // A release pulse, not a queued-pose revision. Scheduling the next shot
        // while the previous release is visible must not restart that motion.
        internal int WaveMotionRevision { get; private set; }
        public void ConfigureSheet(string name)
        {
            sheet = name; poses.Clear();
            runFrames = null;
            guardianPreviousPosition = transform.position;
            var library = KarmaArtwork.Settings.spriteLibrary;
            if (library == null) return;
            foreach (var sprite in library.All(name)) if (sprite != null) poses[sprite.name] = sprite;
            if (name == "Player")
            {
                var crouch = Resources.Load<Sprite>("K4RMAArtwork/PlayerCrouchV2");
                if (crouch != null) poses["Crouch"] = crouch;
            }
            if (name == "Player" && poses.TryGetValue("Run", out var run))
            {
                // The second movement key pose is also used for jump takeoff in
                // the original sheet. Prefer a dedicated Run2 when supplied.
                if (!poses.TryGetValue("Run2", out var next)) next = KarmaArtwork.Settings.playerMove2;
                runFrames = next != null && next != run ? new[] { run, next } : new[] { run };
            }
        }
        public void PlayPose(string name, float seconds)
        {
            explicitPose = name; poseUntil = Time.time + seconds;
            waveSwingAt = float.PositiveInfinity;
            // GuardianPose resolves Wave reservations after hurt/stun/death priority.
            // Player and other sheets retain their immediate pose behavior.
            if (sheet != "Wave") ShowPose(name);
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
        void ShowPose(string name)
        {
            if (picture == null || !poses.TryGetValue(name, out var sprite)) return;
            picture.sprite = sprite; CurrentPose = name;
            picture.transform.localScale = pictureScale;
            GuardianDamageSprite = sprite; GuardianDamageScale = pictureScale;
            GuardianPhaseOverridesPose = false;
            ApplyWaveAppearance();
        }
        public void Initialize()
        {
            blade = transform.Find("Katana"); leftBoot = transform.Find("Boot L"); rightBoot = transform.Find("Boot R");
        }
        public void ConfigureSprites(SpriteRenderer renderer, Sprite[] poses)
        {
            picture = renderer; frames = poses; picture.sprite = frames[0];
            pictureHome = picture.transform.localPosition; pictureColor = picture.color;
            pictureScale = picture.transform.localScale;
        }
        public void SetGuardianAppearance(Sprite sprite)
        {
            if (sprite == null || picture == null) return;
            guardianAppearance = sprite;
            appearanceScale = picture.sprite.bounds.size.y / Mathf.Max(0.01f, sprite.bounds.size.y);
        }
        public void SetWaveAppearance(Sprite sprite)
        {
            if (sprite == null || picture == null) return;
            GuardianDamageSprite = picture.sprite;
            GuardianDamageScale = picture.transform.localScale;
            waveAppearance = sprite;
            // All four imports use their actual sole as pivot. Match the old
            // body's height above that anchor, excluding the transparent foot margin.
            float height = Mathf.Max(0.01f, sprite.bounds.max.y);
            waveAppearanceScale = pictureScale * (picture.sprite.bounds.max.y / height);
            ApplyWaveAppearance();
        }
        void ApplyWaveAppearance()
        {
            if (waveAppearance == null || picture == null) return;
            picture.sprite = waveAppearance;
            picture.transform.localScale = waveAppearanceScale;
            GuardianPhaseOverridesPose = true;
        }
        public void Attack() { Attack(Vector2.right); }
        public void Attack(Vector2 direction)
        {
            slashDirection = direction; actionUntil = Time.time + 0.18f;
            WaveMotionRevision++;
            if (sheet == "Wave") { poseUntil = 0; waveSwingAt = float.PositiveInfinity; }
        }
        public void Hurt() { hurtUntil = Time.time + 0.16f; }
        public void Die() { deathAt = Time.time; }
        public void Pose(float movement, bool airborne, float verticalSpeed = 0, bool crouching = false)
        {
            if (deathAt >= 0)
            {
                if (poses.ContainsKey("Death")) ShowPose("Death");
                else if (picture != null && frames.Length >= 9) picture.sprite = frames[8];
                else transform.localRotation = Quaternion.Euler(0,0,Mathf.Min(85,(Time.time-deathAt)*200));
                return;
            }
            if (picture != null)
            {
                int frame = 0;
                if (frames.Length > 1)
                {
                    if (Time.time < hurtUntil && frames.Length >= 9) frame = 7;
                    else if (Time.time < actionUntil)
                        frame = slashDirection.y < -0.25f ? 6 : slashDirection.y > 0.25f ? 5 : actionUntil - Time.time > 0.09f ? 3 : 4;
                    else if (airborne) frame = 1;
                    else if (Mathf.Abs(movement) > 0.1f) frame = (int)(Time.time * 9) % 2 == 0 ? 1 : 2;
                }
                picture.sprite = frames[frame];
                if (sheet == "Player" && poses.Count > 0)
                {
                    if (wasAirborne && !airborne) landedUntil = Time.time + 0.1f;
                    wasAirborne = airborne;
                    string pose = Time.time < hurtUntil ? "Hurt"
                        : Time.time < poseUntil ? (explicitPose == "Pierce" && poseUntil - Time.time > 0.15f ? "Dash" : explicitPose)
                        : Time.time < actionUntil ? (slashDirection.y < -0.25f ? "AirSlash"
                            : slashDirection.y > 0.25f ? "Rising"
                            : actionUntil - Time.time > 0.13f ? "Windup"
                            : actionUntil - Time.time > 0.07f ? "Slash" : "FollowThrough")
                        : actionUntil > 0 && Time.time < actionUntil + 0.08f ? "Recovery"
                        : airborne ? (verticalSpeed > 2 ? "Jump" : verticalSpeed < -2 ? "Fall" : "Apex")
                        : crouching ? "Crouch"
                        : Time.time < landedUntil ? "Land"
                        : Mathf.Abs(movement) > 0.1f ? "Run" : "Idle";
                    if (pose == "Run" && runFrames != null)
                    {
                        if (CurrentPose != "Run") runAt = Time.time;
                        ShowPose("Run");
                        picture.sprite = runFrames[(int)((Time.time - runAt) * 9) % runFrames.Length];
                    }
                    else ShowPose(pose);
                }
                float bob = airborne || crouching ? 0 : Mathf.Abs(movement) > 0.1f ? Mathf.Sin(Time.time * 18) * 0.025f : Mathf.Sin(Time.time * 2) * 0.008f;
                picture.transform.localPosition = pictureHome + Vector3.up * bob;
                picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
                transform.localRotation = Quaternion.Euler(0, 0, Time.time < hurtUntil ? -8 : 0);
                return;
            }
            float step = airborne ? 12 : Mathf.Abs(movement) > 0.1f ? Mathf.Sin(Time.time * 13) * 18 : Mathf.Sin(Time.time * 2) * 2;
            if (leftBoot != null) leftBoot.localRotation = Quaternion.Euler(0,0,step);
            if (rightBoot != null) rightBoot.localRotation = Quaternion.Euler(0,0,-step);
            if (blade != null) blade.localRotation = Quaternion.Euler(0,0,Time.time < actionUntil ? -65 + (actionUntil-Time.time)*650 : airborne ? 30 : 15);
            transform.localRotation = Quaternion.Euler(0,0,Time.time < hurtUntil ? -12 : 0);
        }
        // Spacing is applied after GuardianPose in the enemy's Update. Refresh
        // its picture in that same frame without recomputing incoming-hit bounds.
        public void ShowGuardianSpacing()
        {
            if (sheet != "Wave" || picture == null || deathAt >= 0 || Time.time < hurtUntil
                || !poses.ContainsKey("Move")) return;
            Sprite damageSprite = GuardianDamageSprite;
            Vector3 damageScale = GuardianDamageScale;
            ShowPose("Move");
            GuardianDamageSprite = damageSprite;
            GuardianDamageScale = damageScale;
            GuardianPhaseOverridesPose = true;
        }
        public void GuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            // Poses follow the actual combat state, including long attacks and recovery.
            if (picture == null || deathAt >= 0) return;
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
            else if (action == "Stunned") pose = sheet == "Neutral" ? "Kneel" : sheet == "Ward" ? "Crouch" : "Hurt";
            else if (action == "Recover") pose = sheet == "Ward" ? "Recover" : sheet == "Rise" ? "Landing" : sheet == "Wave" ? "FollowThrough" : "Recovery";
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
                if (sheet == "Rise") pose = airborne ? "AirSlash" : attack == EnemyAttack.UP01 || attack == EnemyAttack.UP03 ? "Rising" : "Slash";
                else if (sheet == "Ward") pose = attack == EnemyAttack.GD02 ? "ShieldBash" : attack == EnemyAttack.GD03 ? "Thrust" : "Windup";
                else if (sheet == "Wave") pose = attack == EnemyAttack.SW01 || attack == EnemyAttack.SW02 ? "Wave" : airborne ? "Rising" : "Slash";
                else pose = "Slash";
            }
            else pose = armored && poses.ContainsKey("Guard") ? "Guard"
                : (transform.position - guardianPreviousPosition).sqrMagnitude > 0.00001f
                    ? sheet == "Rise" ? "Dash" : "Move" : "Idle";
            if (!poses.ContainsKey(pose)) pose = "Idle";
            // Compute the same pose-dependent damage silhouette as before this
            // integration, before selecting the additional presentation phases.
            Sprite damageSprite = poses.TryGetValue(pose, out var original) ? original : picture.sprite;
            bool damageComposed = guardianAppearance != null && (pose == "Idle" || pose == "Guard");
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
            ShowPose(pose);
            // Inherited idle artwork remains intact. During a Wave attack its
            // single Guard key pose must stay on the same sheet/scale as the
            // following Windup/Slash, avoiding a body swap between each shot.
            bool attackGuard = sheet == "Wave" && pose == "Guard" && (waveAction || phaseAction);
            bool composed = guardianAppearance != null && (pose == "Idle" || pose == "Guard") && !attackGuard;
            if (composed) picture.sprite = guardianAppearance;
            picture.transform.localScale = pictureScale * (composed ? appearanceScale : 1);
            if (sheet == "Wave")
            {
                GuardianDamageSprite = damageSprite; GuardianDamageScale = damageScale;
                GuardianPhaseOverridesPose = phaseOverridesPose || picture.sprite != damageSprite
                    || picture.transform.localScale != damageScale;
            }
            // Keep the selected single-pose cutout through every phase while
            // retaining logical poses, transform motion and the old hit silhouette.
            ApplyWaveAppearance();
            guardianPreviousPosition = transform.position;
            picture.transform.localPosition = pictureHome;
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
        }
        void Update() { if (deathAt >= 0) Pose(0,false); }
    }
}
