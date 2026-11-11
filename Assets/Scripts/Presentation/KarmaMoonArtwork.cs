using UnityEngine;

namespace KarmaPrototype
{
    // The trial hall plate is moonless; the restored mountain plate includes its original moon.
    public static class KarmaMoonArtwork
    {
        static Material material;
        public static void Apply(SpriteRenderer background, bool mountain)
        {
            if (mountain) return;
            var sprite = Resources.Load<Sprite>("Backgrounds/TrialHall/Moon");
            if (sprite == null) return;
            if (material == null) material = Resources.Load<Material>("Shared/Rendering/KarmaMoonBackground");
            if (material == null) return;

            // The native 3344x1882 plate stays centered inside the wider outpaint.
            Vector2 sourceCenter = new Vector2(1832.5f, 641);
            var bounds = background.bounds;
            float unitsPerPixel = bounds.size.y / 1882;
            var go = new GameObject("Round moon"); go.transform.SetParent(background.transform.parent, false);
            go.transform.position = new Vector3(bounds.center.x + (sourceCenter.x - 1672) * unitsPerPixel,
                bounds.center.y + (941 - sourceCenter.y) * unitsPerPixel, background.transform.position.z);
            go.transform.localScale = Vector3.one * (unitsPerPixel * sprite.pixelsPerUnit);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = background.sortingOrder + 1; renderer.sharedMaterial = material;
            go.AddComponent<KarmaMoonProjection>().Initialize(renderer, 1);
        }
    }
}
