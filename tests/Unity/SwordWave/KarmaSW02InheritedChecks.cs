#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Focused phase3 check: inherited SW02 only. Reuses the existing real-cast
// observer, fixed-step fixture and sprite metadata format; no boss regression.
[InitializeOnLoad]
public static class KarmaSW02InheritedChecks
{
    const string Folder = "output/validation/sw02-animation-phase3-20261105";
    const string Key = "K4RMA.SW02Inherited";
    const string Library = "Assets/Data/Resources/BossArtwork/KarmaSwordBossSprites.asset";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Forms = { "WaveRise", "WaveWard", "WaveFinal" };
    static readonly string[] Names = { "Prepare01", "Prepare02", "Swing01", "Swing02", "Recover01", "Recover02" };
    static bool started;
    static KarmaGame game;
    static int form, side;
    static readonly HashSet<string> captured = new HashSet<string>();
    static readonly List<string> results = new List<string>();
    static Type Observer => typeof(KarmaSW02AnimationChecks);
    static object Get(object target, string name)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F); if (field != null) return field.GetValue(target); }
        throw new MissingFieldException(name);
    }
    static KarmaEnemy Enemy => (KarmaEnemy)Observer.GetField("enemy", F).GetValue(null);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static KarmaSW02InheritedChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunImportBatch()
    {
        try
        {
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Library);
            var imports = new List<string>();
            foreach (string name in Forms)
            {
                string sheet = "Assets/Arts/Resources/SwordWave/SW02/" + name + "_SW02_Transitions.png";
                string group = name + "SW02";
                var frames = JsonUtility.FromJson<KarmaSW02SpriteImportChecks.Frames>(File.ReadAllText(Folder + "/" + name + "-import.json")).frames;
                AssetDatabase.ImportAsset(sheet, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
                // Match the authored form's ready body height without adding a
                // runtime scale policy or changing any original Sprite.
                var idle = KarmaArtwork.InheritedPoses(name).Single(s => s.name == "Idle");
                var readyOutline = frames[5].outlines.SelectMany(p => p.points).ToArray();
                float readyHeight = readyOutline.Max(p => p.y) - readyOutline.Min(p => p.y);
                importer.spritePixelsPerUnit = readyHeight / idle.bounds.size.y;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.Tight; importer.SetTextureSettings(settings);
                var factories = new SpriteDataProviderFactories(); factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
                var rects = frames.Select(f => new SpriteRect { name = f.name, rect = new Rect(f.x, f.y, 512, 512),
                    alignment = SpriteAlignment.Custom, pivot = new Vector2(f.pivotX, f.pivotY),
                    spriteID = existing.TryGetValue(f.name, out var id) ? id : GUID.Generate() }).ToArray();
                provider.SetSpriteRects(rects);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                var outline = provider.GetDataProvider<ISpriteOutlineDataProvider>();
                for (int i = 0; i < rects.Length; i++) outline.SetOutlines(rects[i].spriteID, frames[i].outlines.Select(p => p.points).ToList());
                provider.Apply(); importer.SaveAndReimport();
                var sprites = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToArray();
                Check(sprites.Length == 6, name + " six sprites");
                var sheets = library.sheets.Where(s => s.id != group).ToList();
                sheets.Add(new KarmaSpriteLibrary.Sheet { id = group, source = sheet, poses = Names,
                    sprites = Names.Select(n => sprites.Single(s => s.name == n)).ToArray() });
                library.sheets = sheets.ToArray(); EditorUtility.SetDirty(library);
                imports.Add("PASS: " + name + " six sprites; PPU=" + importer.spritePixelsPerUnit + "; ready height=" + idle.bounds.size.y);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Folder + "/import-result.txt", imports);
            if (SessionState.GetBool(Key + ".PlayAfterImport", false)) RunBatch();
            else EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void RunImportAndCheck()
    {
        SessionState.SetBool(Key + ".PlayAfterImport", true);
        RunImportBatch();
    }
    static void CaptureFrame()
    {
        if (Enemy == null) return;
        var animator = (KarmaCharacterAnimator)Get(Enemy, "animator");
        var picture = (SpriteRenderer)Get(animator, "picture");
        if (!(bool)Get(animator, "<SW02UsesIntermediateFrame>k__BackingField") || picture.sprite.texture.name != Forms[form - 1] + "_SW02_Transitions")
        {
            if (side > 0 && picture.sprite.name == "Idle" && picture.sprite.texture.name == Forms[form - 1] && captured.Add("OriginalIdle"))
                CapturePicture(picture, "OriginalIdle");
            return;
        }
        string name = picture.sprite.name;
        if (!Names.Contains(name) || (side < 0 && name != "Swing02") || !captured.Add(name)) return;
        CapturePicture(picture, name);
    }
    static void CapturePicture(SpriteRenderer picture, string name)
    {
        var camera = game.GameCamera;
        var position = camera.transform.position; float size = camera.orthographicSize, aspect = camera.aspect;
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(768, 768, 24); var image = new Texture2D(768, 768, TextureFormat.RGBA32, false);
        try
        {
            camera.transform.position = new Vector3(Enemy.Position.x, 5, -10); camera.orthographicSize = 9; camera.aspect = 1;
            camera.targetTexture = target;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); image.Apply();
            File.WriteAllBytes(Folder + "/renders/" + Forms[form - 1] + "-" + side + "-" + name + ".png", image.EncodeToPNG());
            Vector3 pivot = picture.transform.TransformPoint(Vector3.zero);
            results.Add("RENDER: " + Forms[form - 1] + "/" + side + "/" + name + " footPivot=" + pivot + " bounds=" + picture.bounds);
        }
        finally
        {
            camera.targetTexture = previous; camera.transform.position = position; camera.orthographicSize = size; camera.aspect = aspect;
            RenderTexture.active = active; UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target);
        }
    }
    static async void Run()
    {
        int exit = 0;
        var data = new KarmaSW02AnimationChecks.Data();
        Directory.CreateDirectory(Folder + "/renders");
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60; game.GameCamera.aspect = 1600f / 700;
            Observer.GetField("game", F).SetValue(null, game);
            var old = JsonUtility.FromJson<KarmaSW02AnimationChecks.Data>(File.ReadAllText(Folder + "/phase2-baseline.json"));
            for (form = 1; form <= 3; form++) foreach (int direction in new[] { 1, -1 })
            {
                side = direction; captured.Clear();
                var probe = new GameObject("inherited SW02 rendered frames").AddComponent<KarmaSwordDashFrameProbe>();
                probe.After = CaptureFrame;
                KarmaSW02AnimationChecks.Cast cast;
                try { cast = await (Task<KarmaSW02AnimationChecks.Cast>)Observer.GetMethod("Observe", F).Invoke(null, new object[] { form, side }); }
                finally { probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); }
                var before = old.casts.Single(c => c.key == cast.key);
                Check(cast.releaseTick == before.releaseTick && cast.origin == before.origin && cast.velocity == before.velocity && cast.hitSize == before.hitSize,
                    cast.key + " unchanged release tick and projectile geometry");
                Check(cast.frames.Count == before.frames.Count, cast.key + " unchanged duration");
                for (int i = 0; i < cast.frames.Count; i++)
                    Check(Vector3.Distance(cast.frames[i].center, before.frames[i].center) < 0.0001f && Vector3.Distance(cast.frames[i].size, before.frames[i].size) < 0.0001f,
                        cast.key + " unchanged incoming DamageBounds at frame " + i);
                foreach (string name in Names) Check(cast.frames.Any(f => f.sprite == name), cast.key + " played " + name);
                Check(Names.Count(n => captured.Contains(n)) == (side > 0 ? 6 : 1), cast.key + " actual renders captured");
                results.Add("PASS: " + Forms[form - 1] + "/" + side + " all six frames; release=" + cast.releaseTick + "; unchanged projectile and per-frame incoming bounds");
                data.casts.Add(cast);
            }
            results.Insert(0, "PASS: inherited SW02 only, three forms x two directions; 18 authored poses rendered");
        }
        catch (Exception e) { results.Insert(0, "FAIL: " + e); Debug.LogException(e); exit = 1; }
        File.WriteAllText(Folder + "/updated.json", JsonUtility.ToJson(data, true));
        File.WriteAllLines(Folder + "/result.txt", results);
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(exit);
    }
}
#endif
