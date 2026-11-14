using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void TitleScreen()
        {
            float reveal = ScreenReveal;
            TintTexture(new Rect(62, 151, 565, 405), titleShade, Alpha(DeepInk, 0.44f * reveal));
            BrushDivider(new Rect(133, 190, 20, 151), Alpha(Color.white, reveal));
            ThemeLabel(new Rect(173, 188 + 12 * (1 - reveal), 500, 91), "K4RMA", logoText, Alpha(Ivory, reveal));
            BrushRule(173, 279, 359 * reveal, Alpha(Ivory, 0.85f));
            ThemeLabel(new Rect(179, 293, 470, 39), "네 수호자 · 세 번의 고유화", titleSubtitle, Ivory);
            SelectionPointer(new Vector2(158, 409), Ivory);
            if (ThemeButton(new Rect(173, 375, 285, 67), Keyed("시작", KarmaAction.Confirm), true, large: true)) game.StartOpening();
            if (ThemeButton(new Rect(173, 454, 285, 48), Keyed("환경설정", KarmaAction.Settings), textOnly: true, large: true)) game.OpenSettings();
        }

        void ChoiceScreen()
        {
            if (game.Player == null) return;
            DrawMountainMap(Mathf.Clamp(game.StageIndex + 1, 1, 3), true);
            ThemeLabel(new Rect(44, 10, 230, 42), "K4RMA", mapLogo, Ivory);
            BrushRule(43, 48, 163 * ScreenReveal, Alpha(Ivory, 0.85f));
            BrushDivider(new Rect(21, 10, 12, 78), Color.white);
            ThemeLabel(new Rect(44, 54, 340, 35), "고유화", rowText, Ivory);

            if (game.StageIndex == 0)
                ThemeLabel(new Rect(755, 18, 490, 60), "첫 고유화 선택에 따라 다음 수호자의 유형이 달라집니다.",
                    centered, Ivory);

            var essences = game.Player.Essences;
            int count = 0;
            for (int i = 0; i < names.Length; i++)
                if (essences.HasActive((Essence)i)) count++;

            Essence? pending = game.PendingChoice;
            int row = 0;
            for (int i = 0; i < names.Length; i++)
            {
                var essence = (Essence)i;
                if (!essences.HasActive(essence)) continue;
                bool selected = pending == essence;
                Rect bounds = new Rect(742, 92 + (3 - count) * 60 + row++ * 120, 517, 104);
                float hover = HoverAmount(bounds);
                float accent = selected ? Ease((Time.unscaledTime - selectionAt) / 0.18f) : 0;
                bool pressed = PointerDown(hover);
                Frame(bounds, UiFrame.Card, Color.Lerp(Color.white, Ivory, hover * 0.25f));
                if (selected)
                {
                    Frame(new Rect(bounds.x - 3, bounds.y - 3, bounds.width + 6, bounds.height + 6),
                        UiFrame.SelectedCard, Alpha(Color.white, Mathf.Lerp(0.80f, 1, accent)));
                    SelectionPointer(new Vector2(bounds.x - 8 + 3 * accent, bounds.center.y), Color.white);
                }

                float offset = pressed ? 2 : 0;
                Rect key = new Rect(bounds.x + 31, bounds.y + 35 + offset, 33, 33);
                Frame(key, UiFrame.Parchment, Alpha(Color.white, 0.94f));
                ThemeLabel(key, KarmaInput.BindingLabel(TechniqueKeys[i]), mapNumber, DeepInk);
                BrushDivider(new Rect(bounds.x + 81, bounds.y + 19, 3, 66), Alpha(Color.white, 0.75f));
                TechniqueIcon(new Rect(bounds.x + 87, bounds.y + 10 + offset, 134, 84), essence);
                BrushDivider(new Rect(bounds.x + 227, bounds.y + 19, 3, 66), Alpha(Color.white, 0.47f));
                ThemeLabel(new Rect(bounds.x + 255, bounds.y + offset, bounds.width - 261, bounds.height),
                    names[i] + " → " + KarmaPatternCatalog.UniqueName(essence), rowText, Ivory);
                if (GUI.Button(bounds, GUIContent.none, GUIStyle.none)) game.Choose(essence);
            }

            BrushRule(738, 476, 521, Alpha(Ivory, 0.65f));
            string preview = pending.HasValue ? TechniquePreviews[(int)pending.Value] : "고유화할 기술을 선택하세요.";
            float textFade = pending.HasValue ? Mathf.Lerp(0.55f, 1, Ease((Time.unscaledTime - selectionAt) / 0.15f)) : 1;
            if (game.StageIndex == 0 && pending.HasValue)
            {
                ThemeLabel(new Rect(754, 481, 499, 30), preview, mapLabel, Alpha(Ivory, textFade));
                ThemeLabel(new Rect(754, 512, 499, 25), "다음 상대 · " + names[(int)pending.Value] + "형 수호자",
                    mapLabel, Alpha(Gold, textFade));
            }
            else ThemeLabel(new Rect(754, 482, 499, 59), preview, rowText, Alpha(Ivory, textFade));
            BrushRule(738, 545, 521, Alpha(Ivory, 0.56f));
            if (ThemeButton(new Rect(722, 596, 321, 69), Keyed("고유화", KarmaAction.Confirm), true, pending.HasValue, large: true))
                game.ConfirmChoice();
            if (ThemeButton(new Rect(1061, 602, 198, 59), Keyed("선택 취소", KarmaAction.Pause), textOnly: true, large: true))
                game.CancelChoice();
        }

        void PauseScreen()
        {
            float reveal = ScreenReveal;
            Fill(FullView, Alpha(DeepInk, 0.38f * reveal));
            float x = 874 + 16 * (1 - reveal);
            InkPanel(x - 14);
            ThemeLabel(new Rect(x, 76, 375, 80), "일시정지", modalTitle, Ivory);
            BrushRule(x + 18, 161, 344 * reveal, Alpha(Ivory, 0.84f));
            if (ThemeButton(new Rect(x, 210, 375, 87), Keyed("계속하기", KarmaAction.Pause), true, large: true))
                game.TogglePause();
            if (ThemeButton(new Rect(x, 318, 375, 77), Keyed("처음부터", KarmaAction.Restart), large: true))
                game.RestartSession();
            if (ThemeButton(new Rect(x, 416, 375, 77), Keyed("타이틀", KarmaAction.Title), large: true))
                game.ReturnToTitle();
            if (ThemeButton(new Rect(x, 514, 375, 77), Keyed("환경설정", KarmaAction.Settings), large: true))
                game.OpenSettings();
        }

        void StageTransitionScreen()
        {
            if (DrawBossEntrance()) return;
            if (!game.GuardianInheriting)
            {
                PersonalizationGuide(game.LastPersonalizedEssence);
                DrawChoiceExit();
                return;
            }

            // Keep the actors and the original inheritance caption visible.
            ThemePanel(new Rect(308, 577, 664, 108), 0.82f);
            BrushRule(485, 588, 310, Alpha(Gold, 0.64f));
            string guardian = game.Enemies.Count > 0 ? game.Enemies[0].DisplayName : "다음 수호자";
            ThemeLabel(new Rect(321, 594, 638, 37), "원형 계승 · " + guardian, actionText, Ivory);
            ThemeLabel(new Rect(321, 636, 638, 31), "내려놓은 원형의 힘이 다음 수호자에게 이어집니다.", centered, SoftIvory);
            DrawChoiceExit();
        }

        void PersonalizationGuide(Essence essence)
        {
            string input, effect, difference;
            switch (essence)
            {
                case Essence.SwordWave:
                    input = Keyed("방향을 잡고", KarmaAction.Pierce);
                    effect = "적을 꿰뚫으며 돌진";
                    difference = "원형 검기의 원거리 발사가 이동 공격으로 바뀝니다.";
                    break;
                case Essence.RisingSlash:
                    input = Keyed("공중에서 방향 +", KarmaAction.Attack);
                    effect = "한 번 더 도약하며 베기";
                    difference = "지상에서 올려 베던 원형이 공중 방향 전환 공격으로 바뀝니다.";
                    break;
                default:
                    input = Keyed("공격이 닿기 직전에", KarmaAction.Counter);
                    effect = "막고 즉시 반격";
                    difference = "원형 방어의 보호막 대신, 타이밍을 맞춰 반격 피해를 줍니다.";
                    break;
            }
            ThemePanel(new Rect(308, 554, 664, 131), 0.9f);
            BrushRule(485, 567, 310, Alpha(Gold, 0.64f));
            TechniqueIcon(new Rect(332, 580, 78, 78), essence);
            ThemeLabel(new Rect(423, 575, 527, 33), "고유화 완료 · " + KarmaPatternCatalog.UniqueName(essence), rowText, Ivory);
            ThemeLabel(new Rect(423, 613, 527, 28), input + "  ·  " + effect, text, Ivory);
            ThemeLabel(new Rect(423, 645, 527, 25), difference, mapLabel, SoftIvory);
        }

        void EndScreen(bool victory)
        {
            DrawMountainMap(victory ? 4 : Mathf.Clamp(game.StageIndex, 0, 3), !victory, failed: !victory);
            ThemeLabel(new Rect(44, 10, 230, 42), "K4RMA", mapLogo, Ivory);
            BrushRule(43, 48, 163 * ScreenReveal, Alpha(Ivory, 0.85f));
            BrushDivider(new Rect(21, 10, 12, 78), Color.white);
            ThemeLabel(new Rect(44, 54, 340, 35), "고유화", rowText, Ivory);
            ThemeLabel(new Rect(727, 36 + 8 * (1 - ScreenReveal), 526, 80),
                victory ? "시련의 끝" : "시련 실패", resultsTitle, Ivory);
            BrushRule(813, 120, 373 * ScreenReveal, Alpha(Gold, 0.82f));

            Rect stats = new Rect(738, 154, 512, 114);
            ThemePanel(stats, 0.56f);
            BrushRule(755, 210, 478, Alpha(Ivory, 0.26f));
            ThemeLabel(new Rect(786, 158, 222, 53), "시간", rowText, Ivory);
            ThemeLabel(new Rect(1080, 158, 126, 53), Clock(game.Elapsed), rightNumber, Ivory);
            ThemeLabel(new Rect(786, 211, 222, 56), "누적 피해", rowText, Ivory);
            ThemeLabel(new Rect(1080, 211, 126, 56), game.DamageTaken.ToString("0"), rightNumber, Ivory);

            BrushRule(738, 290, 515, Alpha(Ivory, 0.51f));
            ThemeLabel(new Rect(738, 302, 512, 52), "고유화 순서", actionText, Ivory);
            var order = game.Player != null ? game.Player.Essences.PersonalizationOrder : null;
            int count = order?.Count ?? 0;
            var techniques = new Rect(730, 360, 525, 159);
            ThemePanel(techniques, 0.56f);
            if (count == 0) ThemeLabel(techniques, "없음", actionText, SoftIvory);
            for (int i = 0; i < count; i++)
            {
                float x = techniques.x + (techniques.width / count) * i;
                float width = techniques.width / count;
                if (i > 0) BrushDivider(new Rect(x, techniques.y + 18, 3, 88), Alpha(Ivory, 0.46f));
                TechniqueIcon(new Rect(x + width / 2 - 62, 370, 124, 96), order[i]);
                ThemeLabel(new Rect(x, 469, width, 35), KarmaPatternCatalog.UniqueName(order[i]), mapNumber, Ivory);
            }

            if (!victory)
                ThemeLabel(new Rect(739, 537, 492, 43), "첫 번째 시련부터 다시 도전합니다.", mapLabel, SoftIvory);
            if (ThemeButton(new Rect(722, 590, 349, 72), Keyed(victory ? "다시 수련하기" : "다시 도전", KarmaAction.Restart), true, large: true))
                game.RestartSession();
            if (ThemeButton(new Rect(1087, 600, 160, 51), Keyed("타이틀", KarmaAction.Title), textOnly: true))
                game.ReturnToTitle();
        }
    }
}
