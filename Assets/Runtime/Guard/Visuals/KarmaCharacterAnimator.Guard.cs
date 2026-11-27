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
        const float GuardTrialStrideLength = 3.75f * KarmaSpace.Scale;
        float guardTrialTravel;
        bool GuardTrialShieldAway { get { return guardTrialShield != null && guardTrialShield.gameObject.activeInHierarchy; } }
        Sprite guardThrownRelease, guardThrownHurt;
        string guardFlightPose;
        float guardThrowReleaseAt, guardThrowSettleAt;
        bool guardThrowReaching;
        Sprite[] groundSlam;
        int groundSlamFrame = -1;
        float groundSlamStarted;
        float autoShieldArtUntil, catchArtUntil, guardStunBegan, guardStunRiseUntil;
        double guardWalkPreStepAt = double.NegativeInfinity;
        float guardWalkStarted;
        // Contact, passing knee, and heel lowering for each foot, sampled at 12 fps.
        static readonly int[] GuardWalkDrawings = { 0, 1, 1, 2, 2, 3, 4, 4, 5, 5 };
        bool wasGuardStunned;
        float wardEndingPoseAt = -1f;
        Color wardEndingFallColor;
        internal void ShowAutoShield(float seconds) { autoShieldArtUntil = Time.time + seconds; }
        internal void PrepareGuardThrow(float warning)
        {
            guardThrowReleaseAt = Time.time + warning;
            catchArtUntil = 0;
        }
        internal void ReachForGuardShield() { guardThrowReaching = true; }
        internal void CatchGuardShield() { catchArtUntil = Time.time + 3 * KarmaBootstrap.Step; }
        internal Vector2 GuardThrowHand
        {
            get
            {
                // Centre of the open palm on GD03_03, measured from the top left of its 768 px cell.
                var palm = motionForm == "WardWave" ? new Vector2(644, 274) : new Vector2(685, 255);
                var release = motionArt.Frames(motionForm, "GD03")[3];
                var pixel = Vector2.Scale(new Vector2(palm.x, 768 - palm.y) / 768, release.rect.size);
                return picture.transform.TransformPoint((pixel - release.pivot) / release.pixelsPerUnit);
            }
        }
        string GuardBodyMotion(string action, EnemyAttack attack, bool moved, ref string form)
        {
            bool away = GuardTrialShieldAway || guardThrownShield != null && guardThrownShield.gameObject.activeInHierarchy;
            if (away && form == "WardFinal") form = "WardFinal_NoShield";
            bool bare = form == "WardFinal_NoShield";
            if (action == "Stunned")
            {
                if (!wasGuardStunned) guardStunBegan = Time.time;
                wasGuardStunned = true;
                bool entering = Time.time - guardStunBegan < KarmaBootstrap.Step;
                // Drawing 01 has the lowered head and bent knees. Hold it until combat releases the stun.
                return GuardBodyDrawing(form, bare || entering ? "Stagger" : "Stun", entering ? 0 : 1);
            }
            if (wasGuardStunned)
            {
                wasGuardStunned = false;
                guardStunRiseUntil = Time.time + KarmaBootstrap.Step;
            }
            if (Time.time < guardStunRiseUntil)
            {
                if ((action == "Approach" || action == "Recover") && Time.time >= hurtUntil)
                    return GuardBodyDrawing(form, bare ? "Stagger" : "Stun", 2);
                guardStunRiseUntil = 0;
            }
            // A late hit still flashes and uses its damage silhouette while the hand receives the shield.
            if (Time.time < hurtUntil && !guardThrowReaching && Time.time >= catchArtUntil)
                return away && form == "WardWave" ? "GD03ShieldAwayHurt" : "Hurt";
            if (attack == EnemyAttack.GD03 && guardThrownShield != null && guardThrownShield.gameObject.activeInHierarchy)
            {
                if (bare && !guardThrowReaching && Time.time >= guardThrowSettleAt)
                    return GuardBodyDrawing(form, "GD03Release", 0);
                form = motionForm;
                return GuardBodyDrawing(form, "GD03", 3);
            }
            if (bare)
            {
                if (attack == EnemyAttack.GD05 && heavyStrike.Active)
                {
                    PlayBody("GD05Heavy", form);
                    ApplyHeavyDrawing(false);
                    return null;
                }
                if (attack == EnemyAttack.GD03) return "GD03Release";
                if (action != "Approach" || attack != EnemyAttack.GD05) return moved ? "Move" : "Idle";
                if (guardTrialTravel <= 0f) return "Idle";

                // Six drawings cover one stride at the trial's normal speed (12 fps).
                int count = motionArt.Frames(form, "Move").Length;
                int frame = Mathf.FloorToInt(guardTrialTravel * count / GuardTrialStrideLength) % count;
                return GuardBodyDrawing(form, "Move", frame);
            }
            if (Time.time < catchArtUntil) return GuardCatchDrawing(form);
            if (attack == EnemyAttack.GD03 && action == "Telegraph" && guardThrowReleaseAt > 0)
            {
                float untilRelease = guardThrowReleaseAt - Time.time;
                int frame = untilRelease > 2 * KarmaBootstrap.Step ? 0 : untilRelease > KarmaBootstrap.Step ? 1 : 2;
                return GuardBodyDrawing(form, "GD03", frame);
            }
            if (Time.time < autoShieldArtUntil && action == "Approach") return "GD01";
            if (attack == EnemyAttack.GD02 && !away && heavyStrike != null && heavyStrike.Active)
            {
                if (form == "Ward")
                    motionStarted = Time.time - (heavyStrike.BodyFrame + 0.5f) * KarmaBootstrap.Step;
                PlayBody("GD02", form);
                ApplyHeavyDrawing(false);
                // This phase has already supplied the final, ground-aligned body drawing.
                return null;
            }
            if (attack == EnemyAttack.GD04 && groundSlamFrame >= 0) return GroundSlamBodyDrawing();
            if (action == "Attack" || attack == EnemyAttack.GD02 && CurrentPose != null && CurrentPose.StartsWith("GD02/"))
                return attack == EnemyAttack.GroundStrike ? "GDBasic" : attack.ToString();
            if (action == "Telegraph") return attack == EnemyAttack.GD05 ? "GD05" : "Windup";
            if (action == "Recover") return "Recover";
            bool walking = moved || CurrentPose == "Move";
            if (action == "Approach" && !away && Time.inFixedTimeStep)
            {
                // The host samples idle before Guard moves in the same physics step.
                // Only a real stop, or a change of action or form, starts a new stride.
                if (!walking && displayForm == form && displayMotion == "Move")
                    guardWalkPreStepAt = Time.fixedTimeAsDouble;
                if (walking)
                {
                    if (displayForm != form || (displayMotion != "Move" && guardWalkPreStepAt != Time.fixedTimeAsDouble))
                        guardWalkStarted = Time.fixedTime;
                    int tick = Mathf.FloorToInt((Time.fixedTime - guardWalkStarted) * KarmaBootstrap.FramesPerSecond + 0.0001f);
                    int frame = GuardWalkDrawings[tick % GuardWalkDrawings.Length];
                    displayForm = form;
                    displayMotion = "Move";
                    motionStarted = Time.time - (frame + 0.5f) * KarmaBootstrap.Step;
                }
            }
            if (walking) return "Move";
            if (CurrentPose != "Guard") return "Idle";

            // Settle into the block once and hold its final drawing while standing guard.
            if (KarmaMotionArt.Has(form, "Guard"))
                PlayBody("Guard", form);
            return null;
        }
        string GuardCatchDrawing(string form)
        {
            int frame = Mathf.Clamp(3 - Mathf.CeilToInt((catchArtUntil - Time.time) / KarmaBootstrap.Step), 0, 2);
            if (frame == 0) return GuardBodyDrawing(form, "GD03", 2);
            return GuardBodyDrawing(form, frame == 1 && form == "WardWave" ? "GD03" : "GD03Catch", frame);
        }
        string GuardBodyDrawing(string form, string motion, int frame)
        {
            displayForm = form;
            displayMotion = motion;
            motionStarted = Time.time - (frame + 0.5f) * KarmaBootstrap.Step;
            return motion;
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
            presentGuardianEndingDefeat = heavyStrike.FormKey == "WardFinal" ? PresentWardFinalEndingDefeat : null;
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
        // The chase moves once per rendered frame; physics can sample that same step twice.
        internal void PresentGuardTrialStep(float deltaX)
        {
            if (deltaX == 0f && guardTrialTravel == 0f) return;
            guardTrialTravel = deltaX == 0f ? 0f : guardTrialTravel + Mathf.Abs(deltaX);
            PresentCombatBody("Approach", EnemyAttack.GD05, deltaX != 0f);
        }
        public void SetGuardTrialShield(SpriteRenderer shield)
        {
            guardTrialTravel = 0f;
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
            guardThrowReaching = false;
            if (shield != null)
            {
                guardThrowSettleAt = Time.time + 2 * KarmaBootstrap.Step;
                GuardianPose("Attack", EnemyAttack.GD03, false, false);
            }
            else if (guardFlightPose != null)
            {
                picture.sprite = poses[guardFlightPose];
                CurrentPose = guardFlightPose;
                if (Time.time < catchArtUntil) PlayBody(GuardCatchDrawing(motionForm), motionForm);
                else PlayBody("Idle", motionForm);
                guardFlightPose = null;
            }
            if (shield == null) guardThrowReleaseAt = 0;
        }
        public void PrepareHeavyStrike(float chargeSeconds = 0.6f) { heavyStrike.Prepare(chargeSeconds); HeavyBody(0); }
        public void HeatHeavyStrike() { heavyStrike.Heat(); HeavyBody(0); }
        public void SwingHeavyStrike() { heavyStrike.Swing(); HeavyBody(2); }
        internal void PrepareHeavyWaveBody(float warningSeconds) => heavyStrike.PrepareWave(warningSeconds);
        internal void ReleaseHeavyWaveBody() => heavyStrike.ReleaseWave();
        void HeavyBody(int frame)
        {
            bool away = GuardTrialShieldAway;
            string form = away ? "WardFinal_NoShield" : motionForm;
            string motion = away ? "GD05Heavy" : "GD02";
            if (form == "Ward") frame = heavyStrike.BodyFrame;
            PlayBody(motion, form);
            motionStarted = Time.time - frame * KarmaBootstrap.Step;
            DrawMotion(form, motion, frame * KarmaBootstrap.Step);
            ApplyHeavyDrawing(true);
        }
        void ApplyHeavyDrawing(bool phaseChanged)
        {
            // DamageBounds includes the blade overlay. Retain the native playback drawing
            // for those bounds while displaying the drawing selected by the combat phase.
            var boundsDrawing = picture.sprite;
            int frame = heavyStrike.BodyFrame;
            var alignmentDrawing = motionArt.Frames(displayForm, displayMotion)[frame];
            picture.sprite = displaySprite = heavyStrike.WaveDrawing(frame) ?? alignmentDrawing;
            heavyStrike.AlignBody(frame, alignmentDrawing, boundsDrawing, phaseChanged);
        }
        public void EndHeavyStrike() { if (heavyStrike != null) heavyStrike.Stop(); }
        void PresentWardFinalEndingDefeat(float elapsed)
        {
            var fall = motionArt.Frames(motionForm, "Death");
            if (deathAt >= 0f)
            {
                // The ending can start immediately or after ceremony spacing. Finish the
                // existing fall using its original clock and floor alignment in both cases.
                Pose(0f, false);
                if (Time.time - deathAt < fall.Length * KarmaBootstrap.Step) return;
                wardEndingPoseAt = elapsed;
                wardEndingFallColor = picture.color;
            }

            float poseElapsed = wardEndingPoseAt < 0f ? elapsed : elapsed - wardEndingPoseAt;
            PresentEndingDefeatDrawing(elapsed, poseElapsed);
            var grounded = fall[fall.Length - 1];
            var drawing = picture.sprite;
            // The ending artwork is laid out in Death_03 pixel space. Match its world scale
            // and pivot without changing the original FinalDefeat slices or import settings.
            picture.transform.localScale = pictureScale * (drawing.pixelsPerUnit / grounded.pixelsPerUnit);
            var pivotOffset = (drawing.pivot - grounded.pivot) / grounded.pixelsPerUnit;
            picture.transform.localPosition = pictureHome + Vector3.Scale(pivotOffset, pictureScale);
            if (wardEndingPoseAt >= 0f)
                picture.color = Color.Lerp(wardEndingFallColor, picture.color,
                    KarmaGame.EndingEase(0f, 0.3f, poseElapsed));
        }
        internal Bounds GuardDamageBounds(SpriteRenderer renderer)
        {
            var overlayDrawing = heavyStrike?.OverlayBoundsDrawing(renderer);
            if (overlayDrawing != null) return GuardHomeBounds(overlayDrawing.bounds, pictureScale);
            if (renderer == picture && !guardDamageOverride && heavyStrike != null &&
                renderer.sprite == heavyStrike.RegisteredBody && renderer.sprite == displaySprite)
                return GuardHomeBounds(displayDamage, pictureScale);
            if (renderer != picture || sheet != "Ward" || !guardDamageOverride || guardDamageSprite == null)
                return DisplayDamageBounds(renderer);
            // Both movement and attack recovery preserve the original unrotated damage silhouette.
            return GuardHomeBounds(KarmaBodyPoses.HitBounds(guardDamageSprite), guardDamageScale);
        }
        Bounds GuardHomeBounds(Bounds local, Vector3 localScale)
        {
            var root = picture.transform.parent;
            var scale = Vector3.Scale(root.lossyScale, localScale);
            var center = root.TransformPoint(pictureHome + Vector3.Scale(local.center, localScale));
            return new Bounds(center, Vector3.Scale(local.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
        }
        public void SetGroundSlamFrame(int frame)
        {
            if (frame == 0) groundSlamStarted = Time.time;
            groundSlamFrame = frame;
            if (frame >= 0 && ShowGroundSlam())
            {
                slamContact = picture.transform.TransformPoint(new Vector3(KarmaBodyPoses.HitBounds(picture.sprite).max.x * 0.96f, 0, 0));
                PlayBody(GroundSlamBodyDrawing(), motionForm);
            }
        }
        string GroundSlamBodyDrawing()
        {
            // Raise through 00–02, hold 03 for descent, and show 04 on the first eruption.
            // Combat releases the grounded follow-through (05) when the sequence ends.
            int drawing = groundSlamFrame == 0
                ? Mathf.Clamp(Mathf.FloorToInt((Time.time - groundSlamStarted) * KarmaBootstrap.FramesPerSecond), 0, 2)
                : groundSlamFrame + 2;
            return GuardBodyDrawing(motionForm, "GD04", drawing);
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
