using UnityEditor;
namespace KarmaPrototype.EditorTools
{
    [InitializeOnLoad]
    public static class KarmaSwordPracticeMenu
    {
        static KarmaSwordPracticeMenu()
        {
            KarmaBossPractice.Register(new KarmaBossPractice.Definition(Essence.SwordWave, "검기형", new[] {
                new KarmaBossPractice.Pattern(EnemyAttack.SWBasic, "기본 공격"),
                new KarmaBossPractice.Pattern(EnemyAttack.SW01, "SW01"),
                new KarmaBossPractice.Pattern(EnemyAttack.SW02, "SW02"),
                new KarmaBossPractice.Pattern(EnemyAttack.SW03, "SW03", Essence.RisingSlash),
                new KarmaBossPractice.Pattern(EnemyAttack.SW04, "SW04", Essence.Guard),
                new KarmaBossPractice.Pattern(EnemyAttack.SW05, "SW05", Essence.RisingSlash, Essence.Guard),
            }));
        }
        [MenuItem("K4RMA/검기형/전체 패턴 연습", false, 10)]
        public static void All() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Combat);
        [MenuItem("K4RMA/검기형/기본 공격 연습", false, 30)]
        public static void Pattern0() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Single, 0);
        [MenuItem("K4RMA/검기형/SW01 연습", false, 31)]
        public static void Pattern1() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Single, 1);
        [MenuItem("K4RMA/검기형/SW02 연습", false, 32)]
        public static void Pattern2() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Single, 2);
        [MenuItem("K4RMA/검기형/SW03 연습", false, 33)]
        public static void Pattern3() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Single, 3);
        [MenuItem("K4RMA/검기형/SW04 연습", false, 34)]
        public static void Pattern4() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Single, 4);
        [MenuItem("K4RMA/검기형/SW05 연습", false, 35)]
        public static void Pattern5() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Single, 5);
        [MenuItem("K4RMA/검기형/전체 패턴 순회 검사", false, 60)]
        public static void Sequence() => KarmaBossPractice.Request(Essence.SwordWave, KarmaBossPractice.Mode.Sequence);
    }
}
