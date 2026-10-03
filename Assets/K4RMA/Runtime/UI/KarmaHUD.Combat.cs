using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void TopBar()
        {
            Panel(new Rect(20, 18, 385, 150));
            Label(35, 25, 360, 32, "K4RMA / " + (game.StageIndex == 3 ? "최종 전투" : (game.StageIndex + 1) + "스테이지"), heading);
            Label(35, 65, 340, 25, "체력 " + Mathf.CeilToInt(game.Player.Health) + " / " + game.Config.playerHealth, small);
            Bar(new Rect(35, 95, 350, 12), game.Player.Health / game.Config.playerHealth, new Color(0.3f, 0.85f, 0.74f));
            Label(35, 119, 350, 34, "무의식화: " + game.Player.Essences.Count + "/3    시간: " + game.Elapsed.ToString("0")  + "초", small);
            if (game.Player.BurnRemaining > 0)
            {
                Fill(new Rect(20, 175, 385, 74), new Color(0.5f, 0.1f, 0.04f, 0.95f));
                Label(35, 183, 360, 28, "화상 " + game.Player.BurnRemaining.ToString("0.0") + "초 · 반복 넉백", heading);
                Label(35, 217, 360, 25, "화염 보호막을 공격하면 화상이 갱신됩니다", small);
            }
            for (int i = 0; i < 3; i++)
            {
                var e = (Essence)i;
                Rect r = new Rect(425 + i * 275, 18, 260, 95);
                Panel(r);
                bool triggered = game.Player.Essences.HasRemnant(e) && game.Player.Essences.JustTriggered(e);
                if (triggered)
                {
                    Color glow = game.Config.ColorOf(e); glow.a = 0.25f;
                    Fill(r, glow);
                    Bar(new Rect(r.x, r.y + r.height - 4, r.width, 4), 1, game.Config.ColorOf(e));
                }
                Color old = GUI.color; GUI.color = game.Config.ColorOf(e);
                Label(r.x + 12, r.y + 8, 240, 30, names[i] + (game.Player.Essences.HasRemnant(e) ? " · 무의식" : " · 의식") + (triggered ? "!" : ""), heading); GUI.color = old;
                bool remnant = game.Player.Essences.HasRemnant(e);
                float cd = game.Player.Essences.Cooldown(e);
                Label(r.x + 12, r.y + 47, 240, 42,
                    remnant ? RemnantStatus(e) : keys[i] + " / " + (cd <= 0 ? "사용 가능" : cd.ToString("0.0") + "초"), small);
            }
            if (game.Enemies.Count > 0 && game.Enemies[0] != null)
            {
                var enemy = game.Enemies[0];
                Panel(new Rect(440, 125, 810, 112));
                Label(455, 130, 780, 27, enemy.DisplayName + " · " + enemy.CombatPhase + "페이즈   "
                    + Mathf.CeilToInt(enemy.Health) + " / " + enemy.MaxHealth + "   " + Clock(enemy.FightSeconds), text);
                Bar(new Rect(455, 165, 780, 9), enemy.Health / enemy.MaxHealth, new Color(0.85f, 0.35f, 0.48f));
                Label(455, 180, 780, 25, enemy.Intent, small);
                Label(455, 207, 780, 25, "합성 패턴: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.SacrificeOrder), small);
                if (enemy.Invulnerable || enemy.Stunned || enemy.BurnRemaining > 0)
                {
                    Fill(new Rect(440, 294, 810, 35), new Color(0.18f, 0.12f, 0.06f, 0.95f));
                    string status = enemy.Stunned ? "벽 충돌 성공 · 기절! 받는 피해 증가 / 공격 기회"
                        : enemy.Invulnerable ? "무적 · 화염 지속 피해 / 벽으로 유도 후 돌진을 피하세요"
                        : "적 화상 " + enemy.BurnRemaining.ToString("0.0") + "초 · 지속 피해 (보스 넉백 없음)";
                    Label(455, 298, 780, 27, status, small);
                }
                if (enemy.FlameWardActive)
                {
                    Fill(new Rect(440, 244, 810, 42), new Color(0.5f, 0.1f, 0.04f, 0.95f));
                    Label(455, 250, 780, 30, "공격 중단! 화염 보호막 적중 시 지속 피해 + 반복 넉백", heading);
                }
            }
        }
        string ShortRemnant(Essence e)
        {
            return e == Essence.Flame ? "검 적중 시 불씨" : e == Essence.Dash ? "검기 발사" : "피해 감소 + 충격파";
        }
        string RemnantStatus(Essence e)
        {
            var state = game.Player.Essences;
            string count = " · " + state.ProcCount(e) + "회 발동";
            if (e == Essence.Flame) return "불씨" + count + "\n검 적중마다 불씨 2개 발사";
            int every = Mathf.Max(1, e == Essence.Dash ? game.Config.waveEveryHits : game.Config.wardBurstEveryHits);
            return (e == Essence.Dash ? "검기" : "충격파") + count + "\n검 적중 " + (state.SwordHits % every) + "/" + every
                + (e == Essence.Ward ? " · 피해 " + (game.Config.wardDamageReduction * 100).ToString("0") + "% 감소" : "");
        }
    }
}
