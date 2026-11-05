using System;
using System.Collections.Generic;
using KarmaPrototype;

static class Program
{
    static int plans;
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static void Main()
    {
        Require(KarmaSwordScarPlanner.Center(-15, 55, 0) == -12.8125f, "first cell center");
        Require(KarmaSwordScarPlanner.Center(-15, 55, 15) == 52.8125f, "last cell center");
        var layouts = new HashSet<string>();
        foreach (float width in new[] { 4.5f, 9.5f })
        foreach (float half in new[] { 0.7f, 1.4f, 3f, 5f })
        for (int seed = 0; seed < 100; seed++)
        for (int step = 0; step <= 70; step++)
        {
            float x = Math.Clamp(-15 + step, -15 + half + 0.2f, 55 - half - 0.2f);
            Validate(seed, x, half, 11.2f, null, layouts, width);
        }
        // With no walking available, the draw itself must keep the full body safe.
        for (int i = 0; i <= 70; i++) Validate(i, Math.Clamp(-15 + i, -14.1f, 54.1f), 0.7f, 0, null, layouts, 9.5f);
        // The planner cannot claim a safe route through a wall.
        Validate(21, 19.2f, 0.7f, 11.2f, target => target <= 20, layouts);
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(3), -15, 55, 4.5f, 20, 0.7f, 11.2f, 0.85f,
            target => false, out _, out _, out _), "fully blocked routes must abort safely");
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(3), -15, 55, 4.5f, 20, 40, 11.2f, 0.85f,
            null, out _, out _, out _), "a body wider than the arena cannot fit");
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(3), -15, 55, 15, 20, 0.7f, 11.2f, 0.85f,
            null, out _, out _, out _), "footprint must not exceed two cell widths");
        Require(layouts.Count > 150, "random draws must exercise diverse layouts");
        Console.WriteLine("PASS: " + plans + " SW03 plans; unique cells, in-arena hitboxes, whole-body clearance, reachable safe point, zero-speed and blocked-route checks; " + layouts.Count + " layouts");
    }

    static void Validate(int seed, float x, float half, float speed, Func<float, bool> route, HashSet<string> layouts, float width = 4.5f)
    {
        Require(KarmaSwordScarPlanner.TryPlan(new Random(seed), -15, 55, width, x, half, speed, 0.85f,
            route, out var cells, out float safe, out float warning), "valid arena must yield a safe layout");
        Require(cells.Length == 10 && new HashSet<int>(cells).Count == 10, "exactly ten distinct cells");
        Require(safe - half >= -15 && safe + half <= 55, "whole safe body inside arena");
        foreach (int cell in cells)
        {
            Require(cell >= 0 && cell < 16, "index within sixteen cells");
            float center = Math.Clamp(-15 + (cell + 0.5f) * (70f / 16), -15 + width / 2, 55 - width / 2);
            Require(center - width / 2 >= -15 && center + width / 2 <= 55, "whole attack inside arena");
            Require(Math.Abs(safe - center) >= width / 2 + half + 0.199f, "safe body clears every damage rectangle");
        }
        Require(Math.Abs(safe - x) <= speed * Math.Max(0, warning - 0.28f) + 0.001f, "escape reachable after reaction time");
        Require(route == null || route(safe), "route passes obstruction test");
        layouts.Add(string.Join(",", cells)); plans++;
    }
}
