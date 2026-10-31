#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.Editor
{
    public static class KarmaGuardHeavySetup
    {
        public static string Import()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 실행하세요.");
            AssetDatabase.Refresh();
            string[] forms = { "Ward", "WardWave", "WardRise", "WardFinal" };
            string[] names = { "Windup", "Downstroke", "FollowThrough" };
            float[] feet = { 0.045f, 0.07f, 0.08f, 0.087f };
            float[] centers = { 0.52f, 0.59f, 0.63f, 0.63f };
            for (int form = 0; form < forms.Length; form++)
            {
                string sheet = "Sheets/GD02/" + forms[form];
                var source = (TextureImporter)AssetImporter.GetAtPath("Assets/K4RMA/Resources/K4RMAArtwork/" + sheet + ".png");
                source.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
                float width = sourceWidth / 3f;
                var frames = new SpriteRect[3];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = KarmaArtworkSetup.Frame(names[i], new Rect(i * width, 0, width, sourceHeight),
                        new Vector2(centers[form], feet[form]));
                KarmaArtworkSetup.ImportSprite(sheet, 125, frames);
                foreach (string name in names) KarmaArtworkSetup.Sprite(sheet, name);
            }
            const string cue = "Assets/K4RMA/Resources/K4RMAArtwork/Sheets/GD02/ParryRed.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(cue);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 125;
            importer.spritePivot = Vector2.one * 0.5f;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center; importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            return "GD02: 4 forms / 12 poses / centered red parry cue imported";
        }
    }
}
#endif
