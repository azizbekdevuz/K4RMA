using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace KarmaPrototype
{
    // Preserve the authored body/feet and rigid steel length. A small forearm
    // adjustment registers the reverse-grip contact to the real floor.
    public sealed class KarmaSwordGroundPose : MonoBehaviour
    {
        KarmaSwordBasicArt art;
        SpriteRenderer picture;
        Sprite[] drawings;
        Sprite idleShield;
        struct GripPose
        {
            public Vector2 source, target;
            public Quaternion turn;
        }
        GripPose[] grips;
        bool sharedPlant;
        const int Columns = 32, Rows = 40;

        internal float BladeLength { get; private set; }

        internal void Configure(KarmaSwordBasicArt source, SpriteRenderer renderer, float bladeLength)
        {
            art = source; picture = renderer;
            BladeLength = bladeLength;
            drawings = new Sprite[art.poses.Length];
            grips = new GripPose[art.poses.Length];
            sharedPlant = art.poses[3] == art.poses[4] || art.poses[5] == art.poses[4];
            Vector2 impact = Socket(4);
            float floorOffset = BladeLength - impact.y;
            for (int frame = 0; frame < grips.Length; frame++)
            {
                Vector2 target = Socket(frame);
                bool stroke = frame >= 3 && frame <= 5;
                // A shared contact drawing has one painted fist. Its other keys
                // describe where that fist moves on the descent and withdrawal.
                bool usesContact = stroke && art.poses[frame] == art.poses[4];
                grips[frame] = new GripPose {
                    source = usesContact ? impact : target,
                    target = target + Vector2.up * (stroke ? floorOffset * (usesContact ? 1f : 0.5f) : 0f),
                    turn = usesContact ? Quaternion.Euler(0, 0, art.angles[frame] - art.angles[4]) : Quaternion.identity
                };
            }
        }
        Vector2 Socket(int frame)
        {
            var s = art.poses[frame]; var p = art.sockets[frame];
            return (new Vector2(p.x, s.rect.height - p.y) - s.pivot) / s.pixelsPerUnit;
        }
        internal Vector2 Grip(int frame) => grips[frame].target;

        // Reuse the intact shield painted on WaveFinal's idle texture. The
        // convex outline excludes the skirt and shoulder behind its left rim.
        internal Sprite CutIdleShield(float bodyScale)
        {
            var idle = art.idle;
            float scale = idle.rect.width / 512f;
            const float left = 375, top = 124, width = 92, height = 162;
            var rect = new Rect(idle.rect.x + left * scale, idle.rect.yMax - (top + height) * scale,
                width * scale, height * scale);
            idleShield = Sprite.Create(idle.texture, rect, new Vector2(.5f, .5f),
                idle.pixelsPerUnit * bodyScale, 0, SpriteMeshType.FullRect);
            idleShield.name = "SW03 forearm shield";
            idleShield.hideFlags = HideFlags.HideAndDontSave;
            Vector2[] rim = {
                new(396,128), new(402,128), new(410,132), new(422,143), new(435,160),
                new(447,181), new(457,205), new(463,230), new(465,246), new(464,263),
                new(460,275), new(454,282), new(446,284), new(437,282), new(426,276),
                new(415,265), new(402,247), new(391,225), new(383,202), new(378,180),
                new(377,163), new(378,147), new(382,136), new(388,130)
            };
            idleShield.SetVertexCount(rim.Length);
            var positions = new NativeArray<Vector3>(rim.Length, Allocator.Temp);
            var uv = new NativeArray<Vector2>(rim.Length, Allocator.Temp);
            var triangles = new NativeArray<ushort>((rim.Length - 2) * 3, Allocator.Temp);
            try
            {
                for (int i = 0; i < rim.Length; i++)
                {
                    var pixel = new Vector2(rim[i].x - left, top + height - rim[i].y) * scale;
                    positions[i] = (pixel - idleShield.pivot) / idleShield.pixelsPerUnit;
                    uv[i] = new Vector2((rect.x + pixel.x) / idle.texture.width,
                        (rect.y + pixel.y) / idle.texture.height);
                    if (i < 2) continue;
                    triangles[(i - 2) * 3 + 1] = (ushort)i;
                    triangles[(i - 2) * 3 + 2] = (ushort)(i - 1);
                }
                idleShield.SetVertexAttribute(VertexAttribute.Position, positions);
                idleShield.SetVertexAttribute(VertexAttribute.TexCoord0, uv);
                idleShield.SetIndices(triangles);
            }
            finally { positions.Dispose(); uv.Dispose(); triangles.Dispose(); }
            return idleShield;
        }

        Vector2 Point(Vector2 p, int frame)
        {
            var pose = grips[frame];
            Vector2 grip = pose.source;
            // Keep the painted fist and guard rigid around the blade socket.
            // Blend their movement into the forearm and pin the soles.
            float forward = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(grip.x - 0.85f, grip.x - 0.22f, p.x));
            float below = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(grip.y - 0.9f, grip.y - 0.45f, p.y));
            float above = sharedPlant ? Mathf.InverseLerp(0.38f, 1.58f, p.y - grip.y)
                : Mathf.InverseLerp(0.55f, 0.95f, p.y - grip.y);
            float weight = forward * below * (1 - Mathf.SmoothStep(0, 1, above));
            // Taper the rotating fist toward the shield rim. A wider vertical
            // blend keeps the mesh from folding when the wrist lifts off the floor.
            if (sharedPlant)
                weight *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.27f, 1.12f, p.x - grip.x));
            Vector2 moved = pose.target + (Vector2)(pose.turn * (p - grip));
            return Vector2.LerpUnclamped(p, moved, weight);
        }
        internal bool Matches(Sprite sprite, int frame)
            => sprite != null && (sprite == art.poses[frame] || sprite == drawings[frame]);
        internal Sprite Drawing(int frame)
        {
            if (frame < 3 || frame > 5) return art.poses[frame];
            var grip = grips[frame];
            if ((grip.target - grip.source).sqrMagnitude < 0.000001f && grip.turn == Quaternion.identity)
                return art.poses[frame];
            if (drawings[frame] != null) return drawings[frame];
            var source = art.poses[frame];
            var result = Instantiate(source); result.name = source.name; result.hideFlags = HideFlags.HideAndDontSave;
            int count = (Columns + 1) * (Rows + 1);
            result.SetVertexCount(count);
            var positions = new NativeArray<Vector3>(count, Allocator.Temp);
            var uv = new NativeArray<Vector2>(count, Allocator.Temp);
            var triangles = new NativeArray<ushort>(Columns * Rows * 6, Allocator.Temp);
            try
            {
                for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Columns; x++)
                {
                    int i = y * (Columns + 1) + x;
                    Vector2 pixel = new Vector2(x * source.rect.width / Columns, y * source.rect.height / Rows);
                    positions[i] = Point((pixel - source.pivot) / source.pixelsPerUnit, frame);
                    uv[i] = new Vector2((source.rect.x + pixel.x) / source.texture.width, (source.rect.y + pixel.y) / source.texture.height);
                }
                for (int y = 0, index = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    int a = y * (Columns + 1) + x, b = a + 1, c = a + Columns + 1, d = c + 1;
                    triangles[index++] = (ushort)a; triangles[index++] = (ushort)b; triangles[index++] = (ushort)c;
                    triangles[index++] = (ushort)b; triangles[index++] = (ushort)d; triangles[index++] = (ushort)c;
                }
                result.SetVertexAttribute(VertexAttribute.Position, positions);
                result.SetVertexAttribute(VertexAttribute.TexCoord0, uv); result.SetIndices(triangles);
            }
            finally { positions.Dispose(); uv.Dispose(); triangles.Dispose(); }
            return drawings[frame] = result;
        }
        void OnDisable()
        {
            if (picture == null || art == null) return;
            for (int i = 0; i < drawings.Length; i++)
                if (Matches(picture.sprite, i)) { picture.sprite = art.poses[i]; break; }
        }
        void OnDestroy()
        {
            if (idleShield != null) Destroy(idleShield);
            if (drawings != null) foreach (var drawing in drawings) if (drawing != null) Destroy(drawing);
        }
    }
}
