using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    // Rebuildable native sprite clips; no Animator controller or animation-driven gameplay.
    [InitializeOnLoad]
    public static partial class KarmaPlayerAnimationAssets
    {
        const string Clips = "Assets/Arts/Resources/Player/Animation";
        static KarmaPlayerAnimationAssets()
        {
            EditorApplication.delayCall += () => {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/Transition.anim") == null)
                    Rebuild();
            };
        }
        public static void Rebuild() => KarmaPlayer12AnimationAssets.Rebuild();

        public static void ImportLocomotion(string sourceFolder)
        {
            var names = new[] { "Run", "Jump", "Land", "Crouch" };
            foreach (string name in names)
            {
                string path = "Assets/Arts/Resources/" + KarmaPlayerTimeline.Sheets + name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                string input = Path.Combine(sourceFolder, name + ".png");
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(input)) ||
                        texture.width != width || texture.height != height)
                        throw new InvalidOperationException(input + ": expected existing atlas layout " + width + "x" + height);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            foreach (string name in names)
            {
                string path = "Assets/Arts/Resources/" + KarmaPlayerTimeline.Sheets + name + ".png";
                File.Copy(Path.Combine(sourceFolder, name + ".png"), path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            Rebuild();
        }

        public static void Validate()
        {
            foreach (var folder in new[] { Clips, Clips + "12" })
            {
                var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(folder + "/Frames.asset");
                foreach (var entry in library.sheets)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" + entry.id + ".anim");
                    var actual = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                    var expected = KarmaPlayerTimeline.Find(entry.id).Load();
                    if (clip.frameRate != KarmaBootstrap.FramesPerSecond || actual == null ||
                        !actual.Select(k => k.value as Sprite).SequenceEqual(expected) || !entry.sprites.SequenceEqual(expected))
                        throw new InvalidOperationException("Player motion binding: " + entry.id);
                }
            }
        }
    }
}
