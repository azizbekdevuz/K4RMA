#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed class RisingSlashPatternHarness : EditorWindow
    {
        static readonly EnemyAttack[] Attacks =
        {
            EnemyAttack.GroundStrike, EnemyAttack.UP01, EnemyAttack.UP02,
            EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05
        };
        static readonly string[] Labels = { "UPBasic", "UP01", "UP02", "UP03", "UP04", "UP05" };
        int index;
        bool repeat, invincible, restoreHealth = true, session, armedInvincible;
        int repeats;
        string status = "Idle";

        [MenuItem("K4RMA/Testing/Rising Slash Pattern Harness")]
        static void Open() { GetWindow<RisingSlashPatternHarness>("Rising Slash Harness"); }

        void OnEnable() { EditorApplication.playModeStateChanged += OnPlayMode; }
        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            Stop();
        }
        void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        void Update()
        {
            if (!session || !EditorApplication.isPlaying) return;
            KarmaGame game = Game();
            KarmaEnemy boss = Boss(game);
            if (game == null || game.Player == null || boss == null || game.Phase != RunPhase.Combat)
            { status = "Waiting for a Rising Slash fight"; Repaint(); return; }
            ApplyInvincible(game.Player);
            if (boss.PatternTestRunning) { status = repeat ? "Repeating" : "Running"; Repaint(); return; }
            if (!repeat) { status = "Finished"; Repaint(); return; }
            Launch(game, boss);
        }

        void OnGUI()
        {
            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode and reach a Rising Slash fight. Inherited patterns run here without changing progression.", MessageType.Info);
            index = EditorGUILayout.Popup("Attack", index, Labels);
            invincible = EditorGUILayout.Toggle("Player invincible", invincible);
            restoreHealth = EditorGUILayout.Toggle("Restore health between attacks", restoreHealth);
            EditorGUILayout.LabelField("Status", status);
            EditorGUILayout.LabelField("Selected", Labels[index]);
            EditorGUILayout.LabelField("Repeats", repeats.ToString());
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Run once")) { repeat = false; session = true; LaunchCurrent(); }
                if (GUILayout.Button("Repeat")) { repeat = true; session = true; LaunchCurrent(); }
                if (GUILayout.Button("Stop")) Stop();
                if (GUILayout.Button("Restart")) { session = true; Restart(); }
            }
        }

        void LaunchCurrent()
        {
            KarmaGame game = Game();
            KarmaEnemy boss = Boss(game);
            if (game == null || boss == null || game.Player == null || game.Phase != RunPhase.Combat)
            { status = "Waiting for a Rising Slash fight"; return; }
            Launch(game, boss);
        }

        void Launch(KarmaGame game, KarmaEnemy boss)
        {
            ApplyInvincible(game.Player);
            game.Player.ResetForRoom(new Vector2(game.Player.Position.x, KarmaSpace.GuardianGroundY), restoreHealth);
            ApplyInvincible(game.Player);
            boss.BeginRisingPatternTest(Attacks[index]);
            repeats++;
            status = repeat ? "Repeating" : "Running";
            Repaint();
        }

        void Restart()
        {
            KarmaEnemy boss = Boss(Game());
            if (boss != null) boss.CancelRisingPatternTest(false);
            LaunchCurrent();
        }

        void Stop()
        {
            session = false;
            repeat = false;
            if (!EditorApplication.isPlaying) { ClearInvincible(null); status = "Idle"; return; }
            KarmaGame game = Game();
            KarmaEnemy boss = Boss(game);
            if (boss != null) boss.CancelRisingPatternTest(true);
            ClearInvincible(game != null ? game.Player : null);
            status = "Stopped";
            Repaint();
        }

        void ApplyInvincible(KarmaPlayer player)
        {
            if (player == null) return;
            if (invincible && !player.TestInvincible) { player.ToggleTestInvincibility(); armedInvincible = true; }
            else if (!invincible && armedInvincible && player.TestInvincible) { player.ToggleTestInvincibility(); armedInvincible = false; }
        }
        void ClearInvincible(KarmaPlayer player)
        {
            if (armedInvincible && player != null && player.TestInvincible) player.ToggleTestInvincibility();
            armedInvincible = false;
        }

        static KarmaGame Game() { return Object.FindAnyObjectByType<KarmaGame>(); }
        static KarmaEnemy Boss(KarmaGame game)
        {
            if (game == null) return null;
            KarmaEnemy fallen = null;
            for (int i = 0; i < game.Enemies.Count; i++)
            {
                KarmaEnemy enemy = game.Enemies[i];
                if (enemy == null || enemy.PrimaryType != Essence.RisingSlash) continue;
                if (enemy.Alive) return enemy;
                fallen = enemy;
            }
            return fallen;
        }
    }
}
#endif
