using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] tutorialLabels = { "기본 공격", "검기 명중", "상승베기 명중", "방어 성공" };
        static readonly KarmaAction[] tutorialKeys = { KarmaAction.Attack, KarmaAction.SwordWave, KarmaAction.RisingSlash, KarmaAction.Guard };
        void TutorialScreen()
        {
            var t = game.Tutorial;
            if (game.Paused) return;
            string Key(KarmaAction action) => KarmaInput.BindingLabel(action);
            if (t.Fade > 0) { Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, t.Fade)); return; }
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
                Label(235, 560, 810, 25, t.Step == KarmaTutorialStep.Complete ? tutorialLabels[t.Lesson] + " 3/3 · 수련 완료" : "스승", small);
                Label(235, 588, 810, 65, t.Speech, text);
                if (t.Step != KarmaTutorialStep.Complete) Label(235, 655, 810, 25, "[ " + Key(KarmaAction.DialogueConfirm) + " ] 계속", small);
                return;
            }
            string objective;
            if (t.Step == KarmaTutorialStep.Gate) objective = "수련문 앞으로 이동 · [ " + Key(KarmaAction.Interact) + " ] 시험장으로";
            else if (t.Step == KarmaTutorialStep.Travel) objective = t.Lesson < 0
                ? Key(KarmaAction.Left) + " / " + Key(KarmaAction.Right) + " 이동 · " + Key(KarmaAction.Down) + " 앉기 · " + Key(KarmaAction.Jump) + " 점프 · 앞으로 이동하세요"
                : "다음 수련 마당으로 이동 →";
            else
            {
                objective = tutorialLabels[t.Lesson] + " " + t.Successes + "/3  ·  [ " + Key(tutorialKeys[t.Lesson]) + " ]";
                if (t.Lesson == 1) objective += "  ·  표적에서 거리를 두고 발사";
                if (t.Lesson == 2) objective += "  ·  공중 표적 아래에서 올려베기";
            }
            if (t.WaitingForGuard) return;
            Panel(new Rect(210, 615, 860, 65)); Label(230, 630, 820, 42, objective, centered);
        }
    }
}
