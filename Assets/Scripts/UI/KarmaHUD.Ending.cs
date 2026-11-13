using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void EndingScreen()
        {
            float bars = Mathf.SmoothStep(0, 1, game.EndingTime / 1.5f);
            float captionAlpha = game.EndingLineAlpha;
            float bottomBar = 46 * bars + 52 * captionAlpha;
            Fill(new Rect(0, 0, 1280, 46 * bars), Color.black);
            Fill(new Rect(0, 720 - bottomBar, 1280, bottomBar), Color.black);
            if (game.EndingMemoryDim > 0) FillScreen(new Color(0.025f, 0.035f, 0.07f, game.EndingMemoryDim));
            if (captionAlpha > 0)
            {
                BrushRule(505, 626, 270, Alpha(Gold, captionAlpha * 0.52f));
                ThemeLabel(new Rect(200, 632, 880, 24), game.EndingSpeaker, mapNumber, Alpha(Gold, captionAlpha));
                ThemeLabel(new Rect(160, 656, 960, 46), game.EndingLine, centered, Alpha(Ivory, captionAlpha));
            }
            if (game.EndingFade > 0) FillScreen(new Color(0, 0, 0, game.EndingFade));
            if (game.Paused) PauseScreen();
        }
    }
}
