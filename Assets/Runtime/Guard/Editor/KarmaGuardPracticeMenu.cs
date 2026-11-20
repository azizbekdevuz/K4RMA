using UnityEditor;
namespace KarmaPrototype.EditorTools
{
    [InitializeOnLoad]
    public static class KarmaGuardPracticeMenu
    {
        static KarmaGuardPracticeMenu()
        {
            KarmaBossPractice.Register(new KarmaBossPractice.Definition(Essence.Guard, "방어형", new[] {
                new KarmaBossPractice.Pattern(EnemyAttack.GroundStrike, "기본 공격"),
                new KarmaBossPractice.Pattern(EnemyAttack.GD01, "GD01"),
                new KarmaBossPractice.Pattern(EnemyAttack.GD02, "GD02"),
                new KarmaBossPractice.Pattern(EnemyAttack.GD03, "GD03", Essence.SwordWave),
                new KarmaBossPractice.Pattern(EnemyAttack.GD04, "GD04", Essence.RisingSlash),
                new KarmaBossPractice.Pattern(EnemyAttack.GD05, "GD05", Essence.SwordWave, Essence.RisingSlash),
            }, RunPassive));
        }
        [MenuItem("K4RMA/방어형/전체 패턴 연습", false, 10)]
        public static void All() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Combat);
        [MenuItem("K4RMA/방어형/기본 공격 연습", false, 30)]
        public static void Pattern0() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Single, 0);
        [MenuItem("K4RMA/방어형/GD01 연습", false, 31)]
        public static void Pattern1() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Single, 1);
        [MenuItem("K4RMA/방어형/GD02 연습", false, 32)]
        public static void Pattern2() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Single, 2);
        [MenuItem("K4RMA/방어형/GD03 연습", false, 33)]
        public static void Pattern3() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Single, 3);
        [MenuItem("K4RMA/방어형/GD04 연습", false, 34)]
        public static void Pattern4() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Single, 4);
        [MenuItem("K4RMA/방어형/GD05 연습", false, 35)]
        public static void Pattern5() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Single, 5);
        [MenuItem("K4RMA/방어형/전체 패턴 순회 검사", false, 60)]
        public static void Sequence() => KarmaBossPractice.Request(Essence.Guard, KarmaBossPractice.Mode.Sequence);
        static bool RunPassive(KarmaEnemy target, EnemyAttack pattern)
        {
            if (pattern != EnemyAttack.GD01) return false;
            var shield = KarmaBossPractice.Get(KarmaBossPractice.Get(target, "behavior"), "shield");
            KarmaBossPractice.Invoke(shield, "RefreshAutoShield"); return true;
        }
    }
}
