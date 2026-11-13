using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        const int RisingSpriteFps = 24;
        const int BasicStrikeFrames = 6;
        Sprite[] risingBasicStrike;
        double risingStrikeAt = -1;

        internal void ConfigureRisingPresentation()
        {
            // Base poses live in this boss's library; inherited bodies already supplied
            // their own poses and must keep those images.
            foreach (var sprite in KarmaArtwork.Sprites("Rise"))
                if (sprite != null && !poses.ContainsKey(sprite.name)) poses[sprite.name] = sprite;
            if (guardianAppearance == null)
            {
                var strike = Resources.LoadAll<Sprite>("RisingSlash/Forms/RiseBasicStrike");
                if (strike.Length == BasicStrikeFrames)
                {
                    System.Array.Sort(strike, (a, b) => string.CompareOrdinal(a.name, b.name));
                    risingBasicStrike = strike;
                }
            }
            guardianPose = RisingGuardianPose;
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
            bool basic = attack == EnemyAttack.GroundStrike && !airborne && guardianAppearance == null;
            if (!basic || (pose != "Slash" && pose != "Landing")) ClearRisingBasicStrike();
            if (basic && risingBasicStrike != null && risingStrikeAt < 0 && pose == "Slash") pose = "Dash";

            ApplyGuardianPose(pose);
            if (risingStrikeAt < 0) return;
            // The strike starts on the hit event. Recovery can hold the last cel,
            // and the next hit restarts the same clip without changing its hit window.
            int frame = Mathf.Min((int)((Time.timeAsDouble - risingStrikeAt) * RisingSpriteFps),
                BasicStrikeFrames - 1);
            picture.sprite = risingBasicStrike[frame];
        }
    }
}
