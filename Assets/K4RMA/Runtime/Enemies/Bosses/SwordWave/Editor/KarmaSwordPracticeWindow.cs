using UnityEditor;
namespace KarmaPrototype.EditorTools
{
    public sealed class KarmaSwordPracticeWindow : KarmaBossPracticeWindow
    {
        protected override Essence Kind => Essence.SwordWave;
        protected override string BossName => "검기형";
        protected override EnemyAttack[] Patterns => new[] { EnemyAttack.SWBasic, EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 };
        [MenuItem("K4RMA/보스 연습/검기형")]
        public static void Open() { GetWindow<KarmaSwordPracticeWindow>("검기형 연습"); }
    }
}
