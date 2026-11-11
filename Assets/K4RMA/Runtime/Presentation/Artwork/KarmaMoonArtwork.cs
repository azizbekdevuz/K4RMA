using UnityEngine;

namespace KarmaPrototype
{
    // The plate is already moonless. Render only the original source pixels as a separate sprite.
    public static class KarmaMoonArtwork
    {
        static Material material;
        public static void Apply(SpriteRenderer background, bool mountain)
        {
            var sprite = Resources.Load<Sprite>("K4RMAArtwork/Environments/"
                + (mountain ? "MountainDojo" : "TrialHall") + "/Moon");
            if (sprite == null) return;
            if (material == null) material = Resources.Load<Material>("Materials/KarmaMoonBackground");
            if (material == null) return;

            // The native 3344x1882 plate stays centered inside the wider outpaint.
            Vector2 sourceCenter = mountain ? new Vector2(1251, 573.5f) : new Vector2(1832.5f, 641);
            var bounds = background.bounds;
            float unitsPerPixel = bounds.size.y / 1882;
            var go = new GameObject("Round moon"); go.transform.SetParent(background.transform.parent, false);
            go.transform.position = new Vector3(bounds.center.x + (sourceCenter.x - 1672) * unitsPerPixel,
                bounds.center.y + (941 - sourceCenter.y) * unitsPerPixel, background.transform.position.z);
            go.transform.localScale = Vector3.one * (unitsPerPixel * sprite.pixelsPerUnit);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = background.sortingOrder + 1; renderer.sharedMaterial = material;
            // The mountain source disk is 78x79 pixels; compensate its one-pixel aspect difference.
            go.AddComponent<KarmaMoonProjection>().Initialize(renderer, mountain ? 78f / 79 : 1);
        }
    }
}
