#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarmaPrototype.Editor
{
    // Editor-only practice; shared configuration assets are never modified.
    [InitializeOnLoad]
    public static class KarmaGuardPractice
    {
        const string ChoiceKey = "K4RMA.GuardPractice.Pattern";
        const string HealKey = "K4RMA.GuardPractice.Heal";
        const string Menu = "K4RMA/방어형 연습/";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly EnemyAttack[] Patterns = { EnemyAttack.GD01, EnemyAttack.GD02,
            EnemyAttack.GD03, EnemyAttack.GD04, EnemyAttack.GD05 };
        static KarmaGame game;
        static KarmaEnemy enemy;
        static float nextPattern;

        static KarmaGuardPractice()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Bootstrap;
        }

        [MenuItem(Menu + "전체 패턴 자연 전투", false, 0)] static void Natural() { Start(0); }
        [MenuItem(Menu + "GD01 자동방어막", false, 11)] static void Shield() { Start(1); }
        [MenuItem(Menu + "GD02 강공격·패링", false, 12)] static void Heavy() { Start(2); }
        [MenuItem(Menu + "GD03 분열 방패", false, 13)] static void Fragments() { Start(3); }
        [MenuItem(Menu + "GD04 방벽 상승", false, 14)] static void Barrier() { Start(4); }
        [MenuItem(Menu + "GD05 결계 생존 시험", false, 15)] static void Trial() { Start(5); }
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

        // 0 = natural final guardian; 1..5 = repeat one GD pattern.
        public static void Start(int pattern)
        {
            if (pattern < 0 || pattern > 5) throw new System.ArgumentOutOfRangeException(nameof(pattern));
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
            if (state == PlayModeStateChange.ExitingPlayMode)
                Stop();
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
            game.StartRun();
            game.Player.Essences.Sacrifice(Essence.Guard);
            game.Player.Essences.Sacrifice(Essence.SwordWave);
            game.Player.Essences.Sacrifice(Essence.RisingSlash);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
            Call(game, "LoadRoom", true);
            game.Player.ResetForRoom(new Vector2(20, KarmaSpace.Units(1)), true);
            enemy = game.Enemies[0];
            if (game.GetComponent<KarmaGuardPracticeDriver>() == null)
                game.gameObject.AddComponent<KarmaGuardPracticeDriver>();
            Field(enemy, "engaged").SetValue(enemy, true);
            int choice = SessionState.GetInt(ChoiceKey, 0);
            nextPattern = enemy.FightSeconds + 1;
            Application.runInBackground = true;
            Debug.Log("방어형 연습: " + (choice == 0 ? "자연 전투" : Patterns[choice - 1].ToString())
                + " · K 반격 / F6 다시 시작. 체력 자동 회복은 연습 메뉴에서 선택하세요.");
        }

        internal static void Tick()
        {
            int choice = SessionState.GetInt(ChoiceKey, -1);
            if (choice < 0 || !EditorApplication.isPlaying || EditorApplication.isPaused || game == null || !game.IsCombat) return;
            if (game.Enemies.Count > 0 && game.Enemies[0] != enemy) { Begin(); return; }
            if (SessionState.GetBool(HealKey, false)) game.Player.Heal(game.Config.playerHealth);
            if (choice == 0 || enemy == null || !enemy.Alive) return;
            Field(enemy, "timer").SetValue(enemy, float.MaxValue);
            if (choice == 1) return;
            Field(enemy, "nextShield").SetValue(enemy, float.MaxValue);
            if ((bool)Field(enemy, "running").GetValue(enemy) || enemy.Stunned || (bool)Field(enemy, "pendingParry").GetValue(enemy))
            { nextPattern = enemy.FightSeconds + 1; return; }
            if (enemy.FightSeconds < nextPattern) return;
            Call(enemy, "InterruptPattern");
            Field(enemy, "selected").SetValue(enemy, Patterns[choice - 1]);
            Field(enemy, "direction").SetValue(enemy, game.Player.Position.x < enemy.Position.x ? -1f : 1f);
            Field(enemy, "running").SetValue(enemy, true);
            Field(enemy, "patternStarted").SetValue(enemy, enemy.FightSeconds);
            if (choice == 2) Call(enemy, "RefreshAutoShield");
            enemy.StartCoroutine((System.Collections.IEnumerator)Call(enemy, "RunPattern"));
        }

        static FieldInfo Field(object target, string name) { return target.GetType().GetField(name, Private); }
        static object Call(object target, string name, params object[] args)
        { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    }

    // Sprite.OverrideGeometry must run in Unity's player loop, not EditorApplication.update.
    public sealed class KarmaGuardPracticeDriver : MonoBehaviour
    {
        void Update() { KarmaGuardPractice.Tick(); }
    }
}
#endif
