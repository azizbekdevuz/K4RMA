#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using KarmaPrototype;
public sealed class KarmaSwordOwnershipChecks : KarmaBossOwnershipFixture
{
    public override Essence? Primary => Essence.SwordWave;
    public override string BehaviorName => "SwordBossBehavior";
    public override string BasicPattern => "GroundStrikePattern";
    public override Type ProjectileType => typeof(KarmaSwordBossAttack);
    public override List<EnemyAttack> Expected(List<Essence> inherited)
    {
            var result = new List<EnemyAttack> { EnemyAttack.SW01, EnemyAttack.SW02 };
            if (inherited.Contains(Essence.RisingSlash)) result.Add(EnemyAttack.SW03);
            if (inherited.Contains(Essence.Guard)) result.Add(EnemyAttack.SW04);
            if (inherited.Count == 3) result.Add(EnemyAttack.SW05);
        return result;
    }
    public override void CheckSettings(Action<bool, string> check)
    {
        var profile = Resources.Load<KarmaSwordBossProfile>("KarmaSwordBossProfile");
        check(profile != null, "Sword profile imports");
        check(Resources.Load<Material>("SW05/SW05CrimsonBlade") != null, "Sword material Resources path survives move");
        var first = new KarmaBossTuning(); var second = new KarmaBossTuning();
        check(Mathf.Approximately(first.sword.swordSpeed, 32), "Sword saved settings load");
        first.sword.swordSpeed = 123;
        check(Mathf.Approximately(second.sword.swordSpeed, 32), "Sword runtime settings are isolated");
        check(Mathf.Approximately(profile.settings.swordSpeed, 32), "Sword profile remains unchanged");
    }
}
#endif
