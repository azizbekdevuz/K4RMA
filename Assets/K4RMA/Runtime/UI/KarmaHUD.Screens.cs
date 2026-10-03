using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void TitleScreen()
        {
            Panel(new Rect(170, 65, 940, 590));
            GUI.Label(new Rect(240, 95, 800, 85), "K4RMA", title);
            Label(255, 180, 770, 65, "네가 무의식에 새긴 힘이, 다음 수호자의 힘이 된다.", heading);
            Label(255, 255, 770, 125,
                "오랜 수련 끝에 세 기술을 익힌 제자에게 스승이 마지막 시험을 제안한다.\n\n수호자를 넘어설 때마다 기술 하나를 무의식에 새겨라. 그 원리는 기본 동작에 스며들고, 그 과정에서 남은 잔재는 다음 수호자에게 전달된다.", text);
            Label(255, 401, 770, 80, "의식: 직접 꺼내 쓰는 기술 / 무의식: 기본 동작에서 발현되는 힘\n세 기술을 무의식화하고 마지막 수호자를 넘어 시험을 통과하세요.", text);
            if (GUI.Button(new Rect(445, 515, 390, 60), "수련 시작 [Enter]", button)) game.StartRun();
            Label(255, 606, 770, 30, "수호자의 시련 / 목표 플레이 시간 15~20분 / 키보드 조작", small);
        }
        void ChoiceScreen()
        {
            Panel(new Rect(110, 225, 1060, 430));
            Label(145, 245, 980, 38, "다음 시련 / 무의식화할 기술 선택", heading);
            Label(145, 291, 980, 40, "직접 발동하는 기술을 기본 동작에 새깁니다. 남은 잔재는 다음 수호자에게 전달됩니다.", text);
            for (int i = 0; i < 3; i++)
            {
                var e = (Essence)i;
                GUI.enabled = game.Player.Essences.HasActive(e);
                var r = new Rect(140 + i * 340, 350, 320, 205);
                if (GUI.Button(r, (i + 1) + " / " + names[i] + "\n\n" + ChoiceDescription(e), button)) game.Choose(e);
                GUI.enabled = true;
                Label(r.x + 6, 564, 308, 45, "다음 수호자의 합성:\n" + PreviewFusion(e), small);
            }
            Label(145, 618, 980, 28, "무의식화 시 체력 " + game.Config.healAfterSacrifice + " 회복. 이번 도전에서는 선택을 되돌릴 수 없습니다.", small);
        }
        string PreviewFusion(Essence e)
        {
            if (!game.Player.Essences.HasActive(e)) return "이미 무의식화한 기술";
            var returned = new List<Essence>(game.Player.Essences.SacrificeOrder);
            returned.Add(e);
            return returned.Count == 3 ? "두 기술 합성 + 삼중 합성 해금" : KarmaPatternCatalog.FusionSummary(returned);
        }
        string ChoiceDescription(Essence e)
        {
            var c = game.Config;
            if (e == Essence.Flame) return "의식: 화염탄 사용 종료\n무의식: 검 적중마다 불씨\n수호자: 메테오 + 넉백";
            if (e == Essence.Dash) return "의식: 무적 돌진 사용 종료\n무의식: 검 적중 " + c.waveEveryHits + "회마다 검기\n수호자: 돌진 공격";
            return "의식: 보호막 사용 종료\n무의식: " + (c.wardDamageReduction * 100).ToString("0") + "% 피해 감소 + 충격파\n수호자: 피해 감소 + 반사 충격파";
        }
        void TransitionScreen()
        {
            Panel(new Rect(230, 290, 820, 230));
            Label(270, 317, 740, 42, names[(int)game.LastSacrifice] + " 기술을 무의식에 새겼습니다", heading);
            Label(270, 380, 740, 90, "기술의 원리와 감각이 기본 동작에서 발현됩니다.\n\n" + (game.StageIndex == 2 ? "세 기술을 모두 무의식화했습니다. 잔재를 지닌 마지막 수호자가 기다립니다." : "무의식화 과정에서 남은 잔재가 다음 수호자의 힘이 됩니다."), text);
        }
        void PauseScreen()
        {
            Panel(new Rect(390, 260, 500, 320));
            GUI.Label(new Rect(400, 275, 480, 75), "일시정지", title);
            if (GUI.Button(new Rect(435, 370, 410, 65), "계속하기 [Esc]", button)) game.TogglePause();
            if (GUI.Button(new Rect(435, 460, 410, 65), "처음부터 다시 시작", button)) game.StartRun();
        }
        void EndScreen(bool victory)
        {
            Panel(new Rect(255, 235, 770, 400));
            GUI.Label(new Rect(285, 250, 710, 85), victory ? "수호자의 시련을 통과했습니다" : "쓰러졌습니다", title);
            var order = new StringBuilder();
            foreach (var e in game.Player.Essences.SacrificeOrder)
            { if (order.Length > 0) order.Append(" > "); order.Append(names[(int)e]); }
            Label(300, 355, 680, 95, "시간: " + game.Elapsed.ToString("0.0") + "초   누적 피해: " + game.DamageTaken.ToString("0")
                + "   재도전: " + game.Retries + "\n무의식화 순서: " + (order.Length > 0 ? order.ToString() : "없음"), text);
            if (!victory && GUI.Button(new Rect(305, 475, 670, 55), "현재 스테이지 재도전 [R]", button)) game.RetryRoom();
            if (victory)
                Label(300, 463, 680, 62, "힘은 이제 온전히 제자의 것이 되었습니다.\n1시험 " + Clock(game.BossSplits[0]) + " / 2시험 " + Clock(game.BossSplits[1])
                    + " / 3시험 " + Clock(game.BossSplits[2]) + " / 마지막 수호자 " + Clock(game.BossSplits[3]), text);
            if (GUI.Button(new Rect(305, 550, 670, 55), "새 도전 / 다른 무의식화 순서 선택", button)) game.StartRun();
        }
    }
}
