using System;

namespace KarmaPrototype
{
    // Unity-free Rising Slash contracts. Patterns call these so tests and Play Mode share one definition.
    public struct RisingBasicStrike
    {
        public int Index;
        public float ActiveSeconds;
        public float DamageShare;
        public float OffsetX;
        public float OffsetY;
        public float SizeX;
        public float SizeY;
        public string SpritePose;
    }

    public static class RisingSlashRules
    {
        public const int BasicStrikeCount = 3;
        // Gap between active windows. Zero would let one overlapping attack count as two hits.
        public const float BasicGapSeconds = 0.06f;
        public const float BasicRecoverySeconds = 0.55f;
        // Three shares replace the old single melee hit. 0.34 + 0.34 + 0.42 = 1.10, not 3.00.
        public const float BasicDamageBudget = 1.10f;

        public static RisingBasicStrike BasicStrike(int index)
        {
            switch (index)
            {
                case 0: return Strike(0, 0.10f, 0.34f, 1.35f, 1.05f, 2.55f, 1.20f, "Slash");
                case 1: return Strike(1, 0.10f, 0.34f, 1.55f, 0.42f, 2.75f, 1.05f, "SlashEcho");
                default: return Strike(2, 0.12f, 0.42f, 1.75f, 0.72f, 3.20f, 1.70f, "Slash");
            }
        }

        public static float BasicShareTotal()
        {
            float sum = 0f;
            for (int i = 0; i < BasicStrikeCount; i++) sum += BasicStrike(i).DamageShare;
            return sum;
        }

        public static void BasicStrikeBox(float originX, float groundY, float direction, float scale, RisingBasicStrike strike,
            out float centerX, out float centerY, out float sizeX, out float sizeY)
        {
            float face = direction < 0f ? -1f : 1f;
            centerX = originX + face * strike.OffsetX * scale;
            centerY = groundY + strike.OffsetY * scale;
            sizeX = strike.SizeX * scale;
            sizeY = strike.SizeY * scale;
        }

        // One ledger per strike. Reusing a contact would collapse the combo into a single hit.
        public static KarmaAttackContact NewStrikeContact() { return new KarmaAttackContact(); }

        static RisingBasicStrike Strike(int index, float active, float share, float offsetX, float offsetY, float sizeX, float sizeY, string pose)
        {
            return new RisingBasicStrike
            {
                Index = index,
                ActiveSeconds = active,
                DamageShare = share,
                OffsetX = offsetX,
                OffsetY = offsetY,
                SizeX = sizeX,
                SizeY = sizeY,
                SpritePose = pose
            };
        }
    }
}
