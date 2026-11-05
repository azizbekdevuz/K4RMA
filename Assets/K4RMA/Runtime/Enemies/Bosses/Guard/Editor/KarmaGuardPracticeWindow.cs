using UnityEditor;
namespace KarmaPrototype.EditorTools
{
    public sealed class KarmaGuardPracticeWindow : KarmaBossPracticeWindow
    {
        protected override Essence Kind => Essence.Guard;
        protected override string BossName => "방어형";
        protected override EnemyAttack[] Patterns => new[] { EnemyAttack.GroundStrike, EnemyAttack.GD01, EnemyAttack.GD02, EnemyAttack.GD03, EnemyAttack.GD04, EnemyAttack.GD05 };
        [MenuItem("K4RMA/보스 연습/방어형")]
        public static void Open() { GetWindow<KarmaGuardPracticeWindow>("방어형 연습"); }
        protected override bool RunPassive(KarmaEnemy target, EnemyAttack pattern)
        {
            if (pattern != EnemyAttack.GD01) return false;
            var shield = Get(Get(target, "behavior"), "shield");
            Invoke(shield, "RefreshAutoShield");
            return true;
        }
    }
}
