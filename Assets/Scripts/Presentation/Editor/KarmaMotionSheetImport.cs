using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaMotionSheetImport
    {
        const string Root = "Assets/Arts/Resources/Motions12/";
        public static void Import()
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string line in File.ReadLines(Root + "Catalog.txt"))
                {
                    string[] fields = line.Split('\t');
                    string path = Root + fields[0] + "/" + fields[1] + ".png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    int columns = int.Parse(fields[2]), rows = int.Parse(fields[3]), count = int.Parse(fields[4]);
                    int w = int.Parse(fields[5]), h = int.Parse(fields[6]);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.spritePixelsPerUnit = float.Parse(fields[7], CultureInfo.InvariantCulture);
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 4096;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    var data = factory.GetSpriteEditorDataProviderFromObject(importer);
                    data.InitSpriteEditorDataProvider();
                    var capability = data.GetDataProvider<ISpriteFrameEditCapability>();
                    if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                        throw new InvalidOperationException("Cannot slice " + path);
                    var old = data.GetSpriteRects().ToDictionary(r => r.name);
                    var rects = new SpriteRect[count];
                    var pivot = new Vector2(float.Parse(fields[8], CultureInfo.InvariantCulture), float.Parse(fields[9], CultureInfo.InvariantCulture));
                    for (int i = 0; i < count; i++)
                    {
                        string key = fields[1] + "_" + i.ToString("00");
                        rects[i] = new SpriteRect { name = key, spriteID = old.TryGetValue(key, out var prior) ? prior.spriteID : GUID.Generate(),
                            rect = new Rect(i % columns * w, (rows - 1 - i / columns) * h, w, h), alignment = SpriteAlignment.Custom, pivot = pivot };
                    }
                    data.SetSpriteRects(rects);
                    var ids = data.GetDataProvider<ISpriteNameFileIdDataProvider>();
                    ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                    data.Apply();
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }
    }
}
