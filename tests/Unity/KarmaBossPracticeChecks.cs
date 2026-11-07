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
    static async Task Until(Func<bool> done)
    {
        double deadline = EditorApplication.timeSinceStartup + 180;
        while (!done()) { if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Practice sequence timed out"); await Task.Delay(15); }
    }
    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Practice checks require Play Mode");
        int bosses = 0, patterns = 0;
        foreach (var definition in KarmaBossPractice.Definitions)
        {
            KarmaBossPractice.Request(definition.Kind, KarmaBossPractice.Mode.Sequence);
            await Until(() => KarmaBossPractice.Status == definition.Name + " · 전체 패턴 순회 완료");
            var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            foreach (var type in typeof(KarmaSelectionFixture).Assembly.GetTypes())
                if (!type.IsAbstract && typeof(KarmaSelectionFixture).IsAssignableFrom(type))
                {
                    var fixture = (KarmaSelectionFixture)Activator.CreateInstance(type);
                    if (fixture.Kind == definition.Kind) fixture.Run(game.Enemies[0], game);
                }
            KarmaBossPractice.Request(definition.Kind, KarmaBossPractice.Mode.Combat);
            await Until(() => KarmaBossPractice.Status == definition.Name + " · 전체 패턴 연습");
            bosses++; patterns += definition.Patterns.Length;
        }
        KarmaBossPractice.Stop();
        if (bosses != 3 || patterns != 18) throw new Exception("Expected three bosses and eighteen patterns including passive GD01");
        return "PASS: three direct practice menus, all eighteen patterns, combat entry and 6000 weighted selections";
    }
}
#endif
