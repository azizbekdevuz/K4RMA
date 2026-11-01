using System;
using System.Collections.Generic;

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

        public const float Up01WindupSeconds = 0.36f;
        public const float Up01AscentSeconds = 0.32f;
        public const float Up01TrackHoverSeconds = 0.40f;
        public const float Up01LockWarningSeconds = 0.70f;
        public const float Up01DropSeconds = 0.26f;
        // Arena inset used by the previous UP01 clamp. It is already in world units.
        public const float Up01EdgeMargin = 4f;

        public static float Up01TrackingSeconds { get { return Up01AscentSeconds + Up01TrackHoverSeconds; } }

        public struct RisingLandingPlan
        {
            public float LandingX, LandingY, ImpactX, ImpactY, AreaWidth, AreaHeight;
        }

        public static float PursuitRiseAboveGround(float riseHeight, float minimum, float maximum)
        { return Clamp(riseHeight, minimum, maximum); }

        public static float AscentBlend(float elapsed, float duration)
        { return duration <= 0f ? 1f : Clamp(elapsed / duration, 0f, 1f); }

        public static float AdvanceUnpaused(float elapsed, float deltaTime, bool paused)
        { return paused || deltaTime <= 0f ? elapsed : elapsed + deltaTime; }

        public static float ClampLandingX(float playerX, float apexX, float arenaLeft, float arenaRight, float edgeMargin, float apexReach)
        {
            float x = Clamp(playerX, arenaLeft + edgeMargin, arenaRight - edgeMargin);
            return Clamp(x, apexX - apexReach, apexX + apexReach);
        }

        // The returned plan is a value. Callers must keep it and stop sampling the player.
        public static RisingLandingPlan LockLanding(float playerX, float apexX, float groundY, float impactY, float areaWidth, float areaHeight,
            float arenaLeft, float arenaRight, float edgeMargin, float apexReach)
        {
            float x = ClampLandingX(playerX, apexX, arenaLeft, arenaRight, edgeMargin, apexReach);
            return new RisingLandingPlan
            {
                LandingX = x,
                LandingY = groundY,
                ImpactX = x,
                ImpactY = impactY,
                AreaWidth = areaWidth,
                AreaHeight = areaHeight
            };
        }

        // Fully eligible cycle repeats UP01, UP02, UP01, UP02, then one each of the other Rising attacks.
        // Skipped slots are not retried forever: one decision inspects at most one full schedule.
        public static readonly EnemyAttack[] PursuitSchedule =
        {
            EnemyAttack.UP01, EnemyAttack.UP02, EnemyAttack.UP01, EnemyAttack.UP02,
            EnemyAttack.GroundStrike, EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05
        };
        public const int PursuitRepeats = 2;
        public const int OtherRepeats = 1;

        public static bool TrySelect(IReadOnlyList<EnemyAttack> cycle, Func<EnemyAttack, bool> eligible, ref int cursor, out EnemyAttack attack)
        {
            attack = default(EnemyAttack);
            if (cycle == null || cycle.Count == 0 || PursuitSchedule.Length == 0) return false;
            int span = PursuitSchedule.Length;
            if (cursor < 0) cursor = 0;
            cursor %= span;
            for (int i = 0; i < span; i++)
            {
                EnemyAttack candidate = PursuitSchedule[cursor];
                cursor = (cursor + 1) % span;
                if (!CycleContains(cycle, candidate)) continue;
                if (eligible != null && !eligible(candidate)) continue;
                attack = candidate;
                return true;
            }
            return false;
        }

        static bool CycleContains(IReadOnlyList<EnemyAttack> cycle, EnemyAttack attack)
        {
            for (int i = 0; i < cycle.Count; i++) if (cycle[i] == attack) return true;
            return false;
        }

        static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

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
