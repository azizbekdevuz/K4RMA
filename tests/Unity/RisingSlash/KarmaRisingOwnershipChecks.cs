#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using KarmaPrototype;
public sealed class KarmaRisingOwnershipChecks : KarmaBossOwnershipFixture
{
    public override Essence? Primary => Essence.RisingSlash;
    public override string BehaviorName => "RisingBossBehavior";
    public override string BasicPattern => "UPBasicPattern";
    public override Type ProjectileType => typeof(KarmaRisingFragmentAttack);
    public override List<EnemyAttack> Expected(List<Essence> inherited)
    {
            var result = new List<EnemyAttack> { EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02 };
            if (inherited.Contains(Essence.SwordWave)) result.Add(EnemyAttack.UP03);
            if (inherited.Count == 3) result.Add(EnemyAttack.UP05);
        return result;
    }
    public override void CheckSettings(Action<bool, string> check)
    {
        var profile = Resources.Load<KarmaRisingBossProfile>("KarmaRisingBossProfile");
        check(profile != null, "Rising profile imports");
        var first = new KarmaBossTuning(); var second = new KarmaBossTuning();
        check(Mathf.Approximately(first.rising.meleeReach, 8.2f), "Rising saved settings load");
        first.rising.meleeReach = 123;
        check(Mathf.Approximately(second.rising.meleeReach, 8.2f), "Rising runtime settings are isolated");
        check(Mathf.Approximately(profile.settings.meleeReach, 8.2f), "Rising profile remains unchanged");
    }
}
#endif
