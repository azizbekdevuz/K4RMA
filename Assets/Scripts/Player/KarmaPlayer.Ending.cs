using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        float endingTargetX, endingSpeed;
        internal bool EndingWalking => game.Phase == RunPhase.Ending && endingSpeed > 0
            && Mathf.Abs(endingTargetX - Position.x) > 0.05f && Grounded();

        internal void BeginEndingControl()
        {
            ClearStatus(); techniques.Reset();
            move = jumpBufferedUntil = airSlashBufferedUntil = guardUntil = invulnerableUntil = 0;
            Crouching = false; endingSpeed = 0; endingTargetX = Position.x;
            body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            body.gravityScale = game.Config.gravityScale;
            aura.enabled = remnantHalo.enabled = false;
            art.gameObject.SetActive(true);
            animator.BeginEndingPoses("Idle", "Fall", "Walk", "Kneel", "StandFromKneel",
                "Guard", "Rising", "SwordWave", "Attack", "Salute", "Turn");
        }

        internal void EndingWalkTo(float x, float speed)
        {
            endingTargetX = Mathf.Clamp(x, KarmaStage.ArenaLeft + 2, KarmaStage.ArenaRight - 2);
            endingSpeed = Mathf.Max(0, speed);
        }

        void TickEndingMovement()
        {
            body.gravityScale = game.Config.gravityScale;
            float dx = endingTargetX - body.position.x;
            float speed = Grounded() && Mathf.Abs(dx) > 0.05f
                ? Mathf.Sign(dx) * Mathf.Min(endingSpeed, Mathf.Abs(dx) / Time.fixedDeltaTime) : 0;
            body.linearVelocity = new Vector2(speed, body.linearVelocity.y);
            if (speed != 0) FaceGuardian(endingTargetX);
        }

        internal void PresentEnding(float t)
        {
            string pose = "Idle";
            float since = t;
            bool loop = true;
            if (!Grounded()) { pose = "Fall"; loop = false; since = 0; }
            else if (EndingWalking)
            {
                pose = "Walk";
                since = t - (t < KarmaGame.EndingKneelAt ? KarmaGame.EndingApproachAt : KarmaGame.EndingDepartAt);
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
                else if (t >= KarmaGame.EndingUnionAt - KarmaGame.EndingRelease && t < KarmaGame.EndingUnionAt + 0.46f)
                { pose = "Attack"; since = t - (KarmaGame.EndingUnionAt - KarmaGame.EndingRelease); }
                else if (t >= 23.25f && t < 23.5f)
                { pose = "Turn"; since = t - 23.25f; }
                else if (t >= KarmaGame.EndingSaluteAt && t < KarmaGame.EndingSaluteAt + 0.8f)
                { pose = "Salute"; since = t - KarmaGame.EndingSaluteAt; }
                else if (t >= KarmaGame.EndingTurnAt && t < KarmaGame.EndingDepartAt)
                { pose = "Turn"; since = t - KarmaGame.EndingTurnAt; }

                loop = pose == "Idle";
                if (t >= KarmaGame.EndingKneelAt && t < KarmaGame.EndingGuardAt
                    || t >= 23.5f && t < KarmaGame.EndingDepartAt)
                    FaceGuardian(KarmaGame.EndingMasterX);
                else if (t >= KarmaGame.EndingStandAt)
                    FaceGuardian(KarmaStage.Gate.x);
            }

            float glow = KarmaGame.EndingEase(KarmaGame.EndingUnionAt, 22.3f, t)
                * (1 - KarmaGame.EndingEase(22.5f, 24f, t));
            animator.PresentEndingPose(pose, since, loop, glow);
        }

        internal void EndEndingPresentation() { animator?.EndEndingPoses(); }
    }
}
