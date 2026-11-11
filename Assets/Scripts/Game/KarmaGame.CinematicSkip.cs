using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public const float CinematicSkipHoldSeconds = 1.5f;
        float cinematicSkipHeld;
        RunPhase cinematicSkipPhase;
        bool cinematicSkipNeedsRelease;
        public bool CanSkipCinematic => !Paused && !SettingsOpen
            && (Phase == RunPhase.Tutorial && Tutorial != null && Tutorial.Step != KarmaTutorialStep.Fade
                || Phase == RunPhase.Ending && !endingSkipping);
        public float CinematicSkipProgress => CanSkipCinematic
            ? Mathf.Clamp01(cinematicSkipHeld / CinematicSkipHoldSeconds) : 0;

        void ResetCinematicSkip()
        {
            cinematicSkipHeld = 0; cinematicSkipPhase = Phase;
            cinematicSkipNeedsRelease = true;
        }
        void OnApplicationFocus(bool focused) { if (!focused) ResetCinematicSkip(); }
        bool UpdateCinematicSkip()
        {
            bool held = KarmaInput.Held(KarmaAction.SkipCinematic);
            if (cinematicSkipPhase != Phase)
            {
                cinematicSkipPhase = Phase; cinematicSkipHeld = 0;
                cinematicSkipNeedsRelease = held;
            }
            if (!CanSkipCinematic || KarmaInput.RebindingThisFrame)
            { cinematicSkipHeld = 0; cinematicSkipNeedsRelease = true; return false; }
            if (!held) { cinematicSkipHeld = 0; cinematicSkipNeedsRelease = false; return false; }
            if (cinematicSkipNeedsRelease) return false;
            // Tutorial dialogue and guard practice can freeze scaled time.
            cinematicSkipHeld += Time.unscaledDeltaTime;
            if (cinematicSkipHeld < CinematicSkipHoldSeconds) return false;
            cinematicSkipHeld = 0; cinematicSkipNeedsRelease = true;
            if (Phase == RunPhase.Tutorial) Tutorial.Skip(); else SkipEnding();
            return true;
        }
    }
}
