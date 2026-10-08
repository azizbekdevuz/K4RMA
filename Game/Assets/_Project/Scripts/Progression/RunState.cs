using System.Collections.Generic;

namespace K4RMA
{
    public enum RunPhase
    {
        Fight,
        Altar,
        Defeat,
        SliceComplete
    }

    public sealed class RunState
    {
        public int StageIndex = 1;
        public RunPhase Phase = RunPhase.Fight;
        public List<string> OwnedAbilityIds = new List<string>();
        public string ActiveAbilityId;
        public string SacrificedAbilityId;
        public string BossInheritedAbilityId;
        public string CounterAbilityId;

        public static RunState CreatePrototypeStart()
        {
            return new RunState
            {
                StageIndex = 1,
                Phase = RunPhase.Fight,
                OwnedAbilityIds = new List<string>
                {
                    PrototypeIds.Dash,
                    PrototypeIds.Projectile,
                    PrototypeIds.Guard
                },
                ActiveAbilityId = PrototypeIds.Projectile,
                SacrificedAbilityId = null,
                BossInheritedAbilityId = null,
                CounterAbilityId = null
            };
        }
    }
}
