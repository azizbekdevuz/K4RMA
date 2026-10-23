using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly Color HudIvory = new Color(0.97f, 0.93f, 0.81f);

        void TopBar()
        {
            if (game.Player == null) return;
            var healthDiamond = KarmaArtwork.Sprite("UI", "Health");
            var healthOutline = KarmaArtwork.Sprite("UI", "HealthOutline");
            float health = Mathf.Clamp01(game.Player.Health / Mathf.Max(1, game.Config.playerHealth)) * 5;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(47 + i * 23, 43, 20, 20);
                SpriteArt(r, healthOutline, new Color(HudIvory.r, HudIvory.g, HudIvory.b, 0.45f));
                float fraction = Mathf.Clamp01(health - i);
                if (fraction <= 0) continue;
                // Clip the last diamond so small amounts of damage remain visible.
                GUI.BeginGroup(new Rect(r.x, r.y, r.width * fraction, r.height));
                SpriteArt(new Rect(0, 0, r.width, r.height), healthDiamond, Color.white);
                GUI.EndGroup();
            }

            // Only confirmed personalizations appear, packed in acquisition order.
            var owned = game.Player.Essences.SacrificeOrder;
            for (int i = 0; i < owned.Count; i++)
            {
                var essence = owned[i];
                var r = new Rect(43 + i * 45, 76, 32, 32);
                bool cooling = game.Player.Essences.Cooldown(essence) > 0;
                Color color = HudIvory;
                color.a = cooling ? 0.35f : 1;
                if (game.Player.Essences.JustTriggered(essence)) color = Color.white;
<<<<<<< HEAD
                SpriteArt(r, SkillIcon(essence, true), color);
=======
                if(KarmaArtwork.Settings.useCompleteArtwork) HudSprite(r,KarmaArtAtlas.Icon((int)essence+3),color);
                else HudSymbol(r, uniqueSymbols[(int)essence], color);
>>>>>>> e8d524ec86dec83deb348c14c41617b54d84da4c

            }

            if (game.Enemies.Count == 0 || game.Enemies[0] == null) return;
            var enemy = game.Enemies[0];
            if (!enemy.Alive) return;
            float ratio = Mathf.Clamp01(enemy.Health / Mathf.Max(1, enemy.MaxHealth));
            // Keep the boss readable above the action, clear of the player's corner HUD.
            const float left = 330, width = 620, y = 56;
            Fill(new Rect(left - 16, 16, width + 32, 62), new Color(0.015f, 0.025f, 0.045f, 0.65f));
            Label(left, 20, width, 27, enemy.DisplayName, centered);
            if(KarmaArtwork.Settings.useCompleteArtwork) SpriteArt(new Rect(left-12,y-10,width+24,28),KarmaArtAtlas.Hud(2));
            Fill(new Rect(left, y - 2, width, 12), new Color(0.015f, 0.025f, 0.045f, 0.95f));
            Color track = new Color(HudIvory.r, HudIvory.g, HudIvory.b, 0.65f);
            Fill(new Rect(left, y - 2, width, 0.8f), track);
            Fill(new Rect(left, y + 9.2f, width, 0.8f), track);
            Fill(new Rect(left, y, width * ratio, 8), HudIvory);
            Fill(new Rect(left - 3, y - 5, 1.2f, 20), HudIvory);
            Fill(new Rect(left + width + 2, y - 5, 1.2f, 20), HudIvory);
        }

<<<<<<< HEAD
=======
        static void HudSprite(Rect rect,Sprite sprite,Color color)
        {
            Color old=GUI.color;GUI.color=color;var crop=sprite.rect;var texture=sprite.texture;
            GUI.DrawTextureWithTexCoords(rect,texture,new Rect(crop.x/texture.width,crop.y/texture.height,crop.width/texture.width,crop.height/texture.height),true);
            GUI.color=old;
        }
        static void HudSymbol(Rect rect, Texture2D texture, Color color)
        {
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = saved;
        }

        void EnsureHudSymbols()
        {
            if (healthDiamond != null) return;
            healthDiamond = MakeHudSymbol(0);
            healthOutline = MakeHudSymbol(1);
            for (int i = 0; i < 3; i++) uniqueSymbols[i] = MakeHudSymbol(i + 2);
        }

        static Texture2D MakeHudSymbol(int kind)
        {
            // Small antialiased ivory silhouettes; no atlas backgrounds or baked text.
            const int size = 128;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2 - 1;
                float v = (y + 0.5f) / size * 2 - 1;
                float distance;
                if (kind < 2)
                {
                    float diamond = Mathf.Abs(u) + Mathf.Abs(v) - 0.91f;
                    distance = kind == 0 ? diamond : Mathf.Abs(diamond) - 0.055f;
                }
                else if (kind == 2)
                {
                    // Sweeping crescent for the internalized sword technique.
                    float outer = new Vector2(u + 0.12f, v - 0.08f).magnitude - 0.77f;
                    float cutout = new Vector2(u + 0.38f, v - 0.34f).magnitude - 0.76f;
                    distance = Mathf.Max(outer, -cutout);
                }
                else if (kind == 3)
                {
                    // Rising arch / airborne slash.
                    float arch = Mathf.Abs(new Vector2(u / 0.92f, (v + 0.63f) / 1.3f).magnitude - 1) - 0.048f;
                    distance = Mathf.Max(arch, -v - 0.6f);
                }
                else
                {
                    float radius = new Vector2(u, v).magnitude;
                    float outer = Mathf.Abs(radius - 0.79f) - 0.035f;
                    float inner = Mathf.Abs(radius - 0.63f) - 0.024f;
                    distance = Mathf.Min(Mathf.Min(outer, inner), radius - 0.34f);
                }
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - distance * size * 0.5f));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "K4RMA HUD symbol " + kind;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void DestroyHudSymbols()
        {
            if (healthDiamond != null) Destroy(healthDiamond);
            if (healthOutline != null) Destroy(healthOutline);
            foreach (var symbol in uniqueSymbols) if (symbol != null) Destroy(symbol);
        }
>>>>>>> e8d524ec86dec83deb348c14c41617b54d84da4c
    }
}
