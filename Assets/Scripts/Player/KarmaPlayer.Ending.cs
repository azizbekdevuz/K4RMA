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
        internal void PresentEnding(float seconds)
        {
            string pose;
            float clipTime = seconds;
            if (!Grounded()) pose = "Fall";
            else if (EndingWalking) pose = animator.UsesNativePlayer24 ? "Walk" : "Run";
            else if (seconds >= 9.5f && seconds < 17.5f || seconds >= 21 && seconds < 30.5f) pose = "Crouch";
            else if (seconds >= 17.5f && seconds < 20) pose = "Guard";
            else if (seconds >= 30.5f && seconds < 31.2f) { pose = "Recovery"; clipTime = seconds - 30.5f; }
            else if (seconds >= 32.5f && seconds < 34.5f) { pose = "Rising"; clipTime = seconds - 32.5f; }
            else pose = "Idle";
            if (pose == "Run") clipTime *= 0.55f;
            if (animator.UsesNativePlayer24 && Grounded())
            {
                if (EndingWalking) { pose = "Walk"; clipTime = seconds - (seconds < 20 ? 5.5f : 34.5f); }
                else if (seconds >= 9.5f && seconds < 17.5f) { pose = "Kneel"; clipTime = seconds - 9.5f; }
                else if (seconds >= 17.5f && seconds < 20) { pose = "StandFromKneel"; clipTime = seconds - 17.5f; }
                else if (seconds >= 20 && seconds < 30) { pose = "Kneel"; clipTime = seconds - 20; }
                else if (seconds >= 30 && seconds < 31.2f) { pose = "StandFromKneel"; clipTime = seconds - 30; }
                else if (seconds >= 32.5f && seconds < 34.5f) { pose = "Salute"; clipTime = seconds - 32.5f; }
                else { pose = "Idle"; clipTime = seconds; }
            }
            float glow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17.5f, 19.3f, seconds))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(20, 21, seconds)));
            glow = Mathf.Max(glow, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(32.5f, 32.9f, seconds))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(33.3f, 34.5f, seconds))));
            animator.PresentEndingPose(pose, clipTime, pose == "Run" || pose == "Walk" || pose == "Idle", glow);
        }
        internal void EndReviewPresentation() { animator.EndEndingReviewPose(); }
        internal void PresentEndingAlternative(float t)
        {
            string pose = "Idle"; float since = t; bool loop = true;
            if (!Grounded()) pose = "Fall";
            else if (EndingWalking) pose = "Walk";
            else if (game.EndingVersion == KarmaEndingVersion.Legacy)
            {
                if (t >= 6 && t < 6.75f) { pose = "SwordWave"; since = t - 6; }
                else if (t >= 8.7f && t < 9.6f) { pose = "Rising"; since = t - 8.7f; }
                else if (t >= 11.2f && t < 12.3f) { pose = "Guard"; since = t - 11.2f; }
                else if (t >= 15.5f && t < 16.4f) { pose = "Salute"; since = t - 15.5f; }
                else if (t >= 18 && t < 21) { pose = "Kneel"; since = t - 18; }
                else if (t >= 21 && t < 22) { pose = "StandFromKneel"; since = t - 21; }
                loop = pose == "Idle";
            }
            else if (game.EndingVersion == KarmaEndingVersion.Cycle)
            {
                if (t >= 8 && t < 12) { pose = "Kneel"; since = t - 8; }
                else if (t >= 12 && t < 13.5f) { pose = "StandFromKneel"; since = t - 12; }
                else if (t >= 13.5f && t < 14.5f) { pose = "Salute"; since = t - 13.5f; }
                loop = pose == "Idle";
            }
            else
            {
                if (t >= 6.5f && t < 7) { pose = "Salute"; since = t - 6.5f; }
                else if (t >= 11.6f && t < 12.2f) { pose = "Turn"; since = t - 11.6f; }
                else if (t >= 14.1f && t < 17) { pose = "Guard"; since = t - 14.1f; }
                else if (t >= 17 && t < 18.5f) { pose = "Recovery"; since = t - 17; }
                else if (t >= 20 && t < 20.8f) { pose = "Attack"; since = t - 20; }
                loop = pose == "Idle";
            }
            if (!EndingWalking)
            {
                Facing = game.EndingVersion == KarmaEndingVersion.Lesson && t >= 12
                    || game.EndingVersion == KarmaEndingVersion.Legacy && t >= 17 && t < 22.5f ? -1 : 1;
                art.localScale = new Vector3(Facing, 1, 1);
            }
            animator.PresentEndingReviewPose(pose, since, loop);
        }
    }
}
