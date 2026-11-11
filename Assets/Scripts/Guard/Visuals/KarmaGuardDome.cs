using UnityEngine;

namespace KarmaPrototype
{
    // Presentation and collision roof for GD05.
    public sealed class KarmaGuardDome : MonoBehaviour
    {
        const int Sections = 18;
        Color gold = new Color(1, 0.78f, 0.08f);
        KarmaGame game;
        Sprite fillShape;
        SpriteRenderer fill;
        LineRenderer inner, floor;
        LineRenderer[] shell;
        EdgeCollider2D roof;
        float age, dissolveAge;
        bool dissolving, warning, exploding;
        public float Radius { get; private set; }

        public static KarmaGuardDome Create(KarmaGame game, Transform parent, Vector2 center, float radius)
        {
            var root = new GameObject("GD05 노란 반원 보호막");
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
            fill = KarmaVisuals.Box(transform, "보호막 반투명 내부", Vector2.up * Radius / 2,
                new Vector2(Radius * 2, Radius), Color.white, 1);
            fillShape = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            fillShape.name = "GD05 semicircle fill";
            var vertices = new Vector2[66]; var triangles = new ushort[64 * 3];
            vertices[0] = new Vector2(0.5f, 0);
            for (int i = 0; i <= 64; i++)
            {
                float a = Mathf.PI * i / 64;
                vertices[i + 1] = new Vector2(Mathf.Clamp01(0.5f + Mathf.Cos(a) * 0.5f), Mathf.Clamp01(Mathf.Sin(a)));
                if (i < 64) { triangles[i * 3] = 0; triangles[i * 3 + 1] = (ushort)(i + 1); triangles[i * 3 + 2] = (ushort)(i + 2); }
            }
            fillShape.OverrideGeometry(vertices, triangles); fill.sprite = fillShape;
            shell = new LineRenderer[Sections];
            for (int i = 0; i < Sections; i++)
            {
                float a = Mathf.PI * i / Sections, b = Mathf.PI * (i + 1) / Sections;
                shell[i] = Line("황금 보호막 조각", transform, ArcPoints(Radius, a + 0.006f, b - 0.006f, 5), KarmaSpace.Units(0.055f), 12);
            }
            var roofPoints = ArcPoints(Radius, 0, Mathf.PI, 65);
            roof = gameObject.AddComponent<EdgeCollider2D>(); roof.points = roofPoints;
            gameObject.AddComponent<KarmaSurface>();
            inner = Line("안쪽 금빛 테두리", transform, ArcPoints(Radius * 0.985f, 0, Mathf.PI, 65), KarmaSpace.Units(0.025f), 11);
            floor = Line("결계 바닥선", transform, new[] { Vector2.left * Radius, Vector2.right * Radius }, KarmaSpace.Units(0.04f), 4);
            Paint();
        }
        public void WarnFailure() { warning = true; gold = new Color(1, 0.22f, 0.05f); }
        public void Explode() { exploding = true; Dissolve(); }
        public void Dissolve() { dissolving = true; dissolveAge = 0; roof.enabled = false; }
        void Update()
        {
            if (game == null || game.Paused) return;
            age += Time.deltaTime;
            if (dissolving) dissolveAge += Time.deltaTime;
            Paint();
        }
        void Paint()
        {
            float fade = 1 - Mathf.Clamp01(dissolveAge / 0.4f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * (warning ? 18 : 3));
            float opacity = exploding ? 0.45f : warning ? 0.09f + pulse * 0.08f : 0.035f + pulse * 0.015f;
            fill.color = new Color(gold.r, gold.g, gold.b, opacity * fade);
            Tint(inner, 0.4f * fade);
            Tint(floor, 0.5f * fade);
            for (int i = 0; i < shell.Length; i++)
            {
                var section = shell[i];
                if (exploding)
                    section.transform.localPosition = Arc(Mathf.PI * (i + 0.5f) / Sections, Radius * dissolveAge * 1.5f);
                Tint(section, (0.75f + pulse * 0.2f) * fade);
            }
        }
        void OnDestroy() { if (fillShape != null) Destroy(fillShape); }
    }
}
