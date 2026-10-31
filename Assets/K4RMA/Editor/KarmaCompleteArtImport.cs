#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace KarmaPrototype.Editor
{
    // Index current assets only. Deleted images and retired source packs are never recreated.
    [InitializeOnLoad]
    public sealed class KarmaCompleteArtImport : IPreprocessBuildWithReport
    {
        static KarmaCompleteArtImport() { EditorApplication.delayCall += Ensure; }
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) { Validate(); }
        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Sync(); } catch (Exception error) { Debug.LogException(error); }
        }
        public static void Sync()
        {
            var entries = KarmaImageCatalog.Current.images;
            const string path = "Assets/K4RMA/Editor/CompleteArtInventory.asset";
            var inventory = AssetDatabase.LoadAssetAtPath<KarmaArtInventory>(path);
            if (inventory == null) { inventory = ScriptableObject.CreateInstance<KarmaArtInventory>(); AssetDatabase.CreateAsset(inventory, path); }
            inventory.images = new KarmaArtInventory.Entry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                inventory.images[i] = new KarmaArtInventory.Entry { path = entry.path, usage = entry.usage,
                    image = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.path) };
            }
            EditorUtility.SetDirty(inventory); AssetDatabase.SaveAssets();
        }
        public static void Validate()
        {
            Sync();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int poses = 0;
            foreach (var entry in KarmaImageCatalog.Current.images)
            {
                if (!ids.Add(entry.id)) throw new InvalidOperationException("이미지 ID 중복: " + entry.id);
                if (!File.Exists(entry.path) || Resources.Load<Texture2D>(entry.resource) == null)
                    throw new InvalidOperationException("현재 게임 이미지 연결 누락: " + entry.path);
                if (entry.poses == null || entry.poses.Length == 0) continue;
                var sprites = Resources.LoadAll<Sprite>(entry.resource);
                if (sprites.Length != entry.poses.Length) throw new InvalidOperationException("포즈 수 불일치: " + entry.id);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var sprite in sprites)
                {
                    if (sprite.vertices.Length < 3 || sprite.triangles.Length < 3) throw new InvalidOperationException("렌더 메시 누락: " + entry.id);
                    names.Add(sprite.name);
                }
                foreach (var name in entry.poses)
                    if (!names.Contains(name)) throw new InvalidOperationException("포즈 연결 누락: " + entry.id + "/" + name);
                poses += sprites.Length;
            }
            File.WriteAllText("complete-art-validation.txt", "PASS: " + ids.Count + " current-game images, " + poses + " named poses; no legacy source copies required.\n");
        }
    }
}
#endif
