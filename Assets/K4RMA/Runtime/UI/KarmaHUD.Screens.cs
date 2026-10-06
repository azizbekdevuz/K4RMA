using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        int storyPage;
        void TitleScreen()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0.025f, 0.045f, 0.06f));
            // Only quadrant 1: the K4RMA / Master & Disciple cover, never the collage.
            Art(new Rect(30, 45, 850, 580), "WorldOverview", new Rect(0, 0, 640, 427));
            Panel(new Rect(915, 85, 335, 550));
            Label(945, 115, 275, 80, "스승의 원형을 넘어,\n너의 검술을 완성하라.", heading);
            Label(945, 220, 275, 110, "네 수호자 · 세 번의 고유화\n검기 · 상승베기 · 방어\n내려놓은 원형을 넘어서는 시험", text);
            if (GUI.Button(new Rect(945, 365, 275, 65), "수련 시작 [Enter]", button)) game.StartRun();
            if (GUI.Button(new Rect(945, 450, 275, 55), "게임 스토리 [S]", button)) game.ShowPage(RunPhase.Story);
            if (GUI.Button(new Rect(945, 520, 275, 55), "조작 안내 [H]", button)) game.ShowPage(RunPhase.Controls);
        }
        void StoryScreen()
        {
            // Key events are consumed once by the GUI event loop, never on repaint.
            var ev = Event.current;
            if (ev.type == EventType.KeyDown)
            {
                if (ev.keyCode == KeyCode.RightArrow || ev.keyCode == KeyCode.DownArrow) storyPage = Mathf.Min(KarmaStory.Pages.Length - 1, storyPage + 1);
                if (ev.keyCode == KeyCode.LeftArrow || ev.keyCode == KeyCode.UpArrow) storyPage = Mathf.Max(0, storyPage - 1);
            }
            Panel(new Rect(110, 35, 1060, 650));
            Label(155, 60, 970, 40, "스승과 제자 · " + (storyPage + 1) + " / " + KarmaStory.Pages.Length, heading);
            Label(155, 115, 555, 475, KarmaStory.Pages[storyPage], text);
            string illustration = storyPage == 7 ? "PlayerSheet" : storyPage == 3 ? "PlayerRisingSlash" : storyPage >= 5 ? "SwordGuardian" : "WorldOverview";
            Art(new Rect(745, 130, 375, 420), illustration);
            Label(750, 565, 365, 30, "스승과 제자 · 수호자의 시험", small);
            if (GUI.Button(new Rect(155, 615, 250, 45), "이전 [←]", button)) storyPage = Mathf.Max(0, storyPage - 1);
            if (GUI.Button(new Rect(440, 615, 250, 45), "다음 [→]", button)) storyPage = Mathf.Min(KarmaStory.Pages.Length - 1, storyPage + 1);
            if (GUI.Button(new Rect(725, 615, 385, 45), "타이틀 [Esc]", button)) game.ReturnToTitle();
        }
        void ControlsScreen()
        {
            Panel(new Rect(35, 35, 1210, 650));
            Label(65, 65, 730, 45, "조작 안내", heading);
            Label(65, 135, 745, 440,
                "A/D · ←/→ : 이동    Space : 점프    J 유지 : 검 기본 공격\n\nQ : 검기 발사    왼쪽 Shift : 상승베기    E : 원형 방어\n고유화한 원형 키는 사용할 수 없습니다.\n\n관통베기 : 같은 방향키 / WASD 두 번 (0.25초 이내) · 해당 방향으로 관통\n도약베기 : 공중 방향키 / WASD + J 새로 누르기 · 베고 추가 점프 (착지 전 1회)\n↓ / S + J : 아래로 베고 점프 · 방향 입력이 없으면 바라보는 방향\n반격 : K로 짧은 받아내기 동작 · 공격 도착 순간에만 성공\n\n고유화 : 1/2/3 선택 → Enter 확정 · Esc 취소/재선택\nEsc : 일시정지    일시정지/결과에서 R : 처음부터    T : 타이틀\n\n사망하면 네 수호자의 시험을 Stage 1부터 다시 시작합니다.", text);
            Art(new Rect(850, 90, 345, 155), "PlayerSwordWave");
            Label(855, 250, 340, 28, "검기 · 관통베기", small);
            Art(new Rect(850, 285, 345, 155), "PlayerRisingSlash");
            Label(855, 445, 340, 28, "상승베기 · 도약베기", small);
            Art(new Rect(850, 480, 345, 155), "WardGuardian");
            if (GUI.Button(new Rect(150, 600, 560, 55), "타이틀 [Esc]", button)) game.ReturnToTitle();
        }
        void ChoiceScreen()
        {
            Panel(new Rect(110, 245, 1060, 405));
            Label(145, 260, 980, 38, "시험 통과 · 원형 하나를 자신의 기술로", heading);
            for (int i = 0; i < 3; i++)
            {
                var e = (Essence)i;
                GUI.enabled = game.Player.Essences.HasActive(e);
                string selected = game.PendingChoice == e ? "✓ 선택 중 · " : "";
                if (GUI.Button(new Rect(140 + i * 340, 315, 320, 170), "", button)) game.Choose(e);
                Art(new Rect(150 + i * 340, 325, 300, 92), i == 0 ? "PlayerSwordWave" : i == 1 ? "PlayerRisingSlash" : "WardGuardian");
                Label(152 + i * 340, 421, 296, 28, selected + (i + 1) + " / " + names[i], text);
                Label(152 + i * 340, 450, 296, 32, "원형 종료 → " + KarmaPatternCatalog.UniqueName(e), small);
                GUI.enabled = true;
            }
            if (game.PendingChoice.HasValue)
            {
                var order = new List<Essence>(game.Player.Essences.SacrificeOrder); order.Add(game.PendingChoice.Value);
                Label(145, 495, 980, 48, "다음 수호자: " + KarmaPatternCatalog.TypeName(order) + " · 계승 원형: " + KarmaPatternCatalog.FusionSummary(order), text);
            }
            else Label(145, 495, 980, 48, "첫 선택이 이후 수호자의 주 타입을 결정합니다. 확정 전에는 다시 선택할 수 있습니다.", text);
            GUI.enabled = game.PendingChoice.HasValue;
            if (GUI.Button(new Rect(145, 555, 615, 50), "고유화 확정 · 다음 시련 [Enter]", button)) game.ConfirmChoice();
            GUI.enabled = true;
            if (GUI.Button(new Rect(790, 555, 340, 50), "선택 취소 [Esc]", button)) game.CancelChoice();
            Label(145, 616, 980, 25, "확정 후 원형 복구 불가 · 체력 " + game.Config.healAfterSacrifice + " 회복", small);
        }
        string ChoiceDescription(Essence e)
        {
            return "원형 직접 사용 종료\n획득: " + KarmaPatternCatalog.UniqueName(e) + "\n다음 수호자 계승: " + KarmaPatternCatalog.Name(e);
        }
        void TransitionScreen()
        {
            Panel(new Rect(120, 275, 1040, 300));
            Label(155, 300, 570, 45, names[(int)game.LastSacrifice] + " → " + KarmaPatternCatalog.UniqueName(game.LastSacrifice), heading);
            Label(155, 360, 570, 180, "원형은 제자의 동작으로 고유화되었습니다.\n다음 수호자: " + KarmaPatternCatalog.TypeName(game.Player.Essences.SacrificeOrder)
                + "\n계승 원형: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.SacrificeOrder)
                + (game.StageIndex == 2 ? "\n세 고유 기술로 마지막 수호자의 시험에 도전합니다." : "\n내려놓은 원형의 흔적은 이후 수호자에게 유지됩니다."), text);
            int variant = KarmaPatternCatalog.GuardianVisual(game.Player.Essences.SacrificeOrder);
            if (variant >= 0)
                Art(new Rect(760, 300, 360, 250), "GuardianEvolution", new Rect((variant % 3) * 512, (variant / 3) * 512, 512, 512));
            else Art(new Rect(760, 300, 360, 250), "SwordGuardian");
        }
        void PauseScreen()
        {
            Panel(new Rect(390, 260, 500, 360));
            Label(425, 275, 430, 55, "일시정지", heading);
            if (GUI.Button(new Rect(435, 345, 410, 65), "계속하기 [Esc]", button)) game.TogglePause();
            if (GUI.Button(new Rect(435, 430, 410, 65), "처음부터 [R]", button)) game.StartRun();
            if (GUI.Button(new Rect(435, 515, 410, 65), "타이틀 [T]", button)) game.ReturnToTitle();
        }
        void EndScreen(bool victory)
        {
            Panel(new Rect(255, 250, 770, 400));
            Label(300, 270, 680, 55, victory ? "자신만의 검술을 완성했습니다" : "쓰러졌습니다", heading);
            Label(300, 350, 680, 100, "시간: " + Clock(game.Elapsed) + " · 누적 피해: " + game.DamageTaken.ToString("0")
                + "\n고유화 순서: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.SacrificeOrder)
                + (victory ? "\n스승: 이제 그 기술들은 누구의 것이냐?\n제자: 배웠습니다. 하지만 이제는 제 방식으로 쓸 수 있습니다." : "\n원형과 고유화 순서를 초기화하고 Stage 1부터 다시 도전합니다."), text);
            if (GUI.Button(new Rect(305, 480, 670, 55), "처음부터 다시 도전 [R]", button)) game.StartRun();
            if (GUI.Button(new Rect(305, 565, 670, 55), "타이틀 [T]", button)) game.ReturnToTitle();
        }
    }
}
