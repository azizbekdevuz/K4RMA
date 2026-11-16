using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        GUIStyle endingSpeakerLabel;

        void EndingScreen()
        {
            float bars = game.EndingCompleted ? 1 : Mathf.SmoothStep(0, 1, game.EndingTime / 1.5f);
            float captionAlpha = game.EndingLineAlpha;
            float bottomBar = 46 * bars + 52 * captionAlpha;
            Fill(new Rect(0, 0, 1280, 46 * bars), Color.black);
            Fill(new Rect(0, 720 - bottomBar, 1280, bottomBar), new Color(0, 0, 0, DialoguePanelOpacity));
            if (game.EndingMemoryDim > 0) FillScreen(new Color(0.025f, 0.035f, 0.07f, game.EndingMemoryDim));
            if (captionAlpha > 0)
            {
                Color old = GUI.color;
                string speaker = game.EndingSpeaker;
                Color captionHeadingColor = new Color(1, 0.84f, 0.58f, captionAlpha);
                if (speaker == "스승" || speaker == "제자")
                {
                    if (endingSpeakerLabel == null)
                        endingSpeakerLabel = new GUIStyle(centered) { fontSize = 15, fontStyle = FontStyle.Bold };

                    GUI.color = captionHeadingColor;
                    GUI.Label(new Rect(160, 638, 960, 23), speaker, endingSpeakerLabel);
                    GUI.color = new Color(1, 1, 1, captionAlpha);
                    GUI.Label(new Rect(160, 658, 960, 36), game.EndingLine, centered);
                }
                else
                {
                    GUI.color = captionHeadingColor;
                    GUI.Label(new Rect(200, 632, 880, 24), speaker, centered);
                    GUI.color = new Color(1, 1, 1, captionAlpha);
                    GUI.Label(new Rect(160, 656, 960, 46), game.EndingLine, centered);
                }
                GUI.color = old;
            }
            if (game.EndingFade > 0) FillScreen(new Color(0, 0, 0, game.EndingFade));
            if (game.EndingCompleted)
            {
                if (ShortcutButton(new Rect(410, 313, 460, 40), "다시 수련하기", KarmaAction.Restart)) game.RestartSession();
                if (ShortcutButton(new Rect(410, 367, 460, 40), "타이틀", KarmaAction.Title)) game.ReturnToTitle();
            }
            else if (game.Paused) DrawPauseOverlay();
        }
    }
}
