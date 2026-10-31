#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Uses the real artwork factory without replacing the active fight or its inheritance.
// A second factory-created guardian with only the new cutout disabled is the independent
// pre-cutout damage-bounds control. No production Sprite or texture is modified.
public static class KarmaWaveGuardianAppearanceChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception("Wave guardian appearance: " + message); }
    static FieldInfo Field(object target, string name)
        => target.GetType().GetField(name, Private);
    static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    static T Property<T>(object target, string name)
        => (T)target.GetType().GetProperty(name, Private).GetValue(target);
    static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void SameBounds(Bounds actual, Bounds expected, string label)
    {
        Check(Vector3.Distance(actual.center, expected.center) < 0.0002f
            && Vector3.Distance(actual.size, expected.size) < 0.0002f,
            label + " preserves the legacy incoming-hit bounds: " + actual + " vs " + expected);
    }

    sealed class Probe : IDisposable
    {
        public GameObject root;
        public KarmaEnemy enemy;
        public Transform art;
        public KarmaCharacterAnimator animator;
        public SpriteRenderer picture;
        public KarmaWaveMotion motion;
        public Vector3 home;

        public Probe(KarmaGame game, Essence[] order, bool legacy = false)
        {
            root = new GameObject(legacy ? "Legacy Wave silhouette control" : "Guardian cutout probe");
            root.transform.SetParent(game.World, false);
            root.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
            enemy = root.AddComponent<KarmaEnemy>();
            Set(enemy, "game", game);
            Set(enemy, "<Health>k__BackingField", 100f);
            Set(enemy, "<MaxHealth>k__BackingField", 100f);
            Set(enemy, "<PrimaryType>k__BackingField", KarmaPatternCatalog.Primary(order));
            Set(enemy, "direction", 1f);
            Set(enemy, "selected", EnemyAttack.SW01);
            State("Approach");
            art = KarmaArtwork.Guardian(root.transform, order, order.Length == 3);
            art.localScale = Vector3.one * KarmaSpace.GuardianArtScale;
            animator = art.GetComponent<KarmaCharacterAnimator>();
            picture = art.Find("Guardian sprite").GetComponent<SpriteRenderer>();
            motion = art.GetComponent<KarmaWaveMotion>();
            Set(enemy, "art", art); Set(enemy, "animator", animator);
            home = picture.transform.localPosition;
            if (!legacy && motion != null)
                Check(picture.sprite == KarmaArtwork.Settings.Guardian(Essence.SwordWave), "factory starts with the prototype body");
            // Restore precisely the old pose path, retaining inherited appearances,
            // logical poses and scales. The control never calls SetWaveAppearance.
            if (legacy) Set(animator, "waveAppearance", null);
            if (motion != null) Call(motion, "Start");
        }
        void FirstFrameBounds(Essence[] order)
        {
            Check(!Get<bool>(motion, "initialized"),
                "factory returns before the Wave motion Start callback");
            Sprite selectedCutout = KarmaArtwork.Settings.WaveGuardian(order);
            Sprite oldBase = KarmaArtwork.Settings.Guardian(KarmaPatternCatalog.Primary(order));
            Check(picture.sprite == selectedCutout && oldBase != null && oldBase != selectedCutout,
                "factory immediately displays the selected cutout while an independent old base is available");
            // A sibling reference cannot enter the real art subtree's renderer
            // cache and conceal an accidentally enlarged first-frame hurt box.
            var reference = new GameObject("Independent initial legacy sprite reference");
            reference.transform.SetParent(root.transform, false);
            reference.transform.localPosition = art.localPosition;
            reference.transform.localRotation = art.localRotation;
            reference.transform.localScale = art.localScale;
            var image = new GameObject("Old base sprite only");
            image.transform.SetParent(reference.transform, false);
            image.transform.localPosition = home;
            image.transform.localRotation = picture.transform.localRotation;
            image.transform.localScale = Get<Vector3>(animator, "pictureScale");
            var renderer = image.AddComponent<SpriteRenderer>();
            renderer.sprite = oldBase;
            try
            {
                SameBounds(enemy.DamageBounds, renderer.bounds,
                    "first incoming-hit query before manual Start, inheritance length " + order.Length);
                Check(picture.sprite == selectedCutout,
                    "first incoming-hit query keeps the new cutout visible");
            }
            finally { UnityEngine.Object.DestroyImmediate(reference); }
        }
        public void State(string state)
            => Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, state));
        public void ClearClocks()
        {
            Set(animator, "deathAt", -1f); Set(animator, "hurtUntil", -1f);
            Set(animator, "actionUntil", -1f); Set(animator, "poseUntil", -1f);
            Set(animator, "waveSwingAt", float.PositiveInfinity);
            Set(animator, "wavePhase", null);
            Set(animator, "wavePhaseSwingAt", float.PositiveInfinity);
            Set(animator, "waveReleasedUntil", 0f);
        }
        public void Pose(string pose)
        {
            Call(animator, "ShowPose", pose);
            picture.transform.localPosition = home;
            picture.transform.localRotation = Quaternion.identity;
        }
        public void Dispose() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
    }

    public static int Run(KarmaGame game, KarmaEnemy enemy)
    {
        assertions = 0;
        Check(game != null && enemy != null && game.IsCombat && !game.Paused,
            "checks run beside an active, unpaused real fight");
        var orders = new[] {
            new[] { Essence.SwordWave },
            new[] { Essence.SwordWave, Essence.RisingSlash },
            new[] { Essence.SwordWave, Essence.Guard },
            new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard },
            new[] { Essence.SwordWave, Essence.Guard, Essence.RisingSlash }
        };
        Vector3 livePosition = enemy.transform.position;
        Bounds liveBounds = enemy.DamageBounds;
        float liveHealth = enemy.Health;
        int liveFacing = enemy.Facing;
        for (int i = 0; i < orders.Length; i++)
        {
            Sprite sprite = KarmaArtwork.Settings.Guardian(Essence.SwordWave);
            Check(KarmaArtwork.GuardianPreview(orders[i]) == sprite,
                "preview agrees with the actual guardian for inheritance order " + i);
            using (var probe = new Probe(game, orders[i]))
            using (var legacy = new Probe(game, orders[i], true))
            {
                Check(probe.picture.sprite == sprite && !Property<bool>(probe.animator, "WaveFootAnchored")
                    && Get<Sprite>(probe.animator, "guardianAppearance") == null,
                    "every inheritance stage uses the prototype sheet without a body override");
                Check(probe.motion != null && probe.art.GetComponents<KarmaWaveMotion>().Length == 1,
                    "exactly one visual motion component owns the prototype");
                foreach (string pose in new[] { "Idle", "Move", "Windup", "Guard", "Slash", "Wave", "Rising", "FollowThrough", "Hurt", "Death" })
                {
                    probe.Pose(pose); legacy.Pose(pose);
                    Check(probe.picture.sprite == KarmaArtwork.Sprite("Wave", pose), "reuses original Wave/" + pose);
                    SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds, "prototype " + pose);
                }
            }
        }
        var cutouts = new HashSet<Sprite>();
        foreach (string name in new[] { "Wave_Base", "Wave_Rise", "Wave_Ward", "Wave_Rise_Ward" })
            cutouts.Add(Resources.Load<Sprite>("K4RMAArtwork/WaveGuardians/" + name));
        OtherGuardians(game, cutouts);
        Check(enemy.transform.position == livePosition && enemy.Health == liveHealth && enemy.Facing == liveFacing,
            "appearance probes leave the active boss combat state unchanged");
        SameBounds(enemy.DamageBounds, liveBounds, "active fight after isolated probes");
        Debug.Log("PASS: " + assertions + " prototype Wave sheet, inheritance, bounds and other guardian assertions");
        return assertions;
    }

    static Sprite ImportedCutout(string name)
    {
        string path = "Assets/K4RMA/Resources/K4RMAArtwork/WaveGuardians/" + name + ".png";
        Sprite sprite = Resources.Load<Sprite>("K4RMAArtwork/WaveGuardians/" + name);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Check(sprite != null && importer != null, name + " is imported as a Resources Sprite");
        Check(KarmaArtwork.Sprite("WaveGuardians", name) == sprite,
            name + " resolves through the existing sprite library");
        var importSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(importSettings);
        Check(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
            && importSettings.spriteMeshType == SpriteMeshType.FullRect && importer.alphaIsTransparency
            && !importer.mipmapEnabled && Mathf.Abs(sprite.pixelsPerUnit - 100) < 0.0001f,
            name + " imports as one transparent FullRect Sprite at 100 PPU without mipmaps");
        Check(AssetDatabase.GUIDToAssetPath(AssetDatabase.AssetPathToGUID(path)) == path,
            name + " has a resolving asset GUID");
        byte[] bytes = File.ReadAllBytes(path);
        Check(bytes.Length > 26 && bytes[0] == 137 && bytes[1] == 80 && bytes[25] == 6,
            name + " source PNG stores true RGBA pixels");
        var texture = new Texture2D(2, 2);
        try
        {
            Check(texture.LoadImage(bytes), name + " decodes independently of the importer");
            var pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1, clear = 0, fringe = 0;
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    Color32 pixel = pixels[y * texture.width + x];
                    if (pixel.a == 0) clear++;
                    // The generator's RGB-only previews exaggerate near-transparent
                    // chroma noise. A colored matte must never reach visible alpha.
                    bool saturated = pixel.r > 230 && pixel.g < 45 && pixel.b < 80
                        || pixel.r < 45 && pixel.g > 230 && pixel.b > 230
                        || pixel.r < 45 && pixel.g < 45 && pixel.b > 230
                        || pixel.r > 230 && pixel.g > 230 && pixel.b < 45;
                    if (saturated && pixel.a >= 16) fringe++;
                    if (pixel.a < 128) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
            Check(clear > pixels.Length / 2, name + " removes the opaque background and title rather than importing the reference card");
            Check(minX > 0 && minY > 0 && maxX < texture.width - 1 && maxY < texture.height - 1,
                name + " visible silhouette, ribbons and weapon fit inside the canvas");
            Check(fringe == 0, name + " has no visibly opaque saturated extraction fringe");
            // Unity texture pixels are bottom-origin; the lowest visible sole is minY.
            Check(Mathf.Abs(sprite.pivot.x - texture.width * 0.5f) < 0.01f
                && Mathf.Abs(sprite.pivot.y - minY) < 0.01f,
                name + " centers X and places its exact custom pivot on the alpha>=128 lowest sole");
            Debug.Log("WAVE GUARDIAN ART " + name + " size=" + texture.width + "x" + texture.height
                + " core=" + new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1)
                + " pivot=" + sprite.pivot + " transparent=" + clear + " visibleFringe=" + fringe);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        return sprite;
    }

    static void PoseAndBounds(Probe probe, Probe legacy, Sprite expected)
    {
        string[] poses = { "Idle", "Move", "Windup", "Slash", "Wave", "Guard", "Rising", "FollowThrough", "Hurt", "Death" };
        foreach (int facing in new[] { -1, 1 })
        {
            probe.art.localScale = legacy.art.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            foreach (string pose in poses)
            {
                probe.ClearClocks(); legacy.ClearClocks();
                probe.Pose(pose); legacy.Pose(pose);
                Check(probe.animator.CurrentPose == pose && probe.picture.sprite == expected,
                    expected.name + " retains its selected artwork through direct " + pose);
                SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds, expected.name + " direct " + pose + " facing " + facing);
            }
            foreach (var attack in new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 })
                foreach (string action in new[] { "Approach", "Telegraph", "Attack", "Recover", "Stunned" })
                    foreach (bool air in new[] { false, true })
                        foreach (bool armor in new[] { false, true })
                        {
                            probe.ClearClocks(); legacy.ClearClocks();
                            probe.animator.GuardianPose(action, attack, air, armor);
                            legacy.animator.GuardianPose(action, attack, air, armor);
                            Check(probe.picture.sprite == expected && probe.animator.CurrentPose == legacy.animator.CurrentPose,
                                expected.name + " keeps logical " + attack + "/" + action + " without old-sheet fallback");
                            SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds,
                                expected.name + " " + attack + "/" + action + " air=" + air + " armored=" + armor);
                        }
        }
        foreach (var attack in new[] { EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 })
            foreach (string phase in new[] { "Guard", "Windup", "Rising" })
            {
                probe.ClearClocks(); legacy.ClearClocks();
                probe.animator.PlayWavePhase(phase); legacy.animator.PlayWavePhase(phase);
                probe.animator.GuardianPose("Attack", attack, false, true);
                legacy.animator.GuardianPose("Attack", attack, false, true);
                Check(probe.picture.sprite == expected && probe.animator.CurrentPose == phase,
                    expected.name + " retains the cutout in explicit " + attack + "/" + phase);
                SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds, expected.name + " explicit phase " + phase);
            }
    }

    static int VisualFacing(Probe probe)
        => (probe.art.lossyScale.x < 0 ? -1 : 1) * (probe.picture.flipX ? -1 : 1);

    static void Locomotion(Probe probe, Probe legacy, Sprite expected)
    {
        float speed = 3;
        foreach (int fps in new[] { 15, 30, 60, 120 })
            foreach (int facing in new[] { -1, 1 })
                foreach (int travel in new[] { -1, 1 })
                {
                    string label = expected.name + " " + fps + " FPS combat=" + facing + " travel=" + travel;
                    probe.ClearClocks(); legacy.ClearClocks();
                    probe.State("Approach"); legacy.State("Approach");
                    probe.enemy.transform.position = legacy.enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
                    Set(probe.enemy, "direction", (float)facing);
                    probe.art.localScale = legacy.art.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    Call(probe.motion, "ResetVisualTransform"); Call(legacy.motion, "ResetVisualTransform");
                    Set(probe.motion, "previousPose", "");
                    probe.Pose("Move"); legacy.Pose("Move");
                    float dt = 1f / fps;
                    Vector3 combatScale = probe.art.localScale;
                    bool swayed = false;
                    for (int frame = 0; frame < fps / 2; frame++)
                    {
                        probe.enemy.transform.position += Vector3.right * (travel * speed * dt);
                        legacy.enemy.transform.position = probe.enemy.transform.position;
                        Vector3 before = probe.enemy.transform.position;
                        Bounds damageBefore = probe.enemy.DamageBounds;
                        Call(probe.motion, "AdvanceMotion", dt);
                        Check(probe.enemy.transform.position == before && probe.art.localScale == combatScale && probe.enemy.Facing == facing,
                            label + " motion changes only the display, preserving combat position/facing");
                        Check(probe.picture.sprite == expected && VisualFacing(probe) == travel,
                            label + " cutout faces its actual measured travel");
                        float authoredY = probe.art.TransformPoint(probe.home).y;
                        Check(Mathf.Abs(probe.picture.transform.TransformPoint(Vector3.zero).y - authoredY) < 0.0001f,
                            label + " keeps the zero foot pivot at the authored ground height");
                        SameBounds(probe.enemy.DamageBounds, damageBefore, label + " visible sway");
                        SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds, label + " legacy Move silhouette");
                        swayed |= Mathf.Abs(Get<Vector3>(probe.motion, "motionOffset").x) > 0.0001f;
                    }
                    Check(swayed && Get<float>(probe.motion, "gaitPhase") > 0, label + " actual travel produces restrained walking motion");
                    float stoppedGait = Get<float>(probe.motion, "gaitPhase");
                    probe.Pose("Idle");
                    for (int frame = 0; frame < fps; frame++) Call(probe.motion, "AdvanceMotion", dt);
                    Check(VisualFacing(probe) == travel && Get<float>(probe.motion, "gaitPhase") == stoppedGait
                        && Mathf.Abs(Get<float>(probe.motion, "locomotionSpeed")) < 0.00001f
                        && Mathf.Abs(Get<Vector3>(probe.motion, "motionOffset").x) < 0.00005f,
                        label + " stopping retains its last facing and smoothly settles without walking in place");
                    // A target crossing while the boss rests must not flip its body.
                    probe.art.localScale = new Vector3(-facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    Call(probe.motion, "AdvanceMotion", dt);
                    Check(VisualFacing(probe) == travel, label + " idle retains actual-travel facing when combat art turns");
                    probe.Pose("Windup"); Call(probe.motion, "AdvanceMotion", dt);
                    int preparedFacing = -facing;
                    Check(VisualFacing(probe) == preparedFacing, label + " preparation aligns with combat art once");
                    probe.art.localScale = combatScale;
                    for (int frame = 0; frame < fps / 4; frame++) Call(probe.motion, "AdvanceMotion", dt);
                    Check(VisualFacing(probe) == preparedFacing && probe.picture.sprite == expected,
                        label + " ongoing preparation retains its direction through target-side changes");
                }
    }

    static void Lifecycle(Probe probe, Probe legacy, Sprite expected)
    {
        probe.ClearClocks(); legacy.ClearClocks();
        probe.enemy.transform.position = legacy.enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        probe.art.localScale = legacy.art.localScale = Vector3.one * KarmaSpace.GuardianArtScale;
        Call(probe.motion, "ResetVisualTransform"); Call(legacy.motion, "ResetVisualTransform");
        probe.animator.Hurt(); legacy.animator.Hurt();
        probe.animator.GuardianPose("Attack", EnemyAttack.SW02, false, false);
        legacy.animator.GuardianPose("Attack", EnemyAttack.SW02, false, false);
        Check(probe.animator.CurrentPose == "Hurt" && probe.picture.sprite == expected,
            expected.name + " real hurt priority keeps the cutout");
        SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds, expected.name + " hurt");
        probe.ClearClocks(); legacy.ClearClocks();
        probe.State("Stunned"); legacy.State("Stunned");
        probe.animator.GuardianPose("Stunned", EnemyAttack.SW05, false, false);
        legacy.animator.GuardianPose("Stunned", EnemyAttack.SW05, false, false);
        Call(probe.motion, "AdvanceMotion", 0.1f);
        Check(probe.picture.sprite == expected && probe.picture.transform.localPosition == probe.home,
            expected.name + " stun retains the cutout and restores the foot anchor");
        SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds, expected.name + " stun");
        probe.animator.Die(); legacy.animator.Die();
        probe.animator.Pose(0, false); legacy.animator.Pose(0, false);
        Call(probe.motion, "AdvanceMotion", 0.1f);
        Check(probe.animator.CurrentPose == "Death" && probe.picture.sprite == expected
            && probe.picture.transform.localPosition == probe.home && probe.picture.transform.localRotation == Quaternion.identity,
            expected.name + " death retains its selected cutout without secondary motion");
        SameBounds(probe.enemy.DamageBounds, legacy.enemy.DamageBounds, expected.name + " death");
        probe.animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
        Check(probe.picture.sprite == expected, expected.name + " later combat pose calls cannot replace the dying cutout");
    }

    static void OtherGuardians(KarmaGame game, HashSet<Sprite> cutouts)
    {
        var orders = new[] {
            Array.Empty<Essence>(), new[] { Essence.RisingSlash }, new[] { Essence.Guard },
            new[] { Essence.RisingSlash, Essence.SwordWave }, new[] { Essence.Guard, Essence.SwordWave },
            new[] { Essence.RisingSlash, Essence.Guard, Essence.SwordWave },
            new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave }
        };
        foreach (var order in orders)
        {
            Check(KarmaArtwork.Settings.WaveGuardian(order) == null,
                "non-Wave primary never selects a Wave cutout at inheritance length " + order.Length);
            Check(!cutouts.Contains(KarmaArtwork.GuardianPreview(order)), "other guardian previews retain their original artwork");
            using (var probe = new Probe(game, order))
            {
                Check(probe.motion == null && !Property<bool>(probe.animator, "WaveFootAnchored"),
                    "other guardians receive neither Wave cutout motion nor a foot-anchor override");
                probe.animator.GuardianPose("Attack", order.Length > 0 && order[0] == Essence.RisingSlash ? EnemyAttack.UP01 : EnemyAttack.GD02, false, false);
                Check(!cutouts.Contains(probe.picture.sprite), "other guardian attack art stays on its existing sheet");
            }
        }
        foreach (var frame in KarmaArtwork.PlayerFrames())
            Check(frame != null && !cutouts.Contains(frame), "player animation frames retain their existing sprites");
    }
}
#endif
