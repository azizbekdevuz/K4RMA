using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void TopBar()
        {
            Panel(new Rect(20, 18, 385, 140));
            Label(35, 26, 350, 35, "시련 " + (game.StageIndex + 1) + " / 4", heading);
            Label(35, 65, 340, 25, "체력 " + Mathf.CeilToInt(game.Player.Health) + " / " + game.Config.playerHealth, small);
            Bar(new Rect(35, 95, 350, 12), game.Player.Health / game.Config.playerHealth, new Color(0.3f, 0.85f, 0.74f));
            Label(35, 119, 350, 30, "고유화 " + game.Player.Essences.Count + "/3 · " + Clock(game.Elapsed), small);
            for (int i = 0; i < 3; i++)
            {
                var e = (Essence)i; bool unique = game.Player.Essences.HasRemnant(e);
                var r = new Rect(425 + i * 275, 18, 260, 100); Panel(r);
                if (unique && game.Player.Essences.JustTriggered(e)) Bar(new Rect(r.x, r.y + 96, 260, 4), 1, game.Config.ColorOf(e));
                Label(r.x + 12, r.y + 8, 240, 30, names[i] + (unique ? " · 고유화" : " · 원형"), heading);
                float cd = game.Player.Essences.Cooldown(e);
                string state = unique ? KarmaPatternCatalog.UniqueName(e) + " · 원형 사용 불가" : keys[i] + (cd <= 0 ? " · 사용 가능" : " · " + cd.ToString("0.0") + "초");
                Label(r.x + 12, r.y + 48, 240, 46, state + (unique && e == Essence.Ward ? "\nK 받아내기 · " + (cd <= 0 ? "준비" : cd.ToString("0.0") + "초") : unique ? (e == Essence.Flame ? "\n방향키 / WASD 두 번" : "\n공중 방향 + J · 추가 점프") : ""), small);
            }
            if (game.Enemies.Count > 0 && game.Enemies[0] != null)
            {
                var enemy = game.Enemies[0]; Panel(new Rect(440, 130, 810, 105));
                Label(455, 133, 780, 27, enemy.DisplayName + " · " + Mathf.CeilToInt(enemy.Health) + " / " + enemy.MaxHealth, text);
                Bar(new Rect(455, 167, 780, 9), enemy.Health / enemy.MaxHealth, new Color(0.85f, 0.35f, 0.48f));
                Label(455, 182, 780, 25, enemy.Intent, small);
                Label(455, 208, 780, 25, "수호자 계승 원형: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.SacrificeOrder), small);
            }
        }
    }
}
