using System;
using System.Linq;
using KarmaPrototype;

static class Program
{
    static int assertions;
    static void Check(bool ok, string name)
    { assertions++; if (!ok) throw new Exception(name); }
    static void Main()
    {
        int[][] orders = { new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0} };
        foreach (var order in orders)
        {
            var state = new KarmaProgressionState();
            for (int stage = 0; stage <= 3; stage++)
            {
                var cycle = KarmaPatternCatalog.BuildCycle(state.Order);
                Check(cycle.Count == cycle.Distinct().Count(), "no duplicate patterns");
                if (stage == 0) Check(cycle.SequenceEqual(new[]{EnemyAttack.GroundStrike}), "stage one melee only");
                else
                {
                    Check(KarmaPatternCatalog.Primary(state.Order) == (Essence)order[0], "primary stays fixed");
                    var main = (Essence)order[0];
                    Check(cycle.Contains(main == Essence.SwordWave ? EnemyAttack.SW01 : main == Essence.RisingSlash ? EnemyAttack.UP01 : EnemyAttack.GD02), "primary base retained");
                    Check(cycle.Any(KarmaPatternCatalog.IsUltimate) == (stage == 3), "ultimate requires full inheritance");
                    Check(cycle.Contains(EnemyAttack.SW03) == (main == Essence.SwordWave && state.HasRemnant(Essence.RisingSlash)), "SW03 independent unlock");
                    Check(cycle.Contains(EnemyAttack.SW04) == (main == Essence.SwordWave && state.HasRemnant(Essence.Guard)), "SW04 independent unlock");
                    Check(cycle.Contains(EnemyAttack.UP03) == (main == Essence.RisingSlash && state.HasRemnant(Essence.SwordWave)), "UP03 independent unlock");
                    Check(cycle.Contains(EnemyAttack.UP04) == (main == Essence.RisingSlash && state.HasRemnant(Essence.Guard)), "UP04 independent unlock");
                    Check(cycle.Contains(EnemyAttack.GD03) == (main == Essence.Guard && state.HasRemnant(Essence.SwordWave)), "GD03 independent unlock");
                    Check(cycle.Contains(EnemyAttack.GD04) == (main == Essence.Guard && state.HasRemnant(Essence.RisingSlash)), "GD04 independent unlock");
                }
                if (stage == 3) break;
                Essence result;
                Check(state.Preview((Essence)order[stage]), "preview");
                state.Cancel(); Check(state.Count == stage, "cancel reversible");
                state.Preview((Essence)order[stage]);
                Check(state.Confirm(out result) && result == (Essence)order[stage], "confirm selection");
                Check(!state.Preview(result), "cannot duplicate inheritance");
            }
        }
        bool Select(EnemyAttack attack, EnemyAttack prior = EnemyAttack.GroundStrike, float distance = 6,
            bool air = false, bool sight = true, bool shield = true, bool escape = true, bool ready = true)
            => KarmaPatternCatalog.CanSelect(attack, prior, distance, air, sight, shield, escape, ready);
        Check(!Select(EnemyAttack.SW01,distance:3.9f),"SW01 close range forbidden");
        Check(Select(EnemyAttack.SW01,distance:4),"SW01 medium boundary");
        Check(!Select(EnemyAttack.SW02,sight:false),"SW02 blocked sight forbidden");
        Check(!Select(EnemyAttack.UP02,prior:EnemyAttack.UP02,air:true),"UP02 never consecutive");
        Check(!Select(EnemyAttack.UP02,distance:9),"UP02 distant grounded forbidden");
        Check(Select(EnemyAttack.UP02,distance:9,air:true),"UP02 airborne selection");
        Check(!Select(EnemyAttack.GD02,shield:false),"GD02 shield required");
        Check(!Select(EnemyAttack.GD02,distance:6),"GD02 must be reachable");
        Check(Select(EnemyAttack.GD02,distance:4),"GD02 selects at the actual blade reach");
        Check(!Select(EnemyAttack.GD02,distance:4.01f),"GD02 never starts beyond the blade reach");
        Check(!Select(EnemyAttack.GD04,escape:false),"wall corner escape required");
        Check(!Select(EnemyAttack.UP03,escape:false),"ground blade escape required");
        foreach (var a in new[]{EnemyAttack.SW05,EnemyAttack.UP05,EnemyAttack.GD05})
        {
            Check(!Select(a,ready:false),"ultimate cooldown");
            Check(!Select(a,prior:a),"no consecutive ultimate");
        }
        Check(KarmaBossGeometry.SweptBox(0,0,30,0,15,0,1,1),"high speed swept hit");
        Check(!KarmaBossGeometry.SweptBox(0,0,30,0,15,2,1,1),"ground projectile air safety");
        Check(KarmaBossGeometry.SweptBox(0,0,0,0,1,1,1,1),"static boundary contact");
        Check(!KarmaBossGeometry.SweptBox(0,0,10,10,0,10,1,1),"diagonal bounding corner safe");
        Check(KarmaBossGeometry.SweptBox(10,10,0,0,5,5,1,1),"reversed segment hit");
        var contact = new KarmaAttackContact();
        Check(contact.TryConsume(),"first fragment contact");
        Check(!contact.DealtDamage,"contact consumed by a parry or immunity is not damage");
        contact.RecordDamage();
        Check(contact.DealtDamage,"actual damage is remembered independently of later healing");
        for (int i=0;i<100;i++) Check(!contact.TryConsume(),"remaining frames/fragments cannot stun again");
        var trial = new KarmaTrialState();
        trial.Hit(); Check(trial.Hits == 0,"outside trial ignored");
        trial.Begin(5,1); trial.Hit(); trial.Hit();
        Check(trial.Active && !trial.Finished && !trial.Passed,"over threshold must not resolve early");
        for(int i=0;i<4;i++) trial.CompleteAttack();
        Check(!trial.Finished,"wait for last attack");
        trial.CompleteAttack(); Check(trial.Finished && !trial.Passed,"failed result at final attack");
        trial.Clear(); Check(!trial.Active && trial.Hits==0 && trial.Total==0,"cancel clears trial");
        trial.Begin(3,1); trial.Hit();
        for(int i=0;i<3;i++) trial.CompleteAttack();
        Check(trial.Passed,"allowed hit boundary succeeds");
        trial.Hit(); Check(trial.Hits==1,"resolved result immutable");
        trial.Begin(0,-1); Check(trial.Total==1 && trial.Allowed==0,"invalid tuning normalized");
        Console.WriteLine($"PASS: {assertions} core assertions");
    }
}
