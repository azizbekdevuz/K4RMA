using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using KarmaPrototype;

// Run with the Unity Pipeline `script run` command. Kept outside Assets so
// editing this import recipe does not trigger a domain reload.
public static class ImportSpriteSheets
{
    [Serializable] public sealed class Plan { public Sheet[] sheets; }
    [Serializable] public sealed class Sheet { public string id, source; public float ppu; public Slice[] sprites; }
    [Serializable] public sealed class Slice { public string name; public float x, y, width, height, pivotX, pivotY; public float[][] outline; public float[][][] outlines; }
    const string Folder = "Assets/K4RMA/Resources/K4RMAArtwork/Sheets";
    const string LibraryPath = "Assets/K4RMA/Resources/KarmaSpriteLibrary.asset";

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing sheets.");
        var plan = Newtonsoft.Json.JsonConvert.DeserializeObject<Plan>(File.ReadAllText("tools/sprite-slices.json"));
        Directory.CreateDirectory(Folder);
        foreach (var sheet in plan.sheets)
        {
            string path = Folder + "/" + sheet.id + ".png";
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(sheet.source)))
                File.Copy(sheet.source, path, true);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var entries = new System.Collections.Generic.List<KarmaSpriteLibrary.Sheet>();
        int count = 0;
        foreach (var sheet in plan.sheets)
        {
            string path = Folder + "/" + sheet.id + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = sheet.ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = sheet.sprites.Any(s => s.outline != null || s.outlines != null) ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("No sprite provider: " + path);
            provider.InitSpriteEditorDataProvider();
            var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
            var required = EEditCapability.CreateAndDeleteSprite | EEditCapability.EditSpriteName
                | EEditCapability.EditSpriteRect | EEditCapability.EditPivot;
            if (edit == null || !edit.GetEditCapability().HasCapability(required))
                throw new InvalidOperationException("Importer does not support slicing: " + path);
            var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (ids == null) throw new InvalidOperationException("No persistent sprite ID provider: " + path);
            var outlines = provider.GetDataProvider<ISpriteOutlineDataProvider>();
            if (sheet.sprites.Any(s => s.outline != null || s.outlines != null) && outlines == null)
                throw new InvalidOperationException("No render outline capability: " + path);
            var previous = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var rects = sheet.sprites.Select(s => {
                if (s.x < 0 || s.y < 0 || s.width <= 0 || s.height <= 0
                    || s.x + s.width > texture.width || s.y + s.height > texture.height
                    || s.pivotX < 0 || s.pivotX > 1 || s.pivotY < 0 || s.pivotY > 1)
                    throw new InvalidOperationException("Out of bounds slice: " + sheet.id + "/" + s.name);
                return new SpriteRect { name = s.name,
                    rect = new Rect(s.x, texture.height - s.y - s.height, s.width, s.height),
                    alignment = SpriteAlignment.Custom, pivot = new Vector2(s.pivotX, s.pivotY),
                    spriteID = previous.TryGetValue(s.name, out var id) ? id : GUID.Generate() };
            }).ToArray();
            provider.SetSpriteRects(rects);
            ids.SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            for (int i = 0; i < rects.Length; i++)
            {
                var paths = sheet.sprites[i].outlines ?? (sheet.sprites[i].outline != null ? new[] { sheet.sprites[i].outline } : null);
                if (paths != null)
                    outlines.SetOutlines(rects[i].spriteID, paths.Select(points => points.Select(p =>
                        new Vector2(p[0] - rects[i].rect.width / 2, rects[i].rect.height / 2 - p[1])).ToArray()).ToList());
            }
            provider.Apply(); importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            var sprites = sheet.sprites.Select(s => imported.TryGetValue(s.name, out var sprite) ? sprite
                : throw new InvalidOperationException("Sprite failed to import: " + sheet.id + "/" + s.name)).ToArray();
            entries.Add(new KarmaSpriteLibrary.Sheet { id = sheet.id, source = sheet.source, sprites = sprites });
            count += sprites.Length;
        }
        var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(LibraryPath);
        if (library == null) { library = ScriptableObject.CreateInstance<KarmaSpriteLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
        Undo.RecordObject(library, "Import all artwork sheets");
        library.sheets = entries.ToArray(); EditorUtility.SetDirty(library);
        var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>("Assets/K4RMA/Resources/KarmaArtworkSettings.asset");
        Undo.RecordObject(settings, "Connect artwork sheets");
        settings.spriteLibrary = library;
        var player = entries.First(s => s.id == "Player").sprites.ToDictionary(s => s.name);
        settings.playerIdle = player["Idle"]; settings.playerMove1 = player["Run"]; settings.playerMove2 = player["Jump"];
        settings.playerSlash1 = player["Slash"]; settings.playerSlash2 = player["FollowThrough"];
        settings.playerRising = player["Rising"]; settings.playerFalling = player["AirSlash"];
        settings.playerHurt = player["Hurt"]; settings.playerDeath = player["Death"];
        settings.neutralGuardian = entries.First(s => s.id == "Neutral").sprites[0];
        settings.swordGuardian = entries.First(s => s.id == "Wave").sprites[0];
        settings.risingGuardian = entries.First(s => s.id == "Rise").sprites[0];
        settings.wardGuardian = entries.First(s => s.id == "Ward").sprites[0];
        var ui = entries.First(s => s.id == "UI").sprites.ToDictionary(s => s.name);
        settings.skillIcons = new[] { ui["Wave"], ui["Rise"], ui["Ward"], ui["Pierce"], ui["Air"], ui["Parry"] };
        settings.selectionCrescent = ui["Crescent"];
        settings.hitEffect = entries.First(s => s.id == "HitVFX").sprites[0];
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        Selection.activeObject = library; EditorGUIUtility.PingObject(library);
        string message = "PASS: " + entries.Count + " sheets, " + count + " named Sprite assets; player, guardians, UI and VFX connected.";
        Debug.Log(message); return message;
    }
}
