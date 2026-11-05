using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        internal void ConfigureRisingPresentation()
        {
            // Base poses live in this boss's library; inherited bodies already supplied
            // their own poses and must keep those images.
            foreach (var sprite in KarmaArtwork.Sprites("Rise"))
                if (sprite != null && !poses.ContainsKey(sprite.name)) poses[sprite.name] = sprite;
            guardianPose = RisingGuardianPose;
        }
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
