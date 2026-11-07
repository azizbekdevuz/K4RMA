using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        string risingHold;
        float risingHoldUntil;
        internal void ConfigureRisingPresentation() { guardianPose = RisingGuardianPose; }
        public void HoldRisingPose(string pose, float seconds)
        {
            risingHold = pose;
            risingHoldUntil = Time.time + Mathf.Max(0.05f, seconds);
        }
        void RisingGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            string pose = Time.time < hurtUntil ? "Hurt"
                : action == "Stunned" ? "Hurt"
                : action == "Recover" ? "Landing"
                : action == "Attack" ? airborne ? "AirSlash" : attack == EnemyAttack.UP01 || attack == EnemyAttack.UP03 ? "Rising" : "Slash"
                : DefaultGuardianPose(action, airborne, armored);
            bool combatPose = action == "Attack" || action == "Telegraph";
            if (!combatPose || Time.time < hurtUntil) risingHoldUntil = 0f;
            else if (Time.time < risingHoldUntil && !string.IsNullOrEmpty(risingHold)) pose = risingHold;
            if (pose == "Move") pose = "Dash";
            ApplyGuardianPose(pose);
        }
    }
}
