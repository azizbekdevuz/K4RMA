#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using KarmaPrototype;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class KarmaSwordBasicArtChecks
{
    static readonly string[] Forms = { "Wave", "WaveRise", "WaveWard", "WaveFinal" };
    static readonly float[] BodyPpu = { 153.65f, 150.75f, 138.94f, 138.43f };
    static readonly float[] FootX = { 230, 255, 228, 255 };
    static readonly float[] FootY = { 425, 425, 419, 404 };
    // Pixel anchors are measured on the approved forward idle, not its old canvas.
    static readonly Vector2[] IdleFeet = { new(514,1148), new(754,1167), new(718,1265), new(884,1155) };
    static readonly Vector2[] IdleHeel = { new(425,726), new(631,724), new(527,779), new(610,708) };
    static readonly Vector2[] IdleTip = { new(1115,940), new(1293,923), new(1167,956), new(1271,920) };
    static readonly Vector2[][] Sockets = {
        new[] { new Vector2(288,75), new(256,40), new(279,40), new(328,158), new(323,225), new(286,251) },
        new[] { new Vector2(311,88), new(309,43), new(312,64), new(378,160), new(349,230), new(256,247) },
        new[] { new Vector2(268,94), new(277,40), new(279,34), new(332,156), new(297,252), new(238,266) },
        new[] { new Vector2(320,86), new(319,31), new(325,20), new(398,163), new(369,246), new(283,265) }
    };
    static readonly float[][] Angles = {
        new[] { 88f, 150f, 122f, 10f, -42f, -10f },
        new[] { 90f, 143f, 137f, 0f, -42f, -25f },
        new[] { 97f, 146f, 124f, 0f, -48f, -22f },
        new[] { 58f, 144f, 121f, 4f, -33f, -24f }
    };
    static readonly Rect[] BladeRects = {
        new Rect(1356,212,394,20), new(1384,207,355,19), new(1353,213,394,23), new(1353,213,399,23)
    };

    // Run in the live Editor before the targeted Play Mode check. All writes
    // stay in SwordWave-owned paths; existing references and their GUIDs stay intact.
    public static void Import()
    {
        var factory = new SpriteDataProviderFactories(); factory.Init();
        string resources = "Assets/Arts/SwordWave/Resources/SwordWave/Basic";
        Directory.CreateDirectory(resources);
        AssetDatabase.Refresh();
        for (int form = 0; form < Forms.Length; form++)
        {
            string root = "Assets/Arts/SwordWave/" + Forms[form] + "/";
            string idlePath = root + "Idle.png", motionPath = root + "BasicDowncut.png";
            var reference = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/Reference/" + Forms[form] + "_Idle.png");
            var idleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(idlePath);
            var idleImporter = (TextureImporter)AssetImporter.GetAtPath(idlePath);
            Configure(idleImporter);
            idleImporter.spriteImportMode = SpriteImportMode.Single;
            var idleSettings = new TextureImporterSettings(); idleImporter.ReadTextureSettings(idleSettings);
            idleSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            idleSettings.spritePivot = new Vector2(IdleFeet[form].x / idleTexture.width,
                1 - IdleFeet[form].y / idleTexture.height);
            idleSettings.spritePixelsPerUnit = reference.pixelsPerUnit * idleTexture.height / reference.rect.height;
            idleImporter.SetTextureSettings(idleSettings); idleImporter.SaveAndReimport();

            var importer = (TextureImporter)AssetImporter.GetAtPath(motionPath);
            Configure(importer); importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = BodyPpu[form];
            var data = factory.GetSpriteEditorDataProviderFromObject(importer); data.InitSpriteEditorDataProvider();
            var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                throw new InvalidOperationException("Cannot slice " + motionPath);
            var old = data.GetSpriteRects().ToDictionary(r => r.name);
            var rects = new SpriteRect[7];
            for (int i = 0; i < rects.Length; i++)
            {
                string name = i < 6 ? "BasicDowncut_" + i.ToString("00") : "BasicBlade";
                int x = Mathf.RoundToInt((i % 4) * 1774f / 4);
                int top = Mathf.RoundToInt((i / 4) * 887f / 2);
                int w = Mathf.RoundToInt((i % 4 + 1) * 1774f / 4) - x;
                int h = Mathf.RoundToInt((i / 4 + 1) * 887f / 2) - top;
                rects[i] = new SpriteRect { name = name,
                    spriteID = old.TryGetValue(name, out var previous) ? previous.spriteID : GUID.Generate(),
                    rect = i < 6 ? new Rect(x, 887 - top - h, w, h) : BladeRects[form],
                    alignment = SpriteAlignment.Custom,
                    pivot = i < 6 ? new Vector2(FootX[form] / w, (h - FootY[form]) / h) : new Vector2(0,0.5f) };
            }
            data.SetSpriteRects(rects);
            data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            data.Apply(); importer.SaveAndReimport();
            string path = resources + "/" + Forms[form] + ".asset";
            var binding = AssetDatabase.LoadAssetAtPath<KarmaSwordBasicArt>(path);
            if (binding == null) { binding = ScriptableObject.CreateInstance<KarmaSwordBasicArt>(); AssetDatabase.CreateAsset(binding,path); }
            var sprites = AssetDatabase.LoadAllAssetsAtPath(motionPath).OfType<Sprite>().ToArray();
            binding.idle = AssetDatabase.LoadAssetAtPath<Sprite>(idlePath);
            binding.poses = sprites.Where(s => s.name.StartsWith("BasicDowncut_")).OrderBy(s => s.name).ToArray();
            binding.blade = sprites.Single(s => s.name == "BasicBlade");
            binding.sockets = Sockets[form]; binding.angles = Angles[form];
            binding.bladeLength = Vector2.Distance(IdleHeel[form], IdleTip[form]) / binding.idle.pixelsPerUnit;
            if (binding.poses.Length != 6) throw new InvalidOperationException("Incomplete basic sequence " + Forms[form]);
            EditorUtility.SetDirty(binding);
        }
        AssetDatabase.SaveAssets();
    }
    public static void IdleBladePoints(int form, SpriteRenderer picture, out Vector3 heel, out Vector3 tip)
    {
        var sprite = picture.sprite;
        Vector3 Local(Vector2 p) => (new Vector2(p.x, sprite.rect.height - p.y) - sprite.pivot) / sprite.pixelsPerUnit;
        heel = picture.transform.TransformPoint(Local(IdleHeel[form]));
        tip = picture.transform.TransformPoint(Local(IdleTip[form]));
    }

    // SW01/SW02 share the approved Idle-reference action, but never replace
    // the basic sheets or Idle bindings. Reuse their existing rigid steel.
    public static void ImportCasts()
    {
        var factory = new SpriteDataProviderFactories(); factory.Init();
        float[] ppu = { 120.4f, 126.5f, 136.5f, 128.3f };
        float[] footX = { 201, 235, 206, 230 };
        float[][] footY = { new[] {439f,403f}, new[] {417f,415f}, new[] {411f,410f}, new[] {401f,403f} };
        Vector2[][] sockets = {
            new[] {new Vector2(185,322),new(259,219),new(245,143),new(272,220),new(282,213),new(256,272),new(232,273),new(185,285)},
            new[] {new Vector2(214,291),new(301,173),new(264,105),new(308,177),new(313,210),new(294,266),new(261,268),new(218,286)},
            new[] {new Vector2(249,278),new(300,126),new(216,60),new(288,126),new(301,179),new(273,262),new(257,264),new(252,277)},
            new[] {new Vector2(257,259),new(326,128),new(272,71),new(335,125),new(325,187),new(315,254),new(291,256),new(257,259)}
        };
        float[][] angles = {
            new[] {-18f,67f,145f,60f,0f,-32f,-18f,-18f},
            new[] {-18f,60f,145f,60f,0f,-32f,-18f,-18f},
            new[] {-18f,60f,145f,60f,0f,-25f,-18f,-18f},
            new[] {-18f,60f,145f,60f,0f,-20f,-18f,-18f}
        };
        string resources = "Assets/Arts/SwordWave/Resources/SwordWave/Cast";
        Directory.CreateDirectory(resources);
        AssetDatabase.Refresh();
        for (int form = 1; form < Forms.Length; form++)
        {
            string path = "Assets/Arts/SwordWave/" + Forms[form] + "/CastDowncut.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Configure(importer); importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu[form];
            var data = factory.GetSpriteEditorDataProviderFromObject(importer); data.InitSpriteEditorDataProvider();
            var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                throw new InvalidOperationException("Cannot slice " + path);
            var old = data.GetSpriteRects().ToDictionary(r => r.name);
            var rects = new SpriteRect[8];
            for (int i = 0; i < rects.Length; i++)
            {
                int x = Mathf.RoundToInt(i % 4 * width / 4f);
                int top = Mathf.RoundToInt(i / 4 * height / 2f);
                int w = Mathf.RoundToInt((i % 4 + 1) * width / 4f) - x;
                int h = Mathf.RoundToInt((i / 4 + 1) * height / 2f) - top;
                string name = "CastDowncut_" + i.ToString("00");
                rects[i] = new SpriteRect { name = name,
                    spriteID = old.TryGetValue(name, out var previous) ? previous.spriteID : GUID.Generate(),
                    rect = new Rect(x, height - top - h, w, h), alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(footX[form] / w, (h - footY[form][i / 4]) / h) };
            }
            data.SetSpriteRects(rects);
            data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            data.Apply(); importer.SaveAndReimport();
            var basic = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/" + Forms[form]);
            string bindingPath = resources + "/" + Forms[form] + ".asset";
            var binding = AssetDatabase.LoadAssetAtPath<KarmaSwordBasicArt>(bindingPath);
            if (binding == null) { binding = ScriptableObject.CreateInstance<KarmaSwordBasicArt>(); AssetDatabase.CreateAsset(binding, bindingPath); }
            binding.idle = basic.idle; binding.blade = basic.blade; binding.bladeLength = basic.bladeLength;
            binding.poses = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            binding.sockets = sockets[form]; binding.angles = angles[form];
            if (binding.poses.Length != 8) throw new InvalidOperationException("Incomplete cast sequence " + Forms[form]);
            EditorUtility.SetDirty(binding);
        }
        ImportCastWindups();
    }
    // Only the three anticipation drawings change. Release/recovery, steel,
    // gameplay durations and the approved basic/Idle art remain the same.
    public static void ImportCastWindups()
    {
        var factory = new SpriteDataProviderFactories(); factory.Init();
        float[] ppu = { 210.7f, 210.5f, 205.25f, 198.9f };
        float[][] footX = { new[] {338f,340.5f,308.5f}, new[] {344f,349.5f,344f},
            new[] {338.5f,340.5f,309.5f}, new[] {343f,338.5f,342.5f} };
        Vector2[][] sockets = {
            new[] {new Vector2(241,177),new(211,243),new(294,158)},
            new[] {new Vector2(238,170),new(189,270),new(294,160)},
            new[] {new Vector2(225,165),new(172,257),new(278,148)},
            new[] {new Vector2(239,190),new(219,257),new(312,151)}
        };
        float[][] angles = { new[] {145f,196f,123f}, new[] {148f,204f,125f},
            new[] {151f,204f,112f}, new[] {151f,204f,122f} };
        for (int form = 1; form < Forms.Length; form++)
        {
            string path = "Assets/Arts/SwordWave/" + Forms[form] + "/CastWindup.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Configure(importer); importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu[form];
            var data = factory.GetSpriteEditorDataProviderFromObject(importer); data.InitSpriteEditorDataProvider();
            var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                throw new InvalidOperationException("Cannot slice " + path);
            var old = data.GetSpriteRects().ToDictionary(r => r.name);
            var rects = new SpriteRect[3];
            for (int i = 0; i < rects.Length; i++)
            {
                int x = Mathf.RoundToInt(i * width / 3f);
                int w = Mathf.RoundToInt((i + 1) * width / 3f) - x;
                string name = "CastDowncut_" + (i + 1).ToString("00");
                rects[i] = new SpriteRect { name = name,
                    spriteID = old.TryGetValue(name, out var previous) ? previous.spriteID : GUID.Generate(),
                    rect = new Rect(x, 0, w, height), alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(footX[form][i] / w, (height - 839f) / height) };
            }
            data.SetSpriteRects(rects);
            data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            data.Apply(); importer.SaveAndReimport();
            var binding = AssetDatabase.LoadAssetAtPath<KarmaSwordBasicArt>(
                "Assets/Arts/SwordWave/Resources/SwordWave/Cast/" + Forms[form] + ".asset");
            if (binding == null || binding.poses.Length != 8)
                throw new InvalidOperationException("Missing cast sequence " + Forms[form]);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (sprites.Length != 3) throw new InvalidOperationException("Incomplete windup " + Forms[form]);
            for (int i = 0; i < 3; i++)
            {
                binding.poses[i + 1] = sprites[i];
                binding.sockets[i + 1] = sockets[form][i];
                binding.angles[i + 1] = angles[form][i];
            }
            EditorUtility.SetDirty(binding);
        }
        AssetDatabase.SaveAssets();
        ImportWaveDowncut();
    }

    public static void ImportWaveDowncut()
    {
        const string path = "Assets/Arts/SwordWave/Wave/SW02Downcut.png";
        const int width = 440, height = 506;
        var basic = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/Wave");
        var cast = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/Wave");
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.GetSourceTextureWidthAndHeight(out int sheetWidth, out int sheetHeight);
        if (sheetWidth != 4 * width || sheetHeight != 4 * height || basic == null || cast == null)
            throw new InvalidOperationException("Missing basic Wave downcut sheet or binding");
        Configure(importer);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 150; // The source limbs retain their standing Idle dimensions.
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.userData = "WaveInheritedWood";
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var data = factory.GetSpriteEditorDataProviderFromObject(importer);
        data.InitSpriteEditorDataProvider();
        var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
            throw new InvalidOperationException("Cannot slice " + path);
        var old = data.GetSpriteRects().ToDictionary(r => r.name);
        string[] keys = { "01", "01b", "02", "03", "03b", "03c", "04", "04b", "05", "05b", "05c", "06", "07", "07b", "08", "09" };
        var rects = new SpriteRect[keys.Length];
        for (int i = 0; i < rects.Length; i++)
        {
            string name = "SW02Downcut_" + keys[i];
            rects[i] = new SpriteRect { name = name,
                spriteID = old.TryGetValue(name, out var previous) ? previous.spriteID : GUID.Generate(),
                rect = new Rect(i % 4 * width, (3 - i / 4) * height, width, height),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(156f / width, 17f / height) };
        }
        data.SetSpriteRects(rects);
        data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        data.Apply();
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        if (sprites.Length != rects.Length) throw new InvalidOperationException("Incomplete Wave SW02 downcut");
        cast.poses = sprites;
        cast.idle = basic.idle;
        cast.blade = basic.blade;
        cast.bladeLength = basic.bladeLength;
        // Blade heels in pixels from each cell's upper left, just inside the brass guard.
        cast.sockets = new Vector2[] {
            new(241.64f,289.14f), new(237.37f,229.9f), new(219.23f,224.56f), new(178.09f,160.3f),
            new(163.25f,125.97f), new(58.35f,51.54f), new(54.88f,90.26f), new(92.58f,45.78f),
            new(224.24f,130.25f), new(255.1f,172.06f), new(308.08f,290.42f), new(315.47f,326.2f),
            new(322.83f,346.72f), new(307.4f,325.89f), new(259.65f,339f), new(215.46f,321.86f) };
        cast.angles = new[] { -38f, 13f, 65f, 94f, 129f, 151f, 200f, 147f, 121f, 83f, 32f, -23f, -29f, -22f, -27f, -8f };
        EditorUtility.SetDirty(cast);
        AssetDatabase.SaveAssetIfDirty(cast);
    }

    public static void ImportGroundReverse()
    {
        var factory = new SpriteDataProviderFactories(); factory.Init();
        float[] ppu = { 167.5f, 166.8f, 170.2f, 164.3f };
        float[][] feetX = { new[] {281.5f,266.5f,233,259,251.5f,225.5f}, new[] {303.5f,298.5f,285.5f,288,286,292.5f},
            new[] {259f,260.5f,260,253.5f,254.5f,259.5f}, new[] {282.5f,272,240,271.5f,272,240} };
        float[][] feetY = { new[] {477f,477,477,463,463,463}, new[] {483f,483,483,485,485,484},
            new[] {495f,495,495,494,494,494}, new[] {468f,468,468,469,468,468} };
        Vector2[][] sockets = {
            new[] {new Vector2(297,235),new(338,145),new(126,39),new(386,164),new(403,202),new(368,182)},
            new[] {new Vector2(331,234),new(373,166),new(211,53),new(414,181),new(435,214),new(425,200)},
            new[] {new Vector2(285,223),new(319,168),new(142,65),new(355,174),new(388,226),new(375,210)},
            new[] {new Vector2(259,218),new(266,174),new(167,58),new(378.095f,192.420f),new(401,221),new(384.568f,212.338f)}
        };
        float[][] angles = { new[] {-70f,-65,-165,-85,-90,-80}, new[] {-75f,-65,-145,-85,-90,-80},
            new[] {-80f,-70,-135,-85,-90,-80}, new[] {-105f,-100,-145,-85,-90,-80} };
        string resources = "Assets/Arts/SwordWave/Resources/SwordWave/Ground";
        Directory.CreateDirectory(resources); AssetDatabase.Refresh();
        for (int form = 0; form < Forms.Length; form++)
        {
            string path = "Assets/Arts/SwordWave/" + Forms[form] + "/GroundReverse.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer); importer.spriteImportMode = SpriteImportMode.Multiple; importer.spritePixelsPerUnit = ppu[form];
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != 1536 || height != 1024) throw new InvalidOperationException("Unexpected ground sheet size " + path);
            var data = factory.GetSpriteEditorDataProviderFromObject(importer); data.InitSpriteEditorDataProvider();
            var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                throw new InvalidOperationException("Cannot slice " + path);
            var old = data.GetSpriteRects().ToDictionary(r => r.name); var rects = new SpriteRect[6];
            for (int i = 0; i < 6; i++)
            {
                string name = "GroundReverse_" + i.ToString("00");
                rects[i] = new SpriteRect { name = name,
                    spriteID = old.TryGetValue(name, out var previous) ? previous.spriteID : GUID.Generate(),
                    rect = new Rect(i % 3 * 512, (1 - i / 3) * 512, 512, 512), alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(feetX[form][i] / 512, (512 - feetY[form][i]) / 512) };
            }
            data.SetSpriteRects(rects);
            data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            data.Apply(); importer.SaveAndReimport();
            string bindingPath = resources + "/" + Forms[form] + ".asset";
            var binding = AssetDatabase.LoadAssetAtPath<KarmaSwordBasicArt>(bindingPath);
            if (binding == null) { binding = ScriptableObject.CreateInstance<KarmaSwordBasicArt>(); AssetDatabase.CreateAsset(binding, bindingPath); }
            var basic = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/" + Forms[form]);
            binding.idle = basic.idle; binding.blade = basic.blade; binding.bladeLength = basic.bladeLength;
            binding.poses = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            binding.sockets = sockets[form]; binding.angles = angles[form];
            if (Forms[form] != "WaveFinal") AppendGroundLift(binding, form, factory);
            EditorUtility.SetDirty(binding);
        }
        BindWaveRisePlunge();
        AssetDatabase.SaveAssets();
    }

    public static void BindWaveRisePlunge()
    {
        const string sheet = "Assets/Arts/SwordWave/WaveRise/GroundPlunge.png";
        const int width = 447, height = 391;
        const float ppu = 120f; // Standing sole to shoulder crest matches WaveRise Idle.
        var importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
        importer.GetSourceTextureWidthAndHeight(out int w, out int h);
        if (w != 3 * width || h != 3 * height) throw new InvalidOperationException("Unexpected plunge sheet size");
        Configure(importer);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var data = factory.GetSpriteEditorDataProviderFromObject(importer);
        data.InitSpriteEditorDataProvider();
        var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
            throw new InvalidOperationException("Cannot slice " + sheet);
        var previous = data.GetSpriteRects().ToDictionary(r => r.name);
        var basic = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/WaveRise");
        float[] toes = {374,368,373,374,373,378,372,370,379};
        float[] soles = {361,361,361,360,360,360,357,357,357};
        // Keep the planted front toe at the offset it has from the idle pivot.
        float toeOffset = (1137f - basic.idle.pivot.x) / basic.idle.pixelsPerUnit * ppu;
        var rects = new SpriteRect[9];
        for (int i = 0; i < rects.Length; i++)
        {
            string name = "GroundPlunge_" + i.ToString("00");
            rects[i] = new SpriteRect {
                name = name, spriteID = previous.TryGetValue(name, out var old) ? old.spriteID : GUID.Generate(),
                rect = new Rect(i % 3 * width, (2 - i / 3) * height, width, height),
                alignment = SpriteAlignment.Custom, pivot = new Vector2((toes[i] - toeOffset) / width, 1 - soles[i] / height)
            };
        }
        data.SetSpriteRects(rects);
        data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        data.Apply();
        importer.SaveAndReimport();

        var frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (frames.Length != 9) throw new InvalidOperationException("Incomplete WaveRise plunge");
        var binding = Resources.Load<KarmaSwordBasicArt>("SwordWave/Ground/WaveRise");
        // Runtime slots are setup, grip, apex, descent, contact, recovery, then the three lifts.
        int[] order = {0,1,5,6,7,8,2,3,4};
        binding.poses = order.Select(i => frames[i]).ToArray();
        binding.idle = basic.idle;
        binding.blade = basic.blade;
        binding.bladeLength = basic.bladeLength;
        binding.sockets = new Vector2[] {
            new(191,236), new(222,206), new(193,58), new(345,154),
            new(364.5f, soles[7] - basic.bladeLength * ppu), new(366,156),
            new(254,159), new(185,112), new(198,79)
        };
        binding.angles = new float[] {-147,-140,-141,-80,-90,-77,-105,-128,-136};
        EditorUtility.SetDirty(binding);
        AssetDatabase.SaveAssetIfDirty(binding);

        var final = Resources.Load<KarmaSwordBasicArt>("SwordWave/Ground/WaveFinal");
        var finalBasic = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/WaveFinal");
        var shield = AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/SwordWave/WaveFinal/GroundShieldInside.png")
            .OfType<Sprite>().ToDictionary(s => s.name);
        final.poses = binding.poses;
        final.sockets = binding.sockets;
        final.angles = binding.angles;
        final.idle = finalBasic.idle;
        final.blade = finalBasic.blade;
        final.bladeLength = finalBasic.bladeLength;
        final.shieldInside = shield["ShieldInside"];
        final.shieldStrap = shield["ForearmStrap"];
        EditorUtility.SetDirty(final);
        AssetDatabase.SaveAssetIfDirty(final);
    }
    static void AppendGroundLift(KarmaSwordBasicArt binding, int form, SpriteDataProviderFactories factory)
    {
        // Coordinates use a 512-square cell; retain the original high resolution
        // and scale PPU uniformly so the new grip frames match the planted body.
        float[] ppu = {170f,166.8f,170.2f,172.6f};
        Vector2[][] feet = {
            new[] {new Vector2(258.83f,483.71f),new(259.18f,483.71f),new(259.18f,483.71f)},
            new[] {new Vector2(290.3f,485.83f),new(290.3f,485.83f),new(290.3f,485.83f)},
            new[] {new Vector2(260.2f,495),new(260.2f,495),new(260.2f,495)},
            new[] {new Vector2(300.91f,497.15f),new(301.26f,497.15f),new(301.26f,497.15f)}
        };
        Vector2[][] sockets = {
            new[] {new Vector2(318,110),new(220,65),new(164,56)},
            new[] {new Vector2(354,115),new(264,66),new(226,58)},
            new[] {new Vector2(310,147),new(245,113),new(185,71)},
            new[] {new Vector2(268,110),new(240,67),new(235,62)}
        };
        float[][] angles = {new[] {-90f,-125,-145},new[] {-90f,-125,-140},
            new[] {-90f,-90,-125},new[] {-90f,-125,-140}};
        string path = "Assets/Arts/SwordWave/" + Forms[form] + "/GroundLift.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Configure(importer); importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        if (width != height * 3) throw new InvalidOperationException("Unexpected lift sheet size " + path);
        float pixelScale = height / 512f;
        importer.spritePixelsPerUnit = ppu[form] * pixelScale; importer.maxTextureSize = 4096;
        var data = factory.GetSpriteEditorDataProviderFromObject(importer); data.InitSpriteEditorDataProvider();
        var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
            throw new InvalidOperationException("Cannot slice " + path);
        var old = data.GetSpriteRects().ToDictionary(r => r.name); var rects = new SpriteRect[3];
        for (int i = 0; i < 3; i++)
        {
            string name = "GroundLift_" + i.ToString("00");
            rects[i] = new SpriteRect {name = name,
                spriteID = old.TryGetValue(name, out var previous) ? previous.spriteID : GUID.Generate(),
                rect = new Rect(i * height, 0, height, height), alignment = SpriteAlignment.Custom,
                pivot = new Vector2(feet[form][i].x / 512, 1 - feet[form][i].y / 512)};
        }
        data.SetSpriteRects(rects);
        data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        data.Apply(); importer.SaveAndReimport();
        binding.poses = binding.poses.Concat(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name)).ToArray();
        binding.sockets = binding.sockets.Concat(sockets[form].Select(p => p * pixelScale)).ToArray();
        binding.angles = binding.angles.Concat(angles[form]).ToArray();
    }
    static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteGenerateFallbackPhysicsShape = false; settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
#endif
