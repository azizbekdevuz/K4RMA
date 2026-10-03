using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace K4RMA.EditorTools
{
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/PrototypeArena.unity";

        [MenuItem("K4RMA/Create Prototype Arena If Missing")]
        public static void CreateIfMissing()
        {
            if (SceneExists())
            {
                Debug.Log("PrototypeArena already exists. The saved scene was left unchanged.");
                return;
            }

            CreateArena();
        }

        public static void CreateArenaBatch()
        {
            try
            {
                if (!SceneExists())
                    CreateArena();
                else
                    Debug.Log("PrototypeArena already exists. The saved scene was left unchanged.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static bool SceneExists()
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null || File.Exists(ScenePath);
        }

        static void CreateArena()
        {
            RemoveTemplateReadme();
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Prototype");
            EnsureFolder("Assets/_Project/Materials");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Scenes");

            var layers = CreateLayers();
            var sprite = CreateWhiteSprite();
            var materials = CreateMaterials();
            var data = CreateData();
            var projectilePrefab = CreateProjectilePrefab(materials.PlayerShot);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateArenaGeometry(materials, layers.Arena);
            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.position = new Vector3(-6f, 1f, 0f);

            var player = CreatePlayer(materials, layers, data.Tuning, projectilePrefab);
            player.Root.transform.position = spawn.position;
            var stageOne = CreateBoss("BossStage1", materials.Boss, null, materials, layers, data.StageOne, projectilePrefab, player.Root.transform, new Vector3(2.2f, 1.1f, 0f), 2.2f, 0.55f);
            var stageTwo = CreateBoss("BossStage2", materials.BossEvolved, materials.Core, materials, layers, data.StageTwo, projectilePrefab, player.Root.transform, new Vector3(2.2f, 1.25f, 0f), 2.5f, 0.62f);
            stageTwo.Root.SetActive(false);

            var altar = CreateAltar(materials.Altar, layers.Altar, player.Input);
            var camera = CreateCamera(player.Root.transform);
            CreateLights();
            var hud = CreateHud(sprite);
            var director = new GameObject("RunDirector").AddComponent<RunDirector>();

            SetRef(altar, "run", director);
            SetRef(director, "playerHealth", player.Health);
            SetRef(director, "playerBody", player.Body);
            SetRef(director, "playerAbilities", player.Abilities);
            SetRef(director, "stageOneBoss", stageOne.Boss);
            SetRef(director, "stageTwoBoss", stageTwo.Boss);
            SetRef(director, "altar", altar);
            SetRef(director, "hud", hud.Presenter);
            SetRef(director, "playerSpawn", spawn);
            SetRef(director, "input", player.Input);

            SetRef(hud.Presenter, "run", director);
            SetRef(hud.Presenter, "playerHealth", player.Health);
            SetRef(hud.Presenter, "stageOneHealth", stageOne.Health);
            SetRef(hud.Presenter, "stageTwoHealth", stageTwo.Health);
            SetRef(hud.Presenter, "abilities", player.Abilities);
            SetRef(hud.Presenter, "combat", player.Combat);
            SetRef(hud.Presenter, "playerFill", hud.PlayerFill);
            SetRef(hud.Presenter, "bossFill", hud.BossFill);
            SetRef(hud.Presenter, "playerLabel", hud.PlayerLabel);
            SetRef(hud.Presenter, "bossLabel", hud.BossLabel);
            SetRef(hud.Presenter, "abilityLabel", hud.AbilityLabel);
            SetRef(hud.Presenter, "banner", hud.Banner);
            SetRef(hud.Presenter, "prompt", hud.Prompt);

            ApplyCollisionMatrix(layers);
            PlayerSettings.companyName = "K4RMA";
            PlayerSettings.productName = "K4RMA Prototype";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + ScenePath);
        }

        struct LayerSet
        {
            public int Player;
            public int Boss;
            public int PlayerHit;
            public int BossHit;
            public int PlayerShot;
            public int BossShot;
            public int Arena;
            public int Altar;
        }

        struct Materials
        {
            public Material Floor;
            public Material Stripe;
            public Material Wall;
            public Material Pillar;
            public Material Player;
            public Material Weapon;
            public Material Boss;
            public Material BossEvolved;
            public Material Core;
            public Material Altar;
            public Material PlayerShot;
        }

        struct PrototypeData
        {
            public PlayerTuning Tuning;
            public BossEncounterConfig StageOne;
            public BossEncounterConfig StageTwo;
            public AbilityDefinition Dash;
            public AbilityDefinition Projectile;
            public AbilityDefinition Guard;
        }

        struct Actor
        {
            public GameObject Root;
            public Health Health;
            public Rigidbody Body;
            public PlayerInputReader Input;
            public PlayerAbilityState Abilities;
            public PlayerCombat Combat;
            public BossController Boss;
        }

        struct HudParts
        {
            public HudPresenter Presenter;
            public Image PlayerFill;
            public Image BossFill;
            public Text PlayerLabel;
            public Text BossLabel;
            public Text AbilityLabel;
            public Text Banner;
            public Text Prompt;
        }

        static LayerSet CreateLayers()
        {
            return new LayerSet
            {
                Player = EnsureLayer("Player"),
                Boss = EnsureLayer("Boss"),
                PlayerHit = EnsureLayer("PlayerHit"),
                BossHit = EnsureLayer("BossHit"),
                PlayerShot = EnsureLayer("PlayerShot"),
                BossShot = EnsureLayer("BossShot"),
                Arena = EnsureLayer("Arena"),
                Altar = EnsureLayer("Altar")
            };
        }

        static int EnsureLayer(string layerName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                    return i;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            throw new System.InvalidOperationException("No empty layer slot for " + layerName);
        }

        static void ApplyCollisionMatrix(LayerSet layers)
        {
            Ignore(layers.Player, layers.PlayerShot);
            Ignore(layers.Boss, layers.BossShot);
            Ignore(layers.PlayerShot, layers.BossShot);
            Ignore(layers.Player, layers.PlayerHit);
            Ignore(layers.Boss, layers.BossHit);
        }

        static void Ignore(int a, int b)
        {
            Physics.IgnoreLayerCollision(a, b, true);
        }

        static PrototypeData CreateData()
        {
            var dash = CreateAbility("Dash", PrototypeIds.Dash, false, "PROTOTYPE placeholder. Not wired in this slice.");
            var projectile = CreateAbility("Projectile", PrototypeIds.Projectile, true, "PROTOTYPE placeholder. This is the only wired transfer.");
            var guard = CreateAbility("Guard", PrototypeIds.Guard, false, "PROTOTYPE placeholder. Not wired in this slice.");

            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            const string tuningPath = "Assets/_Project/Data/Prototype/PlayerTuning.asset";
            SaveAsset(tuning, tuningPath);
            tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(tuningPath);

            var stageOne = ScriptableObject.CreateInstance<BossEncounterConfig>();
            stageOne.stageLabel = "Stage 1 Guardian";
            stageOne.maxHealth = 60;
            stageOne.inheritedAbilityId = string.Empty;
            const string stageOnePath = "Assets/_Project/Data/Prototype/BossStage1.asset";
            SaveAsset(stageOne, stageOnePath);
            stageOne = AssetDatabase.LoadAssetAtPath<BossEncounterConfig>(stageOnePath);

            var stageTwo = ScriptableObject.CreateInstance<BossEncounterConfig>();
            stageTwo.stageLabel = "Stage 2 Guardian";
            stageTwo.maxHealth = 110;
            stageTwo.approachSpeed = 3.6f;
            stageTwo.meleeDamage = 16;
            stageTwo.meleeTelegraphSeconds = 0.7f;
            stageTwo.inheritedAbilityId = PrototypeIds.Projectile;
            const string stageTwoPath = "Assets/_Project/Data/Prototype/BossStage2.asset";
            SaveAsset(stageTwo, stageTwoPath);
            stageTwo = AssetDatabase.LoadAssetAtPath<BossEncounterConfig>(stageTwoPath);

            return new PrototypeData
            {
                Tuning = tuning,
                StageOne = stageOne,
                StageTwo = stageTwo,
                Dash = dash,
                Projectile = projectile,
                Guard = guard
            };
        }

        static AbilityDefinition CreateAbility(string displayName, string id, bool wired, string note)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.displayName = displayName;
            ability.id = id;
            ability.isPrototype = true;
            ability.combatWired = wired;
            ability.prototypeNote = note;
            SaveAsset(ability, "Assets/_Project/Data/Prototype/" + displayName + ".asset");
            return ability;
        }

        static Materials CreateMaterials()
        {
            return new Materials
            {
                Floor = SaveMaterial("Floor", new Color(0.34f, 0.35f, 0.38f)),
                Stripe = SaveMaterial("Stripe", new Color(0.27f, 0.28f, 0.31f)),
                Wall = SaveMaterial("Wall", new Color(0.2f, 0.22f, 0.26f)),
                Pillar = SaveMaterial("Pillar", new Color(0.46f, 0.47f, 0.52f)),
                Player = SaveMaterial("Player", new Color(0.24f, 0.56f, 0.95f)),
                Weapon = SaveMaterial("Weapon", new Color(0.86f, 0.9f, 0.95f)),
                Boss = SaveMaterial("Boss", new Color(0.58f, 0.26f, 0.3f)),
                BossEvolved = SaveMaterial("BossEvolved", new Color(0.72f, 0.24f, 0.18f)),
                Core = SaveMaterial("Core", new Color(0.35f, 0.86f, 1f)),
                Altar = SaveMaterial("Altar", new Color(0.95f, 0.76f, 0.22f)),
                PlayerShot = SaveMaterial("PlayerShot", new Color(0.35f, 0.86f, 1f))
            };
        }

        static Material SaveMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("URP Lit shader was not found.");

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", 0f);
            SaveAsset(material, "Assets/_Project/Materials/" + name + ".mat");
            return material;
        }

        static Sprite CreateWhiteSprite()
        {
            const string path = "Assets/_Project/Materials/White.png";
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Projectile CreateProjectilePrefab(Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.localScale = Vector3.one * 0.46f;
            var collider = go.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.AddComponent<BodyTint>();
            var projectile = go.AddComponent<Projectile>();
            SetRef(projectile, "body", body);
            SetRef(projectile, "hitCollider", collider);
            const string prefabPath = "Assets/_Project/Prefabs/Projectile.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(prefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<Projectile>();
        }

        static void CreateArenaGeometry(Materials materials, int arenaLayer)
        {
            var root = new GameObject("Arena");
            CreateBlock(root.transform, "Floor", materials.Floor, new Vector3(0f, -0.5f, 0.4f), new Vector3(22f, 1f, 8f), arenaLayer);
            CreateBlock(root.transform, "Stripe", materials.Stripe, new Vector3(0f, 0.01f, 0.2f), new Vector3(22f, 0.02f, 1.4f), arenaLayer);
            CreateBlock(root.transform, "BackWall", materials.Wall, new Vector3(0f, 2f, 3.4f), new Vector3(22f, 5f, 0.6f), arenaLayer);
            CreateBlock(root.transform, "LeftWall", materials.Wall, new Vector3(-10.6f, 2f, 0.2f), new Vector3(0.6f, 5f, 6f), arenaLayer);
            CreateBlock(root.transform, "RightWall", materials.Wall, new Vector3(10.6f, 2f, 0.2f), new Vector3(0.6f, 5f, 6f), arenaLayer);
            CreateBlock(root.transform, "PillarA", materials.Pillar, new Vector3(-4f, 1.6f, 1.8f), new Vector3(0.7f, 3.2f, 0.7f), arenaLayer);
            CreateBlock(root.transform, "PillarB", materials.Pillar, new Vector3(5.5f, 1.8f, 1.8f), new Vector3(0.8f, 3.6f, 0.8f), arenaLayer);
        }

        static void CreateBlock(Transform parent, string name, Material material, Vector3 position, Vector3 scale, int layer)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.layer = layer;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Actor CreatePlayer(Materials materials, LayerSet layers, PlayerTuning tuning, Projectile projectilePrefab)
        {
            var root = new GameObject("Player");
            root.layer = layers.Player;
            root.transform.position = new Vector3(-6f, 1f, 0f);
            var body = root.AddComponent<Rigidbody>();
            PrepareBody(body);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.45f;
            capsule.center = Vector3.zero;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            CreateVisual(PrimitiveType.Capsule, "Body", visual, materials.Player, Vector3.zero, Vector3.one);
            var weapon = CreateVisual(PrimitiveType.Cube, "Weapon", visual, materials.Weapon, new Vector3(0.55f, 0.15f, 0f), new Vector3(0.75f, 0.16f, 0.16f));

            var meleeObject = new GameObject("MeleeHitbox");
            meleeObject.layer = layers.PlayerHit;
            meleeObject.transform.SetParent(root.transform, false);
            var meleeCollider = meleeObject.AddComponent<BoxCollider>();
            meleeCollider.isTrigger = true;
            meleeCollider.enabled = false;
            var melee = meleeObject.AddComponent<MeleeHitbox>();

            var health = root.AddComponent<Health>();
            var input = root.AddComponent<PlayerInputReader>();
            var abilities = root.AddComponent<PlayerAbilityState>();
            var motor = root.AddComponent<PlayerController>();
            var combat = root.AddComponent<PlayerCombat>();
            var tint = root.AddComponent<BodyTint>();

            SetInt(health, "maxHealth", tuning.maxHealth);
            SetRef(motor, "tuning", tuning);
            SetRef(motor, "input", input);
            SetRef(motor, "health", health);
            SetRef(motor, "bodyCollider", capsule);
            SetRef(motor, "visualRoot", visual);
            SetMask(motor, "groundLayers", layers.Arena);
            SetRef(combat, "tuning", tuning);
            SetRef(combat, "input", input);
            SetRef(combat, "motor", motor);
            SetRef(combat, "abilities", abilities);
            SetRef(combat, "health", health);
            SetRef(combat, "melee", melee);
            SetRef(combat, "weapon", weapon);
            SetRef(combat, "projectilePrefab", projectilePrefab);
            SetRef(combat, "tint", tint);
            SetMask(combat, "projectileHitLayers", layers.Boss, layers.Arena, layers.Altar);
            SetInt(combat, "projectileLayer", layers.PlayerShot);
            SetRef(melee, "hitCollider", meleeCollider);
            SetMask(melee, "targetLayers", layers.Boss);
            SetObjectArray(abilities, "catalog", new Object[] { LoadAbility("Dash"), LoadAbility("Projectile"), LoadAbility("Guard") });

            return new Actor
            {
                Root = root,
                Health = health,
                Body = body,
                Input = input,
                Abilities = abilities,
                Combat = combat
            };
        }

        static Actor CreateBoss(
            string name,
            Material bodyMaterial,
            Material coreMaterial,
            Materials materials,
            LayerSet layers,
            BossEncounterConfig config,
            Projectile projectilePrefab,
            Transform player,
            Vector3 position,
            float height,
            float radius)
        {
            var root = new GameObject(name);
            root.layer = layers.Boss;
            root.transform.position = position;
            var body = root.AddComponent<Rigidbody>();
            PrepareBody(body);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = height;
            capsule.radius = radius;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            float visualScale = height / 2f;
            visual.localScale = Vector3.one * visualScale;
            CreateVisual(PrimitiveType.Capsule, "Body", visual, bodyMaterial, Vector3.zero, Vector3.one);
            if (coreMaterial != null)
                CreateVisual(PrimitiveType.Sphere, "InheritedCore", visual, coreMaterial, new Vector3(0.1f, 0.25f, -0.42f), Vector3.one * 0.38f);

            var aim = new GameObject("AimPoint").transform;
            aim.SetParent(root.transform, false);
            aim.localPosition = new Vector3(0.2f, 0.2f, 0f);

            var meleeObject = new GameObject("MeleeHitbox");
            meleeObject.layer = layers.BossHit;
            meleeObject.transform.SetParent(root.transform, false);
            var meleeCollider = meleeObject.AddComponent<BoxCollider>();
            meleeCollider.isTrigger = true;
            meleeCollider.enabled = false;
            var melee = meleeObject.AddComponent<MeleeHitbox>();

            var health = root.AddComponent<Health>();
            var boss = root.AddComponent<BossController>();
            var tint = root.AddComponent<BodyTint>();

            SetInt(health, "maxHealth", config.maxHealth);
            SetRef(boss, "config", config);
            SetRef(boss, "health", health);
            SetRef(boss, "player", player);
            SetRef(boss, "melee", melee);
            SetRef(boss, "projectilePrefab", projectilePrefab);
            SetRef(boss, "aimPoint", aim);
            SetRef(boss, "tint", tint);
            SetRef(boss, "visualRoot", visual);
            SetMask(boss, "projectileHitLayers", layers.Player, layers.Arena, layers.Altar);
            SetInt(boss, "projectileLayer", layers.BossShot);
            SetRef(melee, "hitCollider", meleeCollider);
            SetMask(melee, "targetLayers", layers.Player);

            return new Actor { Root = root, Health = health, Body = body, Boss = boss };
        }

        static Altar CreateAltar(Material material, int layer, PlayerInputReader input)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Altar";
            root.layer = layer;
            root.transform.position = new Vector3(7.2f, 0.8f, 0f);
            root.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            root.GetComponent<Renderer>().sharedMaterial = material;

            var triggerObject = new GameObject("AltarTrigger");
            triggerObject.layer = layer;
            triggerObject.transform.SetParent(root.transform, false);
            triggerObject.transform.localPosition = Vector3.zero;
            triggerObject.transform.localScale = new Vector3(4.5f, 1.6f, 4.5f);
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            var altar = triggerObject.AddComponent<Altar>();
            SetRef(altar, "input", input);
            SetString(altar, "sacrificeAbilityId", PrototypeIds.Projectile);

            var canvasObject = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 60f);
            rect.localPosition = new Vector3(0f, 1.7f, 0f);
            rect.localScale = Vector3.one * 0.012f;
            canvasObject.AddComponent<FaceCamera>();
            var text = CreateText(canvasObject.transform, "Caption", Font(), 42, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.75f));
            text.text = "ALTAR";
            Stretch(text.rectTransform);
            return altar;
        }

        static Camera CreateCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.transform.rotation = Quaternion.Euler(16f, 14f, 0f);
            camera.transform.position = new Vector3(-4.8f, 3.4f, -12f);
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var follow = cameraObject.AddComponent<SideViewCamera>();
            SetRef(follow, "target", target);
            SetVector3(follow, "worldOffset", new Vector3(1.2f, 2.5f, -12f));
            return camera;
        }

        static void CreateLights()
        {
            var keyObject = new GameObject("Key Light");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.96f, 0.9f);
            keyObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            keyObject.AddComponent<UniversalAdditionalLightData>();

            var fillObject = new GameObject("Fill Light");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fillObject.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            fillObject.AddComponent<UniversalAdditionalLightData>();
        }

        static HudParts CreateHud(Sprite sprite)
        {
            var canvasObject = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.name = "EventSystem";

            var font = Font();
            var playerFill = CreateBar(canvasObject.transform, "PlayerBar", sprite, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(460f, 28f), new Color(0.24f, 0.56f, 0.95f), out var playerLabel, font);
            var bossFill = CreateBar(canvasObject.transform, "BossBar", sprite, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-488f, -28f), new Vector2(460f, 28f), new Color(0.9f, 0.32f, 0.28f), out var bossLabel, font);
            var ability = CreateText(canvasObject.transform, "Abilities", font, 24, TextAnchor.UpperLeft, Color.white);
            Anchor(ability.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(620f, 190f));
            var banner = CreateText(canvasObject.transform, "Banner", font, 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.55f));
            Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, 120f), new Vector2(840f, 140f));
            var prompt = CreateText(canvasObject.transform, "Prompt", font, 26, TextAnchor.MiddleCenter, new Color(0.92f, 0.94f, 0.98f));
            Anchor(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-420f, 24f), new Vector2(840f, 48f));

            var presenter = canvasObject.AddComponent<HudPresenter>();
            return new HudParts
            {
                Presenter = presenter,
                PlayerFill = playerFill,
                BossFill = bossFill,
                PlayerLabel = playerLabel,
                BossLabel = bossLabel,
                AbilityLabel = ability,
                Banner = banner,
                Prompt = prompt
            };
        }

        static Image CreateBar(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Vector2 size, Color fillColor, out Text label, Font font)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(size.x, size.y + 28f);

            var backObject = new GameObject("Back", typeof(RectTransform), typeof(Image));
            backObject.transform.SetParent(root.transform, false);
            var backRect = backObject.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(0f, -26f);
            backRect.sizeDelta = size;
            backObject.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.9f);
            backObject.GetComponent<Image>().sprite = sprite;

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(backObject.transform, false);
            Stretch(fillObject.GetComponent<RectTransform>());
            var fill = fillObject.GetComponent<Image>();
            fill.sprite = sprite;
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            label = CreateText(root.transform, "Label", font, 22, TextAnchor.UpperLeft, Color.white);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(0f, 24f);
            return fill;
        }

        static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Transform CreateVisual(PrimitiveType type, string name, Transform parent, Material material, Vector3 localPosition, Vector3 localScale)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            return visual.transform;
        }

        static void PrepareBody(Rigidbody body)
        {
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
        }

        static AbilityDefinition LoadAbility(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/_Project/Data/Prototype/" + name + ".asset");
        }

        static Font Font()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                throw new System.InvalidOperationException("Unity built-in UI font was not found.");
            return font;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        static void SaveAsset(Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        static void RemoveTemplateReadme()
        {
            if (AssetDatabase.IsValidFolder("Assets/TutorialInfo"))
                AssetDatabase.DeleteAsset("Assets/TutorialInfo");
            if (AssetDatabase.LoadAssetAtPath<Object>("Assets/Readme.asset") != null)
                AssetDatabase.DeleteAsset("Assets/Readme.asset");
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(min.x, min.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            Require(so, property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            Require(so, property).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            Require(so, property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector3(Object target, string property, Vector3 value)
        {
            var so = new SerializedObject(target);
            Require(so, property).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetMask(Object target, string property, params int[] layerIndexes)
        {
            int mask = 0;
            for (int i = 0; i < layerIndexes.Length; i++)
                mask |= 1 << layerIndexes[i];
            SetInt(target, property, mask);
        }

        static void SetObjectArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = Require(so, property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedProperty Require(SerializedObject so, string property)
        {
            var prop = so.FindProperty(property);
            if (prop == null)
                throw new System.InvalidOperationException(so.targetObject.GetType().Name + "." + property + " was not found.");
            return prop;
        }
    }
}
