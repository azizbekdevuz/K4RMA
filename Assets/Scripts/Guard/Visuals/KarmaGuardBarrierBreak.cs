using UnityEngine;

namespace KarmaPrototype
{
    // Prebuilt sectors retain the exact GD01 sprite UVs and transform. They are
    // presentation only: the owner removes damage reduction immediately on parry.
    public sealed class KarmaGuardBarrierBreak : MonoBehaviour
    {
        const int Count = 12, Steps = 8;
        const float Duration = 0.72f, SplitAt = 0.10f;
        readonly Mesh[] meshes = new Mesh[Count];
        readonly Transform[] plates = new Transform[Count];
        readonly MeshRenderer[] renderers = new MeshRenderer[Count];
        readonly LineRenderer[] cracks = new LineRenderer[Count];
        readonly Vector3[] homes = new Vector3[Count];
        readonly Vector3[] directions = new Vector3[Count];
        KarmaGame game;
        KarmaEnemy owner;
        Transform root;
        MaterialPropertyBlock properties;
        Vector2 size;
        float age;
        public bool Playing { get; private set; }
        public float Age => age;
        public int PlayCount { get; private set; }

        public void Initialize(KarmaGame director, KarmaEnemy boss, SpriteRenderer source, Sprite sprite)
        {
            game = director; owner = boss;
            root = new GameObject("GD01 original ring fragments").transform;
            root.SetParent(source.transform, false);
            size = sprite.rect.size / sprite.pixelsPerUnit;
            properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", sprite.texture);
            properties.SetColor("unity_SpriteColor", Color.white);
            properties.SetColor("_Color", Color.white);
            properties.SetVector("unity_SpriteProps", new Vector4(1, 1, 0, 0));
            for (int k = 0; k < Count; k++)
            {
                float start = (15 + k * 30) * Mathf.Deg2Rad;
                float mid = start + 15 * Mathf.Deg2Rad;
                directions[k] = new Vector3(Mathf.Cos(mid) * size.x, Mathf.Sin(mid) * size.y, 0);
                homes[k] = directions[k] * 0.39f + (Vector3)sprite.bounds.center;
                var part = new GameObject("GD01 engraved plate " + k);
                plates[k] = part.transform; plates[k].SetParent(root, false);
                plates[k].localPosition = homes[k];
                var vertices = new Vector3[Steps + 2];
                var uv = new Vector2[vertices.Length];
                var triangles = new int[Steps * 3];
                for (int j = 0; j < vertices.Length; j++)
                {
                    float a = start + (j - 1) * (30 * Mathf.Deg2Rad / Steps);
                    Vector2 axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 p = j == 0 ? Vector2.zero : axis * (0.5f / Mathf.Max(Mathf.Abs(axis.x), Mathf.Abs(axis.y)));
                    Vector2 pixel = (p + Vector2.one * 0.5f) * sprite.rect.size;
                    vertices[j] = (Vector3)((pixel - sprite.pivot) / sprite.pixelsPerUnit) - homes[k];
                    uv[j] = new Vector2((sprite.rect.x + pixel.x) / sprite.texture.width,
                        (sprite.rect.y + pixel.y) / sprite.texture.height);
                    if (j >= 2) { int n = (j - 2) * 3; triangles[n] = 0; triangles[n + 1] = j - 1; triangles[n + 2] = j; }
                }
                var mesh = new Mesh { name = "GD01 original UV sector " + k };
                mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds();
                meshes[k] = mesh; part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>(); renderers[k] = renderer;
                renderer.sharedMaterial = source.sharedMaterial;
                renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder + 1;
                renderer.SetPropertyBlock(properties);
                // Fractures live only on the existing annulus, never across its hollow center.
                var crack = new GameObject("Plate fracture").AddComponent<LineRenderer>(); cracks[k] = crack;
                crack.transform.SetParent(part.transform, false); crack.useWorldSpace = false;
                crack.positionCount = 4; crack.numCapVertices = 2;
                crack.sharedMaterial = KarmaCombatVFX.GlowMaterial;
                crack.sortingLayerID = source.sortingLayerID; crack.sortingOrder = source.sortingOrder + 2;
                crack.widthMultiplier = size.x * 0.004f;
                for (int j = 0; j < 4; j++)
                {
                    float a = mid + (j % 2 == 0 ? -0.035f : 0.045f);
                    float r = Mathf.Lerp(0.32f, 0.46f, j / 3f);
                    crack.SetPosition(j, new Vector3(Mathf.Cos(a) * size.x * r, Mathf.Sin(a) * size.y * r, 0)
                        + sprite.bounds.center - homes[k]);
                }
            }
            root.gameObject.SetActive(false);
        }

        public void Play()
        {
            if (root == null || !owner.Alive) return;
            age = 0; Playing = true; PlayCount++;
            root.gameObject.SetActive(true); Paint();
        }
        void Update()
        {
            if (!Playing) return;
            if (owner == null || !owner.Alive || game.Phase != RunPhase.Combat) { Stop(); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (age >= Duration) { Stop(); return; }
            Paint();
        }
        void Paint()
        {
            float flight = Mathf.Max(0, age - SplitAt);
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.30f, Duration, age));
            properties.SetColor("unity_SpriteColor", new Color(1, 1, 1, fade));
            for (int k = 0; k < Count; k++)
            {
                float speed = 0.38f + (k % 3) * 0.045f;
                plates[k].localPosition = homes[k] + directions[k] * flight * speed
                    + Vector3.down * size.y * flight * flight * 0.23f;
                plates[k].localRotation = Quaternion.Euler(0, 0, flight * (k % 2 == 0 ? 42 : -35));
                plates[k].localScale = Vector3.one * Mathf.Lerp(1, 0.76f, flight / (Duration - SplitAt));
                renderers[k].SetPropertyBlock(properties);
                float crackAlpha = Mathf.Clamp01(age / 0.045f) * fade;
                cracks[k].startColor = cracks[k].endColor = new Color(1, 0.88f, 0.43f, crackAlpha);
            }
        }
        public void Stop()
        {
            Playing = false;
            if (root != null) root.gameObject.SetActive(false);
        }
        void OnDisable() { Stop(); }
        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
