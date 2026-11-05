using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace KarmaPrototype.EditorTools
{
    // Editor-only controls. Boss menus, pattern catalogs and AI holds are owned by each boss.
    public abstract class KarmaBossPracticeWindow : EditorWindow
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        protected abstract Essence Kind { get; }
        protected abstract EnemyAttack[] Patterns { get; }
        protected abstract string BossName { get; }
        protected virtual bool RunPassive(KarmaEnemy target, EnemyAttack pattern) { return false; }
        [SerializeField] int form = 3, patternIndex;
        [SerializeField] bool openRequested, randomCombat, repeat, sequence;
        [SerializeField] bool protectPlayer = true, preserveBoss = true;
        KarmaGame game;
        KarmaEnemy enemy;
        Label status;
        ScrollView history;
        bool awaitingCompletion, startingPattern;
        double nextRunAt, deadline;
        float playerBefore, bossBefore;
        int parriesBefore;
        string lastStatus = "전체 패턴 전투 또는 개별 패턴 실행을 선택하세요.";
        protected virtual void OnEnable() { EditorApplication.update += Tick; }
        protected virtual void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (enemy != null && EditorApplication.isPlaying) { StopPractice(); SetHold(false); }
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12;
            rootVisualElement.Add(new Label(BossName + " 패턴 연습") { style = { fontSize = 19, unityFontStyleAndWeight = FontStyle.Bold } });
            rootVisualElement.Add(new HelpBox("실제 전투 코드로 연습합니다. 전체 전투는 확률 AI를 사용하고, 개별·순회 실행은 선택한 패턴을 강제로 발동합니다.", HelpBoxMessageType.Info));
            var forms = new List<string> { "기본형", "첫 번째 계승", "두 번째 계승", "최종 계승 · 전체 패턴" };
            var formField = new DropdownField("보스 형태", forms, Mathf.Clamp(form, 0, 3));
            formField.RegisterValueChangedCallback(e => form = forms.IndexOf(e.newValue)); rootVisualElement.Add(formField);
            var labels = new List<string>(); foreach (var p in Patterns) labels.Add(p == EnemyAttack.GroundStrike ? "기본 공격" : p.ToString());
            var patternField = new DropdownField("검사할 패턴", labels, Mathf.Clamp(patternIndex, 0, labels.Count - 1));
            patternField.RegisterValueChangedCallback(e => patternIndex = labels.IndexOf(e.newValue)); rootVisualElement.Add(patternField);
            var invincible = new Toggle("플레이어 무적") { value = protectPlayer };
            invincible.RegisterValueChangedCallback(e => protectPlayer = e.newValue); rootVisualElement.Add(invincible);
            var life = new Toggle("보스 체력 유지") { value = preserveBoss };
            life.RegisterValueChangedCallback(e => preserveBoss = e.newValue); rootVisualElement.Add(life);
            AddButton("전체 패턴 전투로 바로 입장", () => RequestRoom(true, false, false));
            AddButton("선택 패턴 1회 실행", () => RequestRoom(false, false, false));
            AddButton("선택 패턴 반복", () => RequestRoom(false, true, false));
            AddButton("전체 패턴 순회 검사", () => RequestRoom(false, false, true));
            AddButton("현재 패턴 중단", StopPractice);
            AddButton("체력 49% · AI 전투 재개", () => SetHealthFraction(0.49f));
            AddButton("체력 24% · AI 전투 재개", () => SetHealthFraction(0.24f));
            status = new Label(lastStatus) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 10 } }; rootVisualElement.Add(status);
            history = new ScrollView { style = { flexGrow = 1, minHeight = 100, marginTop = 8 } }; rootVisualElement.Add(history);
        }
        void AddButton(string text, Action action) { rootVisualElement.Add(new Button(action) { text = text, style = { marginTop = 4, height = 27 } }); }
        void SetHealthFraction(float fraction)
        {
            if (enemy == null) return;
            preserveBoss = false; SetProperty(enemy, "Health", enemy.MaxHealth * fraction);
            SetHold(false); randomCombat = true; repeat = sequence = awaitingCompletion = false;
            Report("체력 " + Mathf.RoundToInt(fraction * 100) + "% · 보스의 체력 조건을 적용한 AI 전투를 재개합니다.");
        }
        public void RequestRoom(bool combat, bool loop, bool all)
        {
            randomCombat = combat; repeat = loop; sequence = all; openRequested = true;
            awaitingCompletion = startingPattern = false; nextRunAt = 0;
            if (combat || all) form = 3;
            if (all) patternIndex = 0;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { openRequested = false; return; }
                EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
                EditorApplication.EnterPlaymode();
            }
        }
        void OpenRoom()
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) return;
            game.StartRun();
            var others = new List<Essence>();
            foreach (Essence kind in Enum.GetValues(typeof(Essence))) if (kind != Kind) others.Add(kind);
            var order = new List<Essence> { Kind };
            if (form == 1 || form == 3) order.Add(others[0]);
            if (form == 2 || form == 3) order.Add(others[1]);
            foreach (var kind in order) { game.Player.Essences.Preview(kind); game.Player.Essences.Confirm(out _); }
            SetProperty(game, "StageIndex", order.Count);
            Invoke(game, "LoadRoom", true);
            enemy = game.Enemies[0];
            var playerAt = new Vector2(18, KarmaSpace.PlayerHalfHeight);
            game.Player.ResetForRoom(playerAt, true);
            enemy.transform.position = new Vector3(24, KarmaSpace.GuardianGroundY, 0);
            Set(enemy, "engaged", true);
            if (preserveBoss) { SetProperty(enemy, "MaxHealth", 1000000f); SetProperty(enemy, "Health", 1000000f); }
            SetHold(!randomCombat);
            openRequested = false;
            if (randomCombat) Report("전체 패턴 전투 시작 · 최종 계승형 · 확률 AI");
            else StartPattern();
        }
        void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (openRequested) { OpenRoom(); return; }
            if (enemy == null || game == null) return;
            if (game.Player.TestInvincible != protectPlayer) game.Player.ToggleTestInvincibility();
            if (game.Paused) return;
            if (preserveBoss && enemy.Alive) SetProperty(enemy, "Health", enemy.MaxHealth);
            if (startingPattern) return;
            if (awaitingCompletion)
            {
                if (!game.IsCombat) { Report("전투 종료 · 재입장해서 계속 연습할 수 있습니다."); awaitingCompletion = false; repeat = sequence = false; return; }
                if (!(bool)Get(enemy, "running") && EditorApplication.timeSinceStartup >= nextRunAt)
                {
                    Report(Patterns[patternIndex] + " 완료 · 플레이어 피해 " + (playerBefore - game.Player.Health).ToString("F1")
                        + " · 보스 피해 " + (bossBefore - enemy.Health).ToString("F1") + " · 패링 " + (game.Player.ParryCount - parriesBefore));
                    awaitingCompletion = false;
                    if (sequence && ++patternIndex >= Patterns.Length) { sequence = false; patternIndex = Patterns.Length - 1; Report("전체 패턴 순회 완료"); }
                    nextRunAt = EditorApplication.timeSinceStartup + 0.8;
                }
                else if (EditorApplication.timeSinceStartup > deadline)
                { StopPractice(); Report("시간 초과 · 현재 패턴을 중단했습니다. Console에서 오류를 확인하세요."); }
            }
            else if ((repeat || sequence) && EditorApplication.timeSinceStartup >= nextRunAt) StartPattern();
        }
        public void StartPattern()
        {
            if (enemy == null || !game.IsCombat || startingPattern) return;
            startingPattern = true;
            enemy.StartCoroutine(StartOnPlayerLoop(enemy));
        }
        System.Collections.IEnumerator StartOnPlayerLoop(KarmaEnemy target)
        {
            // Editor callbacks cannot safely change runtime Sprite geometry. Resume in
            // the normal player coroutine loop before constructing pattern warnings.
            yield return null;
            startingPattern = false;
            if (target != enemy || enemy == null || !game.IsCombat) yield break;
            ExecutePattern();
        }
        void ExecutePattern()
        {
            var pattern = Patterns[Mathf.Clamp(patternIndex, 0, Patterns.Length - 1)];
            Invoke(enemy, "InterruptPattern"); SetHold(true);
            playerBefore = game.Player.Health; bossBefore = enemy.Health; parriesBefore = game.Player.ParryCount;
            awaitingCompletion = true; nextRunAt = EditorApplication.timeSinceStartup + 0.25;
            deadline = EditorApplication.timeSinceStartup + 90;
            Report(pattern + " 실행 중 · 실제 명중·패링·완료를 확인하세요.");
            if (RunPassive(enemy, pattern)) { nextRunAt += 2; return; }
            Set(enemy, "selected", pattern); Set(enemy, "previous", (EnemyAttack)(-1));
            float facing = game.Player.Position.x < enemy.Position.x ? -1 : 1;
            Set(enemy, "direction", facing);
            ((Transform)Get(enemy, "art")).localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
            Invoke(Get(enemy, "behavior"), "OnSelected");
            enemy.StartCoroutine((System.Collections.IEnumerator)Invoke(enemy, "RunPattern"));
        }
        void StopPractice()
        {
            repeat = sequence = awaitingCompletion = startingPattern = randomCombat = openRequested = false;
            if (enemy != null && EditorApplication.isPlaying) { Invoke(enemy, "InterruptPattern"); SetHold(true); }
            Report("연습 중단 · 다음 실행을 기다립니다.");
        }
        void SetHold(bool value)
        {
            if (enemy == null) return;
            var behavior = Get(enemy, "behavior");
            Set(behavior, "PracticeHold", value);
        }
        void Report(string message)
        {
            lastStatus = message;
            if (status != null) status.text = message;
            if (history != null) history.Add(new Label(message) { style = { whiteSpace = WhiteSpace.Normal } });
        }
        protected static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
        protected static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType) { var field = type.GetField(name, Members); if (field != null) return field; }
            throw new MissingFieldException(name);
        }
        protected static object Invoke(object target, string name, params object[] arguments)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            { var method = type.GetMethod(name, Members | BindingFlags.DeclaredOnly); if (method != null) return method.Invoke(target, arguments); }
            throw new MissingMethodException(name);
        }
        static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name, Members).SetValue(target, value);
    }
}
