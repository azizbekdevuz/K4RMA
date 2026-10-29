#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarmaPrototype.Editor
{
    // Editor-only practice. Shared configuration assets are never written.
    [InitializeOnLoad]
    public static class KarmaRisingPractice
    {
        const string ChoiceKey = "K4RMA.RisingPractice.Pattern";
        const string HealKey = "K4RMA.RisingPractice.Heal";
        const string Menu = "K4RMA/상승베기 연습/";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly EnemyAttack[] Patterns =
        {
            EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02,
            EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05
        };
        static KarmaGame game;
        static KarmaEnemy enemy;
        static float nextPattern;

        static KarmaRisingPractice()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Bootstrap;
        }

        [MenuItem(Menu + "전체 패턴 자연 전투", false, 0)] static void Natural() { Start(0); }
        [MenuItem(Menu + "기본 베기", false, 11)] static void Melee() { Start(1); }
        [MenuItem(Menu + "UP01 착지 범위", false, 12)] static void Area() { Start(2); }
        [MenuItem(Menu + "UP02 공중 추격", false, 13)] static void Pursuit() { Start(3); }
        [MenuItem(Menu + "UP03 계승 검기", false, 14)] static void Wave() { Start(4); }
        [MenuItem(Menu + "UP04 파편 패링", false, 15)] static void Fragments() { Start(5); }
        [MenuItem(Menu + "UP05 연속 횡단", false, 16)] static void Ultimate() { Start(6); }
        [MenuItem(Menu + "다시 시작 _F6", false, 30)]
        static void Restart() { Start(Mathf.Max(0, SessionState.GetInt(ChoiceKey, 0))); }
        [MenuItem(Menu + "체력 자동 회복", false, 31)]
        static void ToggleHealing() { SessionState.SetBool(HealKey, !SessionState.GetBool(HealKey, false)); }
        [MenuItem(Menu + "체력 자동 회복", true)]
        static bool HealingCheck()
        { UnityEditor.Menu.SetChecked(Menu + "체력 자동 회복", SessionState.GetBool(HealKey, false)); return true; }
        [MenuItem(Menu + "연습 종료·타이틀", false, 32)]
        public static void Stop()
        {
            SessionState.SetInt(ChoiceKey, -1);
            if (game != null) game.ReturnToTitle();
            game = null; enemy = null;
        }

        public static void Start(int pattern)
        {
            if (pattern < 0 || pattern > 6) throw new System.ArgumentOutOfRangeException(nameof(pattern));
            if (!EditorApplication.isPlaying)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
                SessionState.SetInt(ChoiceKey, pattern);
                EditorApplication.EnterPlaymode();
            }
            else
            {
                SessionState.SetInt(ChoiceKey, pattern);
                Begin();
            }
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        static void Bootstrap()
        {
            if (EditorApplication.isPlaying && game == null && SessionState.GetInt(ChoiceKey, -1) >= 0
                && Object.FindAnyObjectByType<KarmaGame>() != null) Begin();
        }

        static void Begin()
        {
            if (!EditorApplication.isPlaying || SessionState.GetInt(ChoiceKey, -1) < 0) return;
            game = Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) return;
            int choice = SessionState.GetInt(ChoiceKey, 0);
            var order = OrderFor(choice);
            game.StartRun();
            foreach (var essence in order) game.Player.Essences.Sacrifice(essence);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Length);
            Call(game, "LoadRoom", true);
            game.Player.ResetForRoom(new Vector2(18, KarmaSpace.Units(1)), true);
            enemy = game.Enemies[0];
            if (game.GetComponent<KarmaRisingPracticeDriver>() == null)
                game.gameObject.AddComponent<KarmaRisingPracticeDriver>();
            Field(enemy, "engaged").SetValue(enemy, true);
            nextPattern = enemy.FightSeconds + 0.6f;
            Application.runInBackground = true;
            Debug.Log("상승베기 연습: " + (choice == 0 ? "자연 전투" : Patterns[choice - 1].ToString())
                + " · 이동 A/D, 점프 Space, 공격 J, 반격 K, F6 다시 시작.");
        }

        internal static void Tick()
        {
            int choice = SessionState.GetInt(ChoiceKey, -1);
            if (choice < 0 || !EditorApplication.isPlaying || EditorApplication.isPaused || game == null || !game.IsCombat) return;
            if (game.Enemies.Count == 0 || game.Enemies[0] == null) return;
            if (game.Enemies[0] != enemy) { Begin(); return; }
            if (SessionState.GetBool(HealKey, false)) game.Player.Heal(game.Config.playerHealth);
            if (choice == 0 || enemy == null || !enemy.Alive) return;
            Field(enemy, "timer").SetValue(enemy, float.MaxValue);
            if ((bool)Field(enemy, "running").GetValue(enemy) || enemy.Stunned || (bool)Field(enemy, "pendingParry").GetValue(enemy))
            { nextPattern = enemy.FightSeconds + 0.85f; return; }
            if (enemy.FightSeconds < nextPattern) return;
            Call(enemy, "InterruptPattern");
            Field(enemy, "selected").SetValue(enemy, Patterns[choice - 1]);
            Field(enemy, "direction").SetValue(enemy, game.Player.Position.x < enemy.Position.x ? -1f : 1f);
            Field(enemy, "running").SetValue(enemy, true);
            Field(enemy, "patternStarted").SetValue(enemy, enemy.FightSeconds);
            enemy.StartCoroutine((System.Collections.IEnumerator)Call(enemy, "RunPattern"));
        }

        static Essence[] OrderFor(int choice)
        {
            if (choice == 4) return new[] { Essence.Dash, Essence.Flame };
            if (choice == 5) return new[] { Essence.Dash, Essence.Ward };
            if (choice == 0 || choice == 6) return new[] { Essence.Dash, Essence.Flame, Essence.Ward };
            return new[] { Essence.Dash };
        }

        static FieldInfo Field(object target, string name) { return target.GetType().GetField(name, Private); }
        static object Call(object target, string name, params object[] args)
        { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    }

    public sealed class KarmaRisingPracticeDriver : MonoBehaviour
    {
        void Update() { KarmaRisingPractice.Tick(); }
    }
}
#endif
