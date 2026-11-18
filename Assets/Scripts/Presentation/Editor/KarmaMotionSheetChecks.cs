using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaMotionSheetChecks
    {
        [MenuItem("K4RMA/Animation/Reimport and verify motion sheets")]
        public static void ReimportAndVerify()
        {
            KarmaMotionSheetImport.Import();
            KarmaMotionBindings.Apply();
            Run();
        }

        public static void Run()
        {
            KarmaPlayer12AnimationAssets.Rebuild();
            var rows = new List<string> { "form,motion,frames,source" };
            var root = new GameObject("Motion binding check");
            try
            {
                var picture = root.AddComponent<SpriteRenderer>();
                foreach (string line in Resources.Load<TextAsset>("Motions12/Catalog").text.Split(new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = line.Split('\t');
                    string path = "Motions12/" + f[0] + "/" + f[1];
                    var sprites = Resources.LoadAll<Sprite>(path).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                    if (sprites.Length != int.Parse(f[4])) throw new Exception(path + ": count");
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        picture.sprite = sprites[i];
                        if (picture.sprite.name != f[1] + "_" + i.ToString("00") ||
                            AssetDatabase.GetAssetPath(picture.sprite) != "Assets/Arts/Resources/" + path + ".png")
                            throw new Exception(path + ": incorrect frame " + i);
                    }
                    rows.Add(string.Join(",",f[0],f[1],sprites.Length,path));
                }
                foreach(var form in rows.Skip(1).Select(s => s.Split(',')[0]).Distinct()) KarmaArtwork.StageIdle(form);
                KarmaPlayerAnimationAssets.VerifyParrySuccess();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Directory.CreateDirectory("output/validation/motions12");
            File.WriteAllLines("output/validation/motions12/sheets.csv", rows);
            KarmaMotionRouteChecks.Run();
            Debug.Log("Verified " + (rows.Count-1) + " Resources sheets and both player clip libraries.");
        }
    }
}
