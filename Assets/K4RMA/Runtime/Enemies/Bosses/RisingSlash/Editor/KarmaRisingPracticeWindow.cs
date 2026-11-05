using UnityEditor;
namespace KarmaPrototype.EditorTools
{
    public sealed class KarmaRisingPracticeWindow : KarmaBossPracticeWindow
    {
        protected override Essence Kind => Essence.RisingSlash;
        protected override string BossName => "상승베기형";
        protected override EnemyAttack[] Patterns => new[] { EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02, EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05 };
        [MenuItem("K4RMA/보스 연습/상승베기형")]
        public static void Open() { GetWindow<KarmaRisingPracticeWindow>("상승베기형 연습"); }
    }
}
