using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace K4RMA.EditorTools
{
    public static class TempleArenaPass
    {
        const string ScenePath = "Assets/_Project/Scenes/PrototypeArena.unity";
        const string Rg = "Assets/Assets/Stylized Asia RG/Prefabs/";
        const string Jp = "Assets/_Project/Art/Environment/JapaneseProps/";

        public static void ApplyBatch()
        {
            try
            {
                Apply();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("K4RMA/Dress Temple Arena")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var arena = GameObject.Find("Arena");
            if (arena == null)
                throw new System.InvalidOperationException("Arena is missing.");

            var bounds = arena.GetComponent<ArenaBounds>() ?? arena.AddComponent<ArenaBounds>();
            var so = new SerializedObject(bounds);
            so.FindProperty("left").floatValue = -8.4f;
            so.FindProperty("right").floatValue = 8.4f;
            so.ApplyModifiedPropertiesWithoutUndo();

            HideRenderer("Floor");
            HideRenderer("Stripe");
            HideRenderer("BackWall");
            HideRenderer("PillarA");
            HideRenderer("PillarB");
            HideRenderer("LeftWall");
            HideRenderer("RightWall");
            var dressing = arena.transform.Find("Dressing");
            if (dressing != null)
                dressing.gameObject.SetActive(false);

            MoveWall("LeftWall", -8.7f);
            MoveWall("RightWall", 8.7f);

            var player = GameObject.Find("Player");
            var camera = Object.FindAnyObjectByType<SideViewCamera>();
            if (player != null)
                SetRef(player.GetComponent<PlayerController>(), "arenaBounds", bounds);
            if (camera != null)
                SetRef(camera, "arenaBounds", bounds);

            EnsureAnchor(player != null ? player.transform : null, "CurrentPlayerVisual");
            foreach (var boss in Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                EnsureAnchor(boss.transform, "CurrentGuardianVisual");

            BuildTemple(arena.transform);
            DressAltar();
            BuildHud();
            SilenceCopy();
            LightAndFog();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Temple arena dressing saved.");
        }

        static void BuildTemple(Transform arena)
        {
            var old = arena.Find("TempleSet");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("TempleSet").transform;
            root.SetParent(arena, false);

            Place(root, Rg + "Dojo_1 Variant.prefab", "Dojo", new Vector3(0f, 0f, 10f), 7f, 0f);
            Place(root, Rg + "Pagoda Variant.prefab", "Pagoda", new Vector3(-6f, 0f, 18f), 9f, 20f);
            Place(root, Rg + "Building_1 Variant.prefab", "HallLeft", new Vector3(-14f, 0f, 12f), 6f, 15f);
            Place(root, Rg + "Building_3 Variant.prefab", "HallRight", new Vector3(14f, 0f, 12f), 6f, -15f);
            Place(root, Rg + "Gate_1 Variant.prefab", "GateLeft", new Vector3(-6.5f, 0f, 4.2f), 4.2f, 0f);
            Place(root, Rg + "Gate_1 Variant.prefab", "GateRight", new Vector3(6.5f, 0f, 4.2f), 4.2f, 0f);
            Place(root, Rg + "GardenWall_3 Variant.prefab", "WallBackL", new Vector3(-8f, 0f, 7f), 2.4f, 0f);
            Place(root, Rg + "GardenWall_3 Variant.prefab", "WallBackR", new Vector3(8f, 0f, 7f), 2.4f, 0f);
            Place(root, Rg + "Lampion_1 Variant.prefab", "LanternL", new Vector3(-4.2f, 0f, -1.6f), 1.6f, 0f);
            Place(root, Rg + "Lampion_2 Variant.prefab", "LanternR", new Vector3(4.6f, 0f, -1.6f), 1.6f, 0f);
            Place(root, Rg + "Rock_2 Variant.prefab", "RockL", new Vector3(-7.2f, 0f, -2.1f), 1.1f, 25f);
            Place(root, Rg + "Rock_1 Variant.prefab", "RockR", new Vector3(7.4f, 0f, -2f), 0.9f, -10f);
            Place(root, Rg + "Fence 1_1 Variant.prefab", "FenceL", new Vector3(-5f, 0f, 2.4f), 1.5f, 0f);
            Place(root, Rg + "Bush_1 Variant.prefab", "Bush", new Vector3(3.2f, 0f, 2.2f), 1.2f, 0f);

            Place(root, Jp + "Torii Gate/ToriiGate.fbx", "Torii", new Vector3(0f, 0f, 8f), 6.2f, 0f);
            Place(root, Jp + "Arch/Arch.fbx", "Arch", new Vector3(0f, 0f, 5.6f), 3.6f, 0f);
            Place(root, Jp + "Shoji Wall/Shoji.fbx", "ShojiL", new Vector3(-3.4f, 0f, 2.6f), 2.2f, 0f);
            Place(root, Jp + "Shoji Wall/Shoji.fbx", "ShojiR", new Vector3(3.4f, 0f, 2.6f), 2.2f, 0f);
            Place(root, Jp + "Red Wood Wall/RedWood.fbx", "RedL", new Vector3(-11f, 0f, 5f), 2.6f, 0f);
            Place(root, Jp + "Red Wood Wall/RedWood.fbx", "RedR", new Vector3(11f, 0f, 5f), 2.6f, 0f);
            for (int i = -4; i <= 4; i++)
                Place(root, Jp + "Wood Floor/Wood.fbx", "Lane" + i, new Vector3(i * 2.05f, 0.02f, 0.15f), 0.35f, 0f);
            Place(root, Jp + "Tile Floor/Tiles.fbx", "AltarTiles", new Vector3(7.2f, 0.03f, 0.2f), 0.55f, 0f);
        }

        static void Place(Transform parent, string path, string name, Vector3 position, float height, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("Missing dressing asset: " + path);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
            FitHeight(instance, height);
        }

        static void FitHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || targetHeight <= 0f)
                return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.01f)
                return;
            go.transform.localScale *= targetHeight / bounds.size.y;
        }

        static void DressAltar()
        {
            var altar = GameObject.Find("Altar");
            if (altar == null)
                return;
            var renderer = altar.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
            var presence = altar.GetComponent<AltarPresence>() ?? altar.AddComponent<AltarPresence>();
            var light = altar.GetComponentInChildren<Light>();
            SetRef(presence, "altar", altar.GetComponent<Altar>());
            SetRef(presence, "glow", light);
            if (altar.transform.Find("Rune") == null)
            {
                var rune = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(rune.GetComponent<Collider>());
                rune.name = "Rune";
                rune.transform.SetParent(altar.transform, false);
                rune.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                rune.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var material = new Material(shader);
                    material.SetColor("_BaseColor", new Color(0.9f, 0.78f, 0.45f));
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(1.2f, 0.7f, 0.2f));
                    var path = "Assets/_Project/Art/Environment/AsiaTemple/AltarRune.mat";
                    EnsureFolder("Assets/_Project/Art/Environment/AsiaTemple");
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (existing == null)
                        AssetDatabase.CreateAsset(material, path);
                    rune.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                }
            }

            Place(altar.transform, Rg + "Podest_1 Variant.prefab", "AltarBase", altar.transform.position, 1.1f, 0f);
        }

        static void BuildHud()
        {
            var hud = GameObject.Find("HUD");
            if (hud == null)
                return;
            var presenter = hud.GetComponent<HudPresenter>();
            var guide = hud.GetComponentInChildren<AltarGuide>(true);
            if (guide != null)
            {
                var marker = guide.transform.Find("Marker");
                if (marker != null)
                    marker.gameObject.SetActive(false);
            }

            var sprite = LoadPromptSprite();
            var white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Materials/White.png");
            var glyph = hud.transform.Find("InteractGlyph");
            Image glyphImage;
            if (glyph == null)
            {
                var go = new GameObject("InteractGlyph", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(hud.transform, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 36f);
                rect.sizeDelta = new Vector2(72f, 72f);
                glyphImage = go.GetComponent<Image>();
            }
            else
            {
                glyphImage = glyph.GetComponent<Image>();
            }

            glyphImage.sprite = sprite != null ? sprite : white;
            glyphImage.enabled = false;
            SetRef(presenter, "interactGlyph", glyphImage);

            var slots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var existing = hud.transform.Find("AbilitySlot" + i);
                Image image;
                if (existing == null)
                {
                    var go = new GameObject("AbilitySlot" + i, typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(hud.transform, false);
                    var rect = go.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(0f, 0f);
                    rect.pivot = new Vector2(0f, 0f);
                    rect.anchoredPosition = new Vector2(28f + i * 54f, 28f);
                    rect.sizeDelta = new Vector2(42f, 42f);
                    image = go.GetComponent<Image>();
                    image.sprite = white;
                }
                else
                {
                    image = existing.GetComponent<Image>();
                }

                slots[i] = image;
            }

            var serialized = new SerializedObject(presenter);
            var array = serialized.FindProperty("abilitySlots");
            array.arraySize = 3;
            for (int i = 0; i < 3; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var endingBody = hud.transform.Find("Ending/Body");
            if (endingBody != null)
            {
                var text = endingBody.GetComponent<Text>();
                if (text != null)
                    text.text = string.Empty;
            }
        }

        static Sprite LoadPromptSprite()
        {
            const string path = "Assets/_Project/Art/UI/InputPrompts/keyboard_e_outline.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void SilenceCopy()
        {
            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text == null || string.IsNullOrEmpty(text.text))
                    continue;
                if (text.text.Contains("surrendered a power") || text.text.Contains("learned to counter"))
                    text.text = string.Empty;
            }
        }

        static void LightAndFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.45f, 0.38f, 0.32f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.48f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.28f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.09f);

            var key = GameObject.Find("Key Light");
            if (key != null)
            {
                var light = key.GetComponent<Light>();
                light.color = new Color(1f, 0.78f, 0.52f);
                light.intensity = 1.35f;
                key.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            }

            var fill = GameObject.Find("Fill Light");
            if (fill != null)
            {
                var light = fill.GetComponent<Light>();
                light.color = new Color(0.45f, 0.62f, 1f);
                light.intensity = 0.45f;
            }

            if (Object.FindAnyObjectByType<Volume>() == null)
            {
                EnsureFolder("Assets/_Project/Art/Environment/AsiaTemple");
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, "Assets/_Project/Art/Environment/AsiaTemple/TempleVolume.asset");
                var bloom = profile.Add<Bloom>();
                bloom.active = true;
                bloom.intensity.Override(0.4f);
                bloom.threshold.Override(0.9f);
                var vignette = profile.Add<Vignette>();
                vignette.active = true;
                vignette.intensity.Override(0.16f);
                var color = profile.Add<ColorAdjustments>();
                color.active = true;
                color.postExposure.Override(0.12f);
                color.contrast.Override(10f);
                EditorUtility.SetDirty(profile);

                var volumeObject = new GameObject("Temple Volume");
                var volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }
        }

        static void EnsureAnchor(Transform root, string currentName)
        {
            if (root == null)
                return;
            var visual = root.Find("Visual");
            if (visual == null)
                return;
            var anchor = root.Find("VisualAnchor");
            if (anchor == null)
            {
                var anchorObject = new GameObject("VisualAnchor");
                anchorObject.transform.SetParent(root, false);
                anchor = anchorObject.transform;
            }

            var current = anchor.Find(currentName);
            if (current == null)
            {
                var currentObject = new GameObject(currentName);
                currentObject.transform.SetParent(anchor, false);
                current = currentObject.transform;
            }

            if (visual.parent != current)
                visual.SetParent(current, true);
        }

        static void HideRenderer(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        static void MoveWall(string name, float x)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return;
            var position = go.transform.localPosition;
            position.x = x;
            go.transform.localPosition = position;
        }

        static void SetRef(Object target, string property, Object value)
        {
            if (target == null)
                return;
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(property);
            if (prop == null)
                return;
            prop.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
