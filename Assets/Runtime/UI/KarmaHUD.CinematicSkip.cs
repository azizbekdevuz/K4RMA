using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void CinematicSkipHint()
        {
            if (!game.CanSkipCinematic) return;
            Panel(new Rect(892, 16, 352, 71));
            KeyPrompt(new Rect(907, 23, 322, 45), 20, false, KarmaAction.SkipCinematic, "  길게 눌러 건너뛰기");
            Fill(new Rect(907, 72, 322, 5), WithOpacity(HudBrass, 0.22f));
            float progress = game.CinematicSkipProgress;
            if (progress > 0)
                Fill(new Rect(907, 72, 322 * progress, 5), HudBrass);
        }
    }
}
