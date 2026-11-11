using UnityEngine;

namespace KarmaPrototype
{
    // Keep the approved background untouched outside the lunar region. The disk owns projection correction.
    public static class KarmaMoonArtwork
    {
        static Material material;
        public static void Apply(SpriteRenderer background, bool mountain)
        {

            var texture = Resources.Load<Texture2D>("K4RMAArtwork/Environments/" + (mountain ? "MountainDojo" : "TrialHall") + "/Extended");
            if (texture == null) return;
            if (material == null) material = Resources.Load<Material>("Materials/KarmaMoonBackground");
            if (material == null) return;
            Vector4 region = mountain
                ? new Vector4(1251f / 3344f, 1 - 573.5f / 1882f, 39f / 3344f, 39.5f / 1882f)
                : new Vector4(1832.5f / 3344f, 1 - 641f / 1882f, 39.5f / 3344f, 39f / 1882f);
            var skyProperties = new MaterialPropertyBlock();
            skyProperties.SetVector("_MoonRegion", region); skyProperties.SetFloat("_DiskOnly", 0);
            skyProperties.SetFloat("_SkyRight", 3.2f);
            background.sharedMaterial = material; background.SetPropertyBlock(skyProperties);
            var at = new Vector2(region.x * texture.width, region.y * texture.height);
            var radius = new Vector2(region.z * texture.width, region.w * texture.height);
            var sprite = Sprite.Create(texture, new Rect(at - radius * 1.5f, radius * 3), Vector2.one * 0.5f, 1, 0, SpriteMeshType.FullRect);
            sprite.name = "Isolated lunar disk";
            var go = new GameObject("Round moon"); go.transform.SetParent(background.transform.parent, false);
            var bounds = background.bounds;
            float oldFloor = mountain ? 0.62f : 0.65f;
            go.transform.position = new Vector3(20 + (region.x - 0.5f) * bounds.size.x,
                (region.y - (1 - oldFloor)) * bounds.size.y, background.transform.position.z);
            float diameter = Mathf.Sqrt(bounds.size.x * region.z * 2 * bounds.size.y * region.w * 2);
            go.transform.localScale = new Vector3(diameter / (radius.x * 2), diameter / (radius.y * 2), 1);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = background.sortingOrder + 1; renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetVector("_MoonRegion", region); properties.SetFloat("_MoonStretch", 1); properties.SetFloat("_DiskOnly", 1);
            renderer.SetPropertyBlock(properties);
            go.AddComponent<KarmaMoonProjection>().Initialize(renderer, 1, sprite);
        }
    }
}
