using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public const float CinematicSkipHoldSeconds = 1f;
        float cinematicSkipHeld;
        double cinematicSkipPressedAt = -1;
        RunPhase cinematicSkipPhase;
        bool cinematicSkipNeedsRelease;
        public bool CanSkipCinematic => !Paused && !SettingsOpen
            && (Phase == RunPhase.Tutorial && Tutorial != null && Tutorial.Step != KarmaTutorialStep.Fade
                || Phase == RunPhase.Ending && !endingSkipping);
        public float CinematicSkipProgress => CanSkipCinematic
            ? Mathf.Clamp01(cinematicSkipHeld / CinematicSkipHoldSeconds) : 0;

        void ResetCinematicSkip()
        {
            cinematicSkipPressedAt = -1;
            cinematicSkipHeld = 0; cinematicSkipPhase = Phase;
            cinematicSkipNeedsRelease = true;
        }
        void OnApplicationFocus(bool focused) { if (!focused) ResetCinematicSkip(); }
        bool UpdateCinematicSkip()
        {
            bool held = KarmaInput.Held(KarmaAction.SkipCinematic);
            if (cinematicSkipPhase != Phase)
            {
                ResetCinematicSkip();
                cinematicSkipNeedsRelease = held;
                return false;
            }
            if (!CanSkipCinematic || KarmaInput.RebindingThisFrame)
            { ResetCinematicSkip(); return false; }

            var edges = KarmaInput.Edges(KarmaAction.SkipCinematic);
            bool completedHold = false;
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge.Down)
                {
                    if (!cinematicSkipNeedsRelease) cinematicSkipPressedAt = edge.RealTime;
                }
                else
                {
                    completedHold |= cinematicSkipPressedAt >= 0 &&
                        edge.RealTime - cinematicSkipPressedAt >= CinematicSkipHoldSeconds;
                    cinematicSkipPressedAt = -1;
                    cinematicSkipNeedsRelease = false;
                }
            }
            if (!held) { cinematicSkipPressedAt = -1; cinematicSkipNeedsRelease = false; }
            cinematicSkipHeld = held && cinematicSkipPressedAt >= 0
                ? (float)(Time.realtimeSinceStartupAsDouble - cinematicSkipPressedAt) : 0;
            if (!completedHold && cinematicSkipHeld < CinematicSkipHoldSeconds) return false;
            ResetCinematicSkip();
            if (Phase == RunPhase.Tutorial) Tutorial.Skip(); else SkipEnding();
            return true;
        }
    }
}
