using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    [InitializeOnLoad]
    public static class KarmaAdaptiveGrowthTour
    {
        const string Menu = "K4RMA/수호자 성장 미리보기/";
        const string RequestKey = "K4RMA.AdaptiveGrowthTour";
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static CancellationTokenSource active;
        static KarmaGame game;

        static KarmaAdaptiveGrowthTour()
        {
            EditorApplication.update += LaunchPending;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingPlayMode) return;
                SessionState.EraseString(RequestKey);
                active?.Cancel();
            };
        }

        [MenuItem(Menu + "전체 자동 재생", false, 0)]
        public static void PlayFullTour() => Request("Full");

        [MenuItem(Menu + "3스테이지/검기", false, 10)]
        static void PlayWave() => Request(nameof(Essence.SwordWave));
        [MenuItem(Menu + "3스테이지/상승베기", false, 11)]
        static void PlayRising() => Request(nameof(Essence.RisingSlash));
        [MenuItem(Menu + "3스테이지/방어", false, 12)]
        static void PlayGuard() => Request(nameof(Essence.Guard));

        [MenuItem(Menu + "재생 중지", false, 20)]
        static void Stop()
        {
            SessionState.EraseString(RequestKey);
            var director = game;
            active?.Cancel();
            if (director != null && EditorApplication.isPlaying) director.ReturnToTitle();
        }

        [MenuItem(Menu + "재생 중지", true)]
        static bool CanStop() => active != null || SessionState.GetString(RequestKey, "") != "";

        static void Request(string request)
        {
            if (active != null)
            {
                Notify("현재 미리보기를 먼저 중지하세요.");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                var director = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
                if (director != null && director.Phase != RunPhase.Title && director.Phase != RunPhase.Victory
                    && !EditorUtility.DisplayDialog("수호자 성장 미리보기", "현재 플레이를 끝내고 미리보기를 시작할까요?", "미리보기", "취소"))
                    return;
            }
            else
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
            }

            SessionState.SetString(RequestKey, request);
            EditorApplication.isPaused = false;
            GameView()?.Show();
            if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
        }

        static void LaunchPending()
        {
            string request = SessionState.GetString(RequestKey, "");
            if (request.Length == 0) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPlaying || active != null) return;
            var director = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            SessionState.EraseString(RequestKey);
            if (director == null) { Notify("KarmaPrototype 씬에서 미리보기를 실행하세요."); return; }
            Play(director, request);
        }

        static async void Play(KarmaGame director, string request)
        {
            var lifetime = new CancellationTokenSource();
            active = lifetime;
            game = director;
            try
            {
                KarmaBossPractice.Stop();
                game.StartRun();
                if (request == "Full") await FullTour(lifetime.Token);
                else
                {
                    var remaining = Enum.Parse<Essence>(request);
                    SeedStageThree(remaining);
                    var enemy = PrepareCombat();
                    await ShowFinalTechnique(enemy, lifetime.Token);
                    game.ReturnToTitle();
                }
                Notify("수호자 성장 미리보기 완료");
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogException(error); Notify("미리보기 중단 · Console을 확인하세요."); }
            finally { game = null; active = null; lifetime.Dispose(); }
        }

        static async Task FullTour(CancellationToken token)
        {
            var enemy = PrepareCombat();
            Notify("1/4 · 관찰과 숙련도 선두 변화");
            GiveLead(Essence.Guard);
            await ShowImitation(enemy, Essence.Guard, token);
            GiveLead(Essence.SwordWave);
            await ShowImitation(enemy, Essence.SwordWave, token);
            await DefeatAndAdvance(enemy, Essence.SwordWave, token);

            enemy = PrepareCombat();
            Notify("2/4 · 첫 계승과 두 번째 기술 모방");
            GiveLead(Essence.Guard);
            await ShowImitation(enemy, Essence.Guard, token);
            await DefeatAndAdvance(enemy, Essence.Guard, token);

            enemy = PrepareCombat();
            Notify("3/4 · 마지막 원형 시연");
            await ShowFinalTechnique(enemy, token);
            await DefeatAndAdvance(enemy, Essence.RisingSlash, token);

            enemy = PrepareCombat();
            Notify("4/4 · 세 기술을 결합한 궁극기와 엔딩");
            enemy.TakeDamage(enemy.MaxHealth * 0.62f, enemy.Position);
            Set(enemy, "nextUltimate", 0f);
            await Until(() => GameObject.Find("Mastery stance recall") != null, "최종 궁극기", token);
            await Until(() => !(bool)KarmaBossPractice.Get(enemy, "running") && !enemy.Invulnerable, "궁극기 종료", token);
            enemy.TakeDamage(enemy.MaxHealth * 2, enemy.Position);
            await Until(() => game.Phase == RunPhase.Victory, "엔딩", token, 120);
        }

        static KarmaEnemy PrepareCombat()
        {
            var enemy = game.Enemies[0];
            if (!game.Player.TestInvincible) game.Player.ToggleTestInvincibility();
            game.Player.ResetForRoom(new Vector2(enemy.Position.x - 6, KarmaSpace.PlayerHalfHeight), false);
            Physics2D.SyncTransforms();
            return enemy;
        }

        static void GiveLead(Essence technique)
        {
            var essences = game.Player.Essences;
            if (!game.IsCombat || !essences.HasActive(technique))
                throw new InvalidOperationException("The preview technique is not active: " + technique);
            foreach (Essence candidate in Enum.GetValues(typeof(Essence)))
            {
                if (candidate == technique || !essences.HasActive(candidate)) continue;
                while (essences.Mastery(technique) <= essences.Mastery(candidate))
                {
                    int before = essences.Mastery(technique);
                    KarmaBossPractice.Invoke(essences, "RecordSuccess", technique);
                    if (essences.Mastery(technique) <= before)
                        throw new InvalidOperationException("The preview could not award mastery: " + technique);
                }
            }
        }

        static async Task ShowImitation(KarmaEnemy enemy, Essence technique, CancellationToken token)
        {
            await Until(() => (bool)KarmaBossPractice.Get(enemy, "presentingCopy"), "전투 중 모방", token);
            Notify("모방 · " + KarmaPatternCatalog.Name(technique));
            await Until(() => !(bool)KarmaBossPractice.Get(enemy, "presentingCopy"), "모방 종료", token);
        }

        static async Task ShowFinalTechnique(KarmaEnemy enemy, CancellationToken token)
        {
            await Until(() => (bool)KarmaBossPractice.Get(enemy, "finalTechniqueReleased"), "마지막 기술 시연", token);
            await Until(() => !(bool)KarmaBossPractice.Get(enemy, "presentingCopy"), "시연 종료", token);
            float showUntil = Time.time + 1.4f;
            await Until(() => Time.time >= showUntil, "시연 여운", token);
        }

        static async Task DefeatAndAdvance(KarmaEnemy enemy, Essence expected, CancellationToken token)
        {
            await Until(() => !enemy.Invulnerable, "수호자 방어 종료", token);
            enemy.TakeDamage(enemy.MaxHealth * 2, enemy.Position);
            await Until(() => game.GuardianInheriting, "자동 고유화", token);
            if (game.LastPersonalizedEssence != expected)
                throw new InvalidOperationException("Unexpected personalization: " + game.LastPersonalizedEssence);
            await Until(() => (bool)typeof(KarmaGame).GetProperty("EncounterPromptReady", Members).GetValue(game), "수호자 계승", token);
            // Take the same release step as the entrance prompt after Space is accepted.
            var prompt = typeof(KarmaGame).GetProperty("CurrentEncounterPrompt", Members);
            prompt.SetValue(game, Enum.Parse(prompt.PropertyType, "WaitForRelease"));
            KarmaBossPractice.Invoke(game, "AdvanceEncounterPrompt");
            await Until(() => game.IsCombat, "다음 스테이지", token);
        }

        static void SeedStageThree(Essence remaining)
        {
            foreach (Essence technique in Enum.GetValues(typeof(Essence)))
            {
                if (technique == remaining) continue;
                game.Player.Essences.Preview(technique);
                game.Player.Essences.Confirm(out _);
            }
            typeof(KarmaGame).GetProperty(nameof(KarmaGame.StageIndex)).SetValue(game, 2);
            KarmaBossPractice.Invoke(game, "LoadRoom", true);
            Notify("3스테이지 · " + KarmaPatternCatalog.Name(remaining));
        }

        static async Task Until(Func<bool> ready, string step, CancellationToken token, float seconds = 60)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (game == null || game.Phase == RunPhase.Title || game.Phase == RunPhase.Defeat)
                    throw new OperationCanceledException();
                if (ready()) return;
                if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException(step);
                await Awaitable.NextFrameAsync(token);
            }
        }

        static void Set(KarmaEnemy enemy, string field, object value) =>
            typeof(KarmaEnemy).GetField(field, Members).SetValue(enemy, value);

        static EditorWindow GameView() => EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        static void Notify(string message)
        {
            Debug.Log("K4RMA · " + message);
            GameView()?.ShowNotification(new GUIContent(message));
        }
    }
}
