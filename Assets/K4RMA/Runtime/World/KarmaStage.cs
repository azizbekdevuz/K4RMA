using UnityEngine;

namespace KarmaPrototype
{
    // Stage geometry and scenery only: no progression or combat rules.
    public static class KarmaStage
    {
        // Inner wall faces (-0.5, 40.5), minus/plus the overdrive shield half-width (1.6).
        public const float BossLeftWall = 1.1f, BossRightWall = 38.9f;
        public static readonly Vector2 Spawn = new Vector2(3, 1);
        public static readonly Vector2 Gate = new Vector2(38, 1.8f);
        public static void Build(KarmaGame game, Transform parent, int stage)
        {
            Color stone = new Color(0.16f + stage * 0.014f, 0.17f, 0.23f);
            bool suppliedBackground = KarmaArtwork.Background(parent);
            if (!suppliedBackground)
            {
            KarmaVisuals.Box(parent, "수련장 배경", new Vector2(20, 4), new Vector2(70, 26), new Color(0.09f, 0.07f, 0.10f), -20);
            for (int i = 0; i < 12; i++)
            {
                float x = i * 4 - 2;
                KarmaVisuals.Box(parent, "Pillar", new Vector2(x, 4), new Vector2(0.6f, 9), stone * 0.8f, -15);
                KarmaVisuals.Box(parent, "Window", new Vector2(x + 1.8f, 5.3f), new Vector2(1, 2.4f), new Color(0.16f, 0.17f, 0.3f), -14);
                KarmaVisuals.Box(parent, "Window light", new Vector2(x + 1.8f, 5.3f), new Vector2(0.08f, 2.4f), new Color(0.45f, 0.33f, 0.6f), -13);
            }
            for (int i = 0; i < 8; i++)
            {
                float x = 2 + i * 5;
                KarmaVisuals.Box(parent, "障子", new Vector2(x,4.5f),new Vector2(3.8f,5),new Color(0.35f,0.29f,0.25f),-12);
                for (int j = 0; j < 4; j++) KarmaVisuals.Box(parent,"障子 木枠",new Vector2(x-1.5f+j,4.5f),new Vector2(.07f,5),stone,-11);
                for (int j = 0; j < 5; j++) KarmaVisuals.Box(parent,"障子 横枠",new Vector2(x,2.5f+j),new Vector2(3.8f,.07f),stone,-11);
                KarmaVisuals.Box(parent,"燈籠",new Vector2(x,6.8f),new Vector2(.6f,.85f),new Color(.86f,.53f,.28f),-9);
            }
            KarmaVisuals.Box(parent,"도장 처마",new Vector2(20,8),new Vector2(44,.5f),new Color(.3f,.12f,.12f),-8);
            }
            Solid(parent, "Ground", new Vector2(20, -0.55f), new Vector2(44, 1.1f), stone).GetComponent<SpriteRenderer>().enabled = !suppliedBackground;
            Solid(parent, "Left wall", new Vector2(-1, 4), new Vector2(1, 12), stone).GetComponent<SpriteRenderer>().enabled = !suppliedBackground;
            Solid(parent, "Right wall", new Vector2(41, 4), new Vector2(1, 12), stone).GetComponent<SpriteRenderer>().enabled = !suppliedBackground;
            // Platforms are one-way; projectiles pass through them by design.
            Platform(parent, new Vector2(9, 1.8f), new Vector2(3, 0.3f), stone);
            Platform(parent, new Vector2(18, 2.2f), new Vector2(3, 0.3f), stone);
            Platform(parent, new Vector2(30, 2.2f), new Vector2(3, 0.3f), stone);
            KarmaVisuals.Box(parent, "Arena trim", new Vector2(24.5f, 0.04f), new Vector2(22, 0.08f), new Color(0.5f, 0.4f, 0.58f), 1);
            KarmaVisuals.Box(parent, "수련문", Gate, new Vector2(0.8f, 3.6f), new Color(0.67f, 0.35f, 0.87f, 0.75f), 0);
            KarmaVisuals.Box(parent, "수련문 문틀", Gate + Vector2.up * 1.9f, new Vector2(2.2f, 0.3f), new Color(0.7f, 0.59f, 0.37f), 1);
            if (stage == 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 p = new Vector2(35.5f + i * 1.4f, 4.5f);
                    KarmaVisuals.Box(parent, "수련 깃발", p, new Vector2(0.7f, 2.2f), game.Config.ColorOf((Essence)i), -1);
                    KarmaVisuals.Box(parent, "깃대", p + Vector2.up * 1.2f, new Vector2(0.95f, 0.12f), stone, 0);
                }
            }
        }
        static BoxCollider2D Solid(Transform parent, string name, Vector2 p, Vector2 size, Color color)
        {
            var sr = KarmaVisuals.Box(parent, name, p, size, color);
            sr.gameObject.AddComponent<KarmaSurface>();
            return sr.gameObject.AddComponent<BoxCollider2D>();
        }
        static void Platform(Transform parent, Vector2 p, Vector2 size, Color color)
        {
            var box = Solid(parent, "One-way platform", p, size, color);
            var effector = box.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true; effector.surfaceArc = 180;
            box.usedByEffector = true;
        }
    }
}
