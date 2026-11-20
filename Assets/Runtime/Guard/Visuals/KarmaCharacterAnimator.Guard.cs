using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        KarmaHeavyStrikeMotion heavyStrike;
        Sprite guardStagger;
        Vector3 staggerScale;
        Sprite guardDamageSprite;
        Vector3 guardDamageScale;
        bool guardDamageOverride;
        SpriteRenderer guardThrownShield, guardTrialShield;
        Sprite guardTrialIdle, guardTrialMove, guardTrialStun;
        bool GuardTrialShieldAway { get { return guardTrialShield != null && guardTrialShield.gameObject.activeInHierarchy; } }
        Sprite guardThrownRelease, guardThrownHurt;
        string guardFlightPose;
        Sprite[] groundSlam;
        int groundSlamFrame = -1;
        float autoShieldArtUntil, catchArtUntil, guardStunBegan;
        bool wasGuardStunned;
        internal void ShowAutoShield(float seconds) { autoShieldArtUntil = Time.time + seconds; }
        internal void CatchGuardShield() { catchArtUntil = Time.time + 3 * KarmaBootstrap.Step; }
        string GuardBodyMotion(string action, EnemyAttack attack, bool moved, ref string form)
        {
            bool away = GuardTrialShieldAway || guardThrownShield != null && guardThrownShield.gameObject.activeInHierarchy;
            if (away && form == "WardFinal") form = "WardFinal_NoShield";
            bool bare = form == "WardFinal_NoShield";
            bool stunned = action == "Stunned";
            if (stunned && !wasGuardStunned) guardStunBegan = Time.time;
            wasGuardStunned = stunned;
            if (stunned) return bare || Time.time - guardStunBegan < 3 * KarmaBootstrap.Step ? "Stagger" : "Stun";
            if (Time.time < hurtUntil) return away && form == "WardWave" ? "GD03ShieldAwayHurt" : "Hurt";
            if (bare) return CurrentPose != null && CurrentPose.Contains("GD02") ? "GD05Heavy"
                : attack == EnemyAttack.GD03 ? "GD03Release" : moved ? "Move" : "Idle";
            if (Time.time < catchArtUntil) return "GD03Catch";
            if (Time.time < autoShieldArtUntil && action == "Approach") return "GD01";
            if (action == "Attack" || attack == EnemyAttack.GD02 && CurrentPose != null && CurrentPose.StartsWith("GD02/"))
                return attack == EnemyAttack.GroundStrike ? "GDBasic" : attack.ToString();
            if (attack == EnemyAttack.GD04 && groundSlamFrame >= 0) return "GD04";
            if (action == "Telegraph") return attack == EnemyAttack.GD05 ? "GD05" : "Windup";
            if (action == "Recover") return "Recover";
            return moved || CurrentPose == "Move" ? "Move" : CurrentPose == "Guard" ? "Guard" : "Idle";
        }
        public void ConfigureHeavyStrike(IReadOnlyList<Essence> order)
        {
            // Boss-local libraries also provide the base Ward poses after asset separation.
            // Preserve any inherited poses already selected for this body.
            foreach (var sprite in KarmaArtwork.Sprites("Ward"))
                if (sprite != null && !poses.ContainsKey(sprite.name)) poses[sprite.name] = sprite;
            guardianPose = GuardGuardianPose;
            onActionEnded = EndHeavyStrike;
            heavyStrike = new KarmaHeavyStrikeMotion(picture, pictureHome, pictureScale, poses["Idle"], order);
            string shieldlessSheet = "GuardShieldless" + heavyStrike.FormKey;
            guardThrownRelease = KarmaArtwork.Sprite(shieldlessSheet, "ShieldBash");
            guardThrownHurt = KarmaArtwork.Sprite(shieldlessSheet, "Hurt");
            if (heavyStrike.FormKey == "WardFinal")
            {
                guardTrialIdle = KarmaArtwork.Sprite(shieldlessSheet, "Idle");
                guardTrialMove = KarmaArtwork.Sprite(shieldlessSheet, "Move");
                guardTrialStun = KarmaArtwork.Sprite(shieldlessSheet, "Crouch");
            }
            Sprite reference = null;
            foreach (var sprite in KarmaArtwork.Sprites("GuardStagger" + heavyStrike.FormKey))
            {
                if (sprite.name == "Stagger") guardStagger = sprite;
                else if (sprite.name == "Reference") reference = sprite;
            }
            // Calibrate with a standing figure drawn at the same anatomical scale.
            // Using crouched height here would inflate the boss during every parry.
            if (guardStagger == null || reference == null)
                throw new System.InvalidOperationException("경직 스프라이트가 없습니다: " + heavyStrike.FormKey);
            staggerScale = pictureScale * (DisplayDamageLocalBounds(picture).size.y / KarmaBodyPoses.HitBounds(reference).size.y);
            string slamSheet = "GuardSlam" + heavyStrike.FormKey;
            groundSlam = new[] { KarmaArtwork.Sprite(slamSheet, "Windup"), KarmaArtwork.Sprite(slamSheet, "Descent"),
                KarmaArtwork.Sprite(slamSheet, "Contact"), KarmaArtwork.Sprite(slamSheet, "Rebound") };
        }
        void GuardGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            guardDamageOverride = false;
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
            if (action == "Stunned" && guardStagger != null)
            {
                picture.sprite = guardStagger;
                picture.transform.localPosition = pictureHome;
                picture.transform.localScale = staggerScale;
                picture.transform.localRotation = Quaternion.identity;
                picture.color = new Color(pictureColor.r * 0.82f, pictureColor.g * 0.82f, pictureColor.b * 0.82f, pictureColor.a);
                if (GuardTrialShieldAway)
                {
                    guardDamageSprite = guardStagger; guardDamageScale = staggerScale; guardDamageOverride = true;
                    picture.sprite = guardTrialStun;
                    picture.transform.localScale = pictureScale;
                }
                CurrentPose = (GuardTrialShieldAway ? "GD05/ShieldAway/Stagger/" : "Stagger/") + heavyStrike.FormKey;
                guardianPreviousPosition = transform.position;
                return;
            }
            if (heavyStrike != null)
            {
                heavyStrike.ShieldAway = GuardTrialShieldAway;
                if (heavyStrike.Show(attack))
                {
                    guardDamageSprite = heavyStrike.DamageSprite;
                    guardDamageScale = heavyStrike.DamageScale; guardDamageOverride = true;
                    CurrentPose = (GuardTrialShieldAway ? "GD05/ShieldAway/" : "") + heavyStrike.CurrentPose;
                    return;
                }
            }
            if (attack == EnemyAttack.GD04 && ShowGroundSlam()) return;
            string pose = Time.time < hurtUntil ? "Hurt"
                : action == "Stunned" ? "Crouch"
                : action == "Recover" ? "Recover"
                : action == "Attack" ? attack == EnemyAttack.GD02 ? "ShieldBash" : (attack == EnemyAttack.GroundStrike || attack == EnemyAttack.GD03) ? "Thrust" : "Windup"
                : DefaultGuardianPose(action, airborne, armored);
            if (action == "Recover" && Time.time >= hurtUntil)
            {
                // Attack recovery is not a hit reaction. Keep the original recovery damage silhouette.
                guardDamageSprite = poses["Recover"];
                guardDamageScale = pictureScale;
                guardDamageOverride = true;
                pose = armored ? "Guard" : "Idle";
            }
            if (action == "Approach" && Time.time >= hurtUntil
                && (transform.position - guardianPreviousPosition).sqrMagnitude > 0f
                && poses.ContainsKey("Move"))
            {
                guardDamageSprite = guardianAppearance != null && pose == "Guard" ? guardianAppearance : poses[pose];
                guardDamageScale = pictureScale * (guardDamageSprite == guardianAppearance ? appearanceScale : 1);
                guardDamageOverride = true;
                pose = "Move";
            }
            ApplyGuardianPose(pose);
            if (GuardTrialShieldAway)
            {
                if (!guardDamageOverride)
                {
                    guardDamageSprite = picture.sprite;
                    guardDamageScale = picture.transform.localScale;
                    guardDamageOverride = true;
                }
                picture.sprite = pose == "Hurt" ? guardThrownHurt : pose == "Move" ? guardTrialMove : guardTrialIdle;
                picture.transform.localScale = pictureScale;
                CurrentPose = "GD05/ShieldAway/" + pose;
                if (picture.sprite == guardTrialIdle) ShowStandingArt(true);
            }
            if (attack == EnemyAttack.GD03 && guardThrownShield != null && guardThrownShield.gameObject.activeInHierarchy)
            {
                var replacement = pose == "Hurt" ? guardThrownHurt : guardThrownRelease;
                if (replacement == null)
                    throw new System.InvalidOperationException("GD03 빈손 자세가 없습니다: " + heavyStrike.FormKey + "/" + pose);
                // Only the shield arm extends. Keep the old Thrust/Hurt damage silhouette independently.
                guardDamageSprite = poses[pose];
                guardDamageScale = pictureScale;
                guardDamageOverride = true;
                guardFlightPose = pose == "Hurt" ? "Hurt" : "ShieldBash";
                picture.sprite = replacement;
                CurrentPose = "GD03/ShieldAway/" + heavyStrike.FormKey + "/" + guardFlightPose;
            }
        }
        public void SetGuardTrialShield(SpriteRenderer shield)
        {
            guardTrialShield = shield;
            heavyStrike.ShieldAway = GuardTrialShieldAway;
            GuardianPose("Approach", EnemyAttack.GD05, false, false);
        }
        internal void GuardShieldFlightHurt()
        {
            if (guardThrownShield == null || !guardThrownShield.gameObject.activeInHierarchy) return;
            Hurt();
            GuardianPose("Attack", EnemyAttack.GD03, false, false);
        }
        public void SetGuardThrownShield(SpriteRenderer shield)
        {
            guardThrownShield = shield;
            if (shield != null)
                GuardianPose("Attack", EnemyAttack.GD03, false, false);
            else if (guardFlightPose != null)
            {
                picture.sprite = poses[guardFlightPose];
                CurrentPose = guardFlightPose;
                PlayBody("GD03Catch");
                guardFlightPose = null;
            }
        }
        public void PrepareHeavyStrike(float chargeSeconds = 0.6f) { heavyStrike.Prepare(chargeSeconds); HeavyBody(0); }
        public void HeatHeavyStrike() { heavyStrike.Heat(); HeavyBody(0); }
        public void SwingHeavyStrike() { heavyStrike.Swing(); HeavyBody(2); }
        void HeavyBody(int frame)
        {
            string form = GuardTrialShieldAway ? "WardFinal_NoShield" : motionForm;
            string motion = GuardTrialShieldAway ? "GD05Heavy" : "GD02";
            PlayBody(motion, form);
            motionStarted = Time.time - frame * KarmaBootstrap.Step;
            DrawMotion(form, motion, Time.time - motionStarted);
        }
        public void EndHeavyStrike() { if (heavyStrike != null) heavyStrike.Stop(); }
        internal Bounds GuardDamageBounds(SpriteRenderer renderer)
        {
            if (renderer != picture || sheet != "Ward" || !guardDamageOverride || guardDamageSprite == null)
                return DisplayDamageBounds(renderer);
            // Both movement and attack recovery preserve the original unrotated damage silhouette.
            var local = KarmaBodyPoses.HitBounds(guardDamageSprite);
            var root = picture.transform.parent;
            var scale = Vector3.Scale(root.lossyScale, guardDamageScale);
            var center = root.TransformPoint(pictureHome + Vector3.Scale(local.center, guardDamageScale));
            return new Bounds(center, Vector3.Scale(local.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
        }
        public void SetGroundSlamFrame(int frame)
        {
            groundSlamFrame = frame;
            if (frame >= 0 && ShowGroundSlam())
            {
                slamContact = picture.transform.TransformPoint(new Vector3(KarmaBodyPoses.HitBounds(picture.sprite).max.x * 0.96f, 0, 0));
                int count = motionArt.Frames(motionForm, "GD04").Length;
                int key = Mathf.RoundToInt(Mathf.Clamp01(frame / 3f) * (count - 1));
                PlayBody("GD04");
                motionStarted = Time.time - key * KarmaBootstrap.Step;
                DrawMotion(motionForm, "GD04", key * KarmaBootstrap.Step);
            }
        }
        Vector2 slamContact;
        public Vector2 GroundSlamContact => slamContact;
        bool ShowGroundSlam()
        {
            if (groundSlam == null || groundSlamFrame < 0 || groundSlam[groundSlamFrame] == null) return false;
            picture.sprite = groundSlam[groundSlamFrame];
            picture.transform.localPosition = pictureHome;
            picture.transform.localScale = pictureScale;
            picture.transform.localRotation = Quaternion.identity;
            CurrentPose = "GD04/" + picture.sprite.name;
            return true;
        }
    }
}
