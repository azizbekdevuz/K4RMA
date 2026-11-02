#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Checks the ownership boundaries without changing authored scenes/assets.
public static class KarmaBossIsolationChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    public static void RunBatch()
    {
        string report;
        int code = 0;
        try { Run(); report = "PASS: " + assertions + " boss isolation assertions\n"; }
        catch (Exception error) { report = "FAIL: " + error + "\n"; code = 1; Debug.LogException(error); }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/boss-isolation-validation.txt", report);
        EditorApplication.Exit(code);
    }
    static void Run()
    {
        Check(Resources.Load<KarmaSwordBossProfile>("KarmaSwordBossProfile") != null, "Sword profile imports");
        Check(Resources.Load<KarmaRisingBossProfile>("KarmaRisingBossProfile") != null, "Rising profile imports");
        Check(Resources.Load<KarmaGuardBossProfile>("KarmaGuardBossProfile") != null, "Guard profile imports");
        CheckArtwork();
        var first = new KarmaBossTuning(); var second = new KarmaBossTuning();
        Check(Mathf.Approximately(first.sword.swordSpeed, 32), "Sword saved speed survives migration");
        Check(Mathf.Approximately(first.rising.meleeReach, 8.2f), "Rising saved reach survives migration");
        Check(Mathf.Approximately(first.guard.fragmentChaseSpeed, 30), "Guard saved speed survives migration");
        first.sword.swordSpeed = 123; first.rising.meleeReach = 123; first.guard.fragmentChaseSpeed = 123;
        Check(second.sword.swordSpeed == 32, "Sword runtime settings are isolated");
        Check(Mathf.Approximately(second.rising.meleeReach, 8.2f), "Rising runtime settings are isolated");
        Check(second.guard.fragmentChaseSpeed == 30, "Guard runtime settings are isolated");
        Check(Resources.Load<KarmaSwordBossProfile>("KarmaSwordBossProfile").settings.swordSpeed == 32, "Runtime tuning leaves asset intact");
        var config = ScriptableObject.CreateInstance<KarmaConfig>();
        var clone = UnityEngine.Object.Instantiate(config);
        config.BossTuning.guard.autoShieldPeriod = 123;
        Check(clone.BossTuning.guard.autoShieldPeriod == 8, "Cloned game configs have private boss settings");
        UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(clone);
        var host = new GameObject("Boss ownership check"); host.SetActive(false);
        try
        {
            var enemy = host.AddComponent<KarmaEnemy>();
            var types = new[] { "SwordBossBehavior", "RisingBossBehavior", "GuardBossBehavior", "BossBehavior" };
            var primaries = new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard };
            foreach (var typeName in types)
            {
                var type = typeof(KarmaEnemy).GetNestedType(typeName, BindingFlags.NonPublic);
                var behavior = Activator.CreateInstance(type, new object[] { enemy });
                var build = type.GetMethod("BuildCycle");
                var baseType = typeof(KarmaEnemy).GetNestedType("BossBehavior", BindingFlags.NonPublic);
                var registered = (IDictionary)baseType.GetField("patterns", Hidden).GetValue(behavior);
                for (int mask = 0; mask < 8; mask++)
                {
                    var inherited = new List<Essence>();
                    if (typeName != "BossBehavior")
                    {
                        int primary = Array.IndexOf(types, typeName);
                        inherited.Add(primaries[primary]);
                        for (int i = 0; i < primaries.Length; i++)
                            if (i != primary && (mask & (1 << i)) != 0) inherited.Add(primaries[i]);
                    }
                    var cycle = (List<EnemyAttack>)build.Invoke(behavior, new object[] { inherited });
                    var expected = Expected(typeName, inherited);
                    Check(string.Join(",", cycle) == string.Join(",", expected), typeName + " preserves unlock/order: " + mask);
                    foreach (var attack in cycle) Check(registered.Contains(attack), typeName + " registers " + attack);
                }
                if (typeName == "GuardBossBehavior")
                    Check(registered[EnemyAttack.GroundStrike].GetType().Name == "GDBasicPattern", "Guard basic attack is locally owned");
                var attackHost = new GameObject("Attack ownership check");
                try
                {
                    var component = (KarmaBossAttack)type.GetMethod("CreateAttackComponent").Invoke(behavior, new object[] { attackHost });
                    Check((component is KarmaSwordBossAttack) == (typeName == "SwordBossBehavior"), "Only sword uses sword flight policy");
                }
                finally { UnityEngine.Object.DestroyImmediate(attackHost); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
    static void CheckArtwork()
    {
        var libraries = Resources.LoadAll<KarmaSpriteLibrary>("BossArtwork");
        Check(libraries.Length == 3, "Three independent boss sprite libraries import");
        var neutralAtlas = AssetDatabase.LoadAllAssetsAtPath("Assets/K4RMA/Arts/Common/Resources/K4RMAArtwork/Characters/Guardians/ReferencePoses.png");
        foreach (var library in libraries)
        {
            string folder = library.name.Contains("Sword") ? "SwordWave" : library.name.Contains("Rising") ? "RisingSlash" : "Guard";
            string form = folder == "SwordWave" ? "Wave" : folder == "RisingSlash" ? "Rise" : "Ward";
            Essence essence = folder == "SwordWave" ? Essence.SwordWave : folder == "RisingSlash" ? Essence.RisingSlash : Essence.Guard;
            foreach (var sheet in library.sheets)
                for (int i = 0; i < sheet.sprites.Length; i++)
                {
                    var sprite = sheet.sprites[i];
                    Check(sprite != null, library.name + "/" + sheet.id + " sprite " + i + " preserves its reference");
                    Check(AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/K4RMA/Arts/Bosses/" + folder + "/"),
                        library.name + "/" + sheet.id + " owns its image source");
                    string pose = sheet.poses != null && i < sheet.poses.Length && !string.IsNullOrEmpty(sheet.poses[i]) ? sheet.poses[i] : sprite.name;
                    Check(KarmaArtwork.Sprite(sheet.id, pose) == sprite, "Artwork routing resolves " + sheet.id + "/" + pose);
                    if (sheet.id == "Inheritance")
                    {
                        Sprite original = null;
                        foreach (var item in neutralAtlas) if (item is Sprite candidate && candidate.name == sprite.name) original = candidate;
                        Check(original != null && original.rect == sprite.rect, "Independent preview atlas preserves cutout " + pose);
                    }
                }
            Check(KarmaArtwork.Settings.Guardian(essence) == library.Get("BossAppearance", form), "Boss appearance is locally connected: " + form);
            foreach (string kind in new[] { "Base", "Revival", "Inherited" })
            {
                string key = kind == "Inherited" ? form + "Final" : form;
                Check(Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Guardians/" + kind + "/" + key).Length > 0,
                    "Animation Resources path survives move: " + kind + "/" + key);
            }
        }
        Check(KarmaArtwork.Sprite("GuardianVFX", "Wave") != null, "Shared boss effects remain available");
        Check(Resources.Load<Material>("SW05/SW05CrimsonBlade") != null, "Sword material Resources path survives move");
    }
    static List<EnemyAttack> Expected(string boss, List<Essence> inherited)
    {
        var result = new List<EnemyAttack>();
        if (boss == "SwordBossBehavior")
        {
            result.Add(EnemyAttack.SW01); result.Add(EnemyAttack.SW02);
            if (inherited.Contains(Essence.RisingSlash)) result.Add(EnemyAttack.SW03);
            if (inherited.Contains(Essence.Guard)) result.Add(EnemyAttack.SW04);
            if (inherited.Count == 3) result.Add(EnemyAttack.SW05);
        }
        else if (boss == "RisingBossBehavior")
        {
            result.Add(EnemyAttack.GroundStrike); result.Add(EnemyAttack.UP01); result.Add(EnemyAttack.UP02);
            if (inherited.Contains(Essence.SwordWave)) result.Add(EnemyAttack.UP03);
            if (inherited.Contains(Essence.Guard)) result.Add(EnemyAttack.UP04);
            if (inherited.Count == 3) result.Add(EnemyAttack.UP05);
        }
        else if (boss == "GuardBossBehavior")
        {
            result.Add(EnemyAttack.GroundStrike); result.Add(EnemyAttack.GD02);
            if (inherited.Contains(Essence.SwordWave)) result.Add(EnemyAttack.GD03);
            if (inherited.Contains(Essence.RisingSlash)) result.Add(EnemyAttack.GD04);
            if (inherited.Count == 3) result.Add(EnemyAttack.GD05);
        }
        else result.Add(EnemyAttack.GroundStrike);
        return result;
    }
}
#endif
