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
        CheckRuntimeOptimizations();
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
    static void CheckRuntimeOptimizations()
    {
        var expected = KarmaArtwork.Sprite("BossStun", "Wave");
        for (int i = 0; i < 32; i++)
        { KarmaArtwork.Sprite("BossStun", "Wave"); KarmaArtwork.Sprite("Missing", "Missing"); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++)
        { KarmaArtwork.Sprite("BossStun", "Wave"); KarmaArtwork.Sprite("Missing", "Missing"); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "Warm sprite lookups allocate no managed memory: " + allocated);
        Check(KarmaArtwork.Sprite("BossStun", "Wave") == expected, "Optimized lookup preserves routing");

        var root = new GameObject("Optimization checks");
        var renderer = KarmaVisuals.Box(root.transform, "Warning", Vector2.zero, Vector2.one, Color.white);
        var warning = renderer.gameObject.AddComponent<KarmaAttackWarning>();
        try
        {
            foreach (bool oriented in new[] { false, true })
                foreach (var to in new[] { Vector2.zero, new Vector2(10, 0), new Vector2(10, 8), new Vector2(-8, 10) })
                {
                    Vector2 size = new Vector2(4, 2);
                    warning.SetPath(renderer, Vector2.zero, to, size, oriented: oriented);
                    var edge = warning.transform.Find("Locked danger outline").GetComponent<LineRenderer>();
                    var polygon = new Vector2[edge.positionCount];
                    for (int i = 0; i < polygon.Length; i++)
                        polygon[i] = edge.useWorldSpace ? edge.GetPosition(i) : edge.transform.TransformPoint(edge.GetPosition(i));
                    Vector2 axis = oriented && to.sqrMagnitude > 0.000001f ? to.normalized : Vector2.right;
                    Vector2 normal = new Vector2(-axis.y, axis.x);
                    foreach (var end in new[] { Vector2.zero, to })
                        for (int x = -1; x <= 1; x += 2)
                            for (int y = -1; y <= 1; y += 2)
                                Check(InsideConvex(polygon, end + axis * (size.x * x / 2) + normal * (size.y * y / 2)),
                                    "Warning outline contains every swept-box corner: oriented=" + oriented + " to=" + to
                                    + " corner=" + (end + axis * (size.x * x / 2) + normal * (size.y * y / 2))
                                    + " polygon=" + string.Join(",", polygon));
                    Check(InsideConvex(polygon, to / 2), "Warning outline contains the sweep center");
                    if (!oriented && to == new Vector2(10, 8))
                        Check(!InsideConvex(polygon, new Vector2(-1.9f, 8.9f)), "Diagonal warning outline preserves its safe corner");
                }
            // Warm both geometry and Unity bindings, then track without changing topology.
            for (int i = 0; i < 32; i++) warning.SetPath(renderer, Vector2.zero, new Vector2(10, 8), new Vector2(4, 2));
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) warning.SetPath(renderer, Vector2.zero, new Vector2(10, 8), new Vector2(4, 2));
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, "Warm warning updates allocate no managed memory: " + allocated);
            Debug.Log("Optimization checks: 2048 sprite lookups and 128 warning updates allocated 0 managed bytes");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer.sprite);
            typeof(KarmaAttackWarning).GetField("shape", Hidden).SetValue(warning, null);
            UnityEngine.Object.DestroyImmediate(root);
        }
        CheckTerrainQueries();
    }
    static bool InsideConvex(Vector2[] polygon, Vector2 point)
    {
        for (int i = 0; i < polygon.Length; i++)
        {
            if (Cross(polygon[(i + 1) % polygon.Length] - polygon[i], point - polygon[i]) < -0.001f) return false;
        }
        return true;
    }
    static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
    static void CheckTerrainQueries()
    {
        var root = new GameObject("Terrain query checks");
        var enemy = root.AddComponent<KarmaEnemy>(); enemy.enabled = false;
        bool triggers = Physics2D.queriesHitTriggers;
        try
        {
            Vector2 from = new Vector2(1000, 1000), to = from + Vector2.right * 20;
            for (int i = 1; i <= 12; i++)
            {
                var decoration = new GameObject("Nonblocking collider"); decoration.transform.SetParent(root.transform);
                decoration.transform.position = from + Vector2.right * i;
                decoration.AddComponent<BoxCollider2D>();
            }
            var blocker = new GameObject("Solid surface"); blocker.transform.SetParent(root.transform);
            blocker.transform.position = from + Vector2.right * 15;
            var collider = blocker.AddComponent<BoxCollider2D>(); blocker.AddComponent<KarmaSurface>();
            var clearShot = typeof(KarmaEnemy).GetMethod("ClearShot", Hidden);
            for (int scenario = 0; scenario < 5; scenario++)
            {
                collider.isTrigger = scenario == 1 || scenario == 2;
                Physics2D.queriesHitTriggers = scenario != 2;
                blocker.layer = scenario == 3 ? 2 : 0;
                if (scenario == 4) blocker.AddComponent<PlatformEffector2D>();
                Physics2D.SyncTransforms();
                bool expectedClear = true;
                foreach (var hit in Physics2D.LinecastAll(from, to))
                    if (hit.collider.GetComponent<KarmaSurface>() != null && hit.collider.GetComponent<PlatformEffector2D>() == null) expectedClear = false;
                Check(expectedClear == (scenario >= 2), "Terrain query fixture exercises the intended filter case: " + scenario);
                Check((bool)clearShot.Invoke(enemy, new object[] { from, to }) == expectedClear,
                    "Reusable terrain query preserves dense hits, trigger/layer filters and one-way surfaces: " + scenario);
            }
        }
        finally { Physics2D.queriesHitTriggers = triggers; UnityEngine.Object.DestroyImmediate(root); }
    }
    static void CheckArtwork()
    {
        var libraries = Resources.LoadAll<KarmaSpriteLibrary>("BossArtwork");
        Check(libraries.Length == 3, "Three independent boss sprite libraries import");
        const string sharedParryPath = "Assets/K4RMA/Arts/Common/Resources/K4RMAArtwork/Effects/Parry.png";
        var sharedParry = AssetDatabase.LoadAssetAtPath<Sprite>(sharedParryPath);
        Check(sharedParry != null, "Shared parry sprite imports");
        Check(Resources.Load<Sprite>("K4RMAArtwork/Effects/Parry") == sharedParry, "Shared parry Resources path loads");
        const string sharedAtlasPath = "Assets/K4RMA/Arts/Common/Resources/K4RMAArtwork/Characters/Guardians/ReferencePoses.png";
        var sharedAtlas = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Guardians/ReferencePoses");
        Check(sharedAtlas.Length == 16, "Shared guardian atlas loads all sixteen cutouts");
        foreach (var path in Directory.GetFiles("Assets/K4RMA/Arts", "*.png", SearchOption.AllDirectories))
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
            Check(texture != null && texture.width > 0 && texture.height > 0, "Optimized PNG imports: " + path);
        }
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
                    string pose = sheet.poses != null && i < sheet.poses.Length && !string.IsNullOrEmpty(sheet.poses[i]) ? sheet.poses[i] : sprite.name;
                    bool usesSharedParry = sheet.id == "BossStun" || (sheet.id == "RisingBossVFX" && pose == "Parry");
                    string source = AssetDatabase.GetAssetPath(sprite);
                    Check(sheet.id == "Inheritance" ? source == sharedAtlasPath : usesSharedParry ? sprite == sharedParry
                        : source.StartsWith("Assets/K4RMA/Arts/Bosses/" + folder + "/"),
                        library.name + "/" + sheet.id + " uses its dedicated or shared image source");
                    Check(KarmaArtwork.Sprite(sheet.id, pose) == sprite, "Artwork routing resolves " + sheet.id + "/" + pose);
                    if (sheet.id == "Inheritance")
                    {
                        Sprite original = null;
                        foreach (var candidate in sharedAtlas) if (candidate.name == sprite.name) original = candidate;
                        Check(original == sprite && original.rect == sprite.rect && original.pivot == sprite.pivot
                            && original.pixelsPerUnit == sprite.pixelsPerUnit, "Shared preview atlas resolves cutout " + pose);
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
