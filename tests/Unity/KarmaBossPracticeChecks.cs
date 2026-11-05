#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;
using KarmaPrototype.EditorTools;

public abstract class KarmaSelectionFixture
{
    public abstract Essence Kind { get; }
    public abstract void Run(KarmaEnemy enemy, KarmaGame game);
    protected static object Get(object target, string name)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f.GetValue(target); }
        throw new MissingFieldException(name);
    }
    protected static void Set(object target, string name, object value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) { f.SetValue(target, value); return; } }
        throw new MissingFieldException(name);
    }
    protected static object Select(KarmaEnemy enemy, float distance, out EnemyAttack choice)
    {
        var b = Get(enemy, "behavior");
        object[] args = { distance, true, true, default(EnemyAttack) };
        bool ok = (bool)b.GetType().GetMethod("TrySelect").Invoke(b, args);
        choice = (EnemyAttack)args[3]; return ok;
    }
    protected static void Distribution(KarmaEnemy enemy, float distance, EnemyAttack first, EnemyAttack second)
    {
        var state = UnityEngine.Random.state; UnityEngine.Random.InitState(41273); int count = 0;
        try
        {
            for (int i = 0; i < 2000; i++)
            {
                if (!(bool)Select(enemy, distance, out var selected) || selected != first && selected != second)
                    throw new Exception("Selection escaped the eligible weighted pair: " + selected);
                if (selected == first) count++;
            }
            if (Mathf.Abs(count / 2000f - 0.25f) > 0.04f) throw new Exception("Weighted 1:3 selection frequency: " + count);
        }
        finally { UnityEngine.Random.state = state; }
    }
}
public static class KarmaBossPracticeChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static FieldInfo Field(Type t, string n) { for (; t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f; } throw new MissingFieldException(n); }
    static async Task Until(Func<bool> condition, double timeout = 180)
    {
        double deadline = EditorApplication.timeSinceStartup + timeout;
        while (!condition()) { if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Practice validation timed out"); EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(15); }
    }
    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Practice checks require Play Mode");
        int bosses = 0;
        foreach (var type in typeof(KarmaSelectionFixture).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(KarmaSelectionFixture).IsAssignableFrom(type)) continue;
            var fixture = (KarmaSelectionFixture)Activator.CreateInstance(type);
            Type windowType = null;
            foreach (var candidate in typeof(KarmaBossPracticeWindow).Assembly.GetTypes())
                if (!candidate.IsAbstract && typeof(KarmaBossPracticeWindow).IsAssignableFrom(candidate))
                {
                    var probe = (KarmaBossPracticeWindow)ScriptableObject.CreateInstance(candidate);
                    var kind = (Essence)candidate.GetProperty("Kind", F).GetValue(probe);
                    UnityEngine.Object.DestroyImmediate(probe);
                    if (kind == fixture.Kind) windowType = candidate;
                }
            if (windowType == null) throw new Exception("Missing local practice window: " + fixture.Kind);
            var window = (KarmaBossPracticeWindow)EditorWindow.GetWindow(windowType);
            try
            {
                window.CreateGUI();
                if (window.rootVisualElement.childCount < 10) throw new Exception("Practice controls failed to construct");
                window.RequestRoom(false, false, true);
                await Until(() => ((string)Field(windowType, "lastStatus").GetValue(window)).Contains("전체 패턴 순회 완료"));
                var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); var enemy = game.Enemies[0];
                if (enemy.PrimaryType != fixture.Kind) throw new Exception("Wrong boss practice room");
                fixture.Run(enemy, game);
                window.RequestRoom(true, false, false);
                await Until(() => ((string)Field(windowType, "lastStatus").GetValue(window)).Contains("전체 패턴 전투 시작"), 10);
                bosses++;
            }
            finally { window.Close(); }
        }
        if (bosses != 3) throw new Exception("Expected all three local practice windows");
        return "PASS: three boss practice windows, all 18 patterns, combat entry and 6000 weighted selections";
    }
}
#endif
