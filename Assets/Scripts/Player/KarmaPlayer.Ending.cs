using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        const float EndingAcceleration = 15f;
        float endingTargetX, endingSpeed, endingWalkTime;
        internal bool EndingWalking => game.Phase == RunPhase.Ending && endingSpeed > 0
            && Mathf.Abs(body.linearVelocity.x) > 0.12f && Grounded();

        internal void BeginEndingControl()
        {
            ClearStatus(); techniques.Reset();
            move = jumpBufferedUntil = airSlashBufferedUntil = guardUntil = invulnerableUntil = 0;
            Crouching = false; endingSpeed = endingWalkTime = 0; endingTargetX = Position.x;
            body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            body.gravityScale = game.Config.gravityScale;
            aura.enabled = remnantHalo.enabled = false;
            art.gameObject.SetActive(true);
            animator.BeginEndingPoses("Idle", "Fall", "Walk", "Kneel", "StandFromKneel",
                "Guard", "Rising", "SwordWave", "Attack", "Recovery", "Salute", "Turn");
        }

        internal void EndingWalkTo(float x, float speed)
        {
            endingTargetX = Mathf.Clamp(x, KarmaStage.ArenaLeft + 2, KarmaStage.ArenaRight - 2);
            endingSpeed = Mathf.Max(0, speed);
            if (endingSpeed == 0) endingWalkTime = 0;
        }

        void TickEndingMovement()
        {
            body.gravityScale = game.Config.gravityScale;
            float dx = endingTargetX - body.position.x;
            float speed = 0;
            if (endingSpeed > 0 && Grounded() && Mathf.Abs(dx) > 0.04f)
            {
                float step = Time.fixedDeltaTime;
                float brakingSpeed = Mathf.Sqrt(2 * EndingAcceleration * Mathf.Abs(dx));
                float desired = Mathf.Sign(dx) * Mathf.Min(endingSpeed, brakingSpeed);
                speed = Mathf.MoveTowards(body.linearVelocity.x, desired, EndingAcceleration * step);
                speed = Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(speed), Mathf.Abs(dx) / step);
                endingWalkTime += Mathf.Abs(speed) / 4.8f * step;
            }
            body.linearVelocity = new Vector2(speed, body.linearVelocity.y);
            if (speed != 0) FaceGuardian(endingTargetX);
        }

        internal void PresentEnding(float t)
        {
            float departureOffset = game.EndingDepartureTimeOffset;
            string pose = "Idle";
            float since = t;
            bool loop = true;
            if (!Grounded()) { pose = "Fall"; loop = false; since = 0; }
            else if (EndingWalking)
            {
                pose = "Walk";
                since = endingWalkTime;
            }
            else
            {
                if (t >= KarmaGame.EndingKneelAt && t < KarmaGame.EndingStandAt)
                { pose = "Kneel"; since = t - KarmaGame.EndingKneelAt; }
                else if (t >= KarmaGame.EndingStandAt && t < KarmaGame.EndingGuardAt - 0.25f)
                { pose = "StandFromKneel"; since = t - KarmaGame.EndingStandAt; }
                else if (t >= KarmaGame.EndingGuardAt - 0.25f && t < KarmaGame.EndingGuardAt)
                { pose = "Turn"; since = t - (KarmaGame.EndingGuardAt - 0.25f); }
                else if (t >= KarmaGame.EndingGuardAt && t < KarmaGame.EndingGuardAt + 0.72f)
                { pose = "Guard"; since = t - KarmaGame.EndingGuardAt; }
                else if (t >= KarmaGame.EndingLiftAt && t < KarmaGame.EndingLiftAt + 0.44f)
                { pose = "Rising"; since = t - KarmaGame.EndingLiftAt; }
                else if (t >= KarmaGame.EndingWaveAt && t < KarmaGame.EndingWaveAt + 0.45f)
                { pose = "SwordWave"; since = t - KarmaGame.EndingWaveAt; }
                else if (t >= KarmaGame.EndingDrawAt && t < KarmaGame.EndingUnionAt)
                { pose = "Draw"; since = (t - KarmaGame.EndingDrawAt) / (KarmaGame.EndingUnionAt - KarmaGame.EndingDrawAt); }
                else if (t >= KarmaGame.EndingUnionAt && t < KarmaGame.EndingSheatheAt)
                { pose = "Attack"; since = t - KarmaGame.EndingUnionAt; }
                else if (t >= KarmaGame.EndingSheatheAt && t < KarmaGame.EndingSheatheAt + KarmaGame.EndingSheatheDuration)
                { pose = "Sheathe"; since = (t - KarmaGame.EndingSheatheAt) / KarmaGame.EndingSheatheDuration; }
                else if (t >= KarmaGame.EndingTurnToMasterAt && t < KarmaGame.EndingTurnToMasterAt + 0.25f)
                { pose = "Turn"; since = t - KarmaGame.EndingTurnToMasterAt; }
                else if (t >= KarmaGame.EndingSaluteAt + departureOffset && t < KarmaGame.EndingSaluteAt + departureOffset + 0.8f)
                { pose = "Salute"; since = t - (KarmaGame.EndingSaluteAt + departureOffset); }
                else if (t >= KarmaGame.EndingTurnAt + departureOffset && t < KarmaGame.EndingDepartAt + departureOffset)
                { pose = "Turn"; since = t - (KarmaGame.EndingTurnAt + departureOffset); }

                loop = pose == "Idle";
                if (t >= KarmaGame.EndingKneelAt && t < KarmaGame.EndingGuardAt
                    || t >= KarmaGame.EndingTurnToMasterAt + 0.25f && t < KarmaGame.EndingDepartAt + departureOffset)
                    FaceGuardian(KarmaGame.EndingMasterX);
                else if (t >= KarmaGame.EndingStandAt)
                    FaceGuardian(KarmaStage.Gate.x);
            }

            float glow = KarmaGame.EndingEase(KarmaGame.EndingUnionAt, KarmaGame.EndingUnionAt + 0.7f, t)
                * (1 - KarmaGame.EndingEase(KarmaGame.EndingUnionAt + 0.9f, KarmaGame.EndingUnionAt + 2.1f, t));
            animator.PresentEndingPose(pose, since, loop, Mathf.Max(glow, game.EndingImpactLight * 0.9f));
        }

        internal void EndEndingPresentation() { animator?.EndEndingPoses(); }
    }
}
