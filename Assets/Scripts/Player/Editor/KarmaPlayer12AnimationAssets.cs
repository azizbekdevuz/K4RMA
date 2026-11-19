using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaPlayer12AnimationAssets
    {
        const string ResourcesRoot = "Assets/Data/Resources/";
        const string Folder = ResourcesRoot + KarmaPlayerTimeline.Clips;
        public static readonly string[] Names = KarmaPlayerTimeline.Motions.Select(m => m.Name).ToArray();

        [MenuItem("K4RMA/Animation/Rebuild player clips (12 FPS)")]
        public static void Rebuild()
        {
            var ids = Names.Concat(new[] { "AirSlash", "Dash", "Transition" });
            string current = Folder.TrimEnd('/');
            UpdateLibrary(current, ids);
            string alternate = ResourcesRoot + "Player/Animation";
            var legacyNames = AssetDatabase.FindAssets("t:AnimationClip", new[] { alternate })
                .Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)));
            UpdateLibrary(alternate, legacyNames);
            AssetDatabase.SaveAssets();
            KarmaPlayerAnimationAssets.Validate();
        }

        static void UpdateLibrary(string folder, System.Collections.Generic.IEnumerable<string> ids)
        {
            var sheets = ids.Select(name => WriteClip(folder, name)).ToArray();
            string path = folder + "/Frames.asset";
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<KarmaSpriteLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }
            library.sheets = sheets;
            EditorUtility.SetDirty(library);
        }

        internal static KarmaSpriteLibrary.Sheet WriteClip(string folder, string name)
        {
            var motion = KarmaPlayerTimeline.Find(name);
            string path = KarmaMotionSheetImport.SheetPath("Player", motion.Source);
            var drawings = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            var frames = motion.Sequence(drawings);
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe {
                time = i * KarmaBootstrap.Step, value = sprite
            }).ToArray();
            string clipPath = folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool create = clip == null;
            if (create) clip = new AnimationClip { name = name };
            clip.frameRate = KarmaBootstrap.FramesPerSecond;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0;
            settings.stopTime = motion.Duration;
            settings.loopTime = motion.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (create) AssetDatabase.CreateAsset(clip, clipPath);
            else EditorUtility.SetDirty(clip);
            return new KarmaSpriteLibrary.Sheet { id = name, source = path, sprites = frames };
        }
    }
}
