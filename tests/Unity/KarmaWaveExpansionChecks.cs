#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Independent Unity geometry, imported pixels and real 2D physics movement probes.
public static class KarmaWaveExpansionChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static void Set(object target, string name, object value)
        => target.GetType().GetField(name, Flags).SetValue(target, value);
    static void Check(bool ok, string label)
    { assertions++; if (!ok) throw new Exception("Wave expansion: " + label); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;

    public static int Run(KarmaGame game, KarmaEnemy enemy)
    {
        assertions = 0;
        var player = game.Player;
        var body = player.GetComponent<Rigidbody2D>();
        var collider = player.GetComponent<BoxCollider2D>();
        Vector2 position = player.Position, size = collider.size, offset = collider.offset;
        float health = player.Health, enemyHealth = enemy.Health;
        object selected = typeof(KarmaEnemy).GetField("selected", Flags).GetValue(enemy);
        object primary = typeof(KarmaEnemy).GetField("<PrimaryType>k__BackingField", Flags).GetValue(enemy);
        var root = new GameObject("Wave expansion probes");
        try
        {
            Set(enemy, "selected", EnemyAttack.SW02);
            Set(enemy, "<PrimaryType>k__BackingField", (Essence?)Essence.SwordWave);
            ImportedArtwork(root.transform, enemy);
            RotatedDamage(game, enemy, root.transform);
            AerialCountDistribution(game.Config.BossTuning);
            AerialBoundaryTravel(game, enemy, root.transform);
            foreach (float width in new[] { size.x, 2.6f })
            {
                collider.size = new Vector2(width, size.y);
                collider.offset = new Vector2(0.2f, 0);
                RotatedDamage(game, enemy, root.transform);
                ScarPlacement(game, enemy);
                AerialEscape(game, enemy);
            }
            Debug.Log("PASS: " + assertions + " imported art, rotated damage, scar movement and seeded full-pattern escape assertions");
            return assertions;
        }
        finally
        {
            collider.size = size; collider.offset = offset;
            body.position = position; player.transform.position = position;
            Set(player, "<Health>k__BackingField", health);
            Set(player, "invulnerableUntil", 0f);
            Set(enemy, "<Health>k__BackingField", enemyHealth);
            Set(enemy, "selected", selected); Set(enemy, "<PrimaryType>k__BackingField", primary);
            Physics2D.SyncTransforms();
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static void ImportedArtwork(Transform root, KarmaEnemy enemy)
    {
        foreach (string pose in new[] { "SW01_StaggeredWave", "SW02_AimedSlash", "SW03_RisingBlade",
            "SW03_GroundImpact", "SW04_CounterWave", "SW05_AerialSword" })
        {
            string path = "Assets/K4RMA/Resources/K4RMAArtwork/ExtraWaveVFX/" + pose + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var sprite = Resources.Load<Sprite>("K4RMAArtwork/ExtraWaveVFX/" + pose);
            Check(importer != null && sprite != null && sprite == KarmaArtwork.Sprite("ExtraWaveVFX", pose), pose + " loads through Resources and the existing library");
            Check(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && Near(sprite.pixelsPerUnit, 100) && importer.alphaIsTransparency && !importer.mipmapEnabled,
                pose + " imports as a single transparent sprite at 100 PPU without mipmaps");
            Check(AssetDatabase.GUIDToAssetPath(AssetDatabase.AssetPathToGUID(path)) == path, pose + " GUID resolves");
            var texture = new Texture2D(2, 2);
            try
            {
                Check(texture.LoadImage(File.ReadAllBytes(path)), pose + " source PNG decodes");
                var pixels = texture.GetPixels32();
                int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1, transparent = 0;
                for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                    {
                        byte alpha = pixels[y * texture.width + x].a;
                        if (alpha == 0) transparent++;
                        if (alpha < 128) continue;
                        minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                    }
                var core = KarmaWaveArtwork.CorePixels(pose);
                Check(transparent > 0 && core == new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    pose + " authored core equals independently decoded alpha >=128 pixel bounds: " + new Rect(minX, minY, maxX-minX+1, maxY-minY+1));
                Check(Vector2.Distance(sprite.pivot, core.center) < 0.01f, pose + " pivot centers the damage core rather than glow/transparent padding");
                var picture = new GameObject(pose).AddComponent<SpriteRenderer>(); picture.transform.SetParent(root, false);
                var dimensions = pose.Contains("Rising") ? new Vector2(4.5f, 13.5f)
                    : pose.Contains("Ground") ? new Vector2(4.5f, 2.7f)
                    : pose.Contains("Staggered") ? new Vector2(3.24f, 1.3f)
                    : pose.Contains("Aimed") ? (Vector2)Call(enemy, "SwordSize", EnemyAttack.SW02) : new Vector2(3.12f, 1.3f);
                KarmaWaveArtwork.Apply(picture, pose, dimensions, Vector2.right);
                Check(Near(core.width / sprite.pixelsPerUnit * picture.transform.localScale.x, dimensions.x)
                    && Near(core.height / sprite.pixelsPerUnit * picture.transform.localScale.y, dimensions.y), pose + " visible core equals the damage dimensions");
                Debug.Log("WAVE ART " + pose + " source=" + texture.width + "x" + texture.height + " core=" + core + " pivot=" + sprite.pivot);
                Debug.Log("WAVE NEW SIZE " + pose + " core=" + dimensions + " fullSprite=" + picture.bounds.size);
                if (pose.Contains("Ground")) continue;
                Vector2 previous = pose == "SW02_AimedSlash" ? new Vector2(3.24f, 1.35f)
                    : pose == "SW03_RisingBlade" ? new Vector2(3.6f, 10) : new Vector2(2.4f, 1);
                string legacyPose = pose.Contains("Rising") ? "Pillar" : pose.Contains("Aimed") ? "AimedWave"
                    : pose.Contains("Aerial") ? "Blade" : "Wave";
                var legacy = new GameObject("Legacy size " + pose).AddComponent<SpriteRenderer>(); legacy.transform.SetParent(root, false);
                KarmaBossAttack.ConfigureVisual(legacy, pose.Contains("Rising") ? Vector2.zero : Vector2.right, previous, 1, legacyPose, true);
                Debug.Log("WAVE SIZE " + pose + " oldArt=" + legacy.bounds.size + " oldDamage=" + previous);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
    static void RotatedDamage(KarmaGame game, KarmaEnemy enemy, Transform root)
    {
        var player = game.Player;
        var body = player.GetComponent<Rigidbody2D>();
        var collider = player.GetComponent<BoxCollider2D>();
        var box = new GameObject("Independent Unity oriented collider").AddComponent<BoxCollider2D>();
        box.transform.SetParent(root, false); box.isTrigger = true;
        Vector2 dimensions = (Vector2)Call(enemy, "SwordSize", EnemyAttack.SW02);
        box.size = dimensions;
        foreach (float degrees in new[] { 0f, 30f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
        {
            Vector2 axis = degrees == 0 ? Vector2.right : degrees == 180 ? Vector2.left
                : (Vector2)(Quaternion.Euler(0, 0, degrees) * Vector2.right);
            Vector2 normal = new Vector2(-axis.y, axis.x), center = new Vector2(20, 8);
            box.transform.position = center; box.transform.rotation = Quaternion.Euler(0, 0, degrees);
            var shot = KarmaBossAttack.Spawn(game, enemy, root, center, axis * 40, dimensions, 1.65f, 1, Color.white,
                null, 0, "AimedWave", true, true, "SW02_AimedSlash");
            Check(shot.GetComponentsInChildren<LineRenderer>(true).Length == 0,
                degrees + " degree actual projectile has no independent damage-boundary line");
            Vector2 cuttingWorld = shot.Visual.transform.TransformDirection(Vector3.left * (shot.Visual.flipX ? -1 : 1));
            Check(Vector2.Dot(cuttingWorld, axis) > 0.9999f
                && shot.Visual.flipX == (degrees != 180) && !shot.Visual.flipY,
                degrees + " degree SW02 source correction and world-space rotation follow its actual damage axis");
            // Keep independent ColliderDistance samples away from Box2D's contact
            // skin; exact mathematical touching is covered by the Core checks.
            foreach (float along in new[] { -4.5f, -3f, 0f, 3f, 4.5f })
                foreach (float across in new[] { -6f, -3f, 0f, 3f, 6f })
                {
                    Vector2 p = center + axis * along + normal * across;
                    body.position = p; player.transform.position = p; Physics2D.SyncTransforms();
                    var separation = box.Distance(collider);
                    bool expected = separation.isOverlapped;
                    float before = player.Health;
                    Set(player, "invulnerableUntil", 0f); Set(player, "guardUntil", 0f);
                    enemy.TryAttackHit(center, center, dimensions, 1, new KarmaAttackContact(), 0, axis);
                    Check((player.Health < before) == expected, degrees + " degree real damage agrees with Unity ColliderDistance2D at " + along + "," + across
                        + " width=" + collider.size.x + " distance=" + separation.distance + " expected=" + expected + " actual=" + (player.Health < before));
                }
            body.position = center + axis * 8; player.transform.position = body.position; Physics2D.SyncTransforms();
            float hp = player.Health; Set(player, "invulnerableUntil", 0f);
            enemy.TryAttackHit(center, center + axis * 16, dimensions, 1, new KarmaAttackContact(), 0, axis);
            Check(player.Health < hp, degrees + " degree high-speed sweep cannot tunnel through the player");
            var marker = KarmaVisuals.Box(root, "Oriented path probe", Vector2.zero, Vector2.one, Color.white);
            var warning = marker.gameObject.AddComponent<KarmaAttackWarning>();
            warning.SetPath(marker, center, center + axis * 16, dimensions, true, true);
            var outline = marker.GetComponentInChildren<LineRenderer>();
            // Floating-point rotation can retain collinear endpoint vertices;
            // all eight source corners are valid hull vertices. Check their
            // actual thickness below instead of assuming an AABB's six-corner hull.
            Check(outline != null && outline.enabled && outline.positionCount >= 4 && outline.positionCount <= 8,
                "oriented pre-release danger outline remains visible after removing the live projectile outline");
            for (int i = 0; i < outline.positionCount; i++)
            {
                Vector2 v = (Vector2)outline.GetPosition(i) - center;
                Check(Near(Mathf.Abs(Vector2.Dot(v, normal)), dimensions.y / 2), degrees + " degree warning thickness follows the oriented hit box");
            }
            shot.gameObject.SetActive(false);
            Check(!shot.Visual.gameObject.activeInHierarchy && marker.gameObject.activeInHierarchy,
                "projectile cancellation disables its picture without removing independent pre-release warnings");
        }
    }
    static void ScarPlacement(KarmaGame game, KarmaEnemy enemy)
    {
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        foreach (bool enrage in new[] { false, true })
        {
            Set(enemy, "<Health>k__BackingField", enemy.MaxHealth * (enrage ? 0.4f : 1));
            foreach (float x in new[] { KarmaStage.ArenaLeft + 0.8f, KarmaStage.ArenaLeft + 2, 0, 20, 40, KarmaStage.ArenaRight - 2, KarmaStage.ArenaRight - 0.8f })
                foreach (float span in new[] { 8f, 30f, KarmaStage.ArenaWidth })
                {
                    body.position = new Vector2(x, 2); player.transform.position = body.position; Physics2D.SyncTransforms();
                    float low = KarmaStage.ArenaLeft, high = low + span;
                    object[] args = { low, high, Vector2.zero, Vector2.zero };
                    bool pair = (bool)Call(enemy, "PlaceSwordScars", args);
                    Vector2 a = (Vector2)args[2], b = (Vector2)args[3];
                    var bounds = player.GetComponent<Collider2D>().bounds;
                    Check(pair == (span >= 9 + Mathf.Max(3, bounds.size.x + 0.5f)), "only physically insufficient space uses a single scar");
                    if (!pair) { Check(a == b, "physically narrow scar pair uses one scar"); continue; }
                    float gap = b.x - a.x - 4.5f;
                    Check(gap + 0.0001f >= bounds.size.x + 0.5f, "scar damage boundaries preserve actual collider width + 0.5 world units");
                    Check(Near(gap, Mathf.Max(3, bounds.size.x + 0.5f)) && gap >= 2.8f && gap <= 3.2f,
                        "scar edge gap meets the 2.8-3.2 target: 3.0 for the standard body and 3.1 for a 2.6-wide body");
                    Check(a.x - 2.25f >= low - 0.001f && b.x + 2.25f <= high + 0.001f, "both enlarged scars fit the available arena");
                    float safe = Mathf.Clamp(bounds.center.x, a.x + 2.25f + bounds.extents.x + 0.25f, b.x - 2.25f - bounds.extents.x - 0.25f);
                    float warning = (float)Call(enemy, "Warning", 0.75f);
                    float distance = Mathf.Abs(safe - bounds.center.x);
                    if (span == KarmaStage.ArenaWidth) Check(distance <= game.Config.moveSpeed * (warning - Time.fixedDeltaTime) + 0.001f,
                        "scar safe interval is reachable within the actual warning including a physics-step reserve");
                    Debug.Log("SW03 GAP width=" + bounds.size.x + " gap=" + gap + " move=" + distance + " warning=" + warning + " x=" + x);
                }
        }
    }
    static void AerialCountDistribution(KarmaBossTuning tuning)
    {
        int minimum = int.MaxValue, maximum = 0, smallest = 18, largest = 0;
        var totals = new System.Collections.Generic.HashSet<int>();
        var signatures = new System.Collections.Generic.HashSet<string>();
        for (int seed = 1; seed <= 1024; seed++)
        {
            var groups = KarmaAerialSwordPlanner.CreateGroupSizes(tuning, new System.Random(seed));
            Check(groups.Length == 7, "every seed creates exactly seven volleys, independently of legacy total limits");
            int count = 0;
            foreach (int group in groups)
            {
                Check(group == 15, "default count is exactly fifteen in every volley");
                smallest = Mathf.Min(smallest, group); largest = Mathf.Max(largest, group); count += group;
            }
            Check(count == 105, "seven default volleys contain exactly 105 independent swords");
            minimum = Mathf.Min(minimum, count); maximum = Mathf.Max(maximum, count);
            totals.Add(count); signatures.Add(string.Join(",", groups));
        }
        Check(totals.Count == 1 && smallest == 15 && largest == 15,
            "random direction seeds do not alter the requested fixed default count");
        foreach (int countPerVolley in new[] { 12, 15, 18 })
        {
            var boundary = new KarmaBossTuning { aerialSwordVolleyMin = countPerVolley, aerialSwordVolleyMax = countPerVolley };
            var groups = KarmaAerialSwordPlanner.CreateGroupSizes(boundary, new System.Random(1));
            Check(groups.Length == 7, "minimum/maximum group configurations still have exactly seven releases");
            int sum = 0;
            foreach (int count in groups) { Check(count == countPerVolley, "extreme valid group sizes are honored"); sum += count; }
            Check(sum == countPerVolley * 7, "configurable future counts are honored without a legacy cap");
        }
        var configurable = new KarmaBossTuning { aerialSwordVolleyMin = 12, aerialSwordVolleyMax = 18 };
        signatures.Clear();
        for (int seed = 1; seed <= 1024; seed++)
        {
            var groups = KarmaAerialSwordPlanner.CreateGroupSizes(configurable, new System.Random(seed));
            foreach (int count in groups) Check(count >= 12 && count <= 18, "optional 12..18 setting stays within its bounds");
            signatures.Add(string.Join(",", groups));
        }
        Check(signatures.Count > 100, "optional count range remains seeded and adjustable");
        Debug.Log("SW05 COUNT DISTRIBUTION seeds=1024 total=" + minimum + ".." + maximum + " group=" + smallest + ".." + largest
            + " uniqueCompositions=" + signatures.Count);
    }
    static void AerialBoundaryTravel(KarmaGame game, KarmaEnemy enemy, Transform root)
    {
        float dt = Time.deltaTime;
        Check(dt > 0, "actual projectile updates are checked inside the player loop");
        float horizon = (float)Call(enemy, "AerialSwordReachLife");
        var size = (Vector2)Call(enemy, "SwordSize", EnemyAttack.SW05);
        foreach (var origin in new[] { new Vector2(20, 12), new Vector2(-14.95f, 0.05f),
            new Vector2(54.95f, 23.95f), new Vector2(49, 12) })
            foreach (float degrees in new[] { 0f, 1f, 45f, 90f, 135f, 179f, 180f, 225f, 270f, 315f })
            {
                Vector2 axis = Quaternion.Euler(0, 0, degrees) * Vector2.right;
                Vector2 velocity = axis * game.Config.BossTuning.swordSpeed;
                float life = IndependentArenaLife(origin, velocity, float.PositiveInfinity);
                Check(life > 0 && life <= horizon + 0.0001f, "finite arena diagonal horizon covers each independent boundary lifetime");
                var shot = KarmaBossAttack.Spawn(game, enemy, root, origin, velocity, size, life, 0,
                    Color.white, null, 0, "Blade", true, false, "SW05_AerialSword");
                int steps = 0;
                while (shot.gameObject.activeSelf && steps++ < Mathf.CeilToInt(life / dt) + 3)
                    Call(shot, "Update");
                Vector2 expected = origin + velocity * life;
                Check(!shot.gameObject.activeSelf && Vector2.Distance(shot.transform.position, expected) < 0.002f,
                    "actual SW05 Update reaches its arena exit before deactivating, including shallow and diagonal rays");
                Check(!shot.Visual.gameObject.activeInHierarchy,
                    "projectile art and damage deactivate together at the boundary");
            }
        Debug.Log("SW05 BOUNDARY RAYS PASS: 40 actual runtime projectile traces, cardinal/diagonal/shallow directions and wall-adjacent origins");
    }
    static void AerialEscape(KarmaGame game, KarmaEnemy enemy)
    {
        var player = game.Player;
        var collider = player.GetComponent<BoxCollider2D>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var scene = SceneManager.CreateScene("Wave real-physics escape " + collider.size.x, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics = scene.GetPhysicsScene2D();
        try
        {
            foreach (var surface in game.World.GetComponentsInChildren<KarmaSurface>())
            {
                var source = surface.GetComponent<BoxCollider2D>(); if (source == null) continue;
                var copy = new GameObject("Escape " + surface.name); SceneManager.MoveGameObjectToScene(copy, scene);
                copy.transform.position = surface.transform.position; copy.transform.localScale = surface.transform.lossyScale;
                var shape = copy.AddComponent<BoxCollider2D>(); shape.size = source.size; shape.offset = source.offset;
            }
            var probe = new GameObject("Actual player collider and speed escape probe"); SceneManager.MoveGameObjectToScene(probe, scene);
            var shapePlayer = probe.AddComponent<BoxCollider2D>(); shapePlayer.size = collider.size; shapePlayer.offset = collider.offset;
            var body = probe.AddComponent<Rigidbody2D>(); body.freezeRotation = true; body.gravityScale = game.Config.gravityScale;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            foreach (bool enrage in new[] { false, true })
            {
                Set(enemy, "<Health>k__BackingField", enemy.MaxHealth * (enrage ? 0.4f : 1));
                foreach (float x in new[] { KarmaStage.ArenaLeft + 2, 6, 20, 42, KarmaStage.ArenaRight - 2 })
                {
                    playerBody.position = new Vector2(x, 2); player.transform.position = playerBody.position; Physics2D.SyncTransforms();
                    var bounds = collider.bounds;
                    foreach (float span in new[] { KarmaStage.ArenaWidth })
                    {
                        object[] scars = { KarmaStage.ArenaLeft, KarmaStage.ArenaLeft + span, Vector2.zero, Vector2.zero };
                        bool paired = (bool)Call(enemy, "PlaceSwordScars", scars);
                        Vector2 first = (Vector2)scars[2], second = (Vector2)scars[3];
                        float safeX;
                        if (paired) safeX = Mathf.Clamp(bounds.center.x, first.x + 2.25f + bounds.extents.x + 0.25f,
                            second.x - 2.25f - bounds.extents.x - 0.25f);
                        else
                        {
                            float left = first.x - 2.25f - bounds.extents.x - 0.25f;
                            float right = first.x + 2.25f + bounds.extents.x + 0.25f;
                            safeX = Mathf.Abs(left - bounds.center.x) < Mathf.Abs(right - bounds.center.x) ? left : right;
                            if (bounds.center.x < left || bounds.center.x > right) safeX = bounds.center.x;
                        }
                        body.position = new Vector2(x, 2); body.linearVelocity = Vector2.zero;
                        float scarWarning = (float)Call(enemy, "Warning", 0.75f);
                        for (float t = 0; t + 0.02f <= scarWarning; t += 0.02f)
                        {
                            float dx = safeX - (body.position.x + shapePlayer.offset.x);
                            body.linearVelocity = new Vector2(Mathf.Clamp(dx / 0.02f, -game.Config.moveSpeed, game.Config.moveSpeed), body.linearVelocity.y);
                            physics.Simulate(0.02f);
                        }
                        float actualX = body.position.x + shapePlayer.offset.x;
                        bool firstHit = Mathf.Abs(actualX - first.x) <= 2.25f + bounds.extents.x;
                        bool secondHit = paired && Mathf.Abs(actualX - second.x) <= 2.25f + bounds.extents.x;
                        Check(!firstHit && !secondHit, "real physics body reaches scar safety before warning ends, including walls/single-scar fallback");
                    }
                    foreach (int seed in new[] { 1, 20261029, 8675309, 2147483, 7, 42, 99, 31337,
                        65537, 104729, 314159, 271828, 1234567, 7654321, 20261030, int.MaxValue })
                        Check(FullAerialEscape(game, enemy, physics, body, shapePlayer, new Vector2(x, 2), seed),
                            "real movement/jump survives every seeded independent sword: x=" + x + " enraged=" + enrage + " seed=" + seed);
                }
            }
        }
        finally { SceneManager.UnloadSceneAsync(scene); }
    }
    sealed class SimulatedSword
    {
        public Vector2 origin, position, velocity;
        public float age, life;
        public KarmaBossAttack proxy;
    }
    static bool FullAerialEscape(KarmaGame game, KarmaEnemy enemy, PhysicsScene2D physics,
        Rigidbody2D body, BoxCollider2D collider, Vector2 start, int seed)
    {
        var realBody = game.Player.GetComponent<Rigidbody2D>();
        Vector2 savedPosition = realBody.position, savedVelocity = realBody.linearVelocity;
        var root = new GameObject("Seeded aerial escape " + seed);
        try
        {
            body.simulated = false; body.transform.position = start; body.simulated = true;
            body.position = start; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            for (int settle = 0; settle < 20; settle++) physics.Simulate(Time.fixedDeltaTime);
            Check(Mathf.Abs(body.position.x - start.x) < 0.02f,
                "independent full-cast probe starts at the requested wall/arena position without stale velocity");
            realBody.position = body.position; realBody.linearVelocity = body.linearVelocity;
            game.Player.transform.position = body.position; Physics2D.SyncTransforms();
            var planner = KarmaAerialSwordPlanner.Create(root.transform, game, seed);
            var sizes = planner.GroupSizes;
            int expected = 0;
            foreach (int n in sizes) { Check(n == 15, "seeded default volley has exactly fifteen independent swords"); expected += n; }
            Check(sizes.Length == 7 && expected == 105, "seeded cast contains exactly seven fifteen-sword volleys");
            float warning = (float)Call(enemy, "Warning", 0.5f), step = Time.fixedDeltaTime;
            float interval = (float)Call(enemy, "AerialSwordInterval");
            float maximumLife = (float)Call(enemy, "AerialSwordReachLife");
            float budget = warning + 6 * interval + maximumLife + step * 8;
            Vector2 size = (Vector2)Call(enemy, "SwordSize", EnemyAttack.SW05);
            var shots = new System.Collections.Generic.List<SimulatedSword>();
            var octants = new System.Collections.Generic.HashSet<int>();
            var histogram = new int[8];
            int maxLive = 0, minGroup = 18, maxGroup = 0;
            double maxPlanningMs = 0;
            bool left = false, right = false, up = false, down = false;
            int group = 0, launches = 0;
            float formationAt = 0, nextFormation = 0, launchAt = float.PositiveInfinity;
            float previousLaunch = -1;
            bool forming = false, jumped = false;
            KarmaAerialSwordPlanner.Group plan = null;
            for (float t = 0; t <= budget + 2; t += step)
            {
                if (!forming && group < sizes.Length && t + 0.00001f >= nextFormation)
                {
                    realBody.position = body.position; realBody.linearVelocity = body.linearVelocity;
                    game.Player.transform.position = body.position; Physics2D.SyncTransforms();
                    float life = maximumLife;
                    var elevated = new Vector2(20, KarmaSpace.Units(6));
                    var planningClock = System.Diagnostics.Stopwatch.StartNew();
                    plan = planner.BuildGroup(elevated, group, warning, life, size, game.Config.BossTuning.swordSpeed, t);
                    planningClock.Stop(); maxPlanningMs = Math.Max(maxPlanningMs, planningClock.Elapsed.TotalMilliseconds);
                    minGroup = Mathf.Min(minGroup, plan.shots.Length); maxGroup = Mathf.Max(maxGroup, plan.shots.Length);
                    Check(plan.reachableEscape, "cumulative old/new swords leave a physical route: seed=" + seed + " group=" + group);
                    Check(plan.shots.Length == sizes[group], "runtime planned volley matches its seeded count");
                    Check(Vector2.Distance(plan.escapeCenters[0], body.position + collider.offset) < 0.001f,
                        "physics witness begins at the actual current player collider, not a prior simulation endpoint");
                    Debug.Log("SW05 ROUTE seed=" + seed + " group=" + group + " at=" + t + " player=" + (body.position + collider.offset)
                        + " velocity=" + body.linearVelocity + " input=" + plan.escapeInput + " jumpAt=" + plan.escapeJumpAt
                        + " predictedStart=" + plan.escapeCenters[0] + " predictedEnd=" + plan.escapeCenters[plan.escapeCenters.Length - 1]);
                    if (group == 0 && Near(start.x, 20) && !enemy.Enraged)
                    {
                        var again = KarmaAerialSwordPlanner.Create(root.transform, game, seed);
                        var replay = again.BuildGroup(elevated, 0, warning, life, size, game.Config.BossTuning.swordSpeed, t);
                        Check(replay.shots.Length == plan.shots.Length, "same seed/state repeats the volley count");
                        for (int i = 0; i < plan.shots.Length; i++)
                            Check(Vector2.Distance(replay.shots[i].origin, plan.shots[i].origin) < 0.00001f
                                && Vector2.Distance(replay.shots[i].velocity, plan.shots[i].velocity) < 0.00001f,
                                "same seed/state repeats each independently aimed sword");
                        again.gameObject.SetActive(false); UnityEngine.Object.DestroyImmediate(again.gameObject);
                    }
                    forming = true; jumped = false; formationAt = t; launchAt = t + warning;
                }
                if (forming && t + 0.00001f >= launchAt)
                {
                    Check(Mathf.Abs(t - (warning + group * interval)) <= step * 2 + 0.0001f,
                        "every full-warning volley releases in its independent one-second slot");
                    if (previousLaunch >= 0) Check(Mathf.Abs(t - previousLaunch - interval) <= step * 2 + 0.0001f,
                        "the independent full-cast simulation maintains one-second release-to-release spacing");
                    previousLaunch = t;
                    foreach (var sword in plan.shots)
                    {
                        Check(Mathf.Abs(sword.life - IndependentArenaLife(sword.origin, sword.velocity, float.PositiveInfinity)) < 0.0001f,
                            "each planned sword keeps its full origin/direction/speed-dependent boundary lifetime");
                        var proxy = new GameObject("Independent live sword geometry").AddComponent<KarmaBossAttack>();
                        proxy.transform.SetParent(root.transform, false); proxy.transform.position = sword.origin;
                        planner.RegisterLaunch(sword, proxy, size, t);
                        shots.Add(new SimulatedSword { origin = sword.origin, position = sword.origin, velocity = sword.velocity, life = sword.life, proxy = proxy });
                        Vector2 axis = sword.velocity.normalized;
                        Check(Mathf.Abs(sword.velocity.magnitude - game.Config.BossTuning.swordSpeed) < 0.0001f,
                            "every randomized sword retains actual projectile speed");
                        int octant = Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg + 22.5f, 360) / 45);
                        octants.Add(octant); histogram[octant]++;
                        left |= axis.x < -0.2f; right |= axis.x > 0.2f; up |= axis.y > 0.2f; down |= axis.y < -0.2f;
                        launches++;
                    }
                    nextFormation = (group + 1) * interval;
                    group++; forming = false;
                }
                if (group == sizes.Length && !forming && shots.TrueForAll(shot => shot.age >= shot.life))
                {
                    foreach (var shot in shots)
                        Check(Vector2.Distance(shot.position, shot.origin + shot.velocity * shot.life) < 0.002f,
                            "full seven-volley escape continues through every final sword's actual arena exit");
                    Check(launches == expected, "all independently simulated swords launch before cleanup");
                    Check(left && right && up && down && octants.Count >= 6,
                        "a seeded full pattern covers 360 degrees with cardinal and diagonal directions: seed=" + seed);
                    Debug.Log("SW05 SEEDED FULL ESCAPE seed=" + seed + " x=" + start.x + " width=" + collider.size.x
                        + " enraged=" + enemy.Enraged + " swords=" + launches + " groups=" + sizes.Length + " groupMinMax=" + minGroup + ".." + maxGroup
                        + " octants=" + octants.Count + " histogram=" + string.Join(",", histogram) + " peakLive=" + maxLive
                        + " maxPlannerMs=" + maxPlanningMs.ToString("F3"));
                    return true;
                }
                float vertical = body.linearVelocity.y;
                if (!jumped && plan.escapeJumpAt >= 0 && t - formationAt + 0.00001f >= plan.escapeJumpAt)
                { vertical = game.Config.jumpSpeed; jumped = true; }
                Vector2 before = body.position + collider.offset;
                body.linearVelocity = new Vector2(plan.escapeInput * game.Config.moveSpeed, vertical);
                physics.Simulate(step);
                Vector2 after = body.position + collider.offset;
                int liveCount = 0;
                foreach (var shot in shots)
                {
                    if (shot.age >= shot.life) continue;
                    liveCount++;
                    float dt = Mathf.Min(step, shot.life - shot.age);
                    dt = IndependentArenaLife(shot.position, shot.velocity, dt);
                    Vector2 to = shot.position + shot.velocity * dt, axis = shot.velocity.normalized;
                    Vector2 playerEnd = Vector2.Lerp(before, after, dt / step);
                    if (KarmaBossGeometry.SweptOrientedBox(shot.position.x - before.x, shot.position.y - before.y,
                        to.x - playerEnd.x, to.y - playerEnd.y, 0, 0, axis.x, axis.y, size.x, size.y,
                        collider.bounds.extents.x, collider.bounds.extents.y))
                    {
                        int witnessStep = Mathf.Clamp(Mathf.RoundToInt((t - formationAt) / step), 0, plan.escapeCenters.Length - 1);
                        Debug.Log("SW05 ESCAPE COLLISION seed=" + seed + " group=" + group + " t=" + t + " player=" + before + " ray=" + shot.position
                            + " velocity=" + shot.velocity + " age=" + shot.age + " life=" + shot.life + " input=" + plan.escapeInput
                            + " jumpAt=" + plan.escapeJumpAt + " formationAt=" + formationAt + " expectedPlayer=" + plan.escapeCenters[witnessStep]);
                        return false;
                    }
                    shot.position = to; shot.proxy.transform.position = to;
                    shot.age = dt < step - 0.00001f ? shot.life : shot.age + step;
                    if (shot.age >= shot.life) shot.proxy.gameObject.SetActive(false);
                }
                maxLive = Mathf.Max(maxLive, liveCount);
            }
            return false;
        }
        finally
        {
            root.SetActive(false); UnityEngine.Object.DestroyImmediate(root);
            realBody.position = savedPosition; realBody.linearVelocity = savedVelocity;
            game.Player.transform.position = savedPosition; Physics2D.SyncTransforms();
        }
    }
    static float IndependentArenaLife(Vector2 origin, Vector2 velocity, float life)
    {
        if (velocity.x > 0) life = Mathf.Min(life, (KarmaStage.ArenaRight - origin.x) / velocity.x);
        else if (velocity.x < 0) life = Mathf.Min(life, (KarmaStage.ArenaLeft - origin.x) / velocity.x);
        if (velocity.y > 0) life = Mathf.Min(life, (KarmaSpace.Units(12) - origin.y) / velocity.y);
        else if (velocity.y < 0) life = Mathf.Min(life, -origin.y / velocity.y);
        return Mathf.Max(0, life);
    }
}
#endif
