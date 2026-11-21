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
        internal enum RisingCrossingPose { None, Charge, Approach, Brake, PushOff, Crossing }
        Sprite[] risingBasicStrike, risingDescent, risingPursuit;
        Sprite[] risingMotion;
        double risingMotionAt;
        double risingDescentAt = -1, risingLandingAt = -1;
        double risingAscentAt = -1, risingWaveReleaseAt = -1;
        double risingWaveWindupAt;
        float risingWaveWarning;
        double risingDescentTellAt;
        float risingDescentDuration;
        double risingStrikeAt = -1;
        double risingHitUntil = -1;
        float risingStunLeft = float.PositiveInfinity;
        bool risingDescentPrepared;
        bool risingDescentFromPursuit;
        bool risingWavePrepared;
        bool risingAirborne;
        RisingCrossingPose risingCrossingPose;
        double risingCrossingAt;
        float risingCrossingDuration;

        internal bool RisingBarrierVisible { get; set; }
        internal void RecordRisingHit() => risingHitUntil = Time.timeAsDouble + 2.25f * KarmaBootstrap.Step;
        internal void SetRisingStunTime(float remaining) => risingStunLeft = remaining;

        internal void DrawRisingDefeatFall(float elapsed, float duration)
        {
            if (elapsed < KarmaBootstrap.Step) return;
            var frames = motionArt.Frames(motionForm, "Landing");
            int frame = Mathf.Min(1, frames.Length - 1);
            if (frame < 0) return;
            float sample = elapsed < duration - KarmaBootstrap.Step ? 0 : frame * KarmaBootstrap.Step;
            picture.transform.localRotation = Quaternion.identity;
            DrawMotion(motionForm, "Landing", sample);
        }

        string RisingBodyMotion(string action, EnemyAttack attack, bool moved)
        {
            if ((action == "Approach" || action == "Idle" || action == "Recover")
                && Time.timeAsDouble < risingHitUntil && KarmaMotionArt.Has(motionForm, "Hurt"))
                return "Hurt";
            if (risingLandingAt >= 0) return RisingLandingMotion();
            if (action == "Attack" && risingAscentAt >= 0)
            {
                if (KarmaMotionArt.Has(motionForm, "UP01Ascent")) return "UP01Ascent";
                return "UP02";
            }
            if (action == "Attack" && risingDescentAt >= 0)
            {
                int last = motionArt.Frames(motionForm, "UP01").Length - 1;
                float progress = Mathf.Clamp01(RisingSample(risingDescentAt) / risingDescentDuration);
                // Travel drives the cut. Long dives leave the tuck early and
                // carry the blade toward the floor for the remaining exposure.
                int drawing = Mathf.RoundToInt(Mathf.SmoothStep(0, last, progress) + 0.12f);
                BeginRisingDrawing("UP01", Mathf.Clamp(drawing, 0, last));
                picture.transform.localRotation = Quaternion.Euler(0, 0, -5f * Mathf.Sin(progress * Mathf.PI));
                return "UP01";
            }
            if (action == "Telegraph" && risingDescentPrepared)
            {
                if (risingDescentFromPursuit && RisingSample(risingDescentTellAt) < KarmaBootstrap.Step)
                {
                    BeginRisingDrawing("UP02", motionArt.Frames(motionForm, "UP02").Length - 1);
                    picture.transform.localRotation = Quaternion.Euler(0, 0, -9f);
                    return "UP02";
                }
                BeginRisingDrawing("UP01", 0);
                return "UP01";
            }
            if (risingWaveReleaseAt >= 0)
            {
                float elapsed = RisingSample(risingWaveReleaseAt);
                if (elapsed >= 3 * KarmaBootstrap.Step)
                {
                    BeginRisingDrawing("Landing", elapsed < 4 * KarmaBootstrap.Step ? 1 : 2);
                    return "Landing";
                }
                if (elapsed >= KarmaBootstrap.Step && KarmaMotionArt.Has(motionForm, "UP03Bridge"))
                {
                    BeginRisingDrawing("UP03Bridge", 2);
                    return "UP03Bridge";
                }
                BeginRisingDrawing("UP03", 2);
                return "UP03";
            }
            if (action == "Telegraph" && risingWavePrepared)
            {
                float elapsed = RisingSample(risingWaveWindupAt);
                bool bridge = KarmaMotionArt.Has(motionForm, "UP03Bridge");
                if (bridge && elapsed >= risingWaveWarning - 1.5f * KarmaBootstrap.Step)
                {
                    BeginRisingDrawing("UP03Bridge", 1);
                    return "UP03Bridge";
                }
                if (bridge && elapsed >= KarmaBootstrap.Step && elapsed < 2 * KarmaBootstrap.Step)
                {
                    BeginRisingDrawing("UP03Bridge", 0);
                    return "UP03Bridge";
                }
                BeginRisingDrawing("UP03", elapsed >= (bridge ? 2 : 1) * KarmaBootstrap.Step ? 1 : 0);
                return "UP03";
            }
            if (attack == EnemyAttack.GroundStrike && risingStrikeAt >= 0
                && (action == "Attack" || displayMotion == "UPBasic"
                    && Time.time - motionStarted < risingBasicStrike.Length * KarmaBootstrap.Step))
                return "UPBasic";
            if (action == "Recover")
            {
                // These recoveries are already on the floor. Landing_00 is
                // the airborne entry and would lift both feet again.
                if (displayMotion != "Landing" || displayForm != motionForm)
                    BeginRisingDrawing("Landing", 1);
                return "Landing";
            }
            if (action == "Attack" && attack == EnemyAttack.GroundStrike && CurrentPose == "Dash") return "Dash";
            if (risingMotion != null || action == "Attack") return attack == EnemyAttack.GroundStrike ? "UPBasic"
                : attack == EnemyAttack.UP05 && RisingBarrierVisible ? "UP05_NoFX" : attack.ToString();
            if (action == "Telegraph") return attack == EnemyAttack.UP05 ? "UP05" : "Windup";
            if (moved || CurrentPose == "Dash") return "Dash";
            var breath = Mathf.Floor((Time.time - motionStarted) * KarmaBootstrap.FramesPerSecond) * KarmaBootstrap.Step;
            float settle = (1 - Mathf.Cos(breath * Mathf.PI)) * 0.5f;
            picture.transform.localRotation = Quaternion.Euler(0, 0, settle * -0.8f);
            return "Idle";
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
            risingPursuit = LoadRisingFrames(form == "RiseFinal" ? "RiseFinalCrossing" : form + "Pursuit", 3);
            holdFallenRevivalDrawing = previous => previous == "Rise" || previous == "RiseWave"
                || previous == "RiseWard" || previous == "RiseFinal";
            guardianPose = RisingGuardianPose;
        }

        static Sprite[] LoadRisingFrames(string name, int count)
        {
            var frames = KarmaBodyPoses.All("@RisingSlash/Forms/" + name);
            if (frames.Length != count) return null;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        internal void PrepareRisingDescent()
        {
            risingDescentFromPursuit = risingMotion == risingPursuit && risingPursuit != null;
            risingDescentTellAt = Time.timeAsDouble;
            risingDescentPrepared = true;
            EndRisingMotion();
        }

        internal void StartRisingDescent(float duration)
        {
            risingAscentAt = -1;
            if (risingDescent == null) return;
            risingDescentPrepared = false;
            risingLandingAt = -1;
            risingDescentAt = Time.timeAsDouble;
            risingDescentDuration = Mathf.Max(KarmaBootstrap.Step, duration);
        }

        int RisingDropFrameCount => risingDescent.Length - LandingFrames;

        internal void LandRisingDescent()
        {
            if (risingDescentAt < 0) return;
            risingDescentAt = -1;
            risingLandingAt = Time.timeAsDouble;
            BeginRisingDrawing("UP01Landing", 0);
            // The hit appears in the same frame as the planted drawing.
            picture.transform.localRotation = Quaternion.identity;
            DrawMotion(motionForm, "UP01Landing", 0);
        }

        string RisingLandingMotion()
        {
            int tick = (int)((Time.timeAsDouble - risingLandingAt) * KarmaBootstrap.FramesPerSecond);
            if (tick < LandingFrames) return "UP01Landing";
            if (tick == LandingFrames && (motionForm == "Rise" || motionForm == "RiseWard"))
            {
                // The planted crouch takes the weight before the torso straightens.
                BeginRisingDrawing("Windup", 2);
                return "Windup";
            }
            if (tick < LandingFrames + 2)
            {
                BeginRisingDrawing("Landing", 2);
                return "Landing";
            }
            return "Idle";
        }

        internal void ClearRisingAttackFrames()
        {
            risingDescentPrepared = risingWavePrepared = false;
            risingStrikeAt = risingDescentAt = risingLandingAt = risingAscentAt = -1;
            EndRisingMotion();
        }

        internal void StartRisingPursuit() => StartRisingMotion(risingPursuit);
        internal void StartRisingAscent()
        {
            EndRisingMotion();
            risingAscentAt = Time.timeAsDouble;
            var motion = KarmaMotionArt.Has(motionForm, "UP01Ascent") ? "UP01Ascent" : "UP02";
            BeginRisingDrawing(motion, 0);
            DrawMotion(motionForm, motion, 0);
        }
        internal void PrepareRisingWave(float warning)
        {
            risingWavePrepared = true;
            risingWaveWarning = warning;
            risingWaveWindupAt = Time.timeAsDouble;
        }
        internal void StartRisingWave()
        {
            risingWavePrepared = false;
            risingWaveReleaseAt = Time.timeAsDouble;
            BeginRisingDrawing("UP03", 2);
            DrawMotion(motionForm, "UP03", 2 * KarmaBootstrap.Step);
        }
        internal void SetRisingCrossingPose(RisingCrossingPose pose, float duration = 0)
        {
            risingCrossingPose = pose;
            risingCrossingAt = Time.timeAsDouble;
            risingCrossingDuration = duration;
        }
        internal void EndRisingMotion()
        {
            risingMotion = null;
            risingWaveReleaseAt = -1;
            risingCrossingPose = RisingCrossingPose.None;
        }

        bool PresentRisingCrossing(string action, EnemyAttack attack)
        {
            if (motionForm != "RiseFinal" || attack != EnemyAttack.UP05 || risingCrossingPose == RisingCrossingPose.None ||
                (action != "Telegraph" && action != "Attack")) return false;
            float elapsed = RisingSample(risingCrossingAt);
            switch (risingCrossingPose)
            {
                case RisingCrossingPose.Charge:
                    DrawMotion(motionForm, "Windup", elapsed);
                    break;
                case RisingCrossingPose.Approach:
                    DrawMotion(motionForm, "Dash", elapsed, true);
                    break;
                case RisingCrossingPose.Brake:
                    DrawMotion(motionForm, "Landing", risingAirborne ? Mathf.Min(elapsed, KarmaBootstrap.Step)
                        : 1.5f * KarmaBootstrap.Step + elapsed);
                    if (risingAirborne)
                    {
                        float tuck = Mathf.Clamp01(elapsed / (2 * KarmaBootstrap.Step));
                        picture.transform.localRotation = Quaternion.Euler(0, 0, 7f * tuck);
                        picture.transform.localPosition -= Vector3.up * (0.12f * tuck);
                    }
                    break;
                case RisingCrossingPose.PushOff:
                    DrawMotion(motionForm, "Dash", 2 * KarmaBootstrap.Step);
                    picture.transform.localRotation = Quaternion.Euler(0, 0, -8f);
                    break;
                case RisingCrossingPose.Crossing:
                    // A single extension, tuck and reach runs into the wall brake.
                    int count = motionArt.Frames(motionForm, "UP05").Length;
                    int frame = Mathf.Min(count - 1, Mathf.FloorToInt(count * elapsed
                        / Mathf.Max(count * KarmaBootstrap.Step, risingCrossingDuration)));
                    DrawMotion(motionForm, "UP05", frame * KarmaBootstrap.Step);
                    break;
            }
            return true;
        }

        void StartRisingMotion(Sprite[] frames)
        {
            risingMotion = frames;
            risingMotionAt = Time.timeAsDouble;
        }
        void BeginRisingDrawing(string motion, int firstDrawing)
        {
            displayForm = motionForm;
            displayMotion = motion;
            motionStarted = Time.time - (firstDrawing == 0 ? 0 : firstDrawing * KarmaBootstrap.Step + 0.001f);
        }

        static int RisingFrame(double start, int count)
        {
            int tick = Mathf.Max(0, (int)((Time.timeAsDouble - start) * KarmaBootstrap.FramesPerSecond));
            var drawings = RisingDrawings[count];
            return drawings[Mathf.Min(tick, drawings.Length - 1)];
        }
        static float RisingSample(double start) => Mathf.Max(0, Mathf.Floor(
            (float)(Time.timeAsDouble - start) * KarmaBootstrap.FramesPerSecond + 0.0001f)) * KarmaBootstrap.Step;

        internal void StartRisingBasicStrike() => ShowRisingBasicStrike(0);
        internal void StartRisingBasicFollowup() => ShowRisingBasicStrike(motionForm == "Rise" ? 2 : 1);
        void ShowRisingBasicStrike(int firstDrawing)
        {
            if (risingBasicStrike == null) return;
            risingStrikeAt = Time.timeAsDouble;
            BeginRisingDrawing("UPBasic", firstDrawing);
        }

        internal void ClearRisingBasicStrike() => risingStrikeAt = -1;

        void RisingGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            risingAirborne = airborne;
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
                if (action != "Telegraph") risingDescentPrepared = risingWavePrepared = false;
            }
            ApplyGuardianPose(pose);
            picture.transform.localRotation = Quaternion.identity;
            if (action == "Stunned" && displayMotion == "Stun" && displayForm == motionForm)
            {
                float step = KarmaBootstrap.Step;
                int last = motionArt.Frames(motionForm, "Stun").Length - 1;
                if (risingStunLeft <= last * step)
                    motionStarted = Time.time - (Mathf.Max(0, Mathf.CeilToInt(risingStunLeft / step) - 1) + 0.25f) * step;
                else
                    motionStarted = Mathf.Max(motionStarted, Time.time - (last + 0.5f) * step);
            }
            if (risingStrikeAt >= 0)
                picture.sprite = risingBasicStrike[RisingFrame(risingStrikeAt, risingBasicStrike.Length)];
            else if (risingDescentAt >= 0 && action == "Attack")
                picture.sprite = risingDescent[RisingFrame(risingDescentAt, RisingDropFrameCount)];
            else if (risingLandingAt >= 0 && attack != EnemyAttack.UP05)
                picture.sprite = risingDescent[RisingDropFrameCount + RisingFrame(risingLandingAt, LandingFrames)];
            else if (risingMotion != null)
                picture.sprite = risingMotion[RisingFrame(risingMotionAt, risingMotion.Length)];
        }
    }
}
