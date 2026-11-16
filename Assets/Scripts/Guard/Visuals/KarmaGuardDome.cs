using UnityEngine;

namespace KarmaPrototype
{
    // Presentation and collision roof for GD05.
    public sealed class KarmaGuardDome : MonoBehaviour
    {
        const int ArcSegments = 256;
        Color gold = new Color(1, 0.78f, 0.43f);
        KarmaGame game;
        Sprite fillShape;
        SpriteRenderer fill;
        LineRenderer floor;
        Mesh rimShape, pressureShape;
        MeshRenderer rim, pressureFlash;
        Vector3[] rimVertices;
        Color[] rimColors;
        float rimWidth = -1;
        MaterialPropertyBlock lightProperties;
        MeshRenderer[] junctionFlares;
        SpriteRenderer[] ornaments;
        Vector3[] ornamentHomes;
        LineRenderer[] releaseMotes, releaseThreads;
        LineRenderer[] cracks, crackGlows, fragmentEdges, fragmentGlows, blastRays;
        Vector2[][] crackPaths;
        float[][] crackDistances;
        // Seconds within the existing warning; children start after their parent reaches the junction.
        readonly float[] crackStarts = { 0, 0.12f, 0.02f, 0.12f, 0.28f, 0.04f, 0.13f, 0.28f, 0.25f, 0.25f, 0.28f, 0.30f, 0.26f, 0.29f, 0.28f, 0.38f, 0.42f, 0.30f, 0.43f, 0.45f, 0.37f };
        readonly float[] crackDurations = { 0.12f, 0.12f, 0.10f, 0.12f, 0.16f, 0.09f, 0.12f, 0.17f, 0.22f, 0.23f, 0.19f, 0.17f, 0.21f, 0.20f, 0.12f, 0.11f, 0.08f, 0.12f, 0.07f, 0.05f, 0.12f };
        Vector2[][] platePaths;
        LineRenderer[] fragmentHairlines;
        Transform[] fragments;
        Vector2[] fragmentHomes;
        SpriteRenderer[] fragmentFaces;
        Sprite[] fragmentShapes;
        SpriteRenderer blast, blastGlow;
        LineRenderer shock, groundShock;
        float warningAge;
        EdgeCollider2D roof;
        float age, dissolveAge;
        bool dissolving, warning, exploding;
        public float Radius { get; private set; }

        public static KarmaGuardDome Create(KarmaGame game, Transform parent, Vector2 center, float radius)
        {
            var root = new GameObject("GD05 금빛 봉인진");
            root.transform.SetParent(parent, false); root.transform.localPosition = center;
            var dome = root.AddComponent<KarmaGuardDome>();
            dome.game = game; dome.Radius = radius; dome.Build();
            return dome;
        }
        Vector2 Arc(float angle, float radius) { return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; }
        LineRenderer Line(string name, Transform parent, Vector2[] points, float width, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.positionCount = points.Length;
            line.widthMultiplier = width; line.numCornerVertices = 2; line.numCapVertices = 2;
            line.sharedMaterial = KarmaCombatVFX.GlowMaterial != null ? KarmaCombatVFX.GlowMaterial : KarmaVisuals.DefaultMaterial;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
            Tint(line, 0.8f); return line;
        }
        void Tint(LineRenderer line, float alpha)
        { line.startColor = line.endColor = new Color(gold.r, gold.g, gold.b, alpha); }
        Vector2[] ArcPoints(float radius, float first, float last, int samples)
        {
            var points = new Vector2[samples];
            for (int i = 0; i < samples; i++) points[i] = Arc(Mathf.Lerp(first, last, i / (float)(samples - 1)), radius);
            return points;
        }
        void Build()
        {
            lightProperties = new MaterialPropertyBlock();
            fill = KarmaVisuals.Box(transform, "보호막 반투명 내부", Vector2.up * Radius / 2,
                new Vector2(Radius * 2, Radius), Color.white, 1);
            fillShape = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            fillShape.name = "GD05 semicircle fill";
            var vertices = new Vector2[ArcSegments + 2]; var triangles = new ushort[ArcSegments * 3];
            vertices[0] = new Vector2(0.5f, 0);
            for (int i = 0; i <= ArcSegments; i++)
            {
                float a = Mathf.PI * i / ArcSegments;
                vertices[i + 1] = new Vector2(Mathf.Clamp01(0.5f + Mathf.Cos(a) * 0.5f), Mathf.Clamp01(Mathf.Sin(a)));
                if (i < ArcSegments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = (ushort)(i + 1); triangles[i * 3 + 2] = (ushort)(i + 2); }
            }
            fillShape.OverrideGeometry(vertices, triangles); fill.sprite = fillShape;
            var fillProperties = new MaterialPropertyBlock(); fillProperties.SetTexture("_MainTex", Texture2D.whiteTexture); fill.SetPropertyBlock(fillProperties);
            BuildRim();
            var roofPoints = ArcPoints(Radius, 0, Mathf.PI, 65);
            roof = gameObject.AddComponent<EdgeCollider2D>(); roof.points = roofPoints;
            gameObject.AddComponent<KarmaSurface>();
            floor = Line("결계 바닥선", transform, new[] { Vector2.left * Radius, Vector2.right * Radius }, KarmaSpace.Units(0.04f), 4);
            BuildOrnaments();
            Paint();
        }

        MeshRenderer MeshLayer(string name, Mesh shape, int order)
        {
            var node = new GameObject(name); node.transform.SetParent(transform, false);
            node.AddComponent<MeshFilter>().sharedMesh = shape;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = KarmaCombatVFX.GlowMaterial; renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            lightProperties.Clear(); lightProperties.SetTexture("_MainTex", Texture2D.whiteTexture);
            lightProperties.SetColor("unity_SpriteColor", Color.white); lightProperties.SetVector("unity_SpriteProps", new Vector4(1, 1, 0, 0));
            lightProperties.SetColor("_Color", Color.white); renderer.SetPropertyBlock(lightProperties);
            return renderer;
        }
        void BuildRim()
        {
            // Three continuous ribbons: outer halo, outer core and inner core. Transparent edge rows
            // soften subpixel coverage without changing camera AA or the 65-point collision roof.
            int count = (ArcSegments + 1) * 4 * 3;
            rimVertices = new Vector3[count]; rimColors = new Color[count];
            var uv = new Vector2[count]; var triangles = new int[ArcSegments * 3 * 3 * 6];
            for (int i = 0; i < count; i++) uv[i] = Vector2.one * 0.5f;
            int triangle = 0;
            for (int band = 0; band < 3; band++) for (int i = 0; i < ArcSegments; i++) for (int row = 0; row < 3; row++)
            {
                int a = band * (ArcSegments + 1) * 4 + i * 4 + row, b = a + 4;
                triangles[triangle++] = a; triangles[triangle++] = b; triangles[triangle++] = a + 1;
                triangles[triangle++] = a + 1; triangles[triangle++] = b; triangles[triangle++] = b + 1;
            }
            rimShape = new Mesh { name = "GD05 continuous feathered semicircle" }; rimShape.MarkDynamic();
            rimShape.vertices = rimVertices; rimShape.uv = uv; rimShape.triangles = triangles;
            rim = MeshLayer("Continuous golden semicircle", rimShape, 12);
        }
        void PaintRim(float fade, float pulse, bool release)
        {
            float t = Mathf.Clamp01(dissolveAge / 0.4f);
            float flash = release ? Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) : 0;
            float width = 1 + flash * 1.1f; bool resize = !Mathf.Approximately(width, rimWidth);
            for (int band = 0; band < 3; band++) for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = Mathf.PI * i / ArcSegments;
                float radius = Radius * (band == 2 ? 0.985f : 1);
                float half = band == 0 ? Radius * 0.003f : KarmaSpace.Units(band == 1 ? 0.0175f : 0.0125f) * width;
                float feather = Radius * (band == 0 ? 0.007f : 0.0015f);
                var color = release ? new Color(1, 0.92f, 0.68f) : gold;
                float alpha = release ? (band == 2 ? (0.4f + flash * 0.45f) * (1 - Mathf.Clamp01(t / 0.65f)) : 1 - ReleaseAt(angle, t))
                    : (band == 2 ? 0.4f : 0.75f + pulse * 0.2f) * fade;
                if (band == 0) { color = new Color(1, 0.44f, 0.08f); alpha *= 0.18f; }
                for (int row = 0; row < 4; row++)
                {
                    int index = band * (ArcSegments + 1) * 4 + i * 4 + row;
                    if (resize)
                    {
                        float offset = row == 0 ? -half - feather : row == 1 ? -half : row == 2 ? half : half + feather;
                        rimVertices[index] = Arc(angle, radius + offset);
                    }
                    color.a = row == 0 || row == 3 || exploding ? 0 : alpha;
                    rimColors[index] = color;
                }
            }
            if (resize) { rimShape.vertices = rimVertices; rimShape.RecalculateBounds(); rimWidth = width; }
            rimShape.colors = rimColors;
        }

        void BuildOrnaments()
        {
            var seal = Resources.Load<Sprite>("Guard/Effects/GD05_GoldenSeal");
            var foot = Resources.Load<Sprite>("Guard/Effects/GD05_GoldenFoot");
            if (seal == null || foot == null) throw new System.InvalidOperationException("GD05 golden seal artwork missing");
            ornaments = new SpriteRenderer[10]; ornamentHomes = new Vector3[10];
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.PI * (i + 1) / 8;
                AddOrnament(i, seal, Arc(angle, Radius), Radius * (i == 3 ? 0.19f : 0.12f), angle * Mathf.Rad2Deg - 90);
            }
            AddOrnament(7, seal, Vector2.zero, Radius * 0.23f, 0);
            AddOrnament(8, foot, Vector2.left * Radius, Radius * 0.19f, 0);
            AddOrnament(9, foot, Vector2.right * Radius, Radius * 0.19f, 0);
            ornaments[9].flipX = true;
        }
        void AddOrnament(int index, Sprite sprite, Vector2 at, float width, float angle)
        {
            var node = new GameObject("Golden seal ornament " + index); node.transform.SetParent(transform, false);
            node.transform.localPosition = at; node.transform.localRotation = Quaternion.Euler(0, 0, angle);
            node.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
            var renderer = node.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = 13; KarmaVisuals.ApplyMaterial(renderer);
            var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", sprite.texture); renderer.SetPropertyBlock(properties);
            ornaments[index] = renderer; ornamentHomes[index] = at;
        }
        public void WarnFailure()
        {
            warning = true; warningAge = 0; gold = new Color(1, 0.42f, 0.08f);
            if (cracks == null) BuildFailure();
            Paint();
        }
        public void Explode()
        {
            if (cracks == null) BuildFailure();
            exploding = true; Dissolve();
        }
        void BuildFailure()
        {
            // Traced in the approved six-panel reference's first-panel coordinates.
            // Initial hairlines stay fixed as the network spreads. Every closed seam is reused by both plates.
            const float leftRoot = Mathf.PI * 0.75f, rightRoot = 0.58f;
            const float lowerLeft = 2.93f, upperLeft = 2.05f, lowerRight = 0.30f, upperRight = 0.80f;
            crackPaths = new[] {
                Trace(367,28, 370,49, 368,62, 365,78, 368,90, 374,99, 381,110),
                Trace(381,110, 387,123, 385,136, 380,150, 385,162, 386,176, 377,189, 372,201, 367,213),
                Trace(184,104, 192,118, 197,135, 204,145, 209,158, 223,163, 239,170, 253,175),
                Trace(253,175, 264,191, 277,210, 294,229, 308,246, 320,257),
                Trace(320,257, 330,269, 339,276, 346,286, 355,295, 367,305),
                Trace(580,153, 568,162, 559,176, 548,187, 540,203),
                Trace(540,203, 525,211, 515,221, 504,232, 490,242, 478,251, 472,257),
                Trace(472,257, 455,260, 444,268, 426,270, 413,279, 398,283, 385,295, 367,305),
                Trace(381,110, 368,118, 355,122, 348,135, 339,140, 333,155, 324,165, 315,181, 304,192, 293,198, 281,193, 268,184, 253,175),
                Trace(381,110, 393,115, 402,128, 413,133, 418,146, 432,151, 449,156, 462,159, 475,166, 490,165, 507,174, 515,186, 529,191, 540,203),
                Trace(253,175, 241,181, 233,193, 223,197, 216,210, 201,215, 190,227, 174,232, 159,242, 144,245, 127,253, 118,247),
                Trace(540,203, 554,205, 565,216, 579,222, 589,234, 602,241, 611,249),
                Trace(253,175, 252,159, 246,143, 242,129, 245,114, 241,99, 247,85, 251,73),
                Trace(540,203, 547,186, 554,171, 557,151, 550,139, 549,122, 544,107),
                Trace(368,90, 359,83, 354,72, 346,65),
                Trace(339,140, 328,125, 324,110, 316,99),
                Trace(245,114, 233,104, 229,89),
                Trace(294,229, 294,214, 286,204),
                Trace(462,159, 463,172, 470,185, 474,202),
                Trace(557,151, 569,146, 577,150),
                Trace(515,221, 527,226, 538,224, 552,233)
            };
            crackPaths[0][0] = Vector2.up;
            crackPaths[2][0] = Arc(leftRoot, 1); crackPaths[5][0] = Arc(rightRoot, 1);
            crackPaths[10][crackPaths[10].Length - 1] = Arc(lowerLeft, 1);
            crackPaths[11][crackPaths[11].Length - 1] = Arc(lowerRight, 1);
            crackPaths[12][crackPaths[12].Length - 1] = Arc(upperLeft, 1);
            crackPaths[13][crackPaths[13].Length - 1] = Arc(upperRight, 1);
            cracks = new LineRenderer[crackPaths.Length]; crackGlows = new LineRenderer[crackPaths.Length];
            crackDistances = new float[crackPaths.Length][];
            for (int i = 0; i < cracks.Length; i++)
            {
                cracks[i] = Line("Failure traced fissure " + i, transform, crackPaths[i], Radius * (i < 14 ? 0.0024f : 0.0013f), 15);
                crackGlows[i] = Line("Failure fissure amber glow " + i, transform, crackPaths[i], Radius * (i < 14 ? 0.009f : 0.005f), 14);
                var distances = crackDistances[i] = new float[crackPaths[i].Length];
                for (int p = 1; p < distances.Length; p++)
                    distances[p] = distances[p - 1] + Vector2.Distance(crackPaths[i][p - 1], crackPaths[i][p]);
            }
            platePaths = new[] {
                Join(ArcPoints(1, Mathf.PI, lowerLeft, 5), Seam(10, true), Seam(3), Seam(4)),
                Join(ArcPoints(1, lowerLeft, leftRoot, 9), Seam(2), Seam(10)),
                Join(ArcPoints(1, leftRoot, upperLeft, 6), Seam(12, true), Seam(2, true)),
                Join(ArcPoints(1, upperLeft, Mathf.PI / 2, 9), Seam(0), Seam(8), Seam(12)),
                Join(ArcPoints(1, Mathf.PI / 2, upperRight, 12), Seam(13, true), Seam(9, true), Seam(0, true)),
                Join(ArcPoints(1, upperRight, rightRoot, 5), Seam(5), Seam(13)),
                Join(ArcPoints(1, rightRoot, lowerRight, 5), Seam(11, true), Seam(5, true)),
                Join(ArcPoints(1, lowerRight, 0, 5), new[] { Vector2.zero }, Seam(7, true), Seam(6, true), Seam(11)),
                Join(Seam(8), Seam(3), Seam(4), Seam(7, true), Seam(6, true), Seam(9, true))
            };
            int count = platePaths.Length;
            fragments = new Transform[count]; fragmentHomes = new Vector2[count];
            fragmentFaces = new SpriteRenderer[count]; fragmentEdges = new LineRenderer[count]; fragmentGlows = new LineRenderer[count]; fragmentShapes = new Sprite[count];
            for (int i = 0; i < count; i++) BuildPlate(i, platePaths[i]);
            var detailPaths = new[] { 1, 14, 15, 16, 17, 18, 19, 20 };
            var detailPlates = new[] { 8, 3, 3, 2, 8, 8, 5, 7 };
            fragmentHairlines = new LineRenderer[detailPaths.Length];
            for (int i = 0; i < detailPaths.Length; i++)
            {
                var detail = Seam(detailPaths[i]); int plate = detailPlates[i];
                for (int p = 0; p < detail.Length; p++) detail[p] = detail[p] * Radius - fragmentHomes[plate];
                fragmentHairlines[i] = Line("Traced hairline on flying plate", fragments[plate], detail, Radius * (i == 0 ? 0.0024f : 0.0013f), 15);
            }
            var sprite = Resources.Load<Sprite>("Guard/Effects/GD05_InternalBlast");
            if (sprite == null) throw new System.InvalidOperationException("GD05 internal blast artwork missing");
            var node = new GameObject("GD05 internal detonation"); node.transform.SetParent(transform, false);
            blast = node.AddComponent<SpriteRenderer>(); blast.sprite = sprite; blast.sortingOrder = 16;
            KarmaVisuals.ApplyMaterial(blast);
            var properties = new MaterialPropertyBlock(); properties.SetTexture("_MainTex", sprite.texture); blast.SetPropertyBlock(properties);
            node = new GameObject("GD05 detonation radiance"); node.transform.SetParent(transform, false);
            blastGlow = node.AddComponent<SpriteRenderer>(); blastGlow.sprite = sprite; blastGlow.sortingOrder = 15;
            blastGlow.sharedMaterial = KarmaCombatVFX.GlowMaterial; blastGlow.SetPropertyBlock(properties);
            BuildPressureLight();
            shock = Line("Expelled pressure wave", transform, ArcPoints(Radius, 0, Mathf.PI, 65), Radius * 0.003f, 14);
            groundShock = Line("Ground pressure sweep", transform, new Vector2[65], Radius * 0.004f, 14);
            blastRays = new LineRenderer[18];
            for (int i = 0; i < blastRays.Length; i++)
                blastRays[i] = Line("Outward blast streak", transform, new Vector2[2], Radius * 0.002f, 14);
        }
        void BuildPressureLight()
        {
            var vertices = new Vector3[65]; var colors = new Color[65]; var uv = new Vector2[65];
            var triangles = new int[64 * 3]; colors[0] = Color.white;
            for (int i = 0; i < 65; i++) uv[i] = Vector2.one * 0.5f;
            for (int i = 0; i < 64; i++)
            {
                vertices[i + 1] = Arc(i * Mathf.PI / 32, i % 8 == 0 ? 1 : 0.3f);
                colors[i + 1] = Color.clear;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 64 + 1;
            }
            pressureShape = new Mesh { name = "GD05 pressure light falloff" };
            pressureShape.vertices = vertices; pressureShape.colors = colors; pressureShape.uv = uv; pressureShape.triangles = triangles;
            pressureFlash = MeshLayer("Internal pressure flash", pressureShape, 17);
            pressureFlash.transform.localPosition = Vector2.up * Radius * 0.12f;
            int[] paths = { 0, 2, 5, 8, 9, 12, 13 };
            junctionFlares = new MeshRenderer[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                var path = crackPaths[paths[i]];
                junctionFlares[i] = MeshLayer("Hot crack junction", pressureShape, 16);
                junctionFlares[i].transform.localPosition = path[i < 3 ? path.Length - 1 : path.Length / 2] * Radius;
                junctionFlares[i].transform.localScale = new Vector3(Radius * 0.065f, Radius * 0.045f, 1);
            }
        }
        void Light(MeshRenderer renderer, Color color)
        {
            renderer.enabled = color.a > 0.0001f;
            lightProperties.Clear(); lightProperties.SetTexture("_MainTex", Texture2D.whiteTexture);
            lightProperties.SetColor("unity_SpriteColor", Color.white); lightProperties.SetVector("unity_SpriteProps", new Vector4(1, 1, 0, 0));
            lightProperties.SetColor("_Color", color); renderer.SetPropertyBlock(lightProperties);
        }
        Vector2[] Trace(params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2((xy[i * 2] - 367) / 255, (305 - xy[i * 2 + 1]) / 277);
            return points;
        }
        Vector2[] Seam(int index, bool reverse = false)
        {
            var points = (Vector2[])crackPaths[index].Clone();
            if (reverse) System.Array.Reverse(points);
            return points;
        }
        Vector2[] Join(params Vector2[][] sections)
        {
            var points = new System.Collections.Generic.List<Vector2>();
            foreach (var section in sections) foreach (var point in section)
                if (points.Count == 0 || (point - points[points.Count - 1]).sqrMagnitude > 0.00000001f) points.Add(point);
            if ((points[0] - points[points.Count - 1]).sqrMagnitude < 0.00000001f) points.RemoveAt(points.Count - 1);
            return points.ToArray();
        }
        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        static ushort[] Triangulate(Vector2[] points)
        {
            // Ear clipping keeps concave traced edges intact; a center fan overlaps outside these plates.
            var remaining = new System.Collections.Generic.List<int>();
            float area = 0;
            for (int i = 0; i < points.Length; i++) { remaining.Add(i); area += Cross(points[i], points[(i + 1) % points.Length]); }
            float winding = Mathf.Sign(area);
            var triangles = new System.Collections.Generic.List<ushort>();
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[b] - points[a], points[c] - points[b]) * winding <= 0.00000001f) continue;
                    bool occupied = false;
                    foreach (int p in remaining)
                    {
                        if (p == a || p == b || p == c) continue;
                        if (Cross(points[b] - points[a], points[p] - points[a]) * winding >= -0.00000001f
                            && Cross(points[c] - points[b], points[p] - points[b]) * winding >= -0.00000001f
                            && Cross(points[a] - points[c], points[p] - points[c]) * winding >= -0.00000001f) { occupied = true; break; }
                    }
                    if (occupied) continue;
                    triangles.Add((ushort)a); triangles.Add((ushort)b); triangles.Add((ushort)c);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) throw new System.InvalidOperationException("GD05 traced plate is not a simple polygon");
            }
            return triangles.ToArray();
        }
        void BuildPlate(int index, Vector2[] points)
        {
            Vector2 center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Length; fragmentHomes[index] = center * Radius;
            var node = new GameObject("GD05 transparent shell plate " + index); node.transform.SetParent(transform, false);
            fragments[index] = node.transform; node.transform.localPosition = center * Radius;
            var edgePoints = new Vector2[points.Length + 1];
            var vertices = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                edgePoints[i] = (points[i] - center) * Radius;
                vertices[i] = (points[i] - center) / 4 + Vector2.one * 0.5f;
            }
            edgePoints[points.Length] = edgePoints[0];
            fragmentGlows[index] = Line("Hot fractured glass edge", node.transform, edgePoints, Radius * 0.012f, 13);
            fragmentEdges[index] = Line("Cracked gold rim", node.transform, edgePoints, Radius * 0.0032f, 14);
            var shape = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            shape.OverrideGeometry(vertices, Triangulate(points)); fragmentShapes[index] = shape;
            var face = new GameObject("Transparent membrane"); face.transform.SetParent(node.transform, false);
            face.transform.localScale = Vector3.one * (Radius * 4);
            var renderer = face.AddComponent<SpriteRenderer>(); renderer.sprite = shape; renderer.sortingOrder = 12;
            KarmaVisuals.ApplyMaterial(renderer); fragmentFaces[index] = renderer;
            var properties = new MaterialPropertyBlock(); properties.SetTexture("_MainTex", Texture2D.whiteTexture); renderer.SetPropertyBlock(properties);
        }
        void PaintFailure()
        {
            float t = Mathf.Clamp01(dissolveAge / 0.4f);
            var hot = new Color(1, 0.68f, 0.24f, 1);
            for (int i = 0; i < cracks.Length; i++)
            {
                var path = crackPaths[i];
                float reach = exploding ? 1 : Mathf.Clamp01((warningAge - crackStarts[i]) / crackDurations[i]);
                var distances = crackDistances[i]; float distance = distances[distances.Length - 1] * reach;
                int segment = 1;
                while (segment < path.Length - 1 && distances[segment] < distance) segment++;
                var tip = Vector2.Lerp(path[segment - 1], path[segment], Mathf.InverseLerp(distances[segment - 1], distances[segment], distance));
                for (int p = 0; p < path.Length; p++)
                {
                    Vector2 point = (distances[p] <= distance ? path[p] : tip) * Radius;
                    cracks[i].SetPosition(p, point); crackGlows[i].SetPosition(p, point);
                }
                float alpha = exploding ? Mathf.Clamp01(1 - t * 6) : Mathf.Clamp01(reach * 8);
                cracks[i].startColor = cracks[i].endColor = new Color(1, 0.62f, 0.22f, alpha);
                crackGlows[i].startColor = crackGlows[i].endColor = new Color(1, 0.27f, 0.025f, alpha * (0.20f + Mathf.Clamp01((warningAge - 0.24f) / 0.26f) * 0.35f));
            }
            // Briefly open the shared seams, then drive the pieces outward from the internal blast.
            float fly = exploding ? Mathf.Clamp01((t - 0.08f) / 0.92f) : 0;
            float opening = exploding ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.12f)) * 0.025f : 0;
            float plateAlpha = exploding ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.90f, 1, t)) : 0;
            for (int i = 0; i < fragments.Length; i++)
            {
                var home = fragmentHomes[i];
                Vector2 direction = (home - Vector2.up * Radius * 0.12f).normalized;
                if (i == 0 || i == 7) direction = new Vector2(Mathf.Sign(home.x), 0.12f);
                Vector2 position = home + direction * Radius * (opening + fly * 0.85f) + Vector2.down * Radius * fly * fly * 0.95f;
                float angle = (i % 2 == 0 ? 1 : -1) * fly * (18 + i * 5);
                var rotation = Quaternion.Euler(0, 0, angle);
                float bottom = float.PositiveInfinity, initialBottom = float.PositiveInfinity;
                foreach (var point in platePaths[i])
                {
                    bottom = Mathf.Min(bottom, position.y + (rotation * (Vector3)(point * Radius - home)).y);
                    initialBottom = Mathf.Min(initialBottom, point.y);
                }
                // A visual ground rebound only; these pieces never own collision or damage.
                if (bottom < 0)
                {
                    float speedY = direction.y * 0.85f;
                    float landing = (speedY + Mathf.Sqrt(speedY * speedY + 3.8f * initialBottom)) / 1.9f;
                    float bounce = Mathf.Max(0, Mathf.Sin(Mathf.Clamp01((fly - landing) / 0.24f) * Mathf.PI)) * Radius * 0.035f;
                    position.y += -bottom + bounce;
                }
                fragments[i].localPosition = position; fragments[i].localRotation = rotation;
                fragmentFaces[i].color = new Color(0.83f, 0.89f, 1, plateAlpha * 0.07f);
                fragmentEdges[i].startColor = fragmentEdges[i].endColor = new Color(1, 0.77f, 0.38f, plateAlpha);
                fragmentGlows[i].startColor = fragmentGlows[i].endColor = new Color(1, 0.36f, 0.035f, plateAlpha * 0.28f);
            }
            hot.a = plateAlpha * 0.85f;
            foreach (var hairline in fragmentHairlines) hairline.startColor = hairline.endColor = hot;
            float burst = exploding ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.30f, 0.65f, t)) : 0;
            blast.color = new Color(1, 1, 1, burst);
            float expansion = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.32f));
            blast.transform.localScale = Vector3.one * (Radius * Mathf.Lerp(0.28f, 1.65f, expansion) / blast.sprite.bounds.size.x);
            blastGlow.transform.localScale = blast.transform.localScale * 1.12f;
            blastGlow.color = new Color(1, 0.65f, 0.22f, burst * 0.48f);
            float flash = exploding ? Mathf.Max(0, Mathf.Sin(Mathf.Clamp01((t + 0.04f) / 0.40f) * Mathf.PI)) : 0;
            Light(pressureFlash, new Color(1, 0.92f, 0.62f, flash * 0.9f));
            pressureFlash.transform.localScale = new Vector3(Radius * Mathf.Lerp(0.15f, 1.0f, expansion), Radius * Mathf.Lerp(0.12f, 0.7f, expansion), 1);
            for (int i = 0; i < junctionFlares.Length; i++)
            {
                float start = i < 3 ? 0.16f + i * 0.01f : 0.39f + (i - 3) * 0.016f;
                float strength = exploding ? Mathf.Clamp01(1 - t * 8) : Mathf.SmoothStep(0, 1, Mathf.Clamp01((warningAge - start) / 0.06f));
                Light(junctionFlares[i], new Color(1, 0.66f, 0.18f, strength));
            }
            float pressure = exploding ? Mathf.Sin(Mathf.Clamp01(t / 0.8f) * Mathf.PI) : 0;
            hot.a = pressure * 0.75f; shock.startColor = shock.endColor = hot;
            hot.a = pressure * 0.9f; groundShock.startColor = groundShock.endColor = hot;
            for (int i = 0; i < 65; i++)
            {
                shock.SetPosition(i, Arc(Mathf.PI * i / 64, Radius * (0.7f + t * 0.65f)));
                float a = Mathf.PI * 2 * i / 64;
                groundShock.SetPosition(i, new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.065f) * Radius * (0.3f + t * 1.15f));
            }
            for (int i = 0; i < blastRays.Length; i++)
            {
                float a = Mathf.PI * (i + 0.35f + Mathf.Sin(i * 2.1f) * 0.18f) / blastRays.Length;
                var direction = Arc(a, 1);
                float distance = Radius * (0.15f + t * 1.45f);
                blastRays[i].SetPosition(0, direction * distance);
                blastRays[i].SetPosition(1, direction * (distance + Radius * (0.06f + (i % 3) * 0.025f)));
                hot.a = pressure * 0.9f; blastRays[i].startColor = hot; hot.a = 0; blastRays[i].endColor = hot;
            }
            if (exploding)
            {
                fill.color = Color.clear;
                floor.startColor = floor.endColor = Color.clear;
                foreach (var ornament in ornaments) { var c = ornament.color; c.a *= Mathf.Clamp01(1 - t * 4); ornament.color = c; }
            }
        }
        public void Dissolve()
        {
            dissolving = true; dissolveAge = 0; roof.enabled = false;
            if (!exploding && !warning && releaseMotes == null) BuildRelease();
            Paint();
        }
        void BuildRelease()
        {
            // Decorative geometry only: owned by the dome and its existing 0.4 second lifetime.
            releaseMotes = new LineRenderer[48];
            for (int i = 0; i < releaseMotes.Length; i++)
            {
                float size = Radius * (0.004f + (i % 3) * 0.001f);
                releaseMotes[i] = Line("Success gold mote", transform,
                    new[] { Vector2.up * size, Vector2.right * size * 0.45f,
                        Vector2.down * size, Vector2.left * size * 0.45f, Vector2.up * size },
                    size * 0.55f, 14);
            }
            releaseThreads = new LineRenderer[9];
            for (int i = 0; i < releaseThreads.Length; i++)
                releaseThreads[i] = Line("Success unravelling light", transform, new Vector2[12], Radius * 0.0025f, 14);
        }
        float ReleaseAt(float angle, float progress)
        {
            float fromTop = Mathf.Abs(angle - Mathf.PI * 0.5f) / (Mathf.PI * 0.5f);
            return Mathf.Clamp01((progress - 0.12f - fromTop * 0.35f) / 0.35f);
        }
        void PaintRelease()
        {
            float t = Mathf.Clamp01(dissolveAge / 0.4f);
            var light = new Color(1, 0.92f, 0.68f);
            float flash = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI);
            fill.color = new Color(1, 0.85f, 0.55f, (0.035f + flash * 0.025f) * (1 - t));
            PaintRim(1, 0, true);
            light.a = (0.5f + flash * 0.4f) * (1 - t);
            floor.startColor = floor.endColor = light;
            for (int i = 0; i < ornaments.Length; i++)
            {
                float gone = i < 7 ? ReleaseAt(Mathf.PI * (i + 1) / 8, t) : t;
                ornaments[i].color = new Color(1 + flash * 0.65f, 1 + flash * 0.55f, 1 + flash * 0.35f, 1 - gone);
            }
            for (int i = 0; i < releaseMotes.Length; i++)
            {
                float angle = Mathf.PI * (i + 0.5f) / releaseMotes.Length;
                float life = ReleaseAt(angle, t);
                var home = Arc(angle, Radius * (i % 3 == 0 ? 0.985f : 1));
                var drift = new Vector2(Mathf.Sin(i * 2.4f + life * 3) * 0.012f, life * 0.09f) * Radius;
                releaseMotes[i].transform.localPosition = home + drift;
                light.a = Mathf.Sin(life * Mathf.PI) * (1 - t) * 1.6f;
                releaseMotes[i].startColor = releaseMotes[i].endColor = light;
            }
            for (int i = 0; i < releaseThreads.Length; i++)
            {
                float angle = Mathf.PI * (i + 1) / 10;
                float life = ReleaseAt(angle, t);
                Vector2 home = Arc(angle, Radius);
                var line = releaseThreads[i];
                for (int j = 0; j < line.positionCount; j++)
                {
                    float u = j / (float)(line.positionCount - 1);
                    line.SetPosition(j, home + new Vector2(Mathf.Sin(u * 5 + i) * life * 0.024f,
                        u * life * 0.1f + life * 0.025f) * Radius);
                }
                light.a = Mathf.Sin(life * Mathf.PI) * (1 - t) * 0.8f;
                line.startColor = light; light.a = 0; line.endColor = light;
            }
        }
        void Update()
        {
            if (game == null || game.Paused) return;
            age += Time.deltaTime;
            if (warning && !exploding) warningAge += Time.deltaTime;
            if (dissolving) dissolveAge += Time.deltaTime;
            Paint();
        }
        void Paint()
        {
            float fade = 1 - Mathf.Clamp01(dissolveAge / 0.4f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * (warning ? 18 : 3));
            float opacity = exploding ? 0.45f : warning ? 0.09f + pulse * 0.08f : 0.035f + pulse * 0.015f;
            fill.color = new Color(gold.r, gold.g, gold.b, opacity * fade);
            bool releasing = dissolving && !exploding && !warning && releaseMotes != null;
            if (!releasing) PaintRim(fade, pulse, false);
            Tint(floor, 0.5f * fade);
            if (ornaments != null) for (int i = 0; i < ornaments.Length; i++)
            {
                var color = warning ? new Color(1, 0.28f, 0.08f) : Color.white;
                color.a = (0.82f + pulse * 0.12f) * fade;
                ornaments[i].color = color;
                ornaments[i].transform.localPosition = ornamentHomes[i] * (exploding ? 1 + dissolveAge * 1.5f : 1);
            }
            if (releasing) PaintRelease();
            if ((warning || exploding) && cracks != null) PaintFailure();
        }
        void OnDestroy()
        {
            if (fillShape != null) { if (Application.isPlaying) Destroy(fillShape); else DestroyImmediate(fillShape); }
            if (rimShape != null) { if (Application.isPlaying) Destroy(rimShape); else DestroyImmediate(rimShape); }
            if (pressureShape != null) { if (Application.isPlaying) Destroy(pressureShape); else DestroyImmediate(pressureShape); }
            if (fragmentShapes != null) foreach (var shape in fragmentShapes)
                if (shape != null) { if (Application.isPlaying) Destroy(shape); else DestroyImmediate(shape); }
        }
    }
}
