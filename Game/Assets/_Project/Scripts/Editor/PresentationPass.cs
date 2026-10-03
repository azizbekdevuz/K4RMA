using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace K4RMA.EditorTools
{
    public static class PresentationPass
    {
        const string ScenePath = "Assets/_Project/Scenes/PrototypeArena.unity";

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

        [MenuItem("K4RMA/Apply Presentation Pass")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cloth = Material("Cloth", new Color(0.16f, 0.24f, 0.48f), null);
            var skin = Material("Skin", new Color(0.73f, 0.58f, 0.46f), null);
            var steel = Material("Steel", new Color(0.78f, 0.82f, 0.86f), new Color(0.15f, 0.18f, 0.2f));
            var stone = Material("Stone", new Color(0.34f, 0.31f, 0.29f), null);
            var stoneDark = Material("StoneDark", new Color(0.22f, 0.2f, 0.19f), null);
            var coreAmber = Material("CoreAmber", new Color(1f, 0.55f, 0.2f), new Color(1.1f, 0.4f, 0.05f));
            var coreCold = Material("CoreCold", new Color(0.45f, 0.9f, 1f), new Color(0.2f, 1.4f, 1.8f));
            var gold = Material("AltarGold", new Color(0.86f, 0.64f, 0.24f), new Color(0.7f, 0.35f, 0.05f));
            var deflectMat = Material("Deflect", new Color(0.55f, 0.9f, 1f), new Color(0.4f, 1.2f, 1.5f));

            Tint("Assets/_Project/Materials/Floor.mat", new Color(0.18f, 0.19f, 0.22f), null);
            Tint("Assets/_Project/Materials/Stripe.mat", new Color(0.42f, 0.3f, 0.16f), null);
            Tint("Assets/_Project/Materials/Wall.mat", new Color(0.11f, 0.12f, 0.15f), null);
            Tint("Assets/_Project/Materials/Pillar.mat", new Color(0.3f, 0.28f, 0.26f), null);
            Tint("Assets/_Project/Materials/Altar.mat", new Color(0.9f, 0.7f, 0.28f), new Color(0.8f, 0.4f, 0.08f));

            var player = GameObject.Find("Player");
            if (player == null)
                throw new System.InvalidOperationException("Player is missing from PrototypeArena.");
            var playerVisual = player.transform.Find("Visual");
            BuildDisciple(player, playerVisual, cloth, skin, steel, deflectMat);

            foreach (var boss in Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool risen = boss.gameObject.name.Contains("2");
                BuildGuardian(boss, risen ? stoneDark : stone, risen ? coreCold : coreAmber, risen);
            }

            DressArena(gold);
            DressAltar(gold);
            var shell = BuildShell(player);
            var director = Object.FindAnyObjectByType<RunDirector>();
            if (director != null && shell.Sequence != null)
                SetRef(director, "sacrificeSequence", shell.Sequence);

            var follow = Object.FindAnyObjectByType<SideViewCamera>();
            if (follow != null)
                SetVector(follow, "worldOffset", new Vector3(0.6f, 2.3f, -13.2f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Presentation pass saved PrototypeArena.");
        }

        struct ShellRefs
        {
            public SacrificeSequence Sequence;
        }

        static void BuildDisciple(GameObject player, Transform visual, Material cloth, Material skin, Material steel, Material deflect)
        {
            if (visual == null)
                return;
            Clear(visual);
            var torso = Block("Torso", visual, cloth, new Vector3(0f, 0.16f, 0f), new Vector3(0.42f, 0.52f, 0.26f));
            Block("Head", visual, skin, new Vector3(0f, 0.58f, 0.02f), new Vector3(0.28f, 0.3f, 0.28f), PrimitiveType.Sphere);
            Block("Hip", visual, cloth, new Vector3(0f, -0.18f, 0f), new Vector3(0.38f, 0.2f, 0.24f));
            Block("LegL", visual, cloth, new Vector3(-0.12f, -0.62f, 0f), new Vector3(0.15f, 0.68f, 0.16f));
            Block("LegR", visual, cloth, new Vector3(0.12f, -0.62f, 0f), new Vector3(0.15f, 0.68f, 0.16f));
            var armL = Block("ArmL", visual, skin, new Vector3(-0.32f, 0.18f, 0f), new Vector3(0.12f, 0.46f, 0.12f));
            var armR = new GameObject("ArmR").transform;
            armR.SetParent(visual, false);
            armR.localPosition = new Vector3(0.3f, 0.22f, 0f);
            Block("UpperArm", armR, skin, new Vector3(0.08f, -0.12f, 0f), new Vector3(0.12f, 0.34f, 0.12f));
            var sword = new GameObject("Sword").transform;
            sword.SetParent(armR, false);
            sword.localPosition = new Vector3(0.02f, -0.28f, 0f);
            Block("Grip", sword, stoneSafe(), new Vector3(0.16f, 0f, 0f), new Vector3(0.16f, 0.06f, 0.06f));
            Block("Blade", sword, steel, new Vector3(0.58f, 0f, 0f), new Vector3(0.7f, 0.07f, 0.035f));
            var tip = Block("Tip", sword, steel, new Vector3(0.96f, 0f, 0f), new Vector3(0.08f, 0.05f, 0.03f));
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.startWidth = 0.07f;
            trail.endWidth = 0f;
            trail.emitting = false;
            trail.material = TrailMaterial();
            trail.minVertexDistance = 0.02f;

            var pose = player.GetComponent<DisciplePose>() ?? player.AddComponent<DisciplePose>();
            SetRef(pose, "torso", torso);
            SetRef(pose, "armLeft", armL);
            SetRef(pose, "combat", player.GetComponent<PlayerCombat>());
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null)
                SetRef(combat, "weapon", sword);

            var existing = player.transform.Find("DeflectRing");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            var ring = Block("DeflectRing", player.transform, deflect, new Vector3(0.75f, 0.15f, 0f), new Vector3(0.85f, 0.85f, 0.06f));
            ring.gameObject.SetActive(false);
        }

        static Material TrailMaterial()
        {
            const string path = "Assets/_Project/Materials/SwordTrail.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(0.82f, 0.92f, 1f, 0.8f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material stoneSafe()
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/StoneDark.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Pillar.mat");
        }

        static void BuildGuardian(BossController boss, Material body, Material coreMaterial, bool risen)
        {
            var visual = boss.transform.Find("Visual");
            if (visual == null)
                return;
            Clear(visual);
            var torso = Block("Torso", visual, body, new Vector3(0f, 0.12f, 0f), new Vector3(0.72f, 0.7f, 0.46f));
            Block("Head", visual, body, new Vector3(0f, 0.62f, 0.04f), new Vector3(0.4f, 0.32f, 0.36f));
            Block("Hip", visual, body, new Vector3(0f, -0.32f, 0f), new Vector3(0.62f, 0.24f, 0.4f));
            Block("LegL", visual, body, new Vector3(-0.18f, -0.72f, 0f), new Vector3(0.22f, 0.62f, 0.24f));
            Block("LegR", visual, body, new Vector3(0.18f, -0.72f, 0f), new Vector3(0.22f, 0.62f, 0.24f));
            var arm = new GameObject("ArmR").transform;
            arm.SetParent(visual, false);
            arm.localPosition = new Vector3(0.42f, 0.22f, 0f);
            Block("Forearm", arm, body, new Vector3(0.16f, -0.08f, 0f), new Vector3(0.42f, 0.18f, 0.18f));
            Block("ArmL", visual, body, new Vector3(-0.46f, 0.08f, 0f), new Vector3(0.18f, 0.5f, 0.18f));
            var core = Block("Core", visual, coreMaterial, new Vector3(0f, 0.16f, -0.24f), Vector3.one * (risen ? 0.34f : 0.26f), PrimitiveType.Sphere);
            if (risen)
            {
                Block("CrystalL", visual, coreMaterial, new Vector3(-0.28f, 0.48f, -0.12f), new Vector3(0.12f, 0.28f, 0.12f));
                Block("CrystalR", visual, coreMaterial, new Vector3(0.28f, 0.48f, -0.12f), new Vector3(0.12f, 0.28f, 0.12f));
            }

            var pose = boss.GetComponent<GuardianPose>() ?? boss.gameObject.AddComponent<GuardianPose>();
            SetRef(pose, "boss", boss);
            SetRef(pose, "arm", arm);
            SetRef(pose, "core", core);
            SetColor(pose, "coreColor", risen ? new Color(0.45f, 0.92f, 1f) : new Color(1f, 0.55f, 0.22f));
            SetFloat(pose, "coreEmission", risen ? 2.4f : 1.1f);

            var tint = boss.GetComponent<BodyTint>();
            if (tint != null)
            {
                var renderers = new List<Renderer>();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name is "Core" or "CrystalL" or "CrystalR")
                        continue;
                    renderers.Add(renderer);
                }

                SetRendererArray(tint, renderers.ToArray());
            }
        }

        static void DressArena(Material gold)
        {
            var arena = GameObject.Find("Arena");
            if (arena == null)
                return;
            var old = arena.transform.Find("Dressing");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var dressing = new GameObject("Dressing").transform;
            dressing.SetParent(arena.transform, false);
            var trim = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Pillar.mat");
            var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Wall.mat");
            Block("Lintel", dressing, trim, new Vector3(0f, 3.3f, 2.6f), new Vector3(8f, 0.28f, 0.45f));
            Block("Banner", dressing, gold, new Vector3(0f, 2.2f, 3.05f), new Vector3(1.4f, 2.2f, 0.06f));
            Block("Step", dressing, trim, new Vector3(7.2f, 0.08f, 0f), new Vector3(2.4f, 0.08f, 1.6f));
            Block("RailL", dressing, wall, new Vector3(-6.5f, 0.45f, -1.4f), new Vector3(4f, 0.12f, 0.12f));
            Block("RailR", dressing, wall, new Vector3(2f, 0.45f, -1.4f), new Vector3(5f, 0.12f, 0.12f));
        }

        static void DressAltar(Material gold)
        {
            var altar = GameObject.Find("Altar");
            if (altar == null)
                return;
            var old = altar.transform.Find("Shrine");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var shrine = new GameObject("Shrine").transform;
            shrine.SetParent(altar.transform, false);
            shrine.localPosition = Vector3.zero;
            Block("Bowl", shrine, gold, new Vector3(0f, 0.85f, 0f), new Vector3(1.3f, 0.12f, 1.3f));
            Block("Flame", shrine, gold, new Vector3(0f, 1.15f, 0f), new Vector3(0.28f, 0.45f, 0.28f), PrimitiveType.Sphere);
            if (altar.GetComponentInChildren<Light>() == null)
            {
                var lightObject = new GameObject("Altar Light");
                lightObject.transform.SetParent(altar.transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 1.5f, -0.4f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 8f;
                light.intensity = 6f;
                light.color = new Color(1f, 0.72f, 0.38f);
                lightObject.AddComponent<UniversalAdditionalLightData>();
            }
        }

        static ShellRefs BuildShell(GameObject player)
        {
            var presentation = GameObject.Find("Presentation") ?? new GameObject("Presentation");
            var impact = presentation.GetComponent<ImpactFeedback>() ?? presentation.AddComponent<ImpactFeedback>();
            var audio = presentation.GetComponent<AudioFeedback>() ?? presentation.AddComponent<AudioFeedback>();
            var sequence = presentation.GetComponent<SacrificeSequence>() ?? presentation.AddComponent<SacrificeSequence>();
            var shell = presentation.GetComponent<PresentationShell>() ?? presentation.AddComponent<PresentationShell>();
            var canvas = GameObject.Find("HUD");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Materials/White.png");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = Overlay(canvas, "Title", sprite, font, "K4RMA", "The disciple's final trial begins.", false);
            var ending = Overlay(canvas, "Ending", sprite, font, "Vertical Slice Complete", "You surrendered a power.\nThe guardian inherited it.\nYou learned to counter it.", false);
            var director = Object.FindAnyObjectByType<RunDirector>();
            BossController opening = null;
            foreach (var boss in Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (boss.gameObject.name.Contains("1"))
                    opening = boss;
            }

            SetRef(shell, "titleRoot", title);
            SetRef(shell, "endingRoot", ending);
            SetRef(shell, "director", director);
            SetRef(shell, "openingBoss", opening);
            SetFloat(shell, "titleSeconds", 1.7f);
            _ = impact;
            _ = audio;
            _ = player;
            return new ShellRefs { Sequence = sequence };
        }

        static GameObject Overlay(GameObject canvas, string name, Sprite sprite, Font font, string heading, string body, bool active)
        {
            if (canvas == null)
                return null;
            var existing = canvas.transform.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (sprite != null)
            {
                var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
                dim.transform.SetParent(root.transform, false);
                Stretch(dim.GetComponent<RectTransform>());
                var image = dim.GetComponent<Image>();
                image.sprite = sprite;
                image.color = new Color(0.04f, 0.05f, 0.08f, active ? 0.72f : 0.78f);
            }

            var title = Text(root.transform, "Heading", font, 72, FontStyle.Bold, Color.white);
            Place(title.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(900f, 100f));
            title.alignment = TextAnchor.MiddleCenter;
            title.text = heading;
            var copy = Text(root.transform, "Body", font, 28, FontStyle.Normal, new Color(0.86f, 0.9f, 0.96f));
            Place(copy.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(820f, 140f));
            copy.alignment = TextAnchor.MiddleCenter;
            copy.text = body;
            root.SetActive(active);
            return root;
        }

        static Transform Block(string name, Transform parent, Material material, Vector3 localPosition, Vector3 scale, PrimitiveType type = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            if (material != null)
                part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static void Clear(Transform visual)
        {
            for (int i = visual.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(visual.GetChild(i).gameObject);
        }

        static Material Material(string name, Color color, Color? emission)
        {
            string path = "Assets/_Project/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            Tint(path, color, emission);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static void Tint(string path, Color color, Color? emission)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                return;
            material.SetColor("_BaseColor", color);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
        }

        static Text Text(Transform parent, string name, Font font, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
                throw new System.InvalidOperationException(target.GetType().Name + "." + property);
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector(Object target, string property, Vector3 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetColor(Object target, string property, Color value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRendererArray(Object target, Renderer[] values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty("renderers");
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
