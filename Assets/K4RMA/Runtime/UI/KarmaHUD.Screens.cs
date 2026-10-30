using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        int storyPage;
        void Update()
        {
            if (game == null || game.Phase != RunPhase.Story) return;
            if (KarmaInput.Pressed(KarmaAction.NextPage)) storyPage = Mathf.Min(KarmaStory.Pages.Length - 1, storyPage + 1);
            if (KarmaInput.Pressed(KarmaAction.PreviousPage)) storyPage = Mathf.Max(0, storyPage - 1);
        }
        void TitleScreen()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0.02f, 0.03f, 0.07f, 0.25f));
            Label(330, 160, 620, 100, "K4RMA", title);
            Label(330, 270, 620, 35, "네 수호자 · 세 번의 고유화", centered);
            SpriteArt(new Rect(460, 320, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            if (GUI.Button(new Rect(480, 365, 320, 55), "시작 [Enter]", menuButton)) game.StartRun();
            if (GUI.Button(new Rect(480, 430, 320, 55), "게임 스토리 [S]", menuButton)) game.ShowPage(RunPhase.Story);
            if (GUI.Button(new Rect(480, 495, 320, 55), "조작 안내 [H]", menuButton)) game.ShowPage(RunPhase.Controls);
        }
        void StoryScreen()
        {
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
            Label(65, 565, 745, 30, "↓ / S 유지 : 앉기    P : 테스트 무적 켜기/끄기", small);
            Art(new Rect(850, 90, 345, 155), "PlayerSwordWave");
            Label(855, 250, 340, 28, "검기 · 관통베기", small);
            Art(new Rect(850, 285, 345, 155), "PlayerRisingSlash");
            Label(855, 445, 340, 28, "상승베기 · 도약베기", small);
            Art(new Rect(850, 480, 345, 155), "WardGuardian");
            if (GUI.Button(new Rect(150, 600, 560, 55), "타이틀 [Esc]", button)) game.ReturnToTitle();
        }
        void ChoiceScreen()
        {
            Label(280, 265, 720, 45, "시련 완료 · 고유화", title);
            int count = 0;
            for (int i = 0; i < 3; i++) if (game.Player.Essences.HasActive((Essence)i)) count++;
            int slot = 0;
            for (int i = 0; i < 3; i++)
            {
                var e = (Essence)i;
                if (!game.Player.Essences.HasActive(e)) continue;
                float centerX = 640 + (slot++ - (count - 1) * 0.5f) * 180;
                bool selected = game.PendingChoice == e;
                if (GUI.Button(new Rect(centerX - 80, 335, 160, 145), "", GUIStyle.none)) game.Choose(e);
                SpriteArt(new Rect(centerX - 45, 340, 90, 80), SkillIcon(e, false));
                Label(centerX - 80, 430, 160, 35, (i + 1) + " · " + names[i], centered);
                if (selected) SpriteArt(new Rect(centerX - 10, 480, 20, 25), KarmaArtwork.Settings.selectionCrescent);
                SpriteArt(new Rect(centerX - 12, 312, 24, 24), KarmaArtwork.Sprite("UI", selected ? "FilledSlot" : "EmptySlot"));
            }
            if (game.PendingChoice.HasValue)
            {
                var e = game.PendingChoice.Value;
                Label(280, 520, 720, 35, names[(int)e] + " → " + KarmaPatternCatalog.UniqueName(e), centered);
            }
            else Label(280, 520, 720, 35, "다음 시련에 남길 원형을 선택하세요.", centered);
            GUI.enabled = game.PendingChoice.HasValue;
            if (GUI.Button(new Rect(440, 570, 400, 45), "고유화 [Enter]", menuButton)) game.ConfirmChoice();
            GUI.enabled = true;
            if (GUI.Button(new Rect(490, 625, 300, 40), "선택 취소 [Esc]", menuButton)) game.CancelChoice();
        }
        void TransitionScreen()
        {
            Panel(new Rect(120, 275, 1040, 300));
            Label(155, 300, 570, 45, names[(int)game.LastSacrifice] + " → " + KarmaPatternCatalog.UniqueName(game.LastSacrifice), heading);
            Label(155, 360, 570, 180, "원형은 제자의 동작으로 고유화되었습니다.\n다음 수호자: " + KarmaPatternCatalog.TypeName(game.Player.Essences.SacrificeOrder)
                + "\n계승 원형: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.SacrificeOrder)
                + (game.StageIndex == 2 ? "\n세 고유 기술로 마지막 수호자의 시험에 도전합니다." : "\n내려놓은 원형의 흔적은 이후 수호자에게 유지됩니다."), text);
            SpriteArt(new Rect(780, 295, 310, 260), KarmaArtwork.GuardianPreview(game.Player.Essences.SacrificeOrder));
            SpriteArt(new Rect(650, 300, 65, 65), SkillIcon(game.LastSacrifice, true));
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
            Label(300, 270, 680, 55, victory ? "시련의 끝" : "시련 실패", heading);
            Label(300, 350, 680, 100, "시간: " + Clock(game.Elapsed) + " · 누적 피해: " + game.DamageTaken.ToString("0")
                + "\n고유화 순서: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.SacrificeOrder)
                + (victory ? "\n스승: 이제 그 기술들은 누구의 것이냐?\n제자: 배웠습니다. 하지만 이제는 제 방식으로 쓸 수 있습니다." : "\n원형과 고유화 순서를 초기화하고 Stage 1부터 다시 도전합니다."), text);
            if (GUI.Button(new Rect(305, 480, 670, 55), "처음부터 다시 도전 [R]", button)) game.StartRun();
            if (GUI.Button(new Rect(305, 565, 670, 55), "타이틀 [T]", button)) game.ReturnToTitle();
        }
    }
}
