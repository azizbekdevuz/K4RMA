using System;
using System.Collections.Generic;

namespace KarmaPrototype
{
    // SW03 release clock, independent of component update order. Frame/delta
    // inputs keep pause and same-frame sampling testable without an Editor.
    public sealed class KarmaSwordScarClock
    {
        int frame;
        float elapsed;
        public KarmaSwordScarClock(int creationFrame) { frame = creationFrame; }
        public float Sample(int currentFrame, float delta, bool paused)
        {
            if (frame != currentFrame)
            {
                frame = currentFrame;
                if (!paused) elapsed += delta;
            }
            return elapsed;
        }
    }

    // SW03 only: reserve safe corridors, then fill the remaining danger regions.
    internal static class KarmaSwordScarPlanner
    {
        public const int MaxSwords = 26, Rounds = 5, MaxAttempts = 48;
        public const float Clearance = 0.25f, ReactionSeconds = 0.28f;
        public const float MinimumWarning = 0.8f;
        public const float MinimumLayoutShift = 0.75f;
        static readonly int[] ClusterChoices = { 1, 3, 4 };
        // v7 alpha >=128, bottom-up rows 350..1300: opaque blade body <=264px
        // of the 704px fit. Crossguards/glow may overlap, blade bodies stay apart.
        public static float InternalSpacing(float visualWidth) { return visualWidth * (264f / 704f) + 0.15f; }
        static int seedSequence = Guid.NewGuid().GetHashCode();
        public static int NextSeed() { return System.Threading.Interlocked.Increment(ref seedSequence) & 0x3fffffff; }
        public struct Corridor
        {
            public float Left, Right, CenterMin, CenterMax;
            public float Width { get { return Right - Left; } }
            public float Center { get { return (Left + Right) / 2; } }
        }
        public sealed class Plan
        {
            public float[] Positions;
            public int[] ClusterSizes, SwordClusters, ContactIndices;
            public Corridor[] Corridors;
            public float SafeX, SafeClearance;
            public int SafeOptions;
            public bool Fallback;
            public int CandidatesTried, SpacingCandidates, UnsafeCandidates, SimilarCandidates;
            public int ContactCount { get { return ClusterSizes.Length; } }

        }
        struct Region { public float Left, Right; public Region(float left, float right) { Left = left; Right = right; } }
        static float Clamp(float value, float low, float high) { return Math.Max(low, Math.Min(high, value)); }
        static float Range(Random random, float low, float high) { return low + (high - low) * (float)random.NextDouble(); }
        static float ClearLimit(float from, float target, Func<float, bool> clear)
        {
            if (clear == null || clear(target)) return target;
            float low = 0, high = 1;
            for (int i = 0; i < 12; i++)
            { float t = (low + high) / 2; if (clear(from + (target - from) * t)) low = t; else high = t; }
            return from + (target - from) * low;
        }
        public static bool TryPlan(Random random, float left, float right, float width,
            float visualWidth, float playerX, float half, float velocity, float speed, float warning,
            float previousSafe, Func<float, bool> clearPath, out Plan plan, float[] previousPositions = null)
        {
            plan = null;
            if (random == null || right <= left || width <= 0 || visualWidth <= 0 || half < 0 || speed < 0
                || warning < MinimumWarning || float.IsNaN(playerX) || float.IsNaN(velocity)) return false;
            float inset = half + Clearance, corridorWidth = Math.Max(4 * half, 2 * inset + 0.4f);
            if (playerX < left + half || playerX > right - half || corridorWidth >= right - left
                || clearPath != null && !clearPath(playerX)) return false;
            float reach = speed * Math.Max(0, warning - ReactionSeconds);
            float reaction = ClearLimit(playerX, Clamp(playerX + Clamp(velocity * ReactionSeconds, -6, 6), left + half, right - half), clearPath);
            float low = Math.Max(left + inset, Math.Max(playerX, reaction) - reach);
            float high = Math.Min(right - inset, Math.Min(playerX, reaction) + reach);
            low = Math.Max(low, ClearLimit(playerX, low, clearPath));
            high = Math.Min(high, ClearLimit(playerX, high, clearPath));
            if (low > high) return false;
            float spacing = InternalSpacing(visualWidth), gap = Math.Max(2 * half + 0.1f, 0.4f);
            var cost = Costs(width, spacing, gap);
            Plan best = null; float bestScore = float.NegativeInfinity;
            int rejected = 0, unsafeRoutes = 0, similar = 0, tried = 0;
            // Final attempt: a fresh single corridor at CURRENT reachable space.
            // No stale-layout replay, reduced safety, or unbounded regeneration.
            for (int attempt = 0; attempt <= MaxAttempts; attempt++)
            {
                bool fallback = attempt == MaxAttempts;
                if (fallback && best != null) break;
                tried++;
                float firstWidth = fallback ? corridorWidth : corridorWidth + Range(random, 0, 0.8f);
                float centerLow = Math.Max(left + firstWidth / 2, low - firstWidth / 2 + inset);
                float centerHigh = Math.Min(right - firstWidth / 2, high + firstWidth / 2 - inset);
                if (centerLow > centerHigh) { unsafeRoutes++; continue; }
                float first = fallback ? Clamp((low + high) / 2, centerLow, centerHigh) : Range(random, centerLow, centerHigh);
                var reserved = new List<Region> { new Region(first - firstWidth / 2, first + firstWidth / 2) };
                if (!fallback)
                {
                    float secondWidth = corridorWidth + Range(random, 0, 0.8f);
                    float second = Range(random, left + secondWidth / 2, right - secondWidth / 2);
                    if (Math.Abs(second - first) < (firstWidth + secondWidth) / 2 + width)
                    { rejected++; continue; }
                    reserved.Add(new Region(second - secondWidth / 2, second + secondWidth / 2));
                }
                reserved.Sort((a, b) => a.Left.CompareTo(b.Left));
                var regions = new List<Region>(); float cursor = left;
                foreach (var corridor in reserved) { regions.Add(new Region(cursor, corridor.Left)); cursor = corridor.Right; }
                regions.Add(new Region(cursor, right));
                var candidate = Fill(random, regions, cost, width, spacing, gap);
                if (candidate == null) { rejected++; continue; }
                candidate.Corridors = FreeSpace(candidate.Positions, left, right, width, half, corridorWidth);
                if (candidate.Corridors.Length < reserved.Count || !SafeRoute(candidate, low, high, half, playerX, previousSafe, clearPath))
                { unsafeRoutes++; continue; }
                float shift = Difference(candidate.Positions, previousPositions);
                if (shift < MinimumLayoutShift) similar++;
                float score = candidate.Positions.Length * 100 + Math.Min(3, shift)
                    + Math.Min(2, candidate.SafeOptions) * 0.4f + Range(random, 0, 1)
                    + (float.IsNaN(previousSafe) ? 0 : Math.Min(4, Math.Abs(candidate.SafeX - previousSafe)) * 0.25f);
                if (score <= bestScore) continue;
                candidate.Fallback = fallback; best = candidate; bestScore = score;
            }
            if (best == null) return false;
            best.CandidatesTried = tried; best.SpacingCandidates = rejected;
            best.UnsafeCandidates = unsafeRoutes; best.SimilarCandidates = similar;
            plan = best; return true;
        }
        // Minimum span for every exact sword count using only 1/3/4 clusters.
        static float[] Costs(float width, float spacing, float gap)
        {
            var cost = new float[MaxSwords + 1];
            for (int n = 1; n <= MaxSwords; n++)
            {
                cost[n] = float.PositiveInfinity;
                foreach (int size in ClusterChoices) if (size <= n)
                    cost[n] = Math.Min(cost[n], width + (size - 1) * spacing + (n == size ? 0 : gap + cost[n - size]));
            }
            return cost;
        }
        static Plan Fill(Random random, List<Region> regions, float[] cost, float width, float spacing, float gap)
        {
            var capacities = new int[regions.Count]; int total = 0;
            for (int r = 0; r < regions.Count; r++)
            {
                for (int n = 1; n <= MaxSwords; n++) if (cost[n] <= regions[r].Right - regions[r].Left) capacities[r] = n;
                total += capacities[r];
            }
            int count = Math.Min(MaxSwords, total);
            if (count < 2) return null;
            int[] allocation = null; int choices = 0;
            for (; count >= 2 && allocation == null; count--)
            {
                for (int a = 0; a <= Math.Min(count, capacities[0]); a++)
                for (int b = 0; b <= Math.Min(count - a, capacities[1]); b++)
                {
                    int c = count - a - b;
                    if (cost[a] > regions[0].Right - regions[0].Left || cost[b] > regions[1].Right - regions[1].Left
                        || (regions.Count == 2 ? c != 0 : c > capacities[2] || cost[c] > regions[2].Right - regions[2].Left)) continue;
                    if (random.Next(++choices) == 0) allocation = regions.Count == 2 ? new[] { a, b } : new[] { a, b, c };
                }
            }
            if (allocation == null) return null;
            var positions = new List<float>(); var clusters = new List<int>(); var sites = new List<int>();
            for (int r = 0; r < regions.Count; r++)
            {
                int remaining = allocation[r]; if (remaining == 0) continue;
                float available = regions[r].Right - regions[r].Left;
                var sizes = new List<int>();
                while (remaining > 0)
                {
                    var options = new List<int>();
                    foreach (int size in ClusterChoices) if (size <= remaining
                        && width + (size - 1) * spacing + (remaining == size ? 0 : gap + cost[remaining - size]) <= available + 0.00001f) options.Add(size);
                    if (options.Count == 0) return null;
                    int chosen = options[random.Next(options.Count)]; sizes.Add(chosen); remaining -= chosen;
                    available -= width + (chosen - 1) * spacing + (remaining == 0 ? 0 : gap);
                }
                float[] steps = new float[sizes.Count];
                for (int c = 0; c < sizes.Count; c++)
                {
                    float extra = sizes[c] == 1 ? 0 : Range(random, 0, Math.Min(0.3f, Math.Max(0, available) / (sizes[c] - 1)));
                    steps[c] = spacing + extra; available -= extra * (sizes[c] - 1);
                }
                var extras = new float[sizes.Count + 1]; float sum = 0;
                for (int i = 0; i < extras.Length; i++) { extras[i] = Range(random, 0.1f, 1); sum += extras[i]; }
                for (int i = 0; i < extras.Length; i++) extras[i] *= Math.Max(0, available) / sum;
                float x = regions[r].Left + extras[0] + width / 2;
                for (int c = 0; c < sizes.Count; c++)
                {
                    int cluster = clusters.Count; clusters.Add(sizes[c]);
                    for (int i = 0; i < sizes[c]; i++) { positions.Add(x); sites.Add(cluster); if (i + 1 < sizes[c]) x += steps[c]; }
                    x += width + gap + extras[c + 1];
                }
            }
            if (clusters.Count < 2) return null;
            return new Plan { Positions = positions.ToArray(), ClusterSizes = clusters.ToArray(),
                SwordClusters = sites.ToArray(), ContactIndices = sites.ToArray() };
        }
        // Merge sorted damage intervals before measuring physical free ground.
        public static Corridor[] FreeSpace(float[] positions, float left, float right, float width, float half, float minimumWidth)
        {
            var result = new List<Corridor>(); float cursor = left, inset = half + Clearance;
            for (int i = 0; i <= positions.Length; i++)
            {
                float end = i == positions.Length ? right : Math.Min(right, positions[i] - width / 2);
                if (end - cursor >= minimumWidth - 0.0001f && end - cursor >= 2 * inset)
                    result.Add(new Corridor { Left = cursor, Right = end, CenterMin = cursor + inset, CenterMax = end - inset });
                if (i < positions.Length) cursor = Math.Max(cursor, positions[i] + width / 2);
            }
            return result.ToArray();
        }
        static bool SafeRoute(Plan plan, float low, float high, float half, float playerX, float previousSafe, Func<float, bool> clear)
        {
            float best = float.NegativeInfinity;
            foreach (var corridor in plan.Corridors)
            {
                float from = Math.Max(low, corridor.CenterMin), to = Math.Min(high, corridor.CenterMax);
                if (from > to) continue;
                float target = Clamp(corridor.Center, from, to);
                if (clear != null && !clear(target)) continue;
                plan.SafeOptions++;
                float margin = Math.Min(target - corridor.Left, corridor.Right - target) - half;
                float score = Math.Min(1.5f, margin) - Math.Abs(target - playerX) * 0.03f
                    + (float.IsNaN(previousSafe) ? 0 : Math.Min(3, Math.Abs(target - previousSafe)) * 0.1f);
                if (score > best) { best = score; plan.SafeX = target; plan.SafeClearance = margin; }
            }
            return plan.SafeOptions > 0;
        }
        public static float Difference(float[] positions, float[] previous)
        {
            if (previous == null || previous.Length != positions.Length) return 3;
            float sum = 0; for (int i = 0; i < positions.Length; i++) sum += Math.Abs(positions[i] - previous[i]);
            return sum / positions.Length;
        }
    }
}
