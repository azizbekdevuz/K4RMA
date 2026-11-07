#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using KarmaPrototype;
public sealed class KarmaGuardOwnershipChecks : KarmaBossOwnershipFixture
{
    public override Essence? Primary => Essence.Guard;
    public override string BehaviorName => "GuardBossBehavior";
    public override string BasicPattern => "GDBasicPattern";
    public override Type ProjectileType => typeof(KarmaBossAttack);
    public override List<EnemyAttack> Expected(List<Essence> inherited)
    {
            var result = new List<EnemyAttack> { EnemyAttack.GroundStrike, EnemyAttack.GD02 };
            if (inherited.Contains(Essence.SwordWave)) result.Add(EnemyAttack.GD03);
            if (inherited.Contains(Essence.RisingSlash)) result.Add(EnemyAttack.GD04);
            if (inherited.Count == 3) result.Add(EnemyAttack.GD05);
        return result;
    }
    public override void CheckSettings(Action<bool, string> check)
    {
        var profile = Resources.Load<KarmaGuardBossProfile>("KarmaGuardBossProfile");
        check(profile != null, "Guard profile imports");
        var first = new KarmaBossTuning(); var second = new KarmaBossTuning();
        check(Mathf.Approximately(first.guard.shieldThrowSpeed, 30), "Guard saved settings load");
        first.guard.shieldThrowSpeed = 123;
        check(Mathf.Approximately(second.guard.shieldThrowSpeed, 30), "Guard runtime settings are isolated");
        check(Mathf.Approximately(profile.settings.shieldThrowSpeed, 30), "Guard profile remains unchanged");
            var config = ScriptableObject.CreateInstance<KarmaConfig>();
            var clone = UnityEngine.Object.Instantiate(config);
            config.BossTuning.guard.autoShieldPeriod = 123;
            check(clone.BossTuning.guard.autoShieldPeriod == 8, "Cloned configs have private guard settings");
            UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(clone);
    }
}
#endif
