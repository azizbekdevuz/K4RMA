#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarmaPrototype
{
    // Editor-only practice. It never writes shared configuration assets.
    [InitializeOnLoad]
    public static class KarmaRisingPractice
    {
        const string ChoiceKey = "K4RMA.RisingPractice.Pattern";
        const string HealKey = "K4RMA.RisingPractice.Heal";
        const string InvincibleKey = "K4RMA.RisingPractice.Invincible";
        const string Menu = "K4RMA/상승베기 연습/";
        static readonly EnemyAttack[] Patterns =
        {
            EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02,
            EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05
        };
        static KarmaGame game;
        static KarmaEnemy enemy;
        static float nextPattern;
        static bool armedInvincible;

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
        [MenuItem(Menu + "플레이어 무적", false, 32)]
        static void ToggleInvincible() { SessionState.SetBool(InvincibleKey, !SessionState.GetBool(InvincibleKey, false)); }
        [MenuItem(Menu + "플레이어 무적", true)]
        static bool InvincibleCheck()
        { UnityEditor.Menu.SetChecked(Menu + "플레이어 무적", SessionState.GetBool(InvincibleKey, false)); return true; }
        [MenuItem(Menu + "연습 종료·타이틀", false, 33)]
        public static void Stop()
        {
            SessionState.SetInt(ChoiceKey, -1);
            ClearInvincible();
            if (enemy != null) enemy.ReleasePatternIsolation();
            if (EditorApplication.isPlaying && game != null) game.ReturnToTitle();
            game = null;
            enemy = null;
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

        public static int Choice { get { return SessionState.GetInt(ChoiceKey, -1); } }
        public static string ChoiceLabel
        {
            get
            {
                int choice = Choice;
                if (choice < 0) return "꺼짐";
                return choice == 0 ? "자연 전투" : Patterns[choice - 1].ToString();
            }
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            SessionState.SetInt(ChoiceKey, -1);
            ClearInvincible();
            game = null;
            enemy = null;
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
            Essence[] order = OrderFor(choice);
            game.StartRun();
            for (int i = 0; i < order.Length; i++) game.Player.Essences.GrantForPractice(order[i]);
            game.LoadPracticeRoom(order.Length);
            game.Player.ResetForRoom(new Vector2(18f, KarmaSpace.Units(1f)), true);
            enemy = game.Enemies[0];
            enemy.ReleasePatternIsolation();
            if (choice != 0) enemy.ArmPatternIsolation();
            if (game.GetComponent<KarmaRisingPracticeDriver>() == null)
                game.gameObject.AddComponent<KarmaRisingPracticeDriver>();
            ApplyInvincible();
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
            ApplyInvincible();
            if (SessionState.GetBool(HealKey, false)) game.Player.Heal(game.Config.playerHealth);
            if (choice == 0 || enemy == null || !enemy.Alive) return;
            if (enemy.PracticeBusy)
            {
                nextPattern = enemy.FightSeconds + 0.85f;
                return;
            }
            if (enemy.FightSeconds < nextPattern) return;
            enemy.ReplayIsolatedPattern(Patterns[choice - 1]);
        }

        static Essence[] OrderFor(int choice)
        {
            if (choice == 4) return new[] { Essence.RisingSlash, Essence.SwordWave };
            if (choice == 5) return new[] { Essence.RisingSlash, Essence.Guard };
            if (choice == 0 || choice == 6) return new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard };
            return new[] { Essence.RisingSlash };
        }

        static void ApplyInvincible()
        {
            if (game == null || game.Player == null) return;
            bool want = SessionState.GetBool(InvincibleKey, false);
            if (want && !game.Player.TestInvincible) { game.Player.ToggleTestInvincibility(); armedInvincible = true; }
            else if (!want && armedInvincible && game.Player.TestInvincible) { game.Player.ToggleTestInvincibility(); armedInvincible = false; }
        }

        static void ClearInvincible()
        {
            if (armedInvincible && game != null && game.Player != null && game.Player.TestInvincible)
                game.Player.ToggleTestInvincibility();
            armedInvincible = false;
        }
    }

    public sealed class KarmaRisingPracticeDriver : MonoBehaviour
    {
        void Update() { KarmaRisingPractice.Tick(); }
    }

    public sealed class RisingSlashPatternHarness : EditorWindow
    {
        [MenuItem("K4RMA/Testing/Rising Slash Pattern Harness")]
        static void Open() { GetWindow<RisingSlashPatternHarness>("Rising Slash Harness"); }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("K4RMA > 상승베기 연습 메뉴가 연습을 바로 시작합니다. 씬을 열고 플레이 모드에 들어간 뒤, 고른 패턴만 반복합니다.", MessageType.Info);
            EditorGUILayout.LabelField("현재", KarmaRisingPractice.ChoiceLabel);
            if (GUILayout.Button("자연 전투")) KarmaRisingPractice.Start(0);
            if (GUILayout.Button("기본 베기")) KarmaRisingPractice.Start(1);
            if (GUILayout.Button("UP01")) KarmaRisingPractice.Start(2);
            if (GUILayout.Button("UP02")) KarmaRisingPractice.Start(3);
            if (GUILayout.Button("UP03")) KarmaRisingPractice.Start(4);
            if (GUILayout.Button("UP04")) KarmaRisingPractice.Start(5);
            if (GUILayout.Button("UP05")) KarmaRisingPractice.Start(6);
            if (GUILayout.Button("다시 시작")) KarmaRisingPractice.Start(Mathf.Max(0, KarmaRisingPractice.Choice));
            if (GUILayout.Button("연습 종료")) KarmaRisingPractice.Stop();
        }
    }
}
#endif
