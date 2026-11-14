using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void TitleScreen()
        {
            FillScreen(new Color(0.02f, 0.03f, 0.07f, 0.25f));
            Label(330, 160, 620, 100, "K4RMA", title);
            Label(330, 270, 620, 35, "네 수호자 · 세 번의 고유화", centered);
            SpriteArt(new Rect(460, 320, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            if (ShortcutButton(new Rect(480, 365, 320, 55), "시작", KarmaAction.Confirm)) game.StartOpening();
            if (ShortcutButton(new Rect(480, 430, 320, 55), "환경설정", KarmaAction.Settings)) game.OpenSettings();
        }
        static readonly KarmaAction[] choiceKeys = {
            KarmaAction.ChooseSwordWave, KarmaAction.ChooseRisingSlash, KarmaAction.ChooseGuard
        };

        void ChoiceScreen()
        {
            Label(280, 265, 720, 45, "시련 완료 · 고유화", title);
            if (game.StageIndex == 0)
                Label(210, 217, 860, 40, "첫 고유화 선택에 따라 다음 수호자의 유형이 달라집니다.", centered);
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
                SpriteArt(new Rect(centerX - 45, 340, 90, 80), KarmaArtwork.SkillIcon(e));
                KeyPrompt(new Rect(centerX - 80, 430, 160, 35), 18, true, choiceKeys[i], "  " + names[i]);
                if (selected) SpriteArt(new Rect(centerX - 10, 480, 20, 25), KarmaArtwork.Settings.selectionCrescent);
                SpriteArt(new Rect(centerX - 12, 312, 24, 24), KarmaArtwork.Sprite("UI", selected ? "FilledSlot" : "EmptySlot"));
            }
            if (game.PendingChoice.HasValue)
            {
                var e = game.PendingChoice.Value;
                string preview = names[(int)e] + " → " + KarmaPatternCatalog.UniqueName(e);
                if (game.StageIndex == 0) preview += "   ·   다음 상대  " + names[(int)e] + "형 수호자";
                Label(280, 520, 720, 35, preview, centered);
            }
            else Label(280, 520, 720, 35, "다음 시련에 남길 원형을 선택하세요.", centered);
            GUI.enabled = game.PendingChoice.HasValue;
            if (ShortcutButton(new Rect(440, 570, 400, 45), "고유화", KarmaAction.Confirm)) game.ConfirmChoice();
            GUI.enabled = true;
            if (ShortcutButton(new Rect(490, 625, 300, 40), "선택 취소", KarmaAction.Pause)) game.CancelChoice();
        }
        void DrawPauseOverlay()
        {
            FillScreen(new Color(0.015f, 0.025f, 0.07f, 0.78f));
            Label(280, 245, 720, 55, "일시정지", title);
            SpriteArt(new Rect(460, 315, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            if (ShortcutButton(new Rect(440, 350, 400, 55), "계속하기", KarmaAction.Pause)) game.TogglePause();
            if (ShortcutButton(new Rect(440, 425, 400, 55), "처음부터", KarmaAction.Restart)) game.RestartSession();
            if (ShortcutButton(new Rect(440, 500, 400, 55), "타이틀", KarmaAction.Title)) game.ReturnToTitle();
            if (ShortcutButton(new Rect(440, 575, 400, 55), "환경설정", KarmaAction.Settings)) game.OpenSettings();
        }
        void StageTransitionScreen()
        {
            if (!game.GuardianInheriting)
            {
                Fill(new Rect(280, 590, 720, 82), new Color(0.015f, 0.025f, 0.07f, 0.72f));
                Label(300, 609, 680, 44,
                    "고유화 · " + KarmaPatternCatalog.UniqueName(game.LastPersonalizedEssence), centered);
                return;
            }
            if (game.CurrentEncounterPrompt == KarmaGame.EncounterPrompt.BossName)
            {
                if (!DrawBossEntrance())
                {
                    string guardian = game.Enemies.Count > 0 ? game.Enemies[0].DisplayName : "다음 수호자";
                    Label(300, 594, 680, 35, guardian, centered);
                    if (game.EncounterPromptReady)
                        KeyPrompt(new Rect(300, 645, 680, 30), 16, true, KarmaAction.DialogueConfirm, "  계속");
                }
                return;
            }
            PersonalizationGuide(game.LastPersonalizedEssence);
        }
        void PersonalizationGuide(Essence essence)
        {
            Fill(new Rect(190, 515, 900, 174), new Color(0.015f, 0.025f, 0.07f, 0.92f));
            SpriteArt(new Rect(214, 549, 74, 74), KarmaArtwork.SkillIcon(essence), HudIvory);
            Label(310, 530, 750, 35, KarmaPatternCatalog.UniqueName(essence), heading);
            Label(310, 572, 750, 64, PersonalizationGuideText(essence), text);
            KeyPrompt(new Rect(310, 644, 750, 30), 16, false, KarmaAction.DialogueConfirm, "  전투 시작");
        }

        static string PersonalizationGuideText(Essence essence)
        {
            switch (essence)
            {
                case Essence.SwordWave:
                    string pierce = KarmaInput.BindingLabel(KarmaAction.Pierce);
                    string pierceKey = pierce == "L" ? "L을" : pierce + " 키를";
                    return "방향키와 " + pierceKey
                        + " 함께 누르면 여덟 방향 중 원하는 방향으로 돌진해 적을 관통할 수 있다.";
                case Essence.RisingSlash:
                    string attack = KarmaInput.BindingLabel(KarmaAction.Attack);
                    string attackKey = attack == "J" ? "J를" : attack + " 키를";
                    return "공중에서 방향키와 " + attackKey
                        + " 함께 누르면 여덟 방향 중 원하는 방향으로 베며 한 번 더 도약할 수 있다. 아래 방향을 선택하면 잠시 체공할 수 있다.";
                default:
                    string counter = KarmaInput.BindingLabel(KarmaAction.Counter);
                    string counterKey = counter == "K" ? "K를" : counter + " 키를";
                    return "적의 공격이 닿는 순간 " + counterKey
                        + " 눌러 받아내면 그 힘을 되돌려줄 수 있다.";
            }
        }

        void EndScreen(bool victory)
        {
            if (victory)
            {
                Label(280, 180, 720, 60, "시련의 끝", title);
                SpriteArt(new Rect(460, 255, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
                Label(180, 295, 920, 32, "시간: " + Clock(game.Elapsed) + " · 누적 피해: " + game.DamageTaken.ToString("0"), centered);
                Label(180, 335, 920, 55, "고유화 순서: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.PersonalizationOrder), centered);
                Label(180, 395, 920, 80, "스승: 이제 그 기술들은 누구의 것이냐?\n제자: 배웠습니다. 하지만 이제는 제 방식으로 쓸 수 있습니다.", centered);
                if (ShortcutButton(new Rect(420, 510, 440, 55), "처음부터 다시 도전", KarmaAction.Restart)) game.RestartSession();
                if (ShortcutButton(new Rect(420, 585, 440, 55), "타이틀", KarmaAction.Title)) game.ReturnToTitle();
                return;
            }
            Label(280, 180, 720, 60, "시련 실패", title);
            SpriteArt(new Rect(460, 255, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            Label(180, 295, 920, 32, "시간: " + Clock(game.Elapsed) + " · 누적 피해: " + game.DamageTaken.ToString("0"), centered);
            Label(180, 335, 920, 55, "고유화 순서: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.PersonalizationOrder), centered);
            Label(180, 405, 920, 55, "원형과 고유화 순서를 초기화하고\n첫 번째 시련부터 다시 도전합니다.", centered);
            if (ShortcutButton(new Rect(420, 510, 440, 55), "처음부터 다시 도전", KarmaAction.Restart)) game.RestartSession();
            if (ShortcutButton(new Rect(420, 585, 440, 55), "타이틀", KarmaAction.Title)) game.ReturnToTitle();
        }
    }
}
