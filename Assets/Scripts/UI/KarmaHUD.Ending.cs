using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void EndingScreen()
        {
            float bars = game.EndingCompleted ? 1 : Mathf.SmoothStep(0, 1, game.EndingTime / 1.5f);
            float captionAlpha = game.EndingLineAlpha;
            float bottomBar = 46 * bars + 52 * captionAlpha;
            Fill(new Rect(0, 0, 1280, 46 * bars), Color.black);
            Fill(new Rect(0, 720 - bottomBar, 1280, bottomBar), Color.black);
            if (game.EndingMemoryDim > 0) FillScreen(new Color(0.025f, 0.035f, 0.07f, game.EndingMemoryDim));
            if (captionAlpha > 0)
            {
                Color old = GUI.color;
                GUI.color = new Color(1, 0.84f, 0.58f, captionAlpha);
                GUI.Label(new Rect(200, 632, 880, 24), game.EndingSpeaker, centered);
                GUI.color = new Color(1, 1, 1, captionAlpha);
                GUI.Label(new Rect(160, 656, 960, 46), game.EndingLine, centered);
                GUI.color = old;
            }
            if (game.EndingFade > 0) FillScreen(new Color(0, 0, 0, game.EndingFade));
            if (game.EndingCompleted)
            {
                if (GUI.Button(new Rect(410, 580, 460, 40), "다시 수련하기 [R]", menuButton)) game.RestartSession();
                if (GUI.Button(new Rect(410, 630, 460, 40), "타이틀 [T]", menuButton)) game.ReturnToTitle();
            }
            else if (game.Paused) DrawPauseOverlay();
        }
    }
}
