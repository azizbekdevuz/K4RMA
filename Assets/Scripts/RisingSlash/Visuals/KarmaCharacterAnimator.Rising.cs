using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        const int RisingSpriteFps = 24;
        const int BasicStrikeFrames = 6;
        const int BaseDescentFrames = 7;
        const int LandingFrames = 2;
        Sprite[] risingBasicStrike, risingDescent, risingPursuit, risingWave, risingCrossing;
        Sprite[] risingMotion;
        double risingMotionAt;
        double risingDescentAt = -1, risingLandingAt = -1;
        double risingStrikeAt = -1;

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
            var frames = Resources.LoadAll<Sprite>("RisingSlash/Forms/" + name);
            if (frames.Length != count) return null;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        internal void StartRisingDescent(float duration)
        {
            if (risingDescent == null) return;
            risingLandingAt = -1;
            // Hold the first pose on long drops, then play the cut at its authored rate.
            risingDescentAt = Time.timeAsDouble + Mathf.Max(0, duration - RisingDropFrameCount / (float)RisingSpriteFps);
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

        static int RisingFrame(double start, int count) =>
            Mathf.Clamp((int)((Time.timeAsDouble - start) * RisingSpriteFps), 0, count - 1);

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
