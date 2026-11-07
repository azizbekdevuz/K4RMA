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

    // 0 stays the approved advancing trio. 1 is the dash-first candidate kept for comparison.
    public enum RisingBasicVariant
    {
        AdvancingThreeHit,
        DashTwoCut
    }

    public enum RisingTrailStyle
    {
        Ribbon,
        Silhouette
    }

    public static class RisingSlashRules
    {
        public const int BasicStrikeCount = 3;
        // Gap between active windows. Zero would let one overlapping attack count as two hits.
        public const float BasicGapSeconds = 0.06f;
        public const float BasicRecoverySeconds = 0.55f;
        // Three shares replace the old single melee hit. 0.34 + 0.34 + 0.42 = 1.10, not 3.00.
        public const float BasicDamageBudget = 1.10f;

        // Forward steps during the three committed strikes. Facing does not change between them.
        public static float BasicAdvance(int index)
        {
            if (index <= 0) return 0.42f;
            return index == 1 ? 0.55f : 0.72f;
        }

        public const float DashTwoCutGapSeconds = 0.08f;
        // Second cut only, and only after Guard is inherited. 0.48 + 0.62 * 1.06 stays under +10%.
        public const float GuardSecondCutAccent = 1.06f;

        public static int ComboCount(RisingBasicVariant variant)
        { return variant == RisingBasicVariant.DashTwoCut ? 2 : BasicStrikeCount; }

        public static float ComboAdvance(RisingBasicVariant variant, int index)
        {
            if (variant != RisingBasicVariant.DashTwoCut) return BasicAdvance(index);
            return index <= 0 ? 0.28f : 0.46f;
        }

        public static RisingBasicStrike ComboStrike(RisingBasicVariant variant, int index)
        { return variant == RisingBasicVariant.DashTwoCut ? DashCut(index) : BasicStrike(index); }

        static RisingBasicStrike DashCut(int index)
        {
            if (index <= 0) return Strike(0, 0.11f, 0.48f, 1.25f, 0.55f, 2.40f, 1.15f, "SlashLow");
            return Strike(1, 0.14f, 0.62f, 1.55f, 1.05f, 2.90f, 1.85f, "SlashFinish");
        }

        public static EnemyAttack ContextualPreference(float distance, float meleeReach, bool airborne, bool retreating, bool nearWall)
        {
            if (nearWall && distance > meleeReach * 0.55f) return EnemyAttack.UP01;
            if (airborne) return EnemyAttack.UP02;
            if (distance <= meleeReach * 0.72f) return EnemyAttack.GroundStrike;
            if (retreating) return EnemyAttack.UP01;
            return EnemyAttack.UP02;
        }

        public static float InterceptLead(float velocityX, float maximum)
        {
            if (velocityX > maximum) return maximum;
            if (velocityX < -maximum) return -maximum;
            return velocityX;
        }

        public static RisingBasicStrike BasicStrike(int index)
        {
            switch (index)
            {
                case 0: return Strike(0, 0.10f, 0.34f, 1.35f, 1.05f, 2.55f, 1.20f, "SlashHigh");
                case 1: return Strike(1, 0.10f, 0.34f, 1.55f, 0.42f, 2.75f, 1.05f, "SlashLow");
                default: return Strike(2, 0.12f, 0.42f, 1.75f, 0.72f, 3.20f, 1.70f, "SlashFinish");
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
        public const float Up04AscentSeconds = 0.48f;
        public const float Up04DescentSeconds = 0.22f;
        public const float Up04ComparableRiseRatio = 0.85f;
        public const bool FragmentParryStun = true;
        public static readonly float[] FragmentDegrees = { -6f, 12f, 30f, 48f, 66f, 84f, 102f, 120f, 138f, 156f, 174f, 192f, 208f, 222f };

        public struct FragmentAim
        {
            public float EndX, EndY, VelocityX, VelocityY;
        }

        public static FragmentAim AimFragment(float originX, float originY, float degrees, float life,
            float arenaLeft, float arenaRight, float wallInset, float maxRange, float ceilingY, float floorY, float minRange, float minimumLife)
        {
            double rad = degrees * Math.PI / 180.0;
            double dx = Math.Cos(rad), dy = Math.Sin(rad);
            double magnitude = Math.Sqrt(dx * dx + dy * dy);
            if (magnitude < 0.0001) { dx = 1.0; dy = 0.0; magnitude = 1.0; }
            dx /= magnitude; dy /= magnitude;
            double limit = maxRange;
            if (Math.Abs(dx) > 0.02)
            {
                double wall = dx > 0.0 ? arenaRight - wallInset : arenaLeft + wallInset;
                limit = Math.Min(limit, (wall - originX) / dx);
            }
            if (dy > 0.02) limit = Math.Min(limit, (ceilingY - originY) / dy);
            else if (dy < -0.02) limit = Math.Min(limit, (floorY - originY) / dy);
            if (limit < minRange) limit = minRange;
            float span = life < minimumLife ? minimumLife : life;
            float distance = (float)limit;
            return new FragmentAim
            {
                EndX = originX + (float)dx * distance,
                EndY = originY + (float)dy * distance,
                VelocityX = (float)dx * (distance / span),
                VelocityY = (float)dy * (distance / span)
            };
        }

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
