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
        double risingDescentAt = -1, risingLandingAt = -1;
        double risingStrikeAt = -1;

        internal bool RisingBarrierVisible { get; set; }
        string RisingBodyMotion(string action, EnemyAttack attack, bool moved)
        {
            if (risingLandingAt >= 0) return attack == EnemyAttack.UP01 ? "UP01Landing" : "Landing";
            if (attack == EnemyAttack.GroundStrike && risingStrikeAt >= 0) return "UPBasic";
            if (action == "Attack" && attack == EnemyAttack.GroundStrike && CurrentPose == "Dash") return "Dash";
            if (risingMotion != null || action == "Attack") return attack == EnemyAttack.GroundStrike ? "UPBasic"
                : attack == EnemyAttack.UP05 && RisingBarrierVisible ? "UP05_NoFX" : attack.ToString();
            if (action == "Recover") return "Landing";
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
        }

        internal void StartRisingPursuit() => StartRisingMotion(risingPursuit);
        internal void StartRisingWave() => StartRisingMotion(risingWave);
        internal void StartRisingCrossing() => StartRisingMotion(risingCrossing);
        internal void EndRisingMotion() => risingMotion = null;

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
