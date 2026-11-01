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

        static float BoxY(int index)
        {
            RisingBasicStrike strike = RisingSlashRules.BasicStrike(index);
            float centerX, centerY, sizeX, sizeY;
            RisingSlashRules.BasicStrikeBox(0f, 0f, 1f, 2f, strike, out centerX, out centerY, out sizeX, out sizeY);
            return centerY;
        }
    }
}
