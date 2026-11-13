using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        void CinematicSkipHint()
        {
            if (!game.CanSkipCinematic) return;
            ThemePanel(new Rect(974, 14, 286, 52), 0.84f);
            ThemeLabel(new Rect(988, 18, 258, 28), Keyed("1.5초 눌러 건너뛰기", KarmaAction.SkipCinematic), mapNumber, Ivory);
            ProgressTrack(new Rect(987, 51, 260, 3), game.CinematicSkipProgress);

        }
    }
}
