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

        static float BoxY(int index)
        {
            RisingBasicStrike strike = RisingSlashRules.BasicStrike(index);
            float centerX, centerY, sizeX, sizeY;
            RisingSlashRules.BasicStrikeBox(0f, 0f, 1f, 2f, strike, out centerX, out centerY, out sizeX, out sizeY);
            return centerY;
        }
    }
}
