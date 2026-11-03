using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        internal void ConfigureRisingPresentation() { guardianPose = RisingGuardianPose; }
        void RisingGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            string pose = Time.time < hurtUntil ? "Hurt"
                : action == "Stunned" ? "Hurt"
                : action == "Recover" ? "Landing"
                : action == "Attack" ? airborne ? "AirSlash" : attack == EnemyAttack.UP01 || attack == EnemyAttack.UP03 ? "Rising" : "Slash"
                : DefaultGuardianPose(action, airborne, armored);
            if (pose == "Move") pose = "Dash";
            ApplyGuardianPose(pose);
        }
    }
}
