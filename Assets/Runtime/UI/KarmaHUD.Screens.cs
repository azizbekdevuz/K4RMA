using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void TitleScreen()
        {
            DrawHudShade(0.15f);
            float settle = 1 - HudReveal;
            choiceTitle ??= new GUIStyle(title) { fontSize = 46, alignment = TextAnchor.MiddleLeft };
            Label(116, 181 + 8 * settle, 460, 88, "K4RMA", choiceTitle);
            Label(121, 274, 450, 33, "네 수호자 · 세 번의 고유화", text);
            Fill(new Rect(122, 320, 326 * HudReveal, 1), HudBrass);
            if (ShortcutButton(new Rect(120, 351, 350, 52), "시작", KarmaAction.Confirm, true)) game.StartOpening();
            if (ShortcutButton(new Rect(120, 414, 350, 52), "환경설정", KarmaAction.Settings)) game.OpenSettings();
        }

        static readonly KarmaAction[] choiceKeys = {
            KarmaAction.ChooseSwordWave, KarmaAction.ChooseRisingSlash, KarmaAction.ChooseGuard
        };
        static readonly KarmaAction[] originalSkillKeys = {
            KarmaAction.SwordWave, KarmaAction.RisingSlash, KarmaAction.Guard
        };
        static readonly KarmaAction[] uniqueSkillKeys = {
            KarmaAction.Pierce, KarmaAction.Attack, KarmaAction.Counter
        };
        static readonly string[] uniqueSkillDescriptions = {
            "여덟 방향으로 돌진하며 적을 관통합니다.",
            "공중에서 방향을 정해 베고 한 번 더 도약합니다.\n아래 방향을 누르면 잠시 체공합니다.",
            "공격이 닿는 순간 받아내고 상대에게 되돌려줍니다."
        };
        static readonly string[] uniqueSkillChanges = {
            "검기를 발사하던 기술이 몸으로 돌진하는 기술로 바뀝니다.",
            "지상에서 올려베던 기술이 공중 추가 도약으로 바뀝니다.",
            "방패를 펼치던 기술이 타이밍에 맞춘 반격으로 바뀝니다."
        };
        static readonly string[] guardianTraits = {
            "원거리 검기로 압박합니다.", "공중에서 추격하고 내려찍습니다.", "방패를 두르고 강공격을 가합니다."
        };

        readonly List<Essence> choicePreviewOrder = new List<Essence>(3);
        int choicePreviewRoute;
        Sprite choicePortrait;
        string choiceGuardianName;
        Essence? lastPendingChoice;
        float choiceChangedAt;
        GUIStyle choiceTitle, choiceName, choiceProgressText, choiceNote;
        Texture2D choiceWarningIcon;

        void ChoiceScreen()
        {
            DrawHudShade(0.38f);
            choiceTitle ??= new GUIStyle(title) { fontSize = 46, alignment = TextAnchor.MiddleLeft };
            if (choiceName == null)
            {
                choiceName = new GUIStyle(title) { fontSize = 46, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                if (choiceName.font != null && choiceName.font.dynamic)
                    for (int i = 0; i < choiceKeys.Length; i++)
                        choiceName.font.RequestCharactersInTexture(KarmaPatternCatalog.UniqueName((Essence)i), choiceName.fontSize, choiceName.fontStyle);
            }
            choiceNote ??= new GUIStyle(small) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            choiceNote.normal.textColor = HudMuted;

            var pending = game.PendingChoice;
            if (lastPendingChoice != pending)
            {
                lastPendingChoice = pending;
                choiceChangedAt = Time.unscaledTime;
            }
            var essences = game.Player.Essences;
            int count = 0;
            Essence preview = default;
            for (int i = 0; i < choiceKeys.Length; i++)
            {
                var option = (Essence)i;
                if (!essences.HasActive(option)) continue;
                if (count++ == 0) preview = option;
            }
            preview = pending ?? preview;
            if (count > 0) UpdateChoicePreview(essences.PersonalizationOrder, preview);

            DrawChoiceProgress();
            Label(76, 31, 580, 64, "기술 고유화", choiceTitle);
            Label(79, 101, 590, 29, "원형 기술 하나를 골라 나만의 기술로 바꾸세요.", text);
            if (game.StageIndex == 0) DrawFirstChoiceWarning();
            if (count > 0)
            {
                DrawChoicePortrait();
                DrawChoiceDetails(preview, pending.HasValue);
            }
            const float left = 76, right = 1204, gap = 12;
            float width = count > 0 ? Mathf.Min(368, (right - left - gap * (count - 1)) / count) : 0;
            float x = 640 - (width * count + gap * (count - 1)) * 0.5f;
            for (int i = 0; i < choiceKeys.Length; i++)
            {
                var option = (Essence)i;
                if (!essences.HasActive(option)) continue;
                DrawChoiceOption(new Rect(x, 571, width, 58), option, pending == option);
                x += width + gap;
            }
            Fill(new Rect(left, 645, right - left, 1), HudRule);
            GUI.enabled = pending.HasValue;
            if (ShortcutButton(new Rect(left, 658, 300, 42), "선택 취소", KarmaAction.Pause)) game.CancelChoice();
            if (ShortcutButton(new Rect(904, 658, 300, 42), "고유화 확정", KarmaAction.Confirm, true)) game.ConfirmChoice();
            GUI.enabled = true;
        }

        void UpdateChoicePreview(IReadOnlyList<Essence> acquired, Essence preview)
        {
            int route = 1;
            foreach (var essence in acquired) route = route * 4 + (int)essence + 1;
            route = route * 4 + (int)preview + 1;
            if (choicePreviewRoute == route) return;
            choicePreviewRoute = route;
            choiceChangedAt = Time.unscaledTime;
            choicePreviewOrder.Clear();
            foreach (var essence in acquired) choicePreviewOrder.Add(essence);
            choicePreviewOrder.Add(preview);
            choicePortrait = KarmaArtwork.GuardianPreview(choicePreviewOrder);
            choiceGuardianName = GuardianEncounterTitle(choicePreviewOrder);
        }

        void DrawChoicePortrait()
        {
            if (Event.current.type != EventType.Repaint) return;
            float reveal = Ease((Time.unscaledTime - choiceChangedAt) / 0.2f);
            float opacity = Mathf.Lerp(0.64f, 1, reveal);
            SpriteArt(new Rect(84 + 10 * (1 - reveal), 155, 524, 346), choicePortrait, WithOpacity(Color.white, opacity));
            Panel(new Rect(107, 495, 476, 60));
            string caption = game.StageIndex == 2 ? "다음 상대 · 최종 시련" : "다음 상대";
            Label(107, 495, 476, 21, caption, choiceProgressText);
            Label(107, 516, 476, 36, choiceGuardianName, choicePortraitName ??= new GUIStyle(title) { fontSize = 27 });
        }

        GUIStyle choicePortraitName;

        void DrawChoiceDetails(Essence preview, bool selected)
        {
            int i = (int)preview;
            const float x = 681, width = 495;
            Panel(new Rect(652, 176, 552, 371));
            Color highlight = selected ? HudBrass : HudRule;
            Fill(new Rect(652, 176, 2, 371), highlight);
            choiceNote.normal.textColor = HudBrass;
            Label(x, 193, width, 25, selected ? "선택한 기술  ·  " + names[i] : "미리보기  ·  " + names[i], choiceNote);
            float reveal = Ease((Time.unscaledTime - choiceChangedAt) / 0.2f);
            Label(x, 227 + 5 * (1 - reveal), width, 65, KarmaPatternCatalog.UniqueName(preview), choiceName);
            Label(x, 308, width, 61, uniqueSkillDescriptions[i], text);
            string original = "원형   " + names[i] + "  ";
            KeyPrompt(new Rect(x, 366, width, 31), 17, false, original, originalSkillKeys[i]);
            var useKeys = new Rect(x, 402, width, 32);
            if (preview == Essence.Guard)
                KeyPrompt(useKeys, 17, false, "고유화   공격이 닿는 순간  ", uniqueSkillKeys[i]);
            else
                KeyPrompt(useKeys, 17, false, preview == Essence.RisingSlash ? "고유화   공중  " : "고유화   ",
                    KarmaAction.Left, " ", KarmaAction.Up, " ", KarmaAction.Down, " ", KarmaAction.Right,
                    "  +  ", uniqueSkillKeys[i]);
            Fill(new Rect(x, 448, width, 1), HudRule);
            const float labelWidth = 68;
            choiceNote.normal.textColor = HudBrass;
            Label(x, 455, labelWidth, 35, "플레이어", choiceNote);
            Label(x, 493, labelWidth, 35, "보스", choiceNote);

            choiceNote.normal.textColor = HudMuted;
            Label(x + labelWidth, 455, width - labelWidth, 35, uniqueSkillChanges[i], choiceNote);
            string next = game.StageIndex == 0 ? guardianTraits[i] : names[i] + " 원형을 계승해 새 패턴을 사용합니다.";
            Label(x + labelWidth, 493, width - labelWidth, 35, next, choiceNote);
        }

        void DrawChoiceOption(Rect bounds, Essence option, bool selected)
        {
            DrawHudButtonPlate(bounds, false, selected, KarmaInput.Held(choiceKeys[(int)option]));
            if (GUI.Button(bounds, GUIContent.none, GUIStyle.none)) game.Choose(option);
            KeyPrompt(new Rect(bounds.x + 13, bounds.y + 10, 64, 38), 18, false, choiceKeys[(int)option]);
            SpriteArt(new Rect(bounds.x + 66, bounds.y + 12, 37, 34), KarmaArtwork.SkillIcon(option));
            Label(bounds.x + 115, bounds.y + 8, bounds.width - 182, 25, names[(int)option], text);
            Label(bounds.x + 115, bounds.y + 31, bounds.width - 125, 22, KarmaPatternCatalog.UniqueName(option) + " 획득", small);
            if (selected)
            {
                choiceNote.normal.textColor = HudBrass;
                Label(bounds.xMax - 68, bounds.y + 7, 65, 24, "선택됨", choiceNote);
            }
        }

        void DrawChoiceProgress()
        {
            choiceProgressText ??= new GUIStyle(centered) { fontSize = 13 };
            if (Event.current.type != EventType.Repaint) return;
            const float first = 882, top = 63, spacing = 88, diameter = 23;
            int clearedCount = game.StageIndex + 1;
            for (int i = 0; i < game.BossSplits.Length; i++)
            {
                float x = first + i * spacing;
                bool cleared = i < clearedCount;
                bool next = i == clearedCount;
                Color color = cleared || next ? HudBrass : WithOpacity(HudMuted, 0.63f);
                if (i + 1 < game.BossSplits.Length)
                    Fill(new Rect(x + diameter / 2, top, spacing - diameter, 1), HudRule);
                var node = new Rect(x - diameter / 2, top - diameter / 2, diameter, diameter);
                GUI.DrawTexture(node, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, cleared ? 0 : 1, diameter / 2);
                choiceProgressText.normal.textColor = cleared ? HudInk : color;
                GUI.Label(node, cleared ? "✓" : (i + 1).ToString(), choiceProgressText);
                choiceProgressText.normal.textColor = color;
                Label(x - 42, top + 15, 84, 22, next ? "다음 시련" : "시련 " + (i + 1), choiceProgressText);
            }
            choiceProgressText.normal.textColor = HudBrass;
        }

        void DrawFirstChoiceWarning()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (choiceWarningIcon == null) choiceWarningIcon = Resources.Load<Texture2D>("UI/CautionIcon");
            choiceNote.normal.textColor = new Color32(255, 186, 156, 255);
            if (choiceWarningIcon != null) GUI.DrawTexture(new Rect(80, 139, 19, 19), choiceWarningIcon, ScaleMode.ScaleToFit);
            Label(107, 136, 672, 26, "첫 고유화로 이후 수호자의 유형이 결정됩니다.", choiceNote);
        }

        void DrawPauseOverlay()
        {
            DrawHudShade(0.74f);
            Panel(new Rect(420, 212, 440, 449));
            Label(280, 245, 720, 55, "일시정지", title);
            SpriteArt(new Rect(460, 315, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            if (ShortcutButton(new Rect(440, 350, 400, 55), "계속하기", KarmaAction.Pause, true)) game.TogglePause();
            if (ShortcutButton(new Rect(440, 425, 400, 55), "처음부터", KarmaAction.Restart)) game.RestartSession();
            if (ShortcutButton(new Rect(440, 500, 400, 55), "타이틀", KarmaAction.Title)) game.ReturnToTitle();
            if (ShortcutButton(new Rect(440, 575, 400, 55), "환경설정", KarmaAction.Settings)) game.OpenSettings();
        }
        void StageTransitionScreen()
        {
            if (!game.GuardianInheriting) return;
            if (DrawBossEntrance()) return;

            string guardian = GuardianEncounterTitle(game.Player.Essences.PersonalizationOrder);
            Label(300, 594, 680, 35, guardian, centered);
            if (game.AwaitingBossNameConfirmation)
                KeyPrompt(new Rect(300, 645, 680, 30), 16, true, KarmaAction.DialogueConfirm, "  전투 시작");
        }

        void PlayerDefeatScreen()
        {
            float alpha = game.DefeatLineAlpha;
            if (alpha <= 0 || string.IsNullOrEmpty(game.DefeatLine)) return;
            Fill(new Rect(0, 622, 1280, 98), new Color(0.008f, 0.012f, 0.018f, alpha * 0.65f));
            Color previous = GUI.color;
            GUI.color = new Color(0.94f, 0.93f, 0.87f, alpha);
            GUI.Label(new Rect(160, 658, 960, 36), game.DefeatLine, centered);
            GUI.color = previous;
        }

        void EndScreen(bool victory)
        {
            DrawHudShade(0.76f);
            Panel(new Rect(250, 143, 780, 519));
            if (victory)
            {
                Label(280, 180, 720, 60, "시련의 끝", title);
                SpriteArt(new Rect(460, 255, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
                Label(180, 295, 920, 32, "시간: " + Clock(game.Elapsed) + " · 누적 피해: " + game.DamageTaken.ToString("0"), centered);
                Label(180, 335, 920, 55, "고유화 순서: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.PersonalizationOrder), centered);
                DrawSpeakerNameplate(340, 401, "스승");
                Label(443, 401, 610, 28, "이제 그 기술들은 누구의 것이냐?", text);
                DrawSpeakerNameplate(340, 439, "제자");
                Label(443, 439, 610, 28, "배웠습니다. 하지만 이제는 제 방식으로 쓸 수 있습니다.", text);
                if (ShortcutButton(new Rect(420, 510, 440, 55), "처음부터 다시 도전", KarmaAction.Restart, true)) game.RestartSession();
                if (ShortcutButton(new Rect(420, 585, 440, 55), "타이틀", KarmaAction.Title)) game.ReturnToTitle();
                return;
            }
            Label(280, 180, 720, 60, "시련 실패", title);
            SpriteArt(new Rect(460, 255, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            Label(180, 295, 920, 32, "시간: " + Clock(game.Elapsed) + " · 누적 피해: " + game.DamageTaken.ToString("0"), centered);
            Label(180, 335, 920, 55, "고유화 순서: " + KarmaPatternCatalog.FusionSummary(game.Player.Essences.PersonalizationOrder), centered);
            Label(180, 405, 920, 55, "원형과 고유화 순서를 초기화하고\n첫 번째 시련부터 다시 도전합니다.", centered);
            if (ShortcutButton(new Rect(420, 510, 440, 55), "처음부터 다시 도전", KarmaAction.Restart, true)) game.RestartSession();
            if (ShortcutButton(new Rect(420, 585, 440, 55), "타이틀", KarmaAction.Title)) game.ReturnToTitle();
        }
    }
}
