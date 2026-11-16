using System;
using System.Collections.Generic;
using System.Linq;
using KarmaPrototype;

static class Program
{
    const float Left = -15, Right = 55, Width = 70f / 32, Visual = 3.5f, Half = 0.7f, Speed = 11.2f;
    static int plans, fallbacks, walkingHits, movingCases, alternateGaps, standingHits, standingCases, reversals;
    // Valid bounded RNG values deliberately concentrate all spare space in
    // one gap, exercising the real cap rejection and deterministic fallback.
    sealed class CapRejectingRandom : Random
    {
        bool spike;
        public override int Next(int maxValue) => 0;
        public override int Next(int minValue, int maxValue) { spike=true; return minValue; }
        public override double NextDouble() { bool value=spike;spike=false;return value?0.9999:0.0001; }
    }
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static string Signature(KarmaSwordScarPlanner.Plan p)
        => string.Join(",", p.Positions.Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "/" + string.Join(",", p.Groups);
    static KarmaSwordScarPlanner.Plan Draw(int seed, float x, float velocity, float warning = 0.8f,
        float previous = float.NaN, float speed = Speed, Func<float, bool> clear = null)
    {
        Require(KarmaSwordScarPlanner.TryPlan(new Random(seed), Left, Right, Width, Visual, x, Half,
            velocity, speed, warning, previous, clear, out var p), "valid arena must yield a safe plan: " + seed + "/" + x);
        return p;
    }
    static void Validate(KarmaSwordScarPlanner.Plan p, float x, float warning, float speed, Func<float, bool> route = null, float velocity = 0)
    {
        Require(p.Positions.Length == 16 && p.Groups.Length == 16, "16 distinct physical swords");
        Require(p.Groups.Count(g => g == 0) == 8 && p.Groups.Count(g => g == 1) == 8, "8+8 groups");
        Require(p.Positions.Distinct().Count() == 16, "no duplicate warning/sword sites");
        Require(p.SafeX - Half >= Left && p.SafeX + Half <= Right, "whole body within arena");
        Require(Math.Abs(p.SafeX - x) <= speed * (warning - 0.28f) + 0.001f, "reachable after reaction, before FIRST release");
        Require(route == null || route(p.SafeX), "actual collision-route predicate");
        float spacing = Math.Max(Width + Math.Max(0.75f, 2 * Half + 0.1f), Visual + 0.15f);
        for (int i = 0; i < 16; i++)
        {
            Require(p.Positions[i] - Width / 2 >= Left - 0.0001f && p.Positions[i] + Width / 2 <= Right + 0.0001f, "full hitbox inside arena");
            if (i > 0) Require(p.Positions[i] - p.Positions[i - 1] >= spacing - 0.0001f, "physical and visual spacing");
            Require(Math.Abs(p.SafeX - p.Positions[i]) >= Width / 2 + Half + KarmaSwordScarPlanner.Clearance - 0.001f, "union-safe FULL body clearance");
        }
        for (int i = 0; i < 16; i += 2)
            Require(p.Groups[i] != p.Groups[i + 1], "each shared contact pair spans both groups");
        // Independently step both an immediate stop and continued real
        // horizontal motion DURING reaction, then walk through both windows.
        float end = warning + 0.25f + 0.65f;
        foreach(float initialVelocity in new[]{0f,velocity})
        {
            float reaction=Math.Clamp(x+Math.Clamp(initialVelocity*0.28f,-6,6),Left+Half+0.55f,Right-Half-0.55f);
            if(route!=null && !route(reaction))
            {
                float desired=reaction;reaction=x;
                for(int step=1;step<=1000;step++)
                {
                    float probe=x+(desired-x)*step/1000;
                    if(!route(probe))break;reaction=probe;
                }
            }
            Require(Math.Abs(p.SafeX-reaction)<=speed*(warning-0.28f)+0.03f,"reachable after continuing through reaction");
            if(initialVelocity!=0 && Math.Sign(p.SafeX-reaction)!=Math.Sign(initialVelocity))reversals++;
            for(float t=0;t<=end+0.001f;t+=1f/240)
            {
                float walk=Math.Max(0,t-0.28f)*speed;
                float at=t<0.28f?x:reaction+Math.Sign(p.SafeX-reaction)*Math.Min(Math.Abs(p.SafeX-reaction),walk);
                Require(!Hits(p,at,t,warning),"actual full-body safe route survives stagger overlap");
            }
        }
        plans++; if (p.Fallback) fallbacks++;
    }
    static bool Hits(KarmaSwordScarPlanner.Plan p, float x, float t, float warning)
    {
        for (int i = 0; i < 16; i++)
        {
            float activation = warning + p.Groups[i] * 0.25f;
            if (t >= activation && t <= activation + 0.65f
                && KarmaBossGeometry.SweptBox(p.Positions[i], 1, p.Positions[i], 1,
                    x, 1, Width / 2 + Half, 1)) return true;
        }
        return false;
    }
    static int[] Quarters(KarmaSwordScarPlanner.Plan p, int group = -1)
        => Enumerable.Range(0, 4).Select(q => Enumerable.Range(0, 16).Count(i =>
            (group < 0 || p.Groups[i] == group) && Math.Clamp((int)((p.Positions[i] - Left) * 4 / (Right - Left)), 0, 3) == q)).ToArray();
    static double PositionDifference(KarmaSwordScarPlanner.Plan a, KarmaSwordScarPlanner.Plan b)
        => a.Positions.Zip(b.Positions, (x, y) => (double)Math.Abs(x - y)).Average();
    static void Different(KarmaSwordScarPlanner.Plan previous, KarmaSwordScarPlanner.Plan current)
    {
        Require(PositionDifference(previous, current) >= 1.1 - 0.0001, "mean shift exceeds the former entire 0.6875-wide cell jitter");
        Require(previous.Positions.Zip(current.Positions, (a, b) => Math.Abs(a - b)).Count(d => d >= 1) >= 8,
            "at least half the swords move by >=1 world unit between visible rounds/casts");
        Require(!Quarters(previous).SequenceEqual(Quarters(current)), "regional density changes between visible rounds/casts");
    }
    static void ConsecutiveCasts()
    {
        int historyPlans=0, historyFallbacks=0, attempts=0, unsafeRoutes=0, similar=0, spacing=0, changedSafe=0;
        double shifts=0, safeShift=0; int comparisons=0;
        var seeds=new HashSet<int>();
        for(int i=0;i<10000;i++)Require(seeds.Add(KarmaSwordScarPlanner.NextSeed()),"normal seed source cannot repeat on same tick or battle restart");
        var sequence=typeof(KarmaSwordScarPlanner).GetField("seedSequence",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        int saved=(int)sequence.GetValue(null);
        try
        {
            foreach(int boundary in new[]{-2,int.MaxValue-2,0x3fffffff-2})
            {
                sequence.SetValue(null,boundary);string prior=null;
                for(int i=0;i<5;i++)
                {
                    var random=new Random(KarmaSwordScarPlanner.NextSeed());
                    string stream=string.Join(',',Enumerable.Range(0,8).Select(_=>random.Next()));
                    Require(stream!=prior,"consecutive normal RNG streams differ even at signed/masked seed boundaries");prior=stream;
                }
            }
        }
        finally { sequence.SetValue(null,saved); }
        foreach(float start in new[]{-14.25f,-10f,3f,20f,37f,50f,54.25f})
        foreach(float direction in new[]{-14f,0f,14f})
        {
            KarmaSwordScarPlanner.Plan previous=null;
            for(int cast=0;cast<10;cast++)
            {
                int castSeed=KarmaSwordScarPlanner.NextSeed();var random=new Random(castSeed);
                float x=start, safe=float.NaN;
                for(int round=0;round<5;round++)
                {
                    float velocity=round%2==0?direction:-direction;
                    Require(KarmaSwordScarPlanner.TryPlan(random,Left,Right,Width,Visual,x,Half,velocity,Speed,0.8f,
                        safe,null,out var p,previous?.Positions),$"normal successive cast/round must complete: seed={castSeed}, start={start}, direction={direction}, round={round}");
                    Validate(p,x,0.8f,Speed,velocity:velocity);
                    historyPlans++;historyFallbacks+=p.Fallback?1:0;attempts+=p.CandidatesTried;
                    unsafeRoutes+=p.UnsafeCandidates;similar+=p.SimilarCandidates;spacing+=p.SpacingCandidates;
                    if(previous!=null)
                    {
                        Different(previous,p);shifts+=PositionDifference(previous,p);comparisons++;
                        float gap=Math.Abs(p.SafeX-previous.SafeX);safeShift+=gap;if(gap>=1)changedSafe++;
                        // Explicit seed + the SAME history is reproducible too.
                        if(round==0)
                        {
                            int seed=12345+cast;
                            Require(KarmaSwordScarPlanner.TryPlan(new Random(seed),Left,Right,Width,Visual,x,Half,velocity,Speed,0.8f,
                                safe,null,out var a,previous.Positions)
                                && KarmaSwordScarPlanner.TryPlan(new Random(seed),Left,Right,Width,Visual,x,Half,velocity,Speed,0.8f,
                                safe,null,out var b,previous.Positions) && Signature(a)==Signature(b),"explicit seed and identical history replay exactly");
                        }
                    }
                    previous=p;safe=p.SafeX;x=p.SafeX;
                }
            }
        }
        Console.WriteLine($"HISTORY: {historyPlans} rounds in 210 normal casts; fallback {historyFallbacks}/{historyPlans}; mean position shift {shifts/comparisons:F3}; mean safe shift {safeShift/comparisons:F3}; safe moved >=1 in {changedSafe}/{comparisons}.");
        Console.WriteLine($"HISTORY CANDIDATES: spacing/cap {spacing}, unsafe {unsafeRoutes}, similar {similar} / {attempts}.");
        Require(historyFallbacks==0,"normal gameplay uses varied candidates, never a repetitive fallback");
        Require(changedSafe>comparisons/2,"escape gaps relocate meaningfully across rounds/casts");
    }
    static void Metrics()
    {
        var samples = Enumerable.Range(0, 250).Select(seed => Draw(seed, 20, 0)).ToArray();
        double displacement = samples.Zip(samples.Skip(1), PositionDifference).Average();
        double safeMovement = samples.Zip(samples.Skip(1), (a, b) => (double)Math.Abs(a.SafeX - b.SafeX)).Average();
        var regions = samples.Select(p => string.Join(',', Quarters(p))).Distinct().ToArray();
        var groupRegions = samples.SelectMany(p => new[] { string.Join(',', Quarters(p, 0)), string.Join(',', Quarters(p, 1)) }).Distinct().ToArray();
        Console.WriteLine($"SPATIAL: mean adjacent-seed sorted-position displacement {displacement:F3}; mean safe-gap movement {safeMovement:F3}; quarter distributions {string.Join(" / ", regions)}; group quarter variants {groupRegions.Length}.");
        Console.WriteLine($"CANDIDATES: spacing/cap {samples.Sum(p => p.SpacingCandidates)}, unsafe {samples.Sum(p => p.UnsafeCandidates)}, similar {samples.Sum(p => p.SimilarCandidates)} / {samples.Sum(p => p.CandidatesTried)}; fallback {samples.Count(p => p.Fallback)}/{samples.Length}.");
        Require(displacement>=1.2 && regions.Length>=8 && groupRegions.Length>=8,"different seeds change physical positions and regional/group density, not just signatures");
        Require(safeMovement>=2,"different seeds move safe gaps meaningfully");
        Require(samples.All(p=>p.Positions.Zip(p.Positions.Skip(1),(a,b)=>b-a).Max()
            -p.Positions.Zip(p.Positions.Skip(1),(a,b)=>b-a).Min()>1.0f),"layouts are not rigid, evenly spaced rows");
        foreach (int seed in new[] { 11, 22, 33 })
        {
            var p = Draw(seed, 20, 0);
            Console.WriteLine($"SEED {seed}: safe={p.SafeX:F2}; quarters={string.Join(',', Quarters(p))}; positions={string.Join(",", p.Positions.Select(x => x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)))}");
        }
    }
    static void Main(string[] args)
    {
        Metrics();
        if (args.Contains("--metrics")) return;
        ConsecutiveCasts();
        var unique = new HashSet<string>(); var strategies = new HashSet<int>();
        foreach (float warning in new[] { 0.8f, 0.855f })
        foreach (float velocity in new[] { -14f, 0f, 14f })
        for (int seed = 0; seed < 60; seed++)
        foreach (float x in new[] { -14.25f, -10f, 3f, 20f, 37f, 50f, 54.25f })
        {
            var p = Draw(seed, x, velocity, warning);
            Validate(p, x, warning, Speed, velocity: velocity);
            Require(Signature(p) == Signature(Draw(seed, x, velocity, warning)), "same seed reproduces ALL positions and groups");
            unique.Add(Signature(p)); strategies.Add(p.Strategy);
            if (velocity != 0)
            {
                bool hit = false;
                for (float t = warning; t <= warning + 0.90f; t += 1f / 120)
                    hit |= Hits(p, Math.Clamp(x + velocity * t, Left + Half, Right - Half), t, warning);
                if (hit) walkingHits++; movingCases++;
            }
            else
            {
                if(Hits(p,x,warning+0.25f,warning))standingHits++;
                standingCases++;
            }
        }
        Console.WriteLine($"Layout diversity: {unique.Count}; strategies {string.Join(',', strategies)}; fallbacks {fallbacks}");
        Require(unique.Count > 200 && strategies.Count == 3, "varied seeds and all three weighted strategies");
        Require(walkingHits > movingCases * 0.60f, "continuous left/right walking is reliably pressured");
        Require(standingHits>standingCases/2 && reversals>0,"standing and direction reversal require meaningful repositioning choices");
        foreach (int mode in new[] { -1, 0, 1, 2 })
        {
            var random = new Random(9303 + mode); float x = 20, previous = float.NaN;
            var rounds = new HashSet<string>(); int warnings = 0;
            for (int round = 0; round < 5; round++)
            {
                float velocity = mode == 2 ? (round % 2 == 0 ? 14 : -14) : mode * 14;
                Require(KarmaSwordScarPlanner.TryPlan(random, Left, Right, Width, Visual, x, Half,
                    velocity, Speed, 0.8f, previous, null, out var p), "five-round moving/still/reversing plan");
                Validate(p, x, 0.8f, Speed, velocity: velocity);
                if (!float.IsNaN(previous) && Math.Abs(p.SafeX - previous) > 0.1f) alternateGaps++;
                rounds.Add(Signature(p)); warnings += p.Positions.Length; previous = p.SafeX; x = p.SafeX;
            }
            Require(rounds.Count == 5 && warnings == 80, "five distinct rounds; 80 one-to-one warnings/swords");
        }
        Require(alternateGaps >= 8, "escape pockets relocate across rounds");
        foreach(float velocity in new[]{-100f,100f})
            Validate(Draw(42,20,velocity),20,0.8f,Speed,velocity:velocity);
        float center=20;
        Require(KarmaSwordScarPlanner.TryPlan(new CapRejectingRandom(),Left,Right,Width,Visual,center,Half,0,0,0.8f,
            float.NaN,null,out var fallback) && fallback.Fallback && fallback.SpacingCandidates==48,
            "48 rejected gap allocations reach the bounded fallback");
        Validate(fallback,center,0.8f,0);
        Require(KarmaSwordScarPlanner.TryPlan(new CapRejectingRandom(),Left,Right,Width,Visual,center,Half,0,0,0.8f,
            float.NaN,null,out var replay) && Signature(fallback)==Signature(replay),"fallback geometry deterministic");
        Require(!KarmaSwordScarPlanner.TryPlan(new CapRejectingRandom(),Left,Right,Width,Visual,center,Half,0,0,0.8f,
            float.NaN,null,out _,fallback.Positions),"an exhausted planner cannot display the same fallback twice");
        Func<float, bool> wall = target => target <= 20;
        Validate(Draw(23, 19, 14, clear: wall), 19, 0.8f, Speed, wall, 14);
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(1), Left, Right, Width, Visual, 20, Half, 14, Speed, 0.8f,
            float.NaN, _ => false, out _), "blocked physical routes rejected");
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(1), Left, Right, Width, Visual, 20, 40, 14, Speed, 0.8f,
            float.NaN, null, out _), "body wider than arena rejected");
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(1), Left, Right, Width, Visual, 20, Half, 14, Speed, 0.79f,
            float.NaN, null, out _), "minimum warning never shortened");
        // Adjacent sites share the same real contact ledger across both groups.
        var contact = new KarmaAttackContact(); int damage = 0;
        for (int group = 0; group < 2; group++)
            for (int overlappingFrame = 0; overlappingFrame < 60; overlappingFrame++)
                if (contact.TryConsume()) { contact.RecordDamage(); damage++; }
        Require(damage == 1 && contact.Consumed && contact.DealtDamage, "real shared ledger consumes once across stagger/repeated frames");
        Console.WriteLine($"PASS: {plans} plans; {unique.Count} layouts; moving-left/right pressure {walkingHits}/{movingCases}; standing pressure {standingHits}/{standingCases}; reversal routes {reversals}; relocated gaps {alternateGaps}; fallbacks {fallbacks}.");
        Console.WriteLine("PASS: 5 rounds x (8+8) = 80; full-body collision-safe reaction routes through both active windows; normal/empowered warnings; deterministic seeds/fallback; no duplicate contacts.");
        Console.WriteLine("TIMING: both warnings lock at 0; activations W / W+0.25; each active 0.65; nominal cast increase 5x0.25 = 1.25 seconds.");
    }
}
