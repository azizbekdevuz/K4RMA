using UnityEditor;
namespace KarmaPrototype.EditorTools
{
    [InitializeOnLoad]
    public static class KarmaRisingPracticeMenu
    {
        static KarmaRisingPracticeMenu()
        {
            KarmaBossPractice.Register(new KarmaBossPractice.Definition(Essence.RisingSlash, "상승베기형", new[] {
                new KarmaBossPractice.Pattern(EnemyAttack.GroundStrike, "기본 공격"),
                new KarmaBossPractice.Pattern(EnemyAttack.UP01, "UP01"),
                new KarmaBossPractice.Pattern(EnemyAttack.UP02, "UP02"),
                new KarmaBossPractice.Pattern(EnemyAttack.UP03, "UP03", Essence.SwordWave),
                new KarmaBossPractice.Pattern(EnemyAttack.UP04, "UP04", Essence.Guard),
                new KarmaBossPractice.Pattern(EnemyAttack.UP05, "UP05", Essence.SwordWave, Essence.Guard),
            }));
        }
        [MenuItem("K4RMA/상승베기형/전체 패턴 연습", false, 10)]
        public static void All() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Combat);
        [MenuItem("K4RMA/상승베기형/기본 공격 연습", false, 30)]
        public static void Pattern0() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Single, 0);
        [MenuItem("K4RMA/상승베기형/UP01 연습", false, 31)]
        public static void Pattern1() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Single, 1);
        [MenuItem("K4RMA/상승베기형/UP02 연습", false, 32)]
        public static void Pattern2() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Single, 2);
        [MenuItem("K4RMA/상승베기형/UP03 연습", false, 33)]
        public static void Pattern3() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Single, 3);
        [MenuItem("K4RMA/상승베기형/UP04 연습", false, 34)]
        public static void Pattern4() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Single, 4);
        [MenuItem("K4RMA/상승베기형/UP05 연습", false, 35)]
        public static void Pattern5() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Single, 5);
        [MenuItem("K4RMA/상승베기형/전체 패턴 순회 검사", false, 60)]
        public static void Sequence() => KarmaBossPractice.Request(Essence.RisingSlash, KarmaBossPractice.Mode.Sequence);
    }
}
