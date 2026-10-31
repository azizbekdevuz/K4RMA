#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using KarmaPrototype;

// Visual-only regression checks. The main Play Mode runner calls this in the
// player loop, where Sprite.OverrideGeometry and Time.deltaTime are available.
public static class KarmaWaveVFXChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Update(object target) => target.GetType().GetMethod("Update", Private).Invoke(target, null);
    static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception("Wave VFX: " + message); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;

    public static int Run(KarmaGame game, KarmaEnemy enemy)
    {
        assertions = 0;
        var root = new GameObject("Wave VFX regression probes");
        try
        {
            WarningChecks(root.transform);
            ProjectileChecks(root.transform);
            LayerChecks(root.transform);
            SpawnChecks(root.transform, game, enemy);
            AerialWarningChecks(root.transform, enemy);
            AerialProjectileChecks(root.transform, game, enemy);
            assertions += KarmaHitFlashChecks.Run(game);
            return assertions;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void WarningChecks(Transform root)
    {
        var from = new Vector2(20, 3);
        var size = new Vector2(2.4f, 1);
        var offsets = new[] { Vector2.zero, new Vector2(50, 0), new Vector2(0, 20),
            new Vector2(40, 20), new Vector2(-40, 20), new Vector2(40, -20), new Vector2(-40, -20) };
        foreach (bool locked in new[] { false, true })
        {
            var color = locked ? new Color(1, 0.55f, 0.2f, 0.3f) : new Color(0.3f, 0.85f, 1, 0.22f);
            var marker = KarmaVisuals.Box(root, "Restrained warning probe", Vector2.zero, Vector2.one, color);
            var warning = marker.gameObject.AddComponent<KarmaAttackWarning>();
            foreach (var offset in offsets)
            {
                var to = from + offset;
                warning.SetPath(marker, from, to, size, true);
                var edge = Get<LineRenderer>(warning, "edge");
                var arrow = Get<LineRenderer>(warning, "arrow");
                Check(edge.useWorldSpace && arrow.useWorldSpace, "danger lines have world-unit geometry");
                Check(edge.widthMultiplier >= 0.05f && edge.widthMultiplier <= 0.0701f
                    && Near(arrow.widthMultiplier, 0.08f), "first-frame lines are thin before any Update");
                Check(edge.startColor != Color.white && edge.startColor == arrow.startColor,
                    "first-frame lines never use the default opaque white");
                Check(edge.sharedMaterial == marker.sharedMaterial && arrow.sharedMaterial == marker.sharedMaterial,
                    "danger boundary uses alpha blending rather than additive glare");
                Check(locked ? edge.startColor.r > edge.startColor.b : edge.startColor.b > edge.startColor.r,
                    "orange locked and blue tracking semantics remain distinct");
                Check(marker.color.a <= color.a * 0.3201f && marker.color.a >= color.a * 0.2399f,
                    "warning fill is translucent on its first rendered frame");
                var min = Vector2.Min(from, to) - size / 2;
                var max = Vector2.Max(from, to) + size / 2;
                Check(Vector2.Distance(marker.bounds.min, min) < 0.0001f
                    && Vector2.Distance(marker.bounds.max, max) < 0.0001f, "danger fill retains exact endpoint-box bounds");
                Check(edge.positionCount == (offset.x != 0 && offset.y != 0 ? 6 : 4),
                    "diagonal warning retains the swept hull and safe corners");
                for (int i = 0; i < edge.positionCount; i++)
                {
                    Vector2 point = edge.GetPosition(i);
                    bool endpointCorner = false;
                    foreach (var endpoint in new[] { from, to })
                        for (int x = -1; x <= 1; x += 2)
                            for (int y = -1; y <= 1; y += 2)
                                endpointCorner |= Vector2.Distance(point, endpoint + new Vector2(size.x * x, size.y * y) / 2) < 0.0001f;
                    Check(endpointCorner, "danger outline follows an actual swept-box hull vertex");
                }
                Check(arrow.enabled == (offset.sqrMagnitude > 0.01f), "stationary warnings have no misleading travel arrow");
                if (arrow.enabled)
                {
                    Vector2 head = arrow.GetPosition(1);
                    Vector2 tail = (arrow.GetPosition(0) + arrow.GetPosition(2)) / 2;
                    Check(Vector2.Dot((head - tail).normalized, offset.normalized) > 0.999f,
                        "world-space arrow points along the unchanged attack path");
                }
                // Production SW02 tracking calls omit the last argument after creation.
                warning.SetPath(marker, from, to, size);
                Check(edge.useWorldSpace && Get<bool>(warning, "restrainedVisual"), "tracking retains the Wave visual option");
            }
        }
        var legacy = KarmaVisuals.Box(root, "Legacy warning probe", Vector2.zero, Vector2.one, new Color(1, 0.55f, 0.2f, 0.3f));
        var oldWarning = legacy.gameObject.AddComponent<KarmaAttackWarning>();
        oldWarning.SetPath(legacy, from, from + Vector2.right * 20, size);
        Update(oldWarning);
        var oldEdge = Get<LineRenderer>(oldWarning, "edge");
        var oldArrow = Get<LineRenderer>(oldWarning, "arrow");
        Check(!oldEdge.useWorldSpace && !oldArrow.useWorldSpace && !Get<bool>(oldWarning, "restrainedVisual"),
            "default callers retain legacy local warning geometry");
        Check(oldEdge.sharedMaterial == (KarmaCombatVFX.GlowMaterial != null ? KarmaCombatVFX.GlowMaterial : legacy.sharedMaterial),
            "default callers retain their existing warning material");
        Check(oldEdge.widthMultiplier >= 0.07f && oldEdge.widthMultiplier <= 0.1001f && Near(oldArrow.widthMultiplier, 0.11f),
            "default callers retain their warning widths");
        Check(legacy.color.a >= 0.1799f && legacy.color.a <= 0.2851f, "default callers retain their fill opacity");
    }
    static void ProjectileChecks(Transform root)
    {
        var sr = KarmaVisuals.Box(root, "Projectile fit probe", Vector2.zero, Vector2.one, Color.white);
        var size = new Vector2(3.24f, 1.35f);
        foreach (string pose in new[] { "Wave", "AimedWave", "Blade", "Pillar" })
            foreach (var velocity in new[] { Vector2.zero, Vector2.right, new Vector2(-1, 1), new Vector2(1, -1) })
            {
                KarmaBossAttack.ConfigureVisual(sr, velocity, size, -1, pose);
                var oldScale = sr.transform.localScale;
                var originalSprite = sr.sprite;
                KarmaBossAttack.ConfigureVisual(sr, velocity, size, -1, pose, true);
                Check(sr.sprite == originalSprite && sr.sprite == KarmaArtwork.Sprite("GuardianVFX", pose),
                    "restrained projectile reuses the existing " + pose + " sprite");
                float ratio = (velocity.sqrMagnitude > 0.01f ? 1 : 0.9f) / 1.15f;
                Check(Near(sr.transform.localScale.x / oldScale.x, ratio)
                    && Near(sr.transform.localScale.y / oldScale.y, ratio), "Wave picture uses the maximum aspect-preserved fit without changing its hit box");
                Check(sr.GetComponentsInChildren<Collider2D>().Length == 0, "visual fitting adds no physical collider");
                if (velocity.sqrMagnitude > 0.01f)
                    Check(Vector2.Dot(sr.transform.right, velocity.normalized) > 0.999f,
                        "restrained projectile remains aligned to actual velocity");
            }
    }
    static void LayerChecks(Transform root)
    {
        var sprite = KarmaArtwork.Sprite("GuardianVFX", "Wave");
        Check(sprite != null && Time.deltaTime > 0, "VFX checks run with real artwork inside Play Mode");
        foreach (bool restrained in new[] { false, true })
        {
            var group = new GameObject(restrained ? "Wave layer probe" : "Legacy layer probe");
            group.transform.SetParent(root, false);
            var sr = KarmaVisuals.Box(group.transform, "Moving projectile", Vector2.zero, Vector2.one, Color.white);
            KarmaBossAttack.ConfigureVisual(sr, Vector2.right, new Vector2(2.4f, 1), 1, "Wave", restrained);
            KarmaCombatVFX.AttachAttack(sr, Vector2.right, Color.yellow, group.transform, restrained);
            var fx = sr.GetComponent<KarmaCombatVFX>();
            var radiance = sr.transform.Find("Attack radiance").GetComponent<SpriteRenderer>();
            Check(Near(radiance.color.a, restrained ? 0.18f : 1), "initial radiance preserves the requested visual scope");
            Update(fx);
            Check(restrained ? radiance.color.a >= 0.1249f && radiance.color.a <= 0.1951f
                : radiance.color.a >= 0.4299f && radiance.color.a <= 0.8701f, "sustained radiance uses the scoped opacity range");
            Check(restrained ? radiance.transform.localScale.x <= 1.0501f : radiance.transform.localScale.x >= 1.0899f,
                "restrained radiance does not inflate the moving core");
            var ghost = group.transform.Find("Attack afterimage");
            Check(ghost != null, "moving attack emits its existing-sprite afterimage");
            Check(Near(ghost.GetComponent<SpriteRenderer>().color.a, restrained ? 0.16f : 0.5f)
                && Near(Get<float>(ghost.GetComponent<KarmaEffect>(), "duration"), restrained ? 0.11f : 0.19f),
                "afterimage alpha and lifetime change only for restrained visuals");
            Check(Near(Get<float>(fx, "trailAt"), Get<float>(fx, "age") + (restrained ? 0.075f : 0.045f)),
                "afterimage emission interval changes only for restrained visuals");
            var core = KarmaCombatVFX.Burst(group.transform, Vector2.zero, sprite, "Launch probe", new Vector2(2.4f, 1),
                0.24f, 0, Color.yellow, restrained);
            var burst = core.GetComponentInParent<KarmaCombatVFX>();
            Check(Near(core.color.a, restrained ? 0.78f : 1), "launch first frame has the scoped core alpha");
            Update(burst);
            var glow = Get<SpriteRenderer>(burst, "glow");
            var echo = Get<SpriteRenderer>(burst, "echo");
            Check(restrained ? glow.color.a <= 0.1801f && echo.color.a <= 0.1001f : glow.color.a > 0.8f,
                "launch glow and echo avoid stacked white glare only for Wave visuals");
        }
    }
    static void SpawnChecks(Transform root, KarmaGame game, KarmaEnemy enemy)
    {
        var selected = typeof(KarmaEnemy).GetField("selected", Private);
        object oldSelected = selected.GetValue(enemy);
        SpriteRenderer path = null;
        try
        {
            selected.SetValue(enemy, EnemyAttack.SW02);
            var dimensions = (Vector2)typeof(KarmaEnemy).GetMethod("SwordSize", Private)
                .Invoke(enemy, new object[] { EnemyAttack.SW02 });
            var origin = (Vector2)typeof(KarmaEnemy).GetMethod("AimedSlashOrigin", Private)
                .Invoke(enemy, new object[] { new Vector2(20, KarmaSpace.GuardianGroundY), 1f });
            Vector2 directedVelocity = Vector2.right * game.Config.BossTuning.swordSpeed * 1.25f;
            float directedLife = (float)typeof(KarmaEnemy).GetMethod("AimedSlashLife", Private)
                .Invoke(enemy, new object[] { origin, directedVelocity });
            path = (SpriteRenderer)typeof(KarmaEnemy).GetMethod("PathMark", Private).Invoke(enemy,
                new object[] { origin, origin + directedVelocity * directedLife, dimensions, true });
            Check(Near(path.bounds.max.x, KarmaStage.ArenaRight),
                "SW02 path ends where the projectile's full core reaches the arena boundary");
        }
        finally
        {
            selected.SetValue(enemy, oldSelected);
            if (path != null) UnityEngine.Object.DestroyImmediate(path.gameObject);
        }
        var size = new Vector2(3, 2);
        var velocity = new Vector2(-12, 7);
        foreach (bool separate in new[] { false, true })
        {
            var group = new GameObject(separate ? "Separate Wave swing probe" : "Legacy launch probe");
            group.transform.SetParent(root, false);
            var attack = KarmaBossAttack.Spawn(game, enemy, group.transform, new Vector2(20, 4), velocity, size,
                1.65f, 9, Color.yellow, null, 0.2f, "AimedWave", separate, separate);
            Check(attack.transform.localScale == Vector3.one && attack.transform.rotation == Quaternion.identity,
                "artwork does not rotate or scale the attack root");
            Check(Get<Vector2>(attack, "size") == size && Get<Vector2>(attack, "velocity") == velocity
                && Near(Get<float>(attack, "remaining"), 1.65f) && Near(Get<float>(attack, "damage"), 9)
                && Near(Get<float>(attack, "stun"), 0.2f), "visual options preserve every attack parameter");
            Check(attack.GetComponentsInChildren<Collider2D>().Length == 0, "visual options do not introduce colliders");
            Check(attack.GetComponentsInChildren<LineRenderer>(true).Length == 0,
                "released attacks have no extra damage-boundary line");
            int launches = 0;
            foreach (Transform child in group.transform)
                if (child.name == "Guardian launch/AimedWave") launches++;
            Check(launches == (separate ? 0 : 1),
                "SW01/02 separate swing avoids the duplicate generic launch; default launch is retained");
        }
        foreach (float degrees in new[] { 0f, 30f, 45f, 90f, 180f, 150f, 135f, -90f })
        {
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 heading = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var dimensions = new Vector2(3.24f, 1.35f);
            var attack = KarmaBossAttack.Spawn(game, enemy, root, new Vector2(20, 8), heading * 40, dimensions,
                1.65f, 9, Color.yellow, null, 0, "AimedWave", true, true);
            Check(attack.GetComponentsInChildren<LineRenderer>(true).Length == 0,
                degrees + " degrees has no released damage-boundary line");
            Check(Get<Vector2>(attack, "size") == dimensions,
                "removing the boundary leaves the actual damage dimensions intact");
            Vector2 drawn = attack.Visual.bounds.size;
            Check(drawn.x <= dimensions.x + 0.0001f && drawn.y <= dimensions.y + 0.0001f
                && Near(Mathf.Max(drawn.x / dimensions.x, drawn.y / dimensions.y), 1),
                degrees + " degrees legacy artwork retains the largest fit inside its actual damage box");
            Check(Vector2.Dot(attack.Visual.transform.right, heading) > 0.999f,
                degrees + " degrees picture follows the real velocity");
            var fx = attack.Visual.GetComponent<KarmaCombatVFX>(); Update(fx);
            var echoes = Get<System.Collections.Generic.List<GameObject>>(fx, "ownedAfterimages").ToArray();
            attack.gameObject.SetActive(false);
            Check(!attack.Visual.gameObject.activeInHierarchy, "projectile artwork ends in the same frame as damage");
            foreach (var echo in echoes)
                if (echo != null)
                    Check(!echo.activeInHierarchy, "owned echo cannot outlive its cancelled projectile");
        }
        AimedDirectionChecks(root, game, enemy);
        foreach (var pattern in new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 })
        {
            bool planted = pattern == EnemyAttack.SW03;
            var attack = KarmaBossAttack.Spawn(game, enemy, root, new Vector2(20, 8), planted ? Vector2.zero : Vector2.right * 32,
                planted ? new Vector2(4.5f, 13.5f) : new Vector2(3.24f, 1.35f), planted ? 3.25f : 1.65f,
                9, Color.white, null, 0, null, true, true, KarmaWaveArtwork.Pose(pattern, planted));
            Check(attack.GetComponentsInChildren<LineRenderer>(true).Length == 0,
                pattern + " has no separately drawn hit-box boundary after release");
            Check(attack.Visual.sprite == KarmaArtwork.Sprite("ExtraWaveVFX", KarmaWaveArtwork.Pose(pattern, planted)),
                pattern + " retains its supplied projectile PNG");
            attack.gameObject.SetActive(false);
        }
    }
    static Vector2 SourcePoint(SpriteRenderer picture, Vector2 pixel)
    {
        Vector2 local = (pixel - picture.sprite.pivot) / picture.sprite.pixelsPerUnit;
        if (picture.flipX) local.x = -local.x;
        if (picture.flipY) local.y = -local.y;
        return picture.transform.TransformPoint(local);
    }
    static void AerialWarningStyle(SpriteRenderer marker, KarmaAttackWarning warning)
    {
        var edge = Get<LineRenderer>(warning, "edge");
        var arrow = Get<LineRenderer>(warning, "arrow");
        Color source = Get<Color>(warning, "baseColor");
        Check(Get<bool>(warning, "aerialVisual") && Get<bool>(warning, "restrainedVisual"),
            "SW05 warning keeps its dedicated restrained style");
        Check(marker.color.a >= source.a * 0.10f - 0.0001f && marker.color.a <= source.a * 0.14f + 0.0001f,
            "SW05 overlapping path fills remain faint without hiding their full danger area");
        Check(edge.startColor.a >= 0.3399f && edge.startColor.a <= 0.4401f
            && edge.startColor == edge.endColor && edge.startColor.r > edge.startColor.b,
            "SW05 full danger boundary retains a restrained orange opacity");
        Check(edge.widthMultiplier >= KarmaSpace.Units(0.018f) - 0.0001f
            && edge.widthMultiplier <= KarmaSpace.Units(0.022f) + 0.0001f,
            "SW05 boundary is thinner while its swept footprint stays unchanged");
        Check(arrow.startColor.a >= 0.7999f && arrow.startColor.a <= 0.9001f
            && arrow.startColor == arrow.endColor && Near(arrow.widthMultiplier, KarmaSpace.Units(0.03f)),
            "SW05 near-origin direction cue remains clearer than overlapping full paths");
        Check(edge.sharedMaterial == marker.sharedMaterial && arrow.sharedMaterial == marker.sharedMaterial
            && edge.useWorldSpace && arrow.useWorldSpace,
            "SW05 path layers use alpha blending and world-unit widths");
    }
    static void AerialWarningChecks(Transform root, KarmaEnemy enemy)
    {
        Vector2 from = new Vector2(20, 8), size = new Vector2(3.12f, 1.3f);
        Color color = new Color(1, 0.55f, 0.2f, 0.3f);
        foreach (float distance in new[] { KarmaSpace.Units(0.4f), 50f })
            foreach (float degrees in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                Vector2 axis = Quaternion.Euler(0, 0, degrees) * Vector2.right;
                Vector2 normal = new Vector2(-axis.y, axis.x), to = from + axis * distance;
                var marker = KarmaVisuals.Box(root, "SW05 path style probe", Vector2.zero, Vector2.one, color);
                var warning = marker.gameObject.AddComponent<KarmaAttackWarning>();
                warning.SetPath(marker, from, to, size, true, true, true);
                var control = KarmaVisuals.Box(root, "Unchanged oriented path control", Vector2.zero, Vector2.one, color);
                var controlWarning = control.gameObject.AddComponent<KarmaAttackWarning>();
                controlWarning.SetPath(control, from, to, size, true, true);
                var edge = Get<LineRenderer>(warning, "edge");
                var controlEdge = Get<LineRenderer>(controlWarning, "edge");
                Check(Vector3.Distance(marker.bounds.center, control.bounds.center) < 0.0001f
                    && Vector3.Distance(marker.bounds.size, control.bounds.size) < 0.0001f
                    && edge.positionCount == controlEdge.positionCount,
                    "SW05 lighter path retains the existing full swept oriented hull at " + degrees + " degrees");
                float minAlong = float.PositiveInfinity, maxAlong = float.NegativeInfinity;
                for (int i = 0; i < edge.positionCount; i++)
                {
                    Vector2 point = edge.GetPosition(i);
                    Check(Vector3.Distance(edge.GetPosition(i), controlEdge.GetPosition(i)) < 0.0001f,
                        "SW05 style changes cannot shorten or narrow a danger-boundary vertex");
                    Vector2 offset = point - from;
                    minAlong = Mathf.Min(minAlong, Vector2.Dot(offset, axis));
                    maxAlong = Mathf.Max(maxAlong, Vector2.Dot(offset, axis));
                    Check(Mathf.Abs(Mathf.Abs(Vector2.Dot(offset, normal)) - size.y / 2) < 0.0001f,
                        "SW05 full warning width equals the actual oriented damage width");
                }
                Check(Mathf.Abs(minAlong + size.x / 2) < 0.0001f
                    && Mathf.Abs(maxAlong - distance - size.x / 2) < 0.0001f,
                    "SW05 full warning covers both endpoint damage boxes without truncation");
                var arrow = Get<LineRenderer>(warning, "arrow");
                Vector2 head = arrow.GetPosition(1);
                Vector2 tail = (arrow.GetPosition(0) + arrow.GetPosition(2)) / 2;
                Check(arrow.enabled && Vector2.Dot((head - tail).normalized, axis) > 0.9999f
                    && Vector2.Distance(head, from + axis * Mathf.Min(distance * 0.25f, KarmaSpace.Units(1.2f))) < 0.0001f,
                    "SW05 arrow identifies the launch direction near its own sword rather than the distant overlap");
                AerialWarningStyle(marker, warning);
                Update(warning);
                AerialWarningStyle(marker, warning);
                warning.SetPath(marker, from, to, size, true, true);
                AerialWarningStyle(marker, warning);
                Check(!Get<bool>(controlWarning, "aerialVisual") && controlEdge.startColor.a >= 0.7199f
                    && controlEdge.widthMultiplier >= KarmaSpace.Units(0.025f) - 0.0001f,
                    "SW01-SW04 retain their existing restrained warning style");
            }
        var selected = typeof(KarmaEnemy).GetField("selected", Private);
        object oldSelected = selected.GetValue(enemy);
        SpriteRenderer livePath = null;
        try
        {
            selected.SetValue(enemy, EnemyAttack.SW05);
            livePath = (SpriteRenderer)typeof(KarmaEnemy).GetMethod("PathMark", Private).Invoke(enemy,
                new object[] { from, new Vector2(KarmaStage.ArenaRight + 50, from.y), size, true });
            Check(Get<bool>(livePath.GetComponent<KarmaAttackWarning>(), "aerialVisual"),
                "actual SW05 PathMark selects the aerial-only warning style");
            Check(Near(livePath.bounds.max.x, KarmaStage.ArenaRight + size.x / 2),
                "SW05 path keeps arena-limited center travel plus the full projectile half-width");
            AerialWarningStyle(livePath, livePath.GetComponent<KarmaAttackWarning>());
        }
        finally
        {
            selected.SetValue(enemy, oldSelected);
            if (livePath != null) UnityEngine.Object.DestroyImmediate(livePath.gameObject);
        }
    }
    static void AerialProjectileChecks(Transform root, KarmaGame game, KarmaEnemy enemy)
    {
        var material = Resources.Load<Material>("KarmaAerialSword");
        Check(material != null && material.shader != null && material.shader.name == "K4RMA/AerialSword",
            "SW05 loads its transparent blue-white palette material through Resources");
        var art = Get<Transform>(enemy, "art");
        Vector3 originalScale = art.localScale;
        var dimensions = (Vector2)typeof(KarmaEnemy).GetMethod("SwordSize", Private).Invoke(enemy, new object[] { EnemyAttack.SW05 });
        try
        {
            foreach (int facing in new[] { -1, 1 })
                foreach (float degrees in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
                {
                    art.localScale = new Vector3(Mathf.Abs(originalScale.x) * facing, originalScale.y, originalScale.z);
                    var group = new GameObject("SW05 isolated release probe"); group.transform.SetParent(root, false);
                    Vector2 heading = Quaternion.Euler(0, 0, degrees) * Vector2.right;
                    Vector2 origin = new Vector2(20, 8), velocity = heading * game.Config.BossTuning.swordSpeed;
                    var contact = new KarmaAttackContact();
                    var shot = KarmaBossAttack.Spawn(game, enemy, group.transform, origin, velocity, dimensions,
                        1.65f, 9, Color.red, contact, 0.2f, "Blade", true, false, "SW05_AerialSword");
                    var picture = shot.Visual;
                    Rect core = KarmaWaveArtwork.CorePixels("SW05_AerialSword");
                    Vector2 tip = SourcePoint(picture, new Vector2(core.xMax, core.center.y));
                    Vector2 trail = SourcePoint(picture, new Vector2(core.xMin, core.center.y));
                    Vector2 upper = SourcePoint(picture, new Vector2(core.center.x, core.yMax));
                    Vector2 lower = SourcePoint(picture, new Vector2(core.center.x, core.yMin));
                    Check(picture.sprite == KarmaArtwork.Sprite("ExtraWaveVFX", "SW05_AerialSword")
                        && !picture.flipX && !picture.flipY && picture.drawMode == SpriteDrawMode.Simple,
                        "SW05 uses its complete supplied PNG without reflection or sliced clipping at " + degrees + " degrees / facing " + facing);
                    Check(Vector2.Dot((tip - trail).normalized, heading) > 0.9999f,
                        "SW05 authored sharp right tip follows actual velocity in every cardinal and diagonal direction");
                    Check(Vector2.Distance(SourcePoint(picture, core.center), origin) < 0.0001f
                        && Near(Vector2.Distance(tip, trail), dimensions.x) && Near(Vector2.Distance(upper, lower), dimensions.y),
                        "SW05 source core remains centered on and exactly matches the actual damage dimensions");
                    Check(picture.sharedMaterial == material && picture.color == Color.white && picture.sortingOrder == 26,
                        "released SW05 PNG uses its blue-white material at full opacity above its warning");
                    Check(Get<Vector2>(shot, "size") == dimensions && Get<Vector2>(shot, "velocity") == velocity
                        && Vector2.Dot(Get<Vector2>(shot, "axis"), heading) > 0.9999f && Get<bool>(shot, "oriented")
                        && Near(Get<float>(shot, "remaining"), 1.65f) && Near(Get<float>(shot, "damage"), 9)
                        && Near(Get<float>(shot, "stun"), 0.2f) && Get<KarmaAttackContact>(shot, "contact") == contact,
                        "SW05 visual-only release preserves damage, velocity, lifetime, oriented geometry and contact ledger");
                    Check(!shot.transform.IsChildOf(art) && shot.transform.localScale == Vector3.one
                        && shot.transform.rotation == Quaternion.identity,
                        "SW05 independent root cannot inherit the boss facing reflection or a picture-only damage scale");
                    Check(group.GetComponentsInChildren<SpriteRenderer>(true).Length == 1
                        && group.GetComponentsInChildren<KarmaCombatVFX>(true).Length == 0
                        && group.GetComponentsInChildren<LineRenderer>(true).Length == 0
                        && group.GetComponentsInChildren<Collider2D>(true).Length == 0
                        && group.GetComponentsInChildren<KarmaAttackWarning>(true).Length == 0,
                        "SW05 release creates only its projectile PNG without launch copies, glow, afterimages, outlines or debug colliders");
                    shot.gameObject.SetActive(false);
                    Check(!picture.gameObject.activeInHierarchy, "SW05 PNG disappears in the same frame as its cancelled damage object");
                }
        }
        finally { art.localScale = originalScale; }
        foreach (var pattern in new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04 })
        {
            bool planted = pattern == EnemyAttack.SW03;
            var shot = KarmaBossAttack.Spawn(game, enemy, root, new Vector2(20, 8), planted ? Vector2.zero : Vector2.right * 32,
                planted ? new Vector2(4.5f, 13.5f) : new Vector2(3.24f, 1.35f), 1.65f, 9, Color.white,
                null, 0, null, true, true, KarmaWaveArtwork.Pose(pattern, planted));
            bool singleSlash = pattern == EnemyAttack.SW02;
            Check(shot.Visual.sharedMaterial != material
                && (shot.Visual.GetComponent<KarmaCombatVFX>() != null) == !singleSlash
                && (shot.Visual.transform.Find("Attack radiance") != null) == !singleSlash,
                pattern + " retains its scoped material and the requested combat layers");
            shot.gameObject.SetActive(false);
        }
    }
    static void AimedDirectionChecks(Transform root, KarmaGame game, KarmaEnemy enemy)
    {
        var art = Get<Transform>(enemy, "art");
        Vector3 originalScale = art.localScale;
        try
        {
            foreach (int facing in new[] { -1, 1 })
                foreach (float degrees in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
                {
                    art.localScale = new Vector3(Mathf.Abs(originalScale.x) * facing, originalScale.y, originalScale.z);
                    Vector2 heading = degrees == 0 ? Vector2.right : degrees == 180 ? Vector2.left
                        : (Vector2)(Quaternion.Euler(0, 0, degrees) * Vector2.right);
                    Vector2 origin = new Vector2(20, 8);
                    Vector2 dimensions = (Vector2)typeof(KarmaEnemy).GetMethod("SwordSize", Private)
                        .Invoke(enemy, new object[] { EnemyAttack.SW02 });
                    var shot = KarmaBossAttack.Spawn(game, enemy, root, origin, heading * 40, dimensions,
                        1.65f, 9, Color.white, null, 0, "AimedWave", true, true, "SW02_AimedSlash");
                    var picture = shot.Visual;
                    Rect core = KarmaWaveArtwork.CorePixels("SW02_AimedSlash");
                    // The source's cutting edge is on the left; its long residual glow is on the right.
                    Vector2 cuttingEdge = SourcePoint(picture, new Vector2(core.xMin, core.center.y));
                    Vector2 trailingGlow = SourcePoint(picture, new Vector2(core.xMax, core.center.y));
                    Check(picture.sprite == KarmaArtwork.Sprite("ExtraWaveVFX", "SW02_AimedSlash")
                        && picture.flipX == (degrees != 180) && !picture.flipY,
                        "SW02 authored edge uses the direction's sprite flip at " + degrees + " degrees / boss facing " + facing);
                    if (degrees == 0 || degrees == 180)
                        Check(Quaternion.Angle(picture.transform.rotation, Quaternion.identity) < 0.001f,
                            "horizontal SW02 keeps an upright crescent without rotating the image");
                    Check(Vector2.Dot((cuttingEdge - trailingGlow).normalized, heading) > 0.9999f,
                        "SW02 cutting edge follows velocity while residual glow faces the launch origin");
                    Check(Vector2.Distance(SourcePoint(picture, core.center), origin) < 0.0001f,
                        "mirroring about the core pivot does not shift the attack center");
                    Check(Get<Vector2>(shot, "axis") == heading && Get<Vector2>(shot, "velocity") == heading * 40,
                        "image mirroring leaves the swept damage axis and velocity unchanged");
                    Check(!shot.transform.IsChildOf(art) && shot.transform.localScale == Vector3.one,
                        "boss visual reflection cannot become a second projectile reflection");
                    Check(shot.GetComponentsInChildren<LineRenderer>(true).Length == 0,
                        "SW02 has no released outline in any direction");
                    Check(picture.GetComponent<KarmaCombatVFX>() == null
                        && picture.transform.Find("Attack radiance") == null
                        && shot.GetComponentsInChildren<SpriteRenderer>().Length == 1,
                        "SW02 is one stable crescent with no attached radiance or emitted residual copies");
                    KarmaWaveArtwork.Apply(picture, "SW01_StaggeredWave", dimensions, heading);
                    Check(!picture.flipX, "applying another supplied wave resets the SW02-specific reflection");
                    shot.gameObject.SetActive(false);
                }
        }
        finally { art.localScale = originalScale; }
    }
}
#endif
