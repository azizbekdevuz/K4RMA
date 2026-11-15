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
        const string Art = "Assets/Arts/Resources/Player/";
        const string Clips = "Assets/Arts/Resources/Player/Animation";
        static KarmaPlayerAnimationAssets()
        {
            EditorApplication.delayCall += () => {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/Transition.anim") == null)
                    Rebuild();
            };
        }
        public static void Rebuild()
        {
            var importer = AssetImporter.GetAtPath(Art + "Locomotion.png") as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 130;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var cells = new SpriteMetaData[8];
            for (int i = 0; i < cells.Length; i++) cells[i] = new SpriteMetaData {
                name = "Motion" + i, rect = new Rect(i % 4 * 512, (1 - i / 4) * 512, 512, 512),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 42f / 512)
            };
            #pragma warning disable 618
            importer.spritesheet = cells;
            #pragma warning restore 618
            importer.SaveAndReimport();
            var extraImporter = AssetImporter.GetAtPath(Art + "Additional.png") as TextureImporter;
            if (extraImporter == null) return;
            extraImporter.textureType = TextureImporterType.Sprite;
            extraImporter.spriteImportMode = SpriteImportMode.Multiple;
            extraImporter.spritePixelsPerUnit = 130;
            extraImporter.mipmapEnabled = false; extraImporter.alphaIsTransparency = true;
            extraImporter.textureCompression = TextureImporterCompression.Uncompressed;
            extraImporter.maxTextureSize = 2048;
            var extras = new SpriteMetaData[12];
            for (int i = 0; i < extras.Length; i++) extras[i] = new SpriteMetaData {
                name = "Extra" + i, rect = new Rect(i % 4 * 512, (2 - i / 4) * 512, 512, 512),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 42f / 512)
            };
            #pragma warning disable 618
            extraImporter.spritesheet = extras;
            #pragma warning restore 618
            extraImporter.SaveAndReimport();
            var originalNames = new[] { "Idle", "Jump", "Apex", "Fall", "Slash", "FollowThrough", "Rising", "Guard", "Parry", "Hurt", "Death", "Recovery", "Pierce", "AirSlash", "Riposte" };
            var poseImporter = AssetImporter.GetAtPath(Art + "Poses.png") as TextureImporter;
            if (poseImporter == null) return;
            poseImporter.textureType = TextureImporterType.Sprite;
            poseImporter.spriteImportMode = SpriteImportMode.Multiple;
            poseImporter.spritePixelsPerUnit = 130; poseImporter.mipmapEnabled = false;
            poseImporter.alphaIsTransparency = true;
            poseImporter.textureCompression = TextureImporterCompression.Uncompressed;
            poseImporter.maxTextureSize = 2048;
            var originalCells = new SpriteMetaData[originalNames.Length];
            for (int i = 0; i < originalCells.Length; i++) originalCells[i] = new SpriteMetaData {
                name = originalNames[i], rect = new Rect(i % 4 * 512, (3 - i / 4) * 512, 512, 512),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 42f / 512)
            };
            #pragma warning disable 618
            poseImporter.spritesheet = originalCells;
            #pragma warning restore 618
            poseImporter.SaveAndReimport();
            var original = AssetDatabase.LoadAllAssetsAtPath(Art + "Poses.png").OfType<Sprite>().ToDictionary(s => s.name);
            var added = AssetDatabase.LoadAllAssetsAtPath(Art + "Locomotion.png").OfType<Sprite>().ToDictionary(s => s.name);
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(Art + "Additional.png").OfType<Sprite>()) added[sprite.name] = sprite;
            Sprite Get(string n) => added.TryGetValue(n, out var s) ? s : original[n];
            if (!AssetDatabase.IsValidFolder(Clips)) AssetDatabase.CreateFolder("Assets/Arts/Resources/Player", "Animation");
            var sequences = new System.Collections.Generic.List<KarmaSpriteLibrary.Sheet>();
            void Clip(string name, bool loop, params string[] names)
            {
                if (WriteLocomotionClip(Clips, name, out var locomotion))
                { sequences.Add(locomotion); return; }
                sequences.Add(new KarmaSpriteLibrary.Sheet { id = name, sprites = names.Select(Get).ToArray() });
                string path = Clips + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool fresh = clip == null;
                if (fresh) clip = new AnimationClip();
                clip.name = name; clip.frameRate = 12;
                var keys = new ObjectReferenceKeyframe[names.Length];
                for (int i = 0; i < names.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = Get(names[i]) };
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0; settings.stopTime = names.Length / 12f;
                settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
                if (fresh) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            }
            Clip("Idle", true, "Extra0", "Extra1", "Extra2", "Extra3", "Extra4", "Extra5");
            Clip("Run", true, "Motion0", "Motion1", "Motion2", "Motion3");
            Clip("JumpStart", false, "Jump");
            // Hold coherent airborne silhouettes: generated air poses have a different
            // foot offset, and Extra9 is a grounded stance, not a falling frame.
            Clip("Jump", false, "Jump");
            Clip("Apex", false, "Jump");
            Clip("Fall", false, "Fall");
            Clip("Land", false, "Motion7", "Motion4", "Extra0");
            // Crouch must respond on the input frame, without a wind-down sequence.
            Clip("Crouch", false, "Motion6", "Motion6", "Motion6", "Motion6");
            // The existing basic attack deals damage immediately, so its first key is contact.
            Clip("Attack", false, "Slash", "FollowThrough", "Recovery");
            Clip("Recovery", false, "Extra10", "Extra11", "Recovery");
            Clip("Rising", false, "Rising", "Riposte", "Recovery");
            Clip("Pierce", false, "Pierce", "FollowThrough", "Recovery");
            Clip("AirSlash", true, "AirSlash", "Riposte", "AirSlash");
            Clip("SwordWave", false, "FollowThrough", "Pierce", "Recovery");
            Clip("Guard", false, "Parry", "Guard", "Guard");
            Clip("Parry", false, "Parry", "Guard", "Parry");
            Clip("Riposte", false, "Riposte", "FollowThrough", "Recovery", "Idle");
            Clip("Hurt", false, "Hurt", "Recovery");
            Clip("Death", false, "Hurt", "Hurt", "Motion6", "Motion6", "Death", "Death", "Death", "Death");
            Clip("Transition", false, "Guard", "Guard", "Parry", "Parry", "Rising", "Rising", "Recovery", "Idle");
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Clips + "/Frames.asset");
            if (library == null) { library = ScriptableObject.CreateInstance<KarmaSpriteLibrary>(); AssetDatabase.CreateAsset(library, Clips + "/Frames.asset"); }
            library.sheets = sequences.ToArray(); EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("K4RMA Player: rebuilt clips with per-action sprite timing.");
        }
        static readonly string[] LocomotionPoses = { "JumpStart", "Jump", "Apex", "Fall", "Land", "Crouch" };

        public static void ImportLocomotion(string sourceFolder)
        {
            foreach (string name in new[] { "Run", "Jump", "Land", "Crouch" })
            {
                string path = Art + "Native24/" + name + ".png";
                File.Copy(Path.Combine(sourceFolder, name + ".png"), path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.GetSourceTextureWidthAndHeight(out _, out int sourceHeight);
                importer.spritePixelsPerUnit = name == "Run" && sourceHeight == 1425 ? 118 : 130;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = name == "Run" ? 2048 : 1024; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" }) importer.ClearPlatformTextureSettings(platform);
                importer.SaveAndReimport();
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                if (provider == null) throw new InvalidOperationException(path + ": sprite provider missing");
                provider.InitSpriteEditorDataProvider();
                var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
                if (capability == null) throw new InvalidOperationException(path + ": edit capability missing");
                foreach (var edit in new[] { EEditCapability.CreateAndDeleteSprite, EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect, EEditCapability.EditPivot })
                    if (!capability.GetEditCapability().HasCapability(edit)) throw new InvalidOperationException(path + ": unsupported edit " + edit);
                int count = name == "Run" ? (sourceHeight == 1425 ? 13 : 6) : name == "Jump" ? 9 : name == "Land" ? 2 : 3;
                int columns = name == "Land" ? 2 : 3, rows = (count + columns - 1) / columns;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture.width != columns * 205 || texture.height != rows * 285)
                    throw new InvalidOperationException(path + ": expected a 205x285 cell sheet");
                var old = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
                var rects = Enumerable.Range(0, count).Select(i => {
                    string id = name + "_" + i.ToString("00");
                    return new SpriteRect { name = id, rect = new Rect(i % columns * 205, (rows - 1 - i / columns) * 285, 205, 285),
                        alignment = SpriteAlignment.Custom, pivot = new Vector2(0.526829f, 0.035088f),
                        spriteID = old.TryGetValue(id, out var guid) ? guid : GUID.Generate() };
                }).ToArray();
                provider.SetSpriteRects(rects);
                var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (names == null) throw new InvalidOperationException(path + ": sprite name/ID provider missing");
                names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                provider.Apply(); importer.SaveAndReimport();
            }
            foreach (string folder in new[] { Clips, Clips + "24" })
            {
                var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(folder + "/Frames.asset");
                foreach (string name in LocomotionPoses)
                {
                    if (!WriteLocomotionClip(folder, name, out var replacement)) throw new InvalidOperationException("Missing locomotion: " + name);
                    var entry = library.sheets.Single(s => s.id == name);
                    entry.sprites = replacement.sprites; entry.source = replacement.source;
                }
                EditorUtility.SetDirty(library);
            }
            AssetDatabase.SaveAssets();
        }

        static bool WriteLocomotionClip(string folder, string name, out KarmaSpriteLibrary.Sheet entry)
        {
            entry = null;
            if (!LocomotionPoses.Contains(name)) return false;
            string sheet = name == "JumpStart" || name == "Apex" || name == "Fall" ? "Jump" : name;
            string source = Art + "Native24/" + sheet + ".png";
            var all = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            int start = name == "Fall" ? 5 : name == "Apex" ? 4 : 0;
            int count = name == "Run" || name == "Attack" ? all.Length : name == "Jump" || name == "Fall" ? 4 : name == "Land" ? 2 : name == "Crouch" ? 3 : 1;
            if (count == 0 || all.Length < start + count) return false;
            var sprites = all.Skip(start).Take(count).ToArray();
            entry = new KarmaSpriteLibrary.Sheet { id = name, source = source, sprites = sprites };
            int[] holds = name == "Run" && count == 6 ? Enumerable.Repeat(2, count).ToArray()
                : name == "Jump" ? new[] { 3, 3, 2, 2 } : name == "Fall" ? new[] { 2, 2, 2, 3 }
                : name == "JumpStart" ? new[] { 3 } : name == "Apex" ? new[] { 2 }
                : Enumerable.Repeat(1, count).ToArray();
            var keys = new System.Collections.Generic.List<ObjectReferenceKeyframe>(); int tick = 0;
            for (int i = 0; i < count; i++) for (int hold = 0; hold < holds[i]; hold++, tick++)
                keys.Add(new ObjectReferenceKeyframe { time = name == "Land" ? i * .05f : tick / 24f, value = sprites[i] });
            string path = folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); bool fresh = clip == null;
            if (fresh) clip = new AnimationClip(); clip.name = name; clip.frameRate = 24;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.startTime = 0; clipSettings.stopTime = name == "Land" ? .10f : tick / 24f;
            clipSettings.loopTime = name == "Run"; AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            if (fresh) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            return true;
        }

        public static void Validate()
        {
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Clips + "/Frames.asset");
            var probe = new GameObject("Player clip validation", typeof(SpriteRenderer));
            probe.hideFlags = HideFlags.HideAndDontSave;
            int checks = 0;
            try
            {
                var renderer = probe.GetComponent<SpriteRenderer>();
                var scale = new Vector3(2, 2, 2);
                probe.transform.localScale = scale;
                foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Clips }))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (LocomotionPoses.Contains(clip.name))
                    {
                        if (clip.frameRate != 24) throw new InvalidOperationException(clip.name + ": sample rate");
                        var sequence = library.All(clip.name);
                        var curve = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                        if (curve == null || curve.Any(k => k.value == null || !sequence.Contains(k.value as Sprite)))
                            throw new InvalidOperationException(clip.name + ": locomotion sprite references");
                        checks += curve.Length;
                        continue;
                    }
                    if (clip.frameRate != 12) throw new InvalidOperationException(clip.name + ": sample rate");
                    var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (keys == null || keys.Length == 0) throw new InvalidOperationException(clip.name + ": empty clip");
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i].value == null) throw new InvalidOperationException(clip.name + ": missing sprite");
                        if (Mathf.Abs(keys[i].time - i / clip.frameRate) > 0.00001f) throw new InvalidOperationException(clip.name + ": key timing");
                        renderer.sprite = library.All(clip.name)[i];
                        if (renderer.sprite != keys[i].value || probe.transform.localScale != scale)
                            throw new InvalidOperationException(clip.name + ": sprite=" + (renderer.sprite == null ? "null" : renderer.sprite.name) + " expected=" + keys[i].value.name + " scale=" + probe.transform.localScale);
                        checks++;
                    }
                }
                // Exercise the runtime selector as well as serialized references.
                var animator = probe.AddComponent<KarmaCharacterAnimator>();
                animator.ConfigureSprites(renderer, new[] { library.All("Idle")[0] });
                animator.ConfigureSheet("Player");
                var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                void Elapse(float seconds) => typeof(KarmaCharacterAnimator).GetField("playerClipAt", fields).SetValue(animator, Time.time - seconds);
                void Expect(string pose, string sprite)
                {
                    if (animator.CurrentPose != pose || renderer.sprite.name != sprite || probe.transform.localScale != scale)
                        throw new InvalidOperationException("Player locomotion: expected " + pose + "/" + sprite);
                    checks++;
                }
                animator.Pose(0, false, 0, true);
                Expect("Crouch", "Crouch_00");
                Elapse(0.1f); animator.Pose(0, false, 0, true);
                Expect("Crouch", "Crouch_02");
                Elapse(1); animator.Pose(0, false, 0, true);
                Expect("Crouch", "Crouch_02");
                animator.Pose(0, true, 5);
                Expect("Jump", "Jump_00");
                typeof(KarmaCharacterAnimator).GetField("jumpVisualUntil", fields).SetValue(animator, -1f);
                animator.Pose(0, true, 5); Elapse(1); animator.Pose(0, true, 5);
                Expect("Jump", "Jump_03");
                animator.Pose(0, true, 0);
                Expect("Apex", "Jump_04");
                animator.Pose(0, true, -5); Elapse(1); animator.Pose(0, true, -5);
                Expect("Fall", "Jump_08");
                animator.Pose(0, false);
                Expect("Land", "Land_00");
                Elapse(.06f); animator.Pose(1, false);
                Expect("Land", "Land_01");
                typeof(KarmaCharacterAnimator).GetField("landedUntil", fields).SetValue(animator, -1f);
                animator.Pose(1, false);
                Expect("Run", "Motion0");
                Debug.Log("K4RMA Player locomotion selector PASS: crouch hold, jump phases and moving landing.");
                Debug.Log("K4RMA Player clip/library consistency PASS: " + checks + " keys; per-clip FPS, sprite references, fixed transform scale.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

    }
}
