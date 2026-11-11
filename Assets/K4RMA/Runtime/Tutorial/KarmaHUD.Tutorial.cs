using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] tutorialLabels = { "기본 공격", "검기 명중", "상승베기 명중", "방어 성공" };
        static readonly string[] tutorialTrainingNames = { "기본 공격", "검기", "상승베기", "방어" };
        static readonly KarmaAction[] tutorialKeys = { KarmaAction.Attack, KarmaAction.SwordWave, KarmaAction.RisingSlash, KarmaAction.Guard };
        void TutorialScreen()
        {
            var t = game.Tutorial;
            if (game.Paused) return;
            string Key(KarmaAction action) => KarmaInput.BindingLabel(action);
            // Skip enters Fade at alpha zero, before a lesson may have been selected.
            if (t.Step == KarmaTutorialStep.Fade) { Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, t.Fade)); return; }
            if (t.WaitingForGuard)
            {
                Fill(new Rect(0, 0, 1280, 720), new Color(0.01f, 0.02f, 0.04f, 0.4f));
                Panel(new Rect(250, 540, 780, 150));
                Label(275, 550, 730, 50, "공격이 닿기 전, 보호의 힘을 펼쳐라.", centered);
                float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 5) * 0.2f;
                var old = GUI.color; GUI.color = new Color(1, 0.9f, 0.6f, pulse);
                Label(275, 615, 730, 55, "[ " + Key(KarmaAction.Guard) + " ] 를 새로 눌러 막으세요", heading); GUI.color = old;
            }
            if (!string.IsNullOrEmpty(t.Speech))
            {
                Panel(new Rect(210, 550, 860, 140));
                Label(235, 560, 810, 25, t.Step == KarmaTutorialStep.Complete ? tutorialLabels[t.Lesson] + " 3/3 · 수련 완료" : t.SpeechTitle, small);
                Label(235, 588, 810, 65, t.Speech, text);
                if (t.Step != KarmaTutorialStep.Complete) Label(235, 655, 810, 25, "[ " + Key(KarmaAction.DialogueConfirm) + " ] 계속", small);
                return;
            }
            string objective;
            if (t.Step == KarmaTutorialStep.Gate && Mathf.Abs(game.Player.Position.x - KarmaTutorialStage.FinalGate) > 5) objective = "모든 훈련을 마쳤다. 오른쪽으로 쭉 가보자.";
            else if (t.Step == KarmaTutorialStep.Gate) objective = "관문 앞에서 [" + Key(KarmaAction.Interact) + "]를 눌러 다음 보스 스테이지로 이동";
            else if (t.Step == KarmaTutorialStep.Complete && t.Lesson < 3) objective = tutorialTrainingNames[t.Lesson] + " 훈련을 마쳤다. 다음 훈련을 시작하자.";
            else if (t.Step == KarmaTutorialStep.Travel) objective = t.Lesson < 0
                ? Key(KarmaAction.Left) + " / " + Key(KarmaAction.Right) + " 이동 · " + Key(KarmaAction.Down) + " 앉기 · " + Key(KarmaAction.Jump) + " 점프 · 앞으로 이동"
                : tutorialTrainingNames[t.Lesson] + " 훈련을 마쳤다. 오른쪽으로 쭉 가보자.";
            else
            {
                objective = tutorialLabels[t.Lesson] + " " + t.Successes + "/3  ·  [ " + Key(tutorialKeys[t.Lesson]) + " ]";
                if (t.Lesson == 1) objective += "  ·  표적에서 거리를 두고 발사";
                if (t.Lesson == 2) objective += "  ·  허수아비 가까이에서 올려베기";
                if (t.Lesson > 0)
                {
                    Essence essence = t.Lesson == 1 ? Essence.SwordWave : t.Lesson == 2 ? Essence.RisingSlash : Essence.Guard;
                    float cooldown = game.Player.Essences.Cooldown(essence);
                    if (cooldown > 0) objective += "  ·  재사용 대기 " + cooldown.ToString("0.0") + "초";
                }
            }
            if (t.WaitingForGuard) return;
            if (!string.IsNullOrEmpty(t.BlockedMessage))
            {
                Panel(new Rect(210, 120, 860, 70));
                Label(230, 130, 820, 50, t.BlockedMessage, centered);
            }
            Panel(new Rect(210, 615, 860, 65)); Label(230, 630, 820, 42, objective, centered);
        }
    }
}
