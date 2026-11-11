using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Samples AFTER KarmaWaveMotion. Only artwork is copied: no attacks, lights,
    // colliders, behaviours or materials are instantiated with a silhouette.
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class KarmaSwordRetreatAfterimages : MonoBehaviour
    {
        struct TransformSample
        {
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        struct SpriteSample
        {
            public Sprite sprite;
            public Material material;
            public bool visible, flipX, flipY;
            public float alpha;
            public int layer, order;
            public SpriteDrawMode drawMode;
            public Vector2 size;
            public SpriteSortPoint sortPoint;
        }
        sealed class Echo
        {
            public GameObject root;
            public Transform[] transforms;
            public SpriteRenderer[] sprites;
            public float[] alphas;
            public float age;
            public bool active;
        }

        KarmaEnemy owner;
        KarmaGame game;
        KarmaSwordBossSettings settings;
        Material echoMaterial;
        Transform poolRoot;
        Transform[] sources;
        SpriteRenderer[] sourceSprites;
        TransformSample[] transformSamples;
        SpriteSample[] spriteSamples;
        Echo[] pool;
        Vector2 sampledPosition, lastEmissionPosition;
        float emissionClock;
        int nextSlot;
        bool emitting, hasSample, relocation;
        static readonly Color Tint = new Color(0.62f, 0.86f, 1f);

        internal void Initialize(KarmaEnemy enemy, KarmaGame director, KarmaSwordBossSettings tuning)
        {
            owner = enemy; game = director; settings = tuning;
            echoMaterial = Resources.Load<Material>("SwordWave/Shared/KarmaSwordRetreatEcho");
            sourceSprites = GetComponentsInChildren<SpriteRenderer>(true);
            // Retain the entire transform chain, including reflected/nonuniform
            // ancestors. Copying individual world TRS values would lose shear.
            var nodes = new List<Transform>();
            var indices = new Dictionary<Transform, int>();
            foreach (var sprite in sourceSprites) AddChain(sprite.transform, nodes, indices);
            sources = nodes.ToArray();
            transformSamples = new TransformSample[sources.Length];
            spriteSamples = new SpriteSample[sourceSprites.Length];
            poolRoot = new GameObject("Sword retreat afterimages (world space)").transform;
            pool = new Echo[Mathf.Clamp(settings.swordAfterimageCapacity, 5, 8)];
            for (int slot = 0; slot < pool.Length; slot++)
            {
                var echo = new Echo
                {
                    root = new GameObject("Backstep echo " + slot),
                    transforms = new Transform[sources.Length],
                    sprites = new SpriteRenderer[sourceSprites.Length],
                    alphas = new float[sourceSprites.Length]
                };
                echo.root.transform.SetParent(poolRoot, false);
                echo.root.SetActive(false);
                for (int i = 0; i < sources.Length; i++)
                {
                    var node = new GameObject(sources[i].name).transform;
                    node.gameObject.layer = sources[i].gameObject.layer;
                    node.SetParent(sources[i].parent != null
                        ? echo.transforms[indices[sources[i].parent]] : echo.root.transform, false);
                    echo.transforms[i] = node;
                }
                for (int i = 0; i < sourceSprites.Length; i++)
                    echo.sprites[i] = echo.transforms[indices[sourceSprites[i].transform]].gameObject.AddComponent<SpriteRenderer>();
                pool[slot] = echo;
            }
        }

        static void AddChain(Transform node, List<Transform> nodes, Dictionary<Transform, int> indices)
        {
            if (node == null || indices.ContainsKey(node)) return;
            AddChain(node.parent, nodes, indices);
            indices.Add(node, nodes.Count); nodes.Add(node);
        }

        internal void BeginRetreat(bool relocating = false)
        {
            relocation = relocating;
            emitting = true;
            emissionClock = Mathf.Clamp(settings.swordAfterimageInterval, 0.03f, 0.07f);
            lastEmissionPosition = owner.Position;
        }
        internal void EndRetreat() { emitting = false; }
        internal void Clear()
        {
            emitting = hasSample = false;
            if (pool == null) return;
            foreach (var echo in pool)
            { echo.active = false; if (echo.root != null) echo.root.SetActive(false); }
        }

        void LateUpdate()
        {
            if (pool == null) return;
            if (owner == null || !owner.PresentationActive || owner.Stunned)
            { Clear(); return; }
            if (game != null && game.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            float lifetime = Mathf.Clamp(settings.swordAfterimageLifetime, 0.2f, 0.3f);
            foreach (var echo in pool)
            {
                if (!echo.active) continue;
                echo.age += dt;
                if (echo.age >= lifetime)
                { echo.active = false; echo.root.SetActive(false); continue; }
                float fade = 1 - echo.age / lifetime;
                // A gentle opening then a smooth fade keeps several readable poses.
                fade *= 2 - fade;
                for (int i = 0; i < echo.sprites.Length; i++)
                {
                    var sprite = echo.sprites[i];
                    var color = sprite.color;
                    // Base alpha is stored per renderer (including translucent decoration).
                    color.a = echo.alphas[i] * fade;
                    sprite.color = color;
                }
            }
            if (emitting)
            {
                emissionClock += dt;
                float travelled = (owner.Position.x - sampledPosition.x) * owner.Facing;
                float spacing = Mathf.Abs(owner.Position.x - lastEmissionPosition.x);
                if (hasSample && (relocation ? Mathf.Abs(travelled) > 0.001f : travelled < -0.001f) && spacing >= 0.12f
                    && emissionClock >= Mathf.Clamp(settings.swordAfterimageInterval, 0.03f, 0.07f))
                {
                    // Emit the PREVIOUS displayed frame at its exact old position.
                    // It is already on the player-facing side of the moving body.
                    Emit();
                    lastEmissionPosition = owner.Position;
                    // At low FPS emit once, never stack catch-up copies in one place.
                    emissionClock = 0;
                }
            }
            Capture();
        }

        void Capture()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null) { hasSample = false; return; }
                transformSamples[i] = new TransformSample
                { position = source.localPosition, rotation = source.localRotation, scale = source.localScale };
            }
            for (int i = 0; i < sourceSprites.Length; i++)
            {
                var source = sourceSprites[i];
                spriteSamples[i] = new SpriteSample
                {
                    sprite = source.sprite, material = source.sharedMaterial,
                    visible = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null,
                    flipX = source.flipX, flipY = source.flipY, alpha = source.color.a,
                    layer = source.sortingLayerID, order = source.sortingOrder,
                    drawMode = source.drawMode, size = source.size, sortPoint = source.spriteSortPoint
                };
            }
            sampledPosition = owner.Position; hasSample = true;
        }

        void Emit()
        {
            var echo = pool[nextSlot]; nextSlot = (nextSlot + 1) % pool.Length;
            for (int i = 0; i < sources.Length; i++)
            {
                var sample = transformSamples[i];
                echo.transforms[i].localPosition = sample.position;
                echo.transforms[i].localRotation = sample.rotation;
                echo.transforms[i].localScale = sample.scale;
            }
            for (int i = 0; i < echo.sprites.Length; i++)
            {
                var sample = spriteSamples[i]; var sprite = echo.sprites[i];
                sprite.enabled = sample.visible; sprite.sprite = sample.sprite;
                sprite.sharedMaterial = echoMaterial != null ? echoMaterial : sample.material;
                sprite.flipX = sample.flipX; sprite.flipY = sample.flipY;
                sprite.sortingLayerID = sample.layer; sprite.sortingOrder = sample.order - 1;
                sprite.drawMode = sample.drawMode; sprite.size = sample.size; sprite.spriteSortPoint = sample.sortPoint;
                sprite.color = new Color(Tint.r, Tint.g, Tint.b,
                    sample.alpha * Mathf.Clamp(settings.swordAfterimageAlpha, 0.45f, 0.65f));
                echo.alphas[i] = sprite.color.a;
            }
            echo.age = 0; echo.active = true; echo.root.SetActive(true);
        }

        void OnDisable() { Clear(); }
        void OnDestroy() { if (poolRoot != null) Destroy(poolRoot.gameObject); }
    }
}
