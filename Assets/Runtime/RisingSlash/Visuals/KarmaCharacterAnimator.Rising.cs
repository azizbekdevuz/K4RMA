using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        // Per-tick indices preserve the contact and settled drawings of each source strip.
        static readonly int[][] RisingDrawings = {
            null, new[] { 0 }, new[] { 1 }, new[] { 0, 2 }, null, null,
            new[] { 0, 3, 5 }, new[] { 0, 2, 4, 6 }
        };
        const int BasicStrikeFrames = 6;
        const int BaseDescentFrames = 7;
        const int LandingFrames = 2;
        Sprite[] risingBasicStrike, risingDescent, risingPursuit, risingWave, risingCrossing;
        Sprite[] risingMotion;
        double risingMotionAt;
        double risingWaveWindupAt = double.PositiveInfinity;
        double risingDescentAt = -1, risingLandingAt = -1;
        double risingStrikeAt = -1;
        bool holdingPursuitApex;
        bool UsesChainedDescentArt => motionForm == "Rise" || motionForm == "RiseWave";

        internal bool RisingBarrierVisible { get; set; }

        void ShowFallenRisingGrowth()
        {
            // Combat seats this pose using the revival support point. The rehearsal
            // has its own picture transform and starts with the boss already prone.
            DrawMotion(previousMotionForm, "Death", 3 * KarmaBootstrap.Step);
            var fallen = KarmaBodyPoses.Get("@RisingSlash/Revival/Rise", "Revive00");
            if (KarmaBodyPoses.TryFloor(fallen, out float contact))
                picture.transform.localPosition -= Vector3.up * (contact * pictureScale.y);
        }

        internal void ShowRisingWaveAttempt(float elapsed)
        {
            // Reuse the base body's low draw, extension and recovery from its sword study.
            if (elapsed >= 0.75f) DrawMotion(previousMotionForm, "Idle", 0);
            else DrawMotion(previousMotionForm, "GrowthTechnique", Mathf.Max(0, elapsed - 0.2f));
        }

        internal void ShowRisingMasteryStance(float elapsed)
        {
            DrawMotion(motionForm, "GrowthMastery", Mathf.Clamp(elapsed, 0, KarmaBootstrap.Step));
        }

        internal void ShowRisingMasteryStroke(Essence technique, float elapsed)
        {
            if (technique == Essence.SwordWave)
                DrawMotion(motionForm, "UP03", Mathf.Max(0, elapsed - KarmaBootstrap.Step));
            else if (technique == Essence.Guard)
                DrawMotion(motionForm, "UP04", Mathf.Clamp(elapsed, KarmaBootstrap.Step, 2 * KarmaBootstrap.Step));
            else DrawMotion(motionForm, "GrowthMastery", Mathf.Max(0, elapsed - KarmaBootstrap.Step));
        }

        internal void ShowRisingMasteryFinish(float elapsed, bool ready)
        {
            if (ready) DrawMotion(motionForm, "Idle", 0);
            else if (elapsed < 0.4f) DrawMotion(motionForm, "UP04", KarmaBootstrap.Step + elapsed);
            else if (elapsed < 1.25f) DrawMotion(motionForm, "UPBasic", elapsed - 0.4f);
            else DrawMotion(motionForm, "GrowthMastery", 4 * KarmaBootstrap.Step + elapsed - 1.25f);
        }

        string RisingBodyMotion(string action, EnemyAttack attack, bool moved)
        {
            if (risingLandingAt >= 0)
                return attack == EnemyAttack.UP01 || (attack == EnemyAttack.UP02 && motionForm == "RiseWave")
                    ? "UP01Landing" : "Landing";
            // UP02 hands off directly to UP01. Keep the last pursuit cel through
            // the lock-on, then play the existing descent from its own first cel.
            if (attack == EnemyAttack.UP02 && UsesChainedDescentArt && risingDescentAt >= 0 && action == "Attack")
                return "UP01";
            if (attack == EnemyAttack.GroundStrike && risingStrikeAt >= 0) return "UPBasic";
            if (action == "Attack" && attack == EnemyAttack.GroundStrike && CurrentPose == "Dash") return "Dash";
            if (attack == EnemyAttack.UP03 && action == "Attack" && risingMotion == null)
                return moved ? "Dash" : "Idle";
            if (risingMotion != null || action == "Attack") return attack == EnemyAttack.GroundStrike ? "UPBasic"
                : attack == EnemyAttack.UP05 && RisingBarrierVisible ? "UP05_NoFX" : attack.ToString();
            // UP03 releases its wave with both feet planted.
            if (action == "Recover") return attack == EnemyAttack.UP03 ||
                (attack == EnemyAttack.GroundStrike && motionForm == "RiseWave") ? "Idle" : "Landing";
            if (action == "Telegraph" && attack == EnemyAttack.UP03 && Time.timeAsDouble >= risingWaveWindupAt)
                return "UP03";
            if (action == "Telegraph") return attack == EnemyAttack.UP05 ? "UP05" : "Windup";
            return moved || CurrentPose == "Dash" ? "Dash" : "Idle";
        }
        internal void ConfigureRisingPresentation(string form)
        {
            // Base poses live in this boss's library; inherited bodies already supplied
            // their own poses and must keep those images.
            foreach (var sprite in KarmaArtwork.Sprites("Rise"))
                if (sprite != null && !poses.ContainsKey(sprite.name)) poses[sprite.name] = sprite;
            risingBasicStrike = form == "Rise"
                ? LoadRisingFrames("RiseBasicStrike", BasicStrikeFrames)
                : LoadRisingFrames(form + "Strike", 3);
            int dropFrames = form == "Rise" ? BaseDescentFrames : form == "RiseWave" ? 2 : 3;
            risingDescent = LoadRisingFrames(form + "Descent", dropFrames + LandingFrames);
            if (form == "RiseFinal")
            {
                risingCrossing = LoadRisingFrames("RiseFinalCrossing", 3);
                risingPursuit = risingCrossing;
            }
            else risingPursuit = LoadRisingFrames(form + "Pursuit", 3);
            if (form == "RiseWave") risingWave = LoadRisingFrames("RiseWaveRelease", 3);
            else if (form == "RiseFinal") risingWave = LoadRisingFrames("RiseFinalWaveRelease", 3);
            guardianPose = RisingGuardianPose;
        }

        static Sprite[] LoadRisingFrames(string name, int count)
        {
            var frames = KarmaBodyPoses.All("@RisingSlash/Forms/" + name);
            if (frames.Length != count) return null;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        internal void StartRisingDescent(float duration)
        {
            if (risingDescent == null) return;
            holdingPursuitApex = false;
            risingLandingAt = -1;
            // Hold the first pose on long drops, then play the cut at its authored rate.
            risingDescentAt = Time.timeAsDouble + Mathf.Max(0, duration - RisingDropFrameCount * (0.5f * KarmaBootstrap.Step));
        }

        int RisingDropFrameCount => risingDescent.Length - LandingFrames;

        internal void LandRisingDescent()
        {
            if (risingDescentAt < 0) return;
            risingDescentAt = -1;
            risingLandingAt = Time.timeAsDouble;
        }

        internal void ClearRisingAttackFrames()
        {
            risingStrikeAt = risingDescentAt = risingLandingAt = -1;
            EndRisingMotion();
            holdingPursuitApex = false;
        }

        internal void StartRisingPursuit() => StartRisingMotion(risingPursuit);
        internal void PrepareRisingWave(float warning)
        {
            int leadFrames = motionForm == "RiseWave" || motionForm == "RiseFinal" ? 2 : 1;
            risingWaveWindupAt = Time.timeAsDouble + Mathf.Max(0, warning - leadFrames * KarmaBootstrap.Step);
        }
        internal void StartRisingWave()
        {
            StartRisingMotion(risingWave);
            // The pattern coroutine can release after the regular pose update.
            if (motionForm == "RiseFinal" && risingWave != null)
                DrawMotion(motionForm, "UP03", 2 * KarmaBootstrap.Step);
        }
        internal void StartRisingCrossing() => StartRisingMotion(risingCrossing);
        internal void EndRisingMotion()
        {
            holdingPursuitApex = UsesChainedDescentArt && risingMotion != null && risingMotion == risingPursuit;
            risingMotion = null;
            risingWaveWindupAt = double.PositiveInfinity;
        }

        bool PresentRisingAttackFrame(string action, EnemyAttack attack)
        {
            if (attack == EnemyAttack.UP02 && holdingPursuitApex && action == "Telegraph")
            {
                DrawMotion(motionForm, "UP02", 2 * KarmaBootstrap.Step);
                return true;
            }
            if (attack == EnemyAttack.UP03 && motionForm == "RiseFinal")
            {
                double now = Time.timeAsDouble;
                if (risingMotion != null && risingMotion == risingWave)
                {
                    DrawMotion(motionForm, "UP03", 2 * KarmaBootstrap.Step + (float)(now - risingMotionAt));
                    return true;
                }
                if (action == "Telegraph" && now >= risingWaveWindupAt)
                {
                    // The live wave owns frame 02, even if the warning ends between ticks.
                    DrawMotion(motionForm, "UP03", Mathf.Min((float)(now - risingWaveWindupAt), KarmaBootstrap.Step));
                    return true;
                }
            }
            if (attack != EnemyAttack.UP05 || risingMotion == null || risingMotion != risingCrossing ||
                (action != "Telegraph" && action != "Attack"))
                return false;
            DrawMotion(motionForm, "UP05", (float)(Time.timeAsDouble - risingMotionAt), true);
            return true;
        }

        void StartRisingMotion(Sprite[] frames)
        {
            risingMotion = frames;
            risingMotionAt = Time.timeAsDouble;
        }

        static int RisingFrame(double start, int count)
        {
            int tick = Mathf.Max(0, (int)((Time.timeAsDouble - start) * KarmaBootstrap.FramesPerSecond));
            var drawings = RisingDrawings[count];
            return drawings[Mathf.Min(tick, drawings.Length - 1)];
        }

        internal void StartRisingBasicStrike()
        {
            if (risingBasicStrike != null) risingStrikeAt = Time.timeAsDouble;
        }

        internal void ClearRisingBasicStrike() => risingStrikeAt = -1;

        void RisingGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            string pose = Time.time < hurtUntil ? "Hurt"
                : action == "Stunned" ? "Hurt"
                : action == "Recover" ? "Landing"
                : action == "Attack" ? airborne ? "AirSlash" : attack == EnemyAttack.UP01 || attack == EnemyAttack.UP03 ? "Rising" : "Slash"
                : DefaultGuardianPose(action, airborne, armored);
            if (pose == "Move") pose = "Dash";
            bool basic = attack == EnemyAttack.GroundStrike && !airborne;
            if (!basic || (pose != "Slash" && pose != "Landing")) ClearRisingBasicStrike();
            if (basic && risingBasicStrike != null && risingStrikeAt < 0 && pose == "Slash") pose = "Dash";

            if (pose == "Hurt")
            {
                ClearRisingAttackFrames();
            }
            else if (action != "Attack" && action != "Recover")
            {
                risingDescentAt = risingLandingAt = -1;
            }
            ApplyGuardianPose(pose);
            if (risingStrikeAt >= 0)
                picture.sprite = risingBasicStrike[RisingFrame(risingStrikeAt, risingBasicStrike.Length)];
            else if (risingDescentAt >= 0 && action == "Attack")
                picture.sprite = risingDescent[RisingFrame(risingDescentAt, RisingDropFrameCount)];
            else if (risingLandingAt >= 0)
                picture.sprite = risingDescent[RisingDropFrameCount + RisingFrame(risingLandingAt, LandingFrames)];
            else if (risingMotion != null)
                picture.sprite = risingMotion[RisingFrame(risingMotionAt, risingMotion.Length)];
        }
    }
}
