using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void EndingScreen()
        {
            float bars = game.EndingCompleted ? 1 : Mathf.SmoothStep(0, 1, game.EndingTime / 1.5f);
            Fill(new Rect(0, 0, 1280, 46 * bars), Color.black);
            Fill(new Rect(0, 720 - 46 * bars, 1280, 46 * bars), Color.black);
            if (game.EndingMemoryDim > 0) FillScreen(new Color(0.025f, 0.035f, 0.07f, game.EndingMemoryDim));
            if (game.EndingLine != null)
            {
                Color old = GUI.color;
                GUI.color = new Color(1, 1, 1, game.EndingLineAlpha);
                Fill(new Rect(180, 588, 920, 64), new Color(0.015f, 0.02f, 0.035f, 0.55f * game.EndingLineAlpha));
                GUI.Label(new Rect(200, 594, 880, 52), game.EndingLine, centered);
                GUI.color = old;
            }
            if (game.EndingFade > 0) FillScreen(new Color(0, 0, 0, game.EndingFade));
            if (game.EndingCompleted)
            {
                if (GUI.Button(new Rect(410, 580, 460, 40), "다시 수련하기 [R]", menuButton)) game.RestartSession();
                if (GUI.Button(new Rect(410, 630, 460, 40), "타이틀 [T]", menuButton)) game.ReturnToTitle();
            }
            else if (game.Paused) PauseScreen();
        }
    }
}
