using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    // Convex hull of the two endpoint boxes: exact footprint of a swept AABB.
    // Unlike a diagonal bounding rectangle, this preserves the real safe corners.
    public sealed class KarmaAttackWarning : MonoBehaviour
    {
        Sprite shape;
        public void SetPath(SpriteRenderer renderer, Vector2 from, Vector2 to, Vector2 size)
        {
            Vector2 center = (from + to) / 2;
            Vector2 bounds = new Vector2(Mathf.Abs(to.x - from.x) + size.x, Mathf.Abs(to.y - from.y) + size.y);
            var points = new List<Vector2>(8);
            foreach (var p in new[] { from, to })
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        points.Add(p + new Vector2(size.x * x / 2, size.y * y / 2));
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>();
            foreach (var p in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                var p = points[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            var vertices = new Vector2[hull.Count];
            // OverrideGeometry takes Sprite Rect coordinates, not pivot-relative
            // positions. Unity applies the centered pivot itself. Clamp rounding
            // error so every point stays inside this 1x1 pixel rectangle.
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector2(
                    Mathf.Clamp01((hull[i].x - center.x) / bounds.x + 0.5f),
                    Mathf.Clamp01((hull[i].y - center.y) / bounds.y + 0.5f));
            var triangles = new ushort[(hull.Count - 2) * 3];
            for (int i = 0; i < hull.Count - 2; i++)
            { triangles[i * 3] = 0; triangles[i * 3 + 1] = (ushort)(i + 1); triangles[i * 3 + 2] = (ushort)(i + 2); }
            if (shape == null)
            {
                shape = Sprite.Create(renderer.sprite.texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
                renderer.sprite = shape;
            }
            shape.OverrideGeometry(vertices, triangles);
            transform.localPosition = center; transform.localScale = new Vector3(bounds.x, bounds.y, 1);
        }
        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        void OnDestroy() { if (shape != null) Destroy(shape); }
    }
}
