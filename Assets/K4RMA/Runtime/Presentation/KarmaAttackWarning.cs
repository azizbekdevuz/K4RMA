using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    // Convex hull of the two endpoint boxes: exact footprint of a swept AABB.
    // Unlike a diagonal bounding rectangle, this preserves the real safe corners.
    public sealed class KarmaAttackWarning : MonoBehaviour
    {
        Sprite shape;
        SpriteRenderer visual;
        LineRenderer edge, arrow;
        Color baseColor;
        public void SetPath(SpriteRenderer renderer, Vector2 from, Vector2 to, Vector2 size)
        {
            if (visual == null) { visual = renderer; baseColor = renderer.color; }
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
            if (edge == null)
            {
                var go = new GameObject("Locked danger outline"); go.transform.SetParent(transform, false);
                edge = go.AddComponent<LineRenderer>(); edge.useWorldSpace = false; edge.loop = true;
                edge.sharedMaterial = KarmaCombatVFX.GlowMaterial != null ? KarmaCombatVFX.GlowMaterial : renderer.sharedMaterial;
                edge.sortingOrder = 16; edge.numCornerVertices = 2;
            }
            edge.positionCount = hull.Count;
            for (int i = 0; i < hull.Count; i++) edge.SetPosition(i, new Vector3((hull[i].x - center.x) / bounds.x, (hull[i].y - center.y) / bounds.y, 0));
            if (arrow == null)
            {
                var go = new GameObject("Attack direction"); go.transform.SetParent(transform, false);
                arrow = go.AddComponent<LineRenderer>(); arrow.useWorldSpace = false;
                arrow.sharedMaterial = edge.sharedMaterial; arrow.sortingOrder = 17; arrow.numCornerVertices = 2;
            }
            arrow.enabled = (to - from).sqrMagnitude > 0.01f;
            if (arrow.enabled)
            {
                Vector2 axis = (to - from).normalized, normal = new Vector2(-axis.y, axis.x);
                float length = Mathf.Min(KarmaSpace.Units(0.5f), Vector2.Distance(from, to) * 0.2f, Mathf.Min(size.x, size.y) * 0.45f);
                Vector2 tip = Vector2.Lerp(from, to, 0.7f);
                var head = new[] { tip - axis * length + normal * length, tip, tip - axis * length - normal * length };
                arrow.positionCount = 3;
                for (int i = 0; i < 3; i++) arrow.SetPosition(i, new Vector3((head[i].x - center.x) / bounds.x, (head[i].y - center.y) / bounds.y, 0));
            }
        }
        void Update()
        {
            if (visual == null || edge == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 15);
            var fill = baseColor; fill.a = baseColor.a * (0.6f + pulse * 0.35f); visual.color = fill;
            var color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.7f + pulse * 0.3f);
            arrow.startColor = arrow.endColor = color; arrow.widthMultiplier = KarmaSpace.Units(0.055f);
            edge.startColor = edge.endColor = color; edge.widthMultiplier = KarmaSpace.Units(0.035f + pulse * 0.015f);
        }
        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        void OnDestroy() { if (shape != null) Destroy(shape); }
    }
}
