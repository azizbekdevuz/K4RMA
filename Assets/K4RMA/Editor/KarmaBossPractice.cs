using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    // Boss-local menus supply their patterns, inheritance requirements and passive setup.
    [InitializeOnLoad]
    public static class KarmaBossPractice
    {
        public enum Mode { Combat, Single, Sequence }
        public sealed class Pattern
        {
            public readonly EnemyAttack Attack;
            public readonly string Label;
            public readonly Essence[] Inherited;
            public Pattern(EnemyAttack attack, string label, params Essence[] inherited)
            { Attack = attack; Label = label; Inherited = inherited; }
        }
        public sealed class Definition
        {
            public readonly Essence Kind;
            public readonly string Name;
            public readonly Pattern[] Patterns;
            public readonly Func<KarmaEnemy, EnemyAttack, bool> Passive;
            public Definition(Essence kind, string name, Pattern[] patterns, Func<KarmaEnemy, EnemyAttack, bool> passive = null)
            { Kind = kind; Name = name; Patterns = patterns; Passive = passive; }
        }
        [Serializable] sealed class RequestData { public int kind, mode, index; }
        const string RequestKey = "K4RMA.Practice.MenuRequest";
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Dictionary<Essence, Definition> definitions = new Dictionary<Essence, Definition>();
        public static IEnumerable<Definition> Definitions => definitions.Values;
        public static string Status { get; private set; }
        static Definition active;
        static Mode mode;
        static int index;
        static KarmaGame game;
        static KarmaEnemy enemy;
        static bool awaiting, starting;
        static double nextRunAt, deadline;
        static KarmaBossPractice()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state != PlayModeStateChange.ExitingPlayMode) return;
                Stop(); SessionState.EraseString(RequestKey);
            };
        }
        public static void Register(Definition definition) { definitions[definition.Kind] = definition; }
        public static void Request(Essence kind, Mode requestedMode, int patternIndex = 0)
        {
            if (!definitions.ContainsKey(kind)) throw new InvalidOperationException("Missing practice definition: " + kind);
            Stop();
            SessionState.SetString(RequestKey, JsonUtility.ToJson(new RequestData { kind = (int)kind, mode = (int)requestedMode, index = patternIndex }));
            // A fresh practice request also resumes an Editor paused by a previous error.
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { SessionState.EraseString(RequestKey); return; }
                EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
                EditorApplication.EnterPlaymode();
            }
            GameView()?.Show();
        }
        static EditorWindow GameView() => EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        static void OpenRoom(RequestData request, Definition definition)
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) return;
            active = definition; mode = (Mode)request.mode;
            index = mode == Mode.Sequence ? 0 : Mathf.Clamp(request.index, 0, active.Patterns.Length - 1);
            game.StartRun();
            var order = new List<Essence> { active.Kind };
            if (mode == Mode.Single) order.AddRange(active.Patterns[index].Inherited);
            else foreach (Essence kind in Enum.GetValues(typeof(Essence))) if (kind != active.Kind) order.Add(kind);
            foreach (var kind in order) { game.Player.Essences.Preview(kind); game.Player.Essences.Confirm(out _); }
            SetProperty(game, "StageIndex", order.Count); Invoke(game, "LoadRoom", true);
            enemy = game.Enemies[0];
            game.Player.ResetForRoom(new Vector2(18, KarmaSpace.PlayerHalfHeight), true);
            game.Player.ToggleTestInvincibility();
            enemy.transform.position = new Vector3(24, KarmaSpace.GuardianGroundY, 0);
            Set(enemy, "engaged", true); SetHold(mode != Mode.Combat);
            SessionState.EraseString(RequestKey); awaiting = starting = false;
            if (mode == Mode.Combat) Report(active.Name + " · 전체 패턴 연습");
            else StartPattern();
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            string json = SessionState.GetString(RequestKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var request = JsonUtility.FromJson<RequestData>(json);
                if (definitions.TryGetValue((Essence)request.kind, out var definition)) OpenRoom(request, definition);
                return;
            }
            if (active == null || enemy == null || game == null || game.Paused || mode == Mode.Combat || starting) return;
            if (!game.IsCombat) { Stop(); return; }
            if (enemy.Alive) SetProperty(enemy, "Health", enemy.MaxHealth);
            if (awaiting)
            {
                if (!(bool)Get(enemy, "running") && EditorApplication.timeSinceStartup >= nextRunAt)
                {
                    awaiting = false;
                    if (mode == Mode.Sequence && ++index >= active.Patterns.Length)
                    { Report(active.Name + " · 전체 패턴 순회 완료"); active = null; return; }
                    nextRunAt = EditorApplication.timeSinceStartup + 0.8;
                }
                else if (EditorApplication.timeSinceStartup > deadline)
                { Report(active.Name + " · 패턴 실행 시간 초과"); Stop(); }
            }
            else if (EditorApplication.timeSinceStartup >= nextRunAt) StartPattern();
        }
        static void StartPattern()
        {
            if (enemy == null || !game.IsCombat || starting) return;
            starting = true; enemy.StartCoroutine(StartOnPlayerLoop(enemy));
        }
        static System.Collections.IEnumerator StartOnPlayerLoop(KarmaEnemy target)
        {
            yield return null; starting = false;
            if (target != enemy || active == null || enemy == null || !game.IsCombat) yield break;
            var pattern = active.Patterns[index]; Invoke(enemy, "InterruptPattern"); SetHold(true);
            awaiting = true; nextRunAt = EditorApplication.timeSinceStartup + 0.25; deadline = EditorApplication.timeSinceStartup + 90;
            Report(active.Name + " · " + pattern.Label + (mode == Mode.Sequence ? " · 순회 " + (index + 1) + "/" + active.Patterns.Length : " 연습"));
            if (active.Passive != null && active.Passive(enemy, pattern.Attack)) { nextRunAt += 2; yield break; }
            Set(enemy, "selected", pattern.Attack); Set(enemy, "previous", (EnemyAttack)(-1));
            float facing = game.Player.Position.x < enemy.Position.x ? -1 : 1;
            Set(enemy, "direction", facing);
            ((Transform)Get(enemy, "art")).localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
            Invoke(Get(enemy, "behavior"), "OnSelected");
            enemy.StartCoroutine((System.Collections.IEnumerator)Invoke(enemy, "RunPattern"));
        }
        public static void Stop()
        {
            awaiting = starting = false;
            if (enemy != null && EditorApplication.isPlaying) { Invoke(enemy, "InterruptPattern"); SetHold(false); }
            active = null; enemy = null; game = null;
        }
        static void SetHold(bool value) { if (enemy != null) Set(Get(enemy, "behavior"), "PracticeHold", value); }
        static void Report(string message) { Status = message; Debug.Log(message); GameView()?.ShowNotification(new GUIContent(message)); }
        public static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
        static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType) { var field = type.GetField(name, Members); if (field != null) return field; }
            throw new MissingFieldException(name);
        }
        public static object Invoke(object target, string name, params object[] arguments)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            { var method = type.GetMethod(name, Members | BindingFlags.DeclaredOnly); if (method != null) return method.Invoke(target, arguments); }
            throw new MissingMethodException(name);
        }
        static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name, Members).SetValue(target, value);
    }
}
