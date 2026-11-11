using System;

namespace KarmaPrototype
{
    // SW03 only. Validate the union of both full-sized groups. A walking
    // escape completed before the first release survives their overlap too.
    internal static class KarmaSwordScarPlanner
    {
        public const int ScarCount = 16, GroupCount = 8, Rounds = 5;
        public const float Clearance = 0.55f, ReactionSeconds = 0.28f;
        public const float StaggerSeconds = 0.25f, MinimumWarning = 0.8f;
        public const int MaxAttempts = 48;
        public const float MinimumLayoutShift = 1.1f;
        // One isolated, consecutive cast sequence per process/domain. Battle
        // restarts/identical owner positions cannot reset it; Unity RNG is untouched.
        static int seedSequence = Guid.NewGuid().GetHashCode();
        public static int NextSeed()
        {
            // Positive 30-bit seeds avoid System.Random's +/-seed aliases and
            // int.MinValue/int.MaxValue alias at the signed counter boundary.
            return System.Threading.Interlocked.Increment(ref seedSequence) & 0x3fffffff;
        }

        public sealed class Plan
        {
            public readonly float[] Positions;
            public readonly int[] Groups;
            public readonly float SafeX;
            public readonly bool Fallback;
            public readonly int Strategy;
            public int CandidatesTried, SpacingCandidates, UnsafeCandidates, SimilarCandidates;
            public Plan(float[] positions, int[] groups, float safeX, bool fallback, int strategy)
            { Positions = positions; Groups = groups; SafeX = safeX; Fallback = fallback; Strategy = strategy; }
        }

        static float Clamp(float value, float low, float high)
        { return Math.Max(low, Math.Min(high, value)); }

        public static bool TryPlan(Random random, float left, float right, float width,
            float visualWidth, float playerX, float half, float velocity, float speed, float warning,
            float previousSafe, Func<float, bool> clearPath, out Plan plan, float[] previousPositions = null)
        {
            plan = null;
            if (random == null || right <= left || width <= 0 || half < 0 || speed < 0
                || warning < MinimumWarning || float.IsNaN(playerX) || float.IsNaN(velocity)) return false;
            float inset = half + Clearance;
            if (playerX < left + half || playerX > right - half || inset * 2 >= right - left) return false;
            float reach = speed * Math.Max(0, warning - ReactionSeconds);
            // A moving player may keep travelling during their reaction delay.
            // Require a route from that position as well as an immediate stop.
            float reactionX = Clamp(playerX + Clamp(velocity * ReactionSeconds, -6, 6), left + inset, right - inset);
            if (clearPath != null && !clearPath(reactionX))
            {
                // Resolve the wall-limited part of the same horizontal sweep.
                // Eight bounded probes never claim a route through an obstacle.
                float low = 0, high = 1;
                for (int i = 0; i < 8; i++)
                {
                    float fraction = (low + high) / 2;
                    if (clearPath(playerX + (reactionX - playerX) * fraction)) low = fraction; else high = fraction;
                }
                reactionX = playerX + (reactionX - playerX) * low;
            }
            // Even a wrong-way body cannot straddle two independent contacts.
            float spacing = Math.Max(width + Math.Max(0.75f, 2 * half + 0.1f), visualWidth + 0.15f);
            if ((ScarCount - 1) * spacing + width > right - left) return false;
            // Cap prediction in both time and distance, including during dashes.
            float drift = Clamp(velocity * 0.45f, -6, 6);
            float predicted = Clamp(playerX + drift, left + width / 2, right - width / 2);
            int roll = random.Next(100), strategy = roll < 35 ? 0 : roll < 70 ? 1 : 2;
            float best = float.NegativeInfinity;
            int spacingCandidates = 0, unsafeCandidates = 0, similarCandidates = 0;
            float safeLow = Math.Max(left + inset, Math.Max(playerX, reactionX) - reach);
            float safeHigh = Math.Min(right - inset, Math.Min(playerX, reactionX) + reach);
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                // Randomize the *space between* swords, including both arena
                // margins. No cells or quarter quotas: sparse weights alternate
                // tight, non-overlapping runs with larger readable escape gaps.
                var positions = safeHigh < safeLow ? null : Scatter(random, left, right, width, spacing,
                    safeLow + (safeHigh - safeLow) * (float)random.NextDouble(), inset);
                if (positions == null || !Spaced(positions, left, right, width, spacing))
                { spacingCandidates++; continue; }
                if (!Different(positions, previousPositions, left, right))
                { similarCandidates++; continue; }
                if (!TrySafePoint(positions, left, right, width, playerX, reactionX, inset, reach,
                    previousSafe, clearPath, out float safe)) { unsafeCandidates++; continue; }
                int target = 0;
                for (int i = 1; i < positions.Length; i++)
                    if (Math.Abs(positions[i] - predicted) < Math.Abs(positions[target] - predicted)) target = i;
                var groups = Groups(random);
                // Each adjacent pair contributes one sword to each group.
                // Escape pressure swaps only within a pair, preserving contact
                // sharing without fixing the count in any spatial quarter.
                if (strategy == 2 && groups[target] == 0)
                { groups[target] = 1; groups[target ^ 1] = 0; }
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < positions.Length; i++)
                    if (groups[i] == (strategy == 2 ? 1 : 0)) nearest = Math.Min(nearest, Math.Abs(positions[i] - predicted));
                float score = (strategy == 0 ? 0 : 3 - nearest)
                    + Math.Min(2, Math.Abs(safe - playerX))
                    + (float.IsNaN(previousSafe) ? 0 : Math.Min(2, Math.Abs(safe - previousSafe)));
                if (score <= best) continue;
                best = score; plan = new Plan(positions, groups, safe, false, strategy);
            }
            if (plan != null)
            {
                Diagnostics(plan, spacingCandidates, unsafeCandidates, similarCandidates);
                return true;
            }
            // Bounded deterministic fallback: reserve the whole body at a
            // reachable point, then pack sixteen spaced swords on either side.
            // Obstructed/no-space input fails safely instead of inventing a route.
            float fallbackSafe = Clamp(playerX, Math.Max(left + inset, reactionX - reach), Math.Min(right - inset, reactionX + reach));
            if (Math.Abs(fallbackSafe - playerX) > reach + 0.0001f
                || clearPath != null && !clearPath(fallbackSafe)) return false;
            float lo = left + width / 2, hi = right - width / 2;
            float before = Math.Min(hi, fallbackSafe - inset - width / 2);
            float after = Math.Max(lo, fallbackSafe + inset + width / 2);
            int leftCapacity = before < lo ? 0 : (int)Math.Floor((before - lo) / spacing) + 1;
            int rightCapacity = after > hi ? 0 : (int)Math.Floor((hi - after) / spacing) + 1;
            int nLeft = (int)Math.Round(ScarCount * (fallbackSafe - left) / (right - left));
            nLeft = Math.Max(Math.Max(0, ScarCount - rightCapacity), Math.Min(Math.Min(ScarCount, leftCapacity), nLeft));
            if (nLeft > leftCapacity || ScarCount - nLeft > rightCapacity) return false;
            var fallback = new float[ScarCount];
            Fill(fallback, 0, nLeft, lo, before);
            Fill(fallback, nLeft, ScarCount - nLeft, after, hi);
            if (!Spaced(fallback, left, right, width, spacing)) return false;
            // Never display a repeated fallback after a visible layout. A truly
            // constrained/blocked arena must fail safely before any telegraph.
            if (!Different(fallback, previousPositions, left, right)) return false;
            var fallbackGroups = new int[ScarCount];
            for (int i = 0; i < ScarCount; i++) fallbackGroups[i] = i % 2;
            plan = new Plan(fallback, fallbackGroups, fallbackSafe, true, strategy);
            Diagnostics(plan, spacingCandidates, unsafeCandidates, similarCandidates);
            return true;
        }

        static void Diagnostics(Plan plan, int spacing, int unsafeRoutes, int similar)
        {
            plan.CandidatesTried = MaxAttempts; plan.SpacingCandidates = spacing;
            plan.UnsafeCandidates = unsafeRoutes; plan.SimilarCandidates = similar;
        }
        static float[] Scatter(Random random, float left, float right, float width, float spacing, float safe, float inset)
        {
            float lo = left + width / 2, hi = right - width / 2;
            float before = Math.Min(hi, safe - inset - width / 2);
            float after = Math.Max(lo, safe + inset + width / 2);
            int leftCapacity = before < lo ? 0 : (int)Math.Floor((before - lo) / spacing) + 1;
            int rightCapacity = after > hi ? 0 : (int)Math.Floor((hi - after) / spacing) + 1;
            int minimum = Math.Max(0, ScarCount - rightCapacity), maximum = Math.Min(ScarCount, leftCapacity);
            if (minimum > maximum) return null;
            int nLeft = random.Next(minimum, maximum + 1);
            var positions = new float[ScarCount];
            // Reserve a random reachable pocket before scattering. The unchanged
            // union validator still proves an actual collision-free walking route.
            return ScatterSegment(random, positions, 0, nLeft, lo, before, spacing)
                && ScatterSegment(random, positions, nLeft, ScarCount - nLeft, after, hi, spacing) ? positions : null;
        }
        static bool ScatterSegment(Random random, float[] positions, int start, int count, float low, float high, float spacing)
        {
            if (count == 0) return true;
            float slack = Math.Max(0, high - low - (count - 1) * spacing), sum = 0;
            var gaps = new float[count + 1];
            for (int i = 0; i < gaps.Length; i++)
            {
                float sample = (float)random.NextDouble();
                gaps[i] = 0.02f + sample * sample * sample * sample; sum += gaps[i];
            }
            float cap = Math.Max(spacing, slack * 2 / gaps.Length);
            for (int i = 0; i < gaps.Length; i++)
            {
                gaps[i] *= slack / sum;
                if (gaps[i] > cap) return false;
            }
            float at = low + gaps[0];
            for (int i = 0; i < count; i++)
            { positions[start + i] = at; at += spacing + gaps[i + 1]; }
            return true;
        }
        static int Quarter(float x, float left, float right)
        { return (int)Clamp((x - left) * 4 / (right - left), 0, 3); }
        static bool Different(float[] positions, float[] previous, float left, float right)
        {
            if (previous == null || previous.Length != positions.Length) return true;
            float shift = 0; int moved = 0;
            var counts = new int[4];
            for (int i = 0; i < positions.Length; i++)
            {
                float delta = Math.Abs(positions[i] - previous[i]); shift += delta;
                if (delta >= 1) moved++;
                counts[Quarter(positions[i], left, right)]++;
                counts[Quarter(previous[i], left, right)]--;
            }
            bool regionalChange = false;
            foreach (int count in counts) regionalChange |= count != 0;
            return shift / positions.Length >= MinimumLayoutShift && moved >= ScarCount / 2 && regionalChange;
        }

        static void Fill(float[] positions, int start, int count, float low, float high)
        {
            for (int i = 0; i < count; i++) positions[start + i] = count == 1 ? (low + high) / 2 : low + (high - low) * i / (count - 1);
        }
        static int[] Groups(Random random)
        {
            var groups = new int[ScarCount];
            for (int i = 0; i < ScarCount; i += 2)
            { groups[i] = random.Next(2); groups[i + 1] = 1 - groups[i]; }
            return groups;
        }
        static bool Spaced(float[] positions, float left, float right, float width, float spacing)
        {
            for (int i = 0; i < positions.Length; i++)
                if (positions[i] - width / 2 < left - 0.0001f || positions[i] + width / 2 > right + 0.0001f
                    || i > 0 && positions[i] - positions[i - 1] < spacing - 0.0001f) return false;
            return true;
        }
        static bool TrySafePoint(float[] positions, float left, float right, float width, float playerX,
            float reactionX, float inset, float reach, float previousSafe, Func<float, bool> clearPath, out float safe)
        {
            float cursor = left + inset, limit = right - inset, best = float.PositiveInfinity;
            safe = playerX;
            for (int i = 0; i <= positions.Length; i++)
            {
                float end = i == positions.Length ? limit : Math.Min(limit, positions[i] - width / 2 - inset);
                float reachableStart = Math.Max(cursor, Math.Max(playerX, reactionX) - reach);
                float reachableEnd = Math.Min(end, Math.Min(playerX, reactionX) + reach);
                if (reachableEnd >= reachableStart)
                {
                    float target = Clamp(playerX, reachableStart, reachableEnd), distance = Math.Abs(target - playerX);
                    if (distance <= reach + 0.0001f && (clearPath == null || clearPath(target)))
                    {
                        float score = distance + (!float.IsNaN(previousSafe) && Math.Abs(target - previousSafe) < 1 ? 1.5f : 0);
                        if (score < best) { best = score; safe = target; }
                    }
                }
                if (i < positions.Length) cursor = Math.Max(cursor, positions[i] + width / 2 + inset);
            }
            return !float.IsPositiveInfinity(best);
        }
    }
}
