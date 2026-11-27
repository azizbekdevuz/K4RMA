using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace KarmaPrototype
{
    // Small, planted weight transfers on the approved drawings. Each immutable
    // render sprite keeps its original UVs, so arms/armour never get repainted
    // and captured afterimages retain the exact pose they were emitted with.
    [DisallowMultipleComponent]
    public sealed class KarmaSwordCastWeight : MonoBehaviour
    {
        const int Columns = 16, Rows = 28;
        // Open the chest after contact so the unchanged down-pointing blade
        // clears the floor while the bent knees still carry the follow-through.
        static readonly float[] Lean = { 0, 1.5f, 3.5f, -1.5f, -7f, -2.5f, -2f, 0, 0.5f };
        static readonly float[] Shift = { 0, -0.012f, -0.028f, 0.008f, 0.040f, 0.030f, 0.012f, 0, -0.008f };
        static readonly float[] Sink = { 0, 0.010f, 0.024f, 0.017f, 0.037f, 0.027f, 0.010f, 0, 0.008f };
        static readonly float[] Knee = { 0, 0.006f, 0.012f, 0.010f, 0.024f, 0.016f, 0.006f, 0, 0.006f };
        readonly Sprite[,] drawings = new Sprite[2, Lean.Length];
        readonly Sprite[] basicDrawings = new Sprite[6];
        KarmaSwordBasicArt art;
        KarmaSwordBasicArt basicArt;
        SpriteRenderer picture;
        float height;

        internal void Configure(KarmaSwordBasicArt source, SpriteRenderer renderer, float bodyHeight)
        { art = source; picture = renderer; height = bodyHeight; }
        internal void ConfigureBasic(KarmaSwordBasicArt source) => basicArt = source;

        internal float Angle(int frame, bool heavy, float strengthScale = 1) => Lean[frame] * (heavy ? 1.12f : 1) * strengthScale;

        internal Vector2 Point(Vector2 point, int frame, bool heavy, float strengthScale = 1)
        {
            float strength = (heavy ? 1.12f : 1) * strengthScale;
            Vector2 hip = new Vector2(0, height * 0.54f);
            float radians = Angle(frame, heavy, strengthScale) * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            Vector2 offset = point - hip;
            Vector2 upper = hip + new Vector2(c * offset.x - s * offset.y, s * offset.x + c * offset.y)
                + height * strength * new Vector2(Shift[frame], -Sink[frame]);
            // Both soles/ankles stay planted. Bend through the knees below the
            // robe; the chest, shoulders, elbows and hilt move as one rigid group.
            float leg = Mathf.InverseLerp(height * 0.11f, height * 0.35f, point.y);
            Vector2 posed = Vector2.Lerp(point, upper, Mathf.SmoothStep(0, 1, leg));
            float frontLeg = Mathf.Lerp(0.45f, 1, Mathf.InverseLerp(-height * 0.16f, height * 0.16f, point.x));
            posed.x += height * Knee[frame] * strength * Mathf.Sin(leg * Mathf.PI) * frontLeg;
            return posed;
        }

        internal bool Matches(Sprite sprite, int frame)
            => sprite != null && (sprite == art.poses[frame] || sprite == drawings[0, frame] || sprite == drawings[1, frame]);

        internal Sprite Drawing(int frame, bool heavy)
        {
            if (frame == 0 || frame == 7) return art.poses[frame];
            int variant = heavy ? 1 : 0;
            if (drawings[variant, frame] != null) return drawings[variant, frame];
            return drawings[variant, frame] = CreateDrawing(art.poses[frame], frame, heavy, 1);
        }

        // The basic down-point is steeper than SW01/SW02. Open the body sooner
        // in this one phase so the existing blade stays above the planted feet.
        static float BasicStrength(int phase) => phase == 5 ? 0.6f : 1;
        internal Vector2 BasicPoint(Vector2 point, int phase) => Point(point, phase, false, BasicStrength(phase));
        internal float BasicAngle(int phase) => Angle(phase, false, BasicStrength(phase));
        internal bool MatchesBasic(Sprite sprite, int frame)
            => sprite != null && (sprite == basicArt.poses[frame] || sprite == basicDrawings[frame]);
        internal Sprite BasicDrawing(int frame, int phase)
        {
            if (phase == 7) return basicArt.poses[frame];
            if (basicDrawings[frame] != null) return basicDrawings[frame];
            return basicDrawings[frame] = CreateDrawing(basicArt.poses[frame], phase, false, BasicStrength(phase));
        }

        Sprite CreateDrawing(Sprite source, int frame, bool heavy, float strengthScale)
        {
            Sprite result = Instantiate(source);
            result.name = source.name; result.hideFlags = HideFlags.HideAndDontSave;
            var grid = new Vector2[(Columns + 1) * (Rows + 1)];
            var indices = new ushort[Columns * Rows * 6];
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
                grid[y * (Columns + 1) + x] = new Vector2(x * source.rect.width / Columns, y * source.rect.height / Rows);
            for (int y = 0, index = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                int a = y * (Columns + 1) + x, b = a + 1, c = a + Columns + 1, d = c + 1;
                indices[index++] = (ushort)a; indices[index++] = (ushort)b; indices[index++] = (ushort)c;
                indices[index++] = (ushort)b; indices[index++] = (ushort)d; indices[index++] = (ushort)c;
            }
            // Supply both channels explicitly: geometry coordinates are local
            // units while UVs remain in the original, unmodified sheet rectangle.
            result.SetVertexCount(grid.Length);
            var positions = new NativeArray<Vector3>(grid.Length, Allocator.Temp);
            var textureCoordinates = new NativeArray<Vector2>(grid.Length, Allocator.Temp);
            var triangles = new NativeArray<ushort>(indices, Allocator.Temp);
            try
            {
                for (int i = 0; i < grid.Length; i++)
                {
                    positions[i] = Point((grid[i] - source.pivot) / source.pixelsPerUnit, frame, heavy, strengthScale);
                    textureCoordinates[i] = new Vector2((source.rect.x + grid[i].x) / source.texture.width,
                        (source.rect.y + grid[i].y) / source.texture.height);
                }
                result.SetVertexAttribute(VertexAttribute.Position, positions);
                result.SetVertexAttribute(VertexAttribute.TexCoord0, textureCoordinates);
                result.SetIndices(triangles);
            }
            finally { positions.Dispose(); textureCoordinates.Dispose(); triangles.Dispose(); }
            return result;
        }

        void OnDisable()
        {
            if (picture == null || art == null) return;
            for (int frame = 1; frame < 7; frame++)
                if (Matches(picture.sprite, frame)) { picture.sprite = art.poses[frame]; break; }
            if (basicArt != null)
                for (int frame = 0; frame < basicDrawings.Length; frame++)
                    if (MatchesBasic(picture.sprite, frame)) { picture.sprite = basicArt.poses[frame]; break; }
        }

        void OnDestroy()
        {
            foreach (var sprite in drawings)
                if (sprite != null) Destroy(sprite);
            foreach (var sprite in basicDrawings)
                if (sprite != null) Destroy(sprite);
        }
    }
}
