using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    public static partial class KarmaPlayerAnimationAssets
    {
        // Re-import only the approved parry sheets without rebuilding unrelated player clips.
        public static void ImportParrySuccess()
        {
            ImportParrySheet(Art + "ParrySuccess.png");
            ImportParrySheet("Assets/K4RMA/Arts/Common/Resources/K4RMAArtwork/Effects/ParrySuccess.png");
            AssetDatabase.SaveAssets();
            Debug.Log("Parry success import PASS: two sheets, eight frames each.");
        }

        public static void VerifyParrySuccess()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var root = new GameObject("Parry verification");
            try
            {
                var art = KarmaArtwork.Player(root.transform);
                var animator = art.GetComponent<KarmaCharacterAnimator>();
                var body = art.GetComponentInChildren<SpriteRenderer>();
                var clock = typeof(KarmaCharacterAnimator).GetField("playerClipAt", flags);
                var deadline = typeof(KarmaCharacterAnimator).GetField("poseUntil", flags);
                animator.PlayPose("Parry", 0.3f);
                if (art.GetComponentsInChildren<SpriteRenderer>().Length != 1)
                    throw new Exception("Input-only parry incorrectly spawned success flash");
                animator.PlayPose("Riposte", 0.3f);
                float until = (float)deadline.GetValue(animator);
                foreach (int facing in new[] { 1, -1 })
                {
                    art.localScale = new Vector3(facing, 1, 1);
                    for (int i = 0; i < 8; i++)
                    {
                        clock.SetValue(animator, Time.time - (i / 24f + 0.001f));
                        animator.Pose(0, false);
                        var flash = body.transform.Find("Parry success flash").GetComponent<SpriteRenderer>();
                        if (body.sprite.name != "Frame" + i.ToString("00") || flash.sprite.name != body.sprite.name || !flash.enabled)
                            throw new Exception("Parry frame synchronization failed: " + i);
                        if (Mathf.Sign(flash.transform.position.x - body.transform.position.x) != facing)
                            throw new Exception("Parry facing failed");
                        if ((float)deadline.GetValue(animator) != until) throw new Exception("Parry deadline changed");
                    }
                }
                deadline.SetValue(animator, Time.time - 0.001f);
                animator.Pose(0, false);
                typeof(KarmaCharacterAnimator).GetMethod("LateUpdate", flags).Invoke(animator, null);
                if (body.transform.Find("Parry success flash").GetComponent<SpriteRenderer>().enabled)
                    throw new Exception("Parry flash did not end");
                animator.PlayPose("Riposte", 0.3f); animator.Hurt(); animator.Pose(0, false);
                typeof(KarmaCharacterAnimator).GetMethod("LateUpdate", flags).Invoke(animator, null);
                if (animator.ParrySuccessVisible || body.transform.Find("Parry success flash").GetComponent<SpriteRenderer>().enabled)
                    throw new Exception("Hurt did not interrupt parry presentation");
                animator.Die();
                if (animator.ParrySuccessVisible) throw new Exception("Death did not interrupt parry presentation");
                Debug.Log("Parry presentation PASS: input-only excluded, 16 facing/frame samples, unchanged deadline, expiry, hurt, death.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void ImportParrySheet(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing parry texture: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 145;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
                throw new InvalidOperationException("Parry slicing unsupported: " + path);
            var old = provider.GetSpriteRects();
            var rects = Enumerable.Range(0, 8).Select(i => new SpriteRect {
                name = "Frame" + i.ToString("00"),
                rect = new Rect(i % 4 * 384, (1 - i / 4) * 512, 384, 512),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(0.5f, 74f / 512),
                spriteID = old.FirstOrDefault(s => s.name == "Frame" + i.ToString("00"))?.spriteID ?? GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names == null) throw new InvalidOperationException("Sprite name mapping unsupported");
            names.SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length != 8) throw new InvalidOperationException("Parry frame count mismatch");
        }
    }
}
