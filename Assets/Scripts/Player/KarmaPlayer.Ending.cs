using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        const float EndingAcceleration = 15f;
        float endingTargetX, endingSpeed, endingWalkTime;
        Vector3 endingArtHome;
        internal float EndingPierceStartX { get; private set; }
        internal float EndingAirHeight { get; private set; }
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
            endingArtHome = art.localPosition;
            EndingAirHeight = 0;
            animator.BeginEndingPoses("Idle", "Fall", "Walk", "Kneel", "StandFromKneel",
                "Parry", "Riposte", "Jump", "AirDiagonal", "Land", "Pierce", "Attack", "Recovery", "Salute", "Turn");
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

        internal void EndingPierce()
        {
            EndingPierceStartX = Position.x;
            // Match the personalized move's change of position during the non-combat ending.
            var destination = new Vector2(Mathf.Min(EndingPierceStartX + 3.4f, KarmaStage.ArenaRight - 2), body.position.y);
            body.position = destination;
            transform.position = destination;
        }

        internal void PresentEnding(float t)
        {
            float launch = KarmaGame.EndingEase(KarmaGame.EndingAirJumpAt, KarmaGame.EndingAirSlashAt, t);
            float flight = Mathf.InverseLerp(KarmaGame.EndingAirSlashAt, KarmaGame.EndingAirLandAt, t);
            // The first jump leads into a second impulse during the directional slash.
            EndingAirHeight = 0.95f * launch * (1 - flight) + 5.8f * flight * (1 - flight);
            art.localPosition = endingArtHome + Vector3.up * EndingAirHeight;
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
                else if (t >= KarmaGame.EndingStandAt && t < KarmaGame.EndingCounterAt - 0.25f)
                { pose = "StandFromKneel"; since = t - KarmaGame.EndingStandAt; }
                else if (t >= KarmaGame.EndingCounterAt - 0.25f && t < KarmaGame.EndingCounterAt)
                { pose = "Turn"; since = t - (KarmaGame.EndingCounterAt - 0.25f); }
                else if (t >= KarmaGame.EndingCounterAt && t < KarmaGame.EndingCounterAt + KarmaGame.EndingRelease)
                { pose = "Parry"; since = t - KarmaGame.EndingCounterAt; }
                else if (t >= KarmaGame.EndingCounterAt + KarmaGame.EndingRelease && t < KarmaGame.EndingCounterAt + 0.52f)
                { pose = "Riposte"; since = t - (KarmaGame.EndingCounterAt + KarmaGame.EndingRelease); }
                else if (t >= KarmaGame.EndingAirJumpAt && t < KarmaGame.EndingAirSlashAt)
                { pose = "Jump"; since = t - KarmaGame.EndingAirJumpAt; }
                else if (t >= KarmaGame.EndingAirSlashAt && t < KarmaGame.EndingAirSlashAt + 0.32f)
                { pose = "AirDiagonal"; since = t - KarmaGame.EndingAirSlashAt; }
                else if (t >= KarmaGame.EndingAirSlashAt + 0.32f && t < KarmaGame.EndingAirLandAt)
                { pose = "Fall"; since = t - (KarmaGame.EndingAirSlashAt + 0.32f); }
                else if (t >= KarmaGame.EndingAirLandAt && t < KarmaGame.EndingPierceAt)
                { pose = "Land"; since = t - KarmaGame.EndingAirLandAt; }
                else if (t >= KarmaGame.EndingPierceAt && t < KarmaGame.EndingDrawShotAt)
                { pose = "Pierce"; since = t - KarmaGame.EndingPierceAt; }
                else if (t >= KarmaGame.EndingDrawAt && t < KarmaGame.EndingUnionAt)
                { pose = "Draw"; since = (t - KarmaGame.EndingDrawAt) / (KarmaGame.EndingUnionAt - KarmaGame.EndingDrawAt); }
                else if (t >= KarmaGame.EndingUnionAt && t < KarmaGame.EndingSheatheAt)
                { pose = "Attack"; since = t - KarmaGame.EndingUnionAt; }
                else if (t >= KarmaGame.EndingSheatheAt && t < KarmaGame.EndingSheatheAt + KarmaGame.EndingSheatheDuration)
                { pose = "Sheathe"; since = (t - KarmaGame.EndingSheatheAt) / KarmaGame.EndingSheatheDuration; }
                else if (t >= KarmaGame.EndingTurnToMasterAt && t < KarmaGame.EndingTurnToMasterAt + 0.25f)
                { pose = "Turn"; since = t - KarmaGame.EndingTurnToMasterAt; }
                else if (t >= KarmaGame.EndingSaluteAt && t < KarmaGame.EndingSaluteAt + 0.8f)
                { pose = "Salute"; since = t - (KarmaGame.EndingSaluteAt); }
                else if (t >= KarmaGame.EndingTurnAt && t < KarmaGame.EndingDepartAt)
                { pose = "Turn"; since = t - (KarmaGame.EndingTurnAt); }

                loop = pose == "Idle";
                if (t >= KarmaGame.EndingKneelAt && t < KarmaGame.EndingCounterAt
                    || t >= KarmaGame.EndingTurnToMasterAt + 0.25f && t < KarmaGame.EndingDepartAt)
                    FaceGuardian(KarmaGame.EndingMasterX);
                else if (t >= KarmaGame.EndingStandAt)
                    FaceGuardian(KarmaStage.Gate.x);
            }

            float glow = KarmaGame.EndingEase(KarmaGame.EndingUnionAt, KarmaGame.EndingUnionAt + 0.7f, t)
                * (1 - KarmaGame.EndingEase(KarmaGame.EndingUnionAt + 0.9f, KarmaGame.EndingUnionAt + 2.1f, t));
            animator.PresentEndingPose(pose, since, loop, Mathf.Max(glow, game.EndingImpactLight * 0.9f));
        }

        internal void EndEndingPresentation()
        {
            if (game != null && art != null) art.localPosition -= Vector3.up * EndingAirHeight;
            EndingAirHeight = 0;
            animator?.EndEndingPoses();
        }
    }
}
