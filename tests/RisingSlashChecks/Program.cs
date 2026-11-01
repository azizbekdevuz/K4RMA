using System;
using System.Collections.Generic;

namespace KarmaPrototype
{
    static class RisingSlashChecks
    {
        static int checks;

        static void Check(bool ok, string label)
        {
            checks++;
            if (!ok) throw new InvalidOperationException(label);
        }

        static void Main()
        {
            BasicStrikeChecks();
            Up01LandingChecks();
            SelectionChecks();
            Console.WriteLine("PASS: " + checks + " Rising Slash checks");
        }

        static void BasicStrikeChecks()
        {
            Check(RisingSlashRules.BasicStrikeCount == 3, "R1 strike count");
            float open = 0f;
            var windows = new List<float>();
            float share = 0f;
            var contacts = new KarmaAttackContact[RisingSlashRules.BasicStrikeCount];
            float previousHigh = -1f;
            for (int i = 0; i < RisingSlashRules.BasicStrikeCount; i++)
            {
                RisingBasicStrike strike = RisingSlashRules.BasicStrike(i);
                Check(strike.Index == i, "R1 strike index " + i);
                Check(strike.ActiveSeconds > 0.05f && strike.ActiveSeconds < 0.2f, "R1 fast window " + i);
                Check(strike.DamageShare > 0f && strike.DamageShare < 1f, "R1 share below a full hit " + i);
                Check(!string.IsNullOrEmpty(strike.SpritePose), "R1 pose " + i);
                windows.Add(open);
                windows.Add(open + strike.ActiveSeconds);
                share += strike.DamageShare;
                contacts[i] = RisingSlashRules.NewStrikeContact();
                Check(contacts[i].TryConsume(), "R1 first contact " + i);
                Check(!contacts[i].TryConsume(), "R1 one hit per strike " + i);
                Check(!contacts[i].Consumed || contacts[i] != contacts[Math.Max(0, i - 1)] || i == 0, "R1 contact identity");
                if (i > 0) Check(!object.ReferenceEquals(contacts[i], contacts[i - 1]), "R1 separate ledger " + i);
                float centerX, centerY, sizeX, sizeY;
                RisingSlashRules.BasicStrikeBox(10f, 2.3f, 1f, 2f, strike, out centerX, out centerY, out sizeX, out sizeY);
                Check(centerX > 10f, "R1 hitbox faces right");
                Check(sizeX > 0f && sizeY > 0f, "R1 hitbox size");
                float leftX, leftY, leftW, leftH;
                RisingSlashRules.BasicStrikeBox(10f, 2.3f, -1f, 2f, strike, out leftX, out leftY, out leftW, out leftH);
                Check(leftX < 10f && Math.Abs(leftY - centerY) < 0.0001f, "R1 hitbox faces left");
                if (i > 0) Check(Math.Abs(centerY - previousHigh) > 0.2f, "R1 vertical arcs differ " + i);
                previousHigh = centerY;
                open += strike.ActiveSeconds;
                if (i < RisingSlashRules.BasicStrikeCount - 1)
                {
                    Check(RisingSlashRules.BasicGapSeconds > 0f, "R1 gap");
                    open += RisingSlashRules.BasicGapSeconds;
                }
            }
            Check(Math.Abs(share - RisingSlashRules.BasicShareTotal()) < 0.0001f, "R1 share total");
            Check(Math.Abs(share - RisingSlashRules.BasicDamageBudget) < 0.0001f, "R1 budget");
            Check(share < 1.5f, "R1 combo is not triple damage");
            float melee = 0.8f;
            Check(melee * share < melee * 3f && melee * share > melee, "R1 scaled damage stays near one former hit");
            for (int i = 0; i < RisingSlashRules.BasicStrikeCount - 1; i++)
                Check(windows[i * 2 + 1] <= windows[(i + 1) * 2], "R1 windows do not overlap " + i);
            float highY = BoxY(0);
            float lowY = BoxY(1);
            float finishY = BoxY(2);
            Check(highY > lowY && highY > finishY, "R1 high, low, and finisher arcs differ");
            Check(RisingSlashRules.BasicRecoverySeconds >= 0.38f, "R1 recovery remains readable");
        }

        static void Up01LandingChecks()
        {
            const float left = -15f, right = 55f, margin = RisingSlashRules.Up01EdgeMargin, reach = 9f;
            float apex = 18f;
            float near = RisingSlashRules.ClampLandingX(20f, apex, left, right, margin, reach);
            float far = RisingSlashRules.ClampLandingX(40f, apex, left, right, margin, reach);
            Check(Math.Abs(near - 20f) < 0.0001f, "R2 open landing follows the player");
            Check(Math.Abs(far - 27f) < 0.0001f, "R2 later landing moves before lock");
            Check(Math.Abs(near - far) > 1f, "R2 tracking changes the target");
            var locked = RisingSlashRules.LockLanding(near, apex, 2.3f, 1.24f, 13f, 2.5f, left, right, margin, reach);
            float moved = RisingSlashRules.ClampLandingX(0f, apex, left, right, margin, reach);
            Check(Math.Abs(locked.LandingX - near) < 0.0001f, "R2 locked plan ignores later movement");
            Check(Math.Abs(moved - locked.LandingX) > 1f, "R2 an unlocked sample would still move");
            Check(Math.Abs(locked.LandingX - locked.ImpactX) < 0.0001f, "R2 impact shares landing X");
            Check(Math.Abs(locked.LandingY - 2.3f) < 0.0001f && Math.Abs(locked.ImpactY - 1.24f) < 0.0001f, "R2 ground landing and impact height");
            Check(Math.Abs(locked.AreaWidth - 13f) < 0.0001f && Math.Abs(locked.AreaHeight - 2.5f) < 0.0001f, "R2 blast uses the locked area");
            Check(Math.Abs(RisingSlashRules.ClampLandingX(-1000f, 0f, left, right, margin, 100f) - (left + margin)) < 0.0001f, "R2 left wall");
            Check(Math.Abs(RisingSlashRules.ClampLandingX(1000f, 0f, left, right, margin, 100f) - (right - margin)) < 0.0001f, "R2 right wall");
            Check(Math.Abs(RisingSlashRules.ClampLandingX(1000f, 0f, left, right, margin, reach) - reach) < 0.0001f, "R2 apex reach right");
            Check(Math.Abs(RisingSlashRules.ClampLandingX(-1000f, 0f, left, right, margin, reach) + reach) < 0.0001f, "R2 apex reach left");
            var airborne = RisingSlashRules.LockLanding(22f, 20f, 2.3f, 1.24f, 13f, 2.5f, left, right, margin, reach);
            Check(Math.Abs(airborne.LandingY - 2.3f) < 0.0001f, "R2 airborne player still lands on the ground line");
            Check(Math.Abs(RisingSlashRules.AdvanceUnpaused(1f, 0.016f, true) - 1f) < 0.0001f, "R2 pause freezes tracking time");
            Check(Math.Abs(RisingSlashRules.AdvanceUnpaused(1f, 0.016f, false) - 1.016f) < 0.0001f, "R2 unpaused time advances");
            Check(Math.Abs(RisingSlashRules.AscentBlend(0f, RisingSlashRules.Up01AscentSeconds)) < 0.0001f, "R2 ascent starts at the ground");
            Check(Math.Abs(RisingSlashRules.AscentBlend(0.16f, 0.32f) - 0.5f) < 0.0001f, "R2 ascent midpoint");
            Check(Math.Abs(RisingSlashRules.AscentBlend(1f, 0.32f) - 1f) < 0.0001f, "R2 hover holds the apex");
            Check(RisingSlashRules.Up01TrackingSeconds > RisingSlashRules.Up01AscentSeconds, "R2 tracking continues after the apex");
            Check(RisingSlashRules.Up01LockWarningSeconds >= 0.7f, "R2 locked dodge window");
            float rise = RisingSlashRules.PursuitRiseAboveGround(8f, 4.8f, 10.4f);
            Check(Math.Abs(rise - 8f) < 0.0001f, "R2 serialized rise height is the apex climb");
        }

        static void SelectionChecks()
        {
            int pursuit = 0, other = 0;
            foreach (EnemyAttack attack in RisingSlashRules.PursuitSchedule)
            {
                string name = attack.ToString();
                Check(name == "GroundStrike" || name.StartsWith("UP"), "R3 schedule stays on Rising attacks: " + name);
                if (attack == EnemyAttack.UP01 || attack == EnemyAttack.UP02) pursuit++;
                else other++;
            }
            Check(pursuit == RisingSlashRules.PursuitRepeats * 2 && other == 4, "R3 schedule weight 2:2:1:1:1:1");
            var all = KarmaPatternCatalog.BuildCycle(new List<Essence> { Essence.RisingSlash, Essence.SwordWave, Essence.Guard });
            var log = Simulate(all, 320, 6f, 3f, true, true);
            Check(log.Total == 320, "R3 every full-cycle decision resolves");
            Check(log.Count(EnemyAttack.UP01) == 80 && log.Count(EnemyAttack.UP02) == 80, "R3 pursuit slots " + log);
            Check(log.Count(EnemyAttack.GroundStrike) == 40 && log.Count(EnemyAttack.UP03) == 40, "R3 other slots " + log);
            Check(log.Count(EnemyAttack.UP04) == 40 && log.Count(EnemyAttack.UP05) == 40, "R3 inherited slots " + log);
            Check(log.Count(EnemyAttack.UP01) > log.Count(EnemyAttack.GroundStrike), "R3 UP01 exceeds another pattern");
            Check(log.Count(EnemyAttack.UP02) > log.Count(EnemyAttack.UP04), "R3 UP02 exceeds another pattern");
            Check(log.Ineligible == 0 && log.ConsecutiveUp02 == 0 && log.UltimateViolations == 0, "R3 no illegal picks " + log);

            var rising = KarmaPatternCatalog.BuildCycle(new List<Essence> { Essence.RisingSlash });
            var basic = Simulate(rising, 320, 6f, 3f, true, true);
            Check(basic.Count(EnemyAttack.UP03) + basic.Count(EnemyAttack.UP04) + basic.Count(EnemyAttack.UP05) == 0, "R3 locked patterns stay locked");
            Check(basic.Count(EnemyAttack.UP01) == 128 && basic.Count(EnemyAttack.UP02) == 128 && basic.Count(EnemyAttack.GroundStrike) == 64, "R3 early cycle still prefers pursuit " + basic);
            Check(basic.Ineligible == 0 && basic.ConsecutiveUp02 == 0, "R3 early cycle remains legal");

            var guard = KarmaPatternCatalog.BuildCycle(new List<Essence> { Essence.RisingSlash, Essence.Guard });
            var guarded = Simulate(guard, 300, 6f, 3f, true, true);
            Check(guarded.Count(EnemyAttack.UP03) == 0 && guarded.Count(EnemyAttack.UP05) == 0, "R3 sword and ultimate stay absent");
            Check(guarded.Count(EnemyAttack.UP01) == 100 && guarded.Count(EnemyAttack.UP02) == 100, "R3 guard inheritance keeps pursuit ahead " + guarded);
            Check(guarded.Count(EnemyAttack.UP04) > 0 && guarded.Count(EnemyAttack.GroundStrike) > 0, "R3 eligible non-pursuit attacks are not starved");

            var distant = Simulate(all, 300, 18f, 9f, true, true);
            Check(distant.Count(EnemyAttack.UP02) == 0 && distant.Count(EnemyAttack.GroundStrike) == 0, "R3 far target removes UP02 and melee");
            Check(distant.Count(EnemyAttack.UP01) == 120 && distant.Count(EnemyAttack.UP05) == 60, "R3 UP01 remains the pursuit choice " + distant);
            Check(distant.Count(EnemyAttack.UP01) > distant.Count(EnemyAttack.UP03), "R3 distant UP01 still exceeds others");
            Check(distant.Ineligible == 0, "R3 distant selection never cheats eligibility");

            var onlyBasic = KarmaPatternCatalog.BuildCycle(new List<Essence>());
            var strikes = Simulate(onlyBasic, 300, 6f, 3f, true, true);
            Check(strikes.Total == 300 && strikes.Count(EnemyAttack.GroundStrike) == 300, "R3 sole eligible attack is always chosen");

            int cursor = 0;
            int probes = 0;
            EnemyAttack rejected;
            bool found = RisingSlashRules.TrySelect(all, delegate(EnemyAttack attack) { probes++; return false; }, ref cursor, out rejected);
            Check(!found && probes == RisingSlashRules.PursuitSchedule.Length, "R3 one decision cannot loop forever");

            var cooled = Simulate(all, 320, 6f, 3f, true, true, 10);
            Check(cooled.UltimateViolations == 0 && cooled.Count(EnemyAttack.UP05) > 0, "R3 ultimate respects cooldown and still appears " + cooled);
            Check(cooled.ConsecutiveUp02 == 0 && cooled.Ineligible == 0, "R3 cooldown run stays legal");
        }

        static SelectionLog Simulate(List<EnemyAttack> cycle, int decisions, float worldDistance, float unscaledDistance, bool ultimateReady, bool escape, int ultimateGap = 0)
        {
            var log = new SelectionLog();
            int cursor = 0;
            var previous = EnemyAttack.GroundStrike;
            int readyAgain = 0;
            bool ready = ultimateReady;
            for (int i = 0; i < decisions; i++)
            {
                if (ultimateGap > 0 && i >= readyAgain) ready = true;
                bool readyNow = ready;
                EnemyAttack picked;
                bool ok = RisingSlashRules.TrySelect(cycle, delegate(EnemyAttack attack)
                {
                    return HostEligible(attack, cycle, previous, worldDistance, 8.2f, unscaledDistance, false, true, false, escape, readyNow);
                }, ref cursor, out picked);
                if (!ok) continue;
                log.Total++;
                log.Add(picked);
                if (!HostEligible(picked, cycle, previous, worldDistance, 8.2f, unscaledDistance, false, true, false, escape, readyNow)) log.Ineligible++;
                if (picked == EnemyAttack.UP02 && previous == EnemyAttack.UP02) log.ConsecutiveUp02++;
                if (picked == EnemyAttack.UP05 && !readyNow) log.UltimateViolations++;
                if (picked == EnemyAttack.UP05 && ultimateGap > 0) { ready = false; readyAgain = i + ultimateGap; }
                previous = picked;
            }
            return log;
        }

        static bool HostEligible(EnemyAttack attack, IReadOnlyList<EnemyAttack> cycle, EnemyAttack previous, float worldDistance, float meleeReach,
            float unscaledDistance, bool airborne, bool sight, bool shield, bool escape, bool ultimateReady)
        {
            bool listed = false;
            for (int i = 0; i < cycle.Count; i++) if (cycle[i] == attack) listed = true;
            if (!listed) return false;
            if (attack == EnemyAttack.GroundStrike && worldDistance > meleeReach) return false;
            return KarmaPatternCatalog.CanSelect(attack, previous, unscaledDistance, airborne, sight, shield, escape, ultimateReady);
        }

        sealed class SelectionLog
        {
            readonly Dictionary<EnemyAttack, int> counts = new Dictionary<EnemyAttack, int>();
            public int Total, Ineligible, ConsecutiveUp02, UltimateViolations;
            public void Add(EnemyAttack attack)
            {
                int count;
                counts.TryGetValue(attack, out count);
                counts[attack] = count + 1;
            }
            public int Count(EnemyAttack attack)
            {
                int count;
                return counts.TryGetValue(attack, out count) ? count : 0;
            }
            public override string ToString()
            {
                var text = new List<string>();
                foreach (var pair in counts) text.Add(pair.Key + "=" + pair.Value);
                return string.Join(", ", text) + " illegal=" + Ineligible + " up02=" + ConsecutiveUp02 + " ultimate=" + UltimateViolations;
            }
        }

        static float BoxY(int index)
        {
            RisingBasicStrike strike = RisingSlashRules.BasicStrike(index);
            float centerX, centerY, sizeX, sizeY;
            RisingSlashRules.BasicStrikeBox(0f, 0f, 1f, 2f, strike, out centerX, out centerY, out sizeX, out sizeY);
            return centerY;
        }
    }
}
