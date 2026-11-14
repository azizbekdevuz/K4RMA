using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void CinematicSkipHint()
        {
            if (!game.CanSkipCinematic) return;
            Fill(new Rect(974, 14, 286, 52), new Color(0.015f, 0.02f, 0.035f, 0.7f));
            KeyPrompt(new Rect(987, 18, 260, 28), 14, false, KarmaAction.SkipCinematic, "  1.5초 누르기 · 건너뛰기");
            Fill(new Rect(987, 51, 260, 3), new Color(1, 1, 1, 0.18f));
            Fill(new Rect(987, 51, 260 * game.CinematicSkipProgress, 3), new Color(0.94f, 0.85f, 0.56f));
        }
    }
}
