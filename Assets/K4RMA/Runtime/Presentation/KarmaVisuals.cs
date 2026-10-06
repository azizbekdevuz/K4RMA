using UnityEngine;

namespace KarmaPrototype
{
    // Procedural placeholder art; replace these factories with your prefabs later.
    public static class KarmaVisuals
    {
        static Sprite square;
        static Material material;
        public static void UseMaterial(Material value) { material = value; }
        public static void ApplyMaterial(SpriteRenderer renderer)
        {
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) material = new Material(shader);
            }
            if (material != null) renderer.sharedMaterial = material;
        }
        public static SpriteRenderer Box(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, int order = 0)
        {
            if (square == null)
            {
                var texture = new Texture2D(1, 1);
                texture.name = "K4RMA_RuntimePixel";
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
                // Unlit in both Built-in and Universal pipelines.
                var shader = Shader.Find("Sprites/Default");
                if (material == null && shader != null) material = new Material(shader);
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }
        public static void Flash(Transform parent, Vector2 position, Vector2 size, Color color, float life = 0.18f)
        {
            var sr = Box(parent, "Effect", position, size, color, 10);
            sr.gameObject.AddComponent<KarmaEffect>().Initialize(life);
        }
        public static Transform Character(Transform parent, Color cloak, bool finalGuardian = false)
        {
            var root = new GameObject("Artwork").transform;
            root.SetParent(parent, false);
            Box(root, "Cloak", new Vector2(0, -0.08f), new Vector2(0.85f, 1.15f), cloak, 3);
            Box(root, "Face", new Vector2(0, 0.47f), new Vector2(0.57f, 0.48f), new Color(0.86f, 0.84f, 0.76f), 4);
            Box(root, "Visor", new Vector2(0.13f, 0.48f), new Vector2(0.32f, 0.12f), new Color(0.13f, 0.15f, 0.22f), 5);
            Box(root, "Belt", new Vector2(0, -0.22f), new Vector2(0.9f, 0.12f), new Color(0.92f, 0.75f, 0.4f), 4);
            Box(root, "Boot L", new Vector2(-0.25f, -0.69f), new Vector2(0.3f, 0.25f), new Color(0.13f, 0.15f, 0.22f), 4);
            Box(root, "Boot R", new Vector2(0.25f, -0.69f), new Vector2(0.3f, 0.25f), new Color(0.13f, 0.15f, 0.22f), 4);
            Box(root, "Hakama", new Vector2(0, -0.42f), new Vector2(0.78f, 0.48f), cloak * 0.6f, 4);
            Box(root, "Topknot", new Vector2(-0.05f, 0.78f), new Vector2(0.22f, 0.2f), new Color(0.09f,0.10f,0.14f), 5);
            var katana = Box(root, "Katana", new Vector2(0.65f, -0.12f), new Vector2(0.1f, 1.5f), new Color(0.83f,0.88f,0.90f), 6);
            katana.transform.localRotation = Quaternion.Euler(0,0,15);
            Box(root, "Tsuba", new Vector2(0.65f, -0.45f), new Vector2(0.32f, 0.08f), new Color(0.82f,0.65f,0.32f), 7);
            if (finalGuardian)
            {
                Box(root, "수호자 투구", new Vector2(0, 0.74f), new Vector2(0.85f, 0.3f), cloak * 0.65f, 6);
                Box(root, "왼쪽 뿔", new Vector2(-0.42f, 0.97f), new Vector2(0.18f, 0.55f), new Color(0.8f, 0.68f, 0.4f), 6);
                Box(root, "오른쪽 뿔", new Vector2(0.42f, 0.97f), new Vector2(0.18f, 0.55f), new Color(0.8f, 0.68f, 0.4f), 6);
                Box(root, "왼쪽 견갑", new Vector2(-0.59f, 0.2f), new Vector2(0.48f, 0.5f), cloak * 0.7f, 5);
                Box(root, "오른쪽 견갑", new Vector2(0.59f, 0.2f), new Vector2(0.48f, 0.5f), cloak * 0.7f, 5);
                var core = Box(root, "수호자의 핵", new Vector2(0, 0.08f), new Vector2(0.25f, 0.25f), new Color(0.9f, 0.6f, 1), 7);
                core.transform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            root.gameObject.AddComponent<KarmaCharacterAnimator>().Initialize();
            return root;
        }
    }
}
