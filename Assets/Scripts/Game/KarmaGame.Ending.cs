using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public const float EndingDuration = 40f;
        public float EndingTime { get; private set; }
        public bool EndingCompleted { get; private set; }
        public float EndingFade => EndingCompleted ? 1 : endingSkipping
            ? Mathf.Lerp(endingSkipStartFade, 1, Mathf.SmoothStep(0, 1, endingSkipTime / 0.4f))
            : IsEndingReview ? AlternativeFade : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(38, 40, EndingTime));
        public float EndingMemoryDim => !IsEndingReview && Phase == RunPhase.Ending
            ? 0.42f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(20, 21, EndingTime))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(29, 30, EndingTime))) : 0;
        public string EndingLine => IsEndingReview ? AlternativeLine(out _, out _) : EndingTime >= 21 && EndingTime < 24.8f ? "내가 가르친 것은 기술의 형태일 뿐이다."
            : EndingTime >= 25 && EndingTime < 29.5f ? "그 기술을 어떻게 완성할지는 너에게 달려 있다."
            : EndingTime >= 31 && EndingTime < 34.8f ? "이제, 나만의 기술이 완성되었다." : null;
        public float EndingLineAlpha
        {
            get
            {
                if (IsEndingReview)
                {
                    if (AlternativeLine(out float begins, out float ends) == null) return 0;
                    return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(begins, begins + 0.35f, EndingTime))
                        * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ends - 0.3f, ends, EndingTime)));
                }
                float start = EndingTime < 25 ? 21 : EndingTime < 30 ? 25 : 31;
                float end = start == 21 ? 24.8f : start == 25 ? 29.5f : 34.8f;
                return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, start + 0.6f, EndingTime))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(end - 0.5f, end, EndingTime)));
            }
        }
        KarmaEndingPresentation endingPresentation;
        Vector3 endingCameraStart;
        float endingCameraSize;
        Bounds endingBackground;
        int endingSoundStage;
        bool endingSkipping;
        float endingSkipTime, endingSkipStartFade, endingAmbienceLevel = 1;
        static readonly KarmaSfxId[] endingCues = { KarmaSfxId.PlDash, KarmaSfxId.PlRisingSlash,
            KarmaSfxId.CombatGuardBlock, KarmaSfxId.GameAbilityInherit, KarmaSfxId.PlSwordSwing };
        static readonly float[] endingCueTimes = { 9.5f, 11.5f, 13.5f, 17.5f, 32.5f };

        void BeginEnding()
        {
            if (Phase == RunPhase.Ending || EndingCompleted) return;
            ResetHitFeedback();
            Phase = RunPhase.Ending; EndingTime = 0; endingSoundStage = 0;
            endingSkipping = false; endingSkipTime = 0; endingAmbienceLevel = 1;
            ResetCinematicSkip();
            EndingCompleted = false;
            Player.Essences.Cancel(); Player.BeginEndingControl(); ClearProjectiles();
            foreach (var enemy in Enemies.ToArray()) if (enemy != null) enemy.CancelCombat();
            endingCameraStart = GameCamera.transform.position;
            endingCameraSize = GameCamera.orthographicSize;
            var background = World.Find("Trial hall artwork") ?? World.Find("Mountain dojo artwork");
            endingBackground = background != null ? background.GetComponent<SpriteRenderer>().bounds
                : new Bounds(new Vector3(20, 8, 0), new Vector3(140, 56, 1));
            endingPresentation = KarmaEndingPresentation.Create(this, World, fallenGuardian, fallenGuardianPosition);
            KarmaAudio.StopBus(KarmaSfxBus.Gameplay);
            KarmaAudio.StopBus(KarmaSfxBus.Stinger);
            KarmaAudio.Play(KarmaSfxId.CombatBossDefeat, lifetimeOwner: endingPresentation);
        }
        void UpdateEnding()
        {
            if (endingSkipping)
            {
                endingSkipTime += Time.deltaTime;
                KarmaAudio.SetEndingAmbience(endingAmbienceLevel * (1 - EndingFade));
                if (endingSkipTime >= 0.4f) CompleteEnding();
                return;
            }
            EndingTime = Mathf.Min(ActiveEndingDuration, EndingTime + Time.deltaTime);
            if (IsEndingReview)
            {
                UpdateEndingAlternative();
                if (EndingTime >= ActiveEndingDuration) CompleteEnding();
                return;
            }
            float t = EndingTime;
            if (t >= 5.5f && t < 9.5f) Player.EndingWalkTo(KarmaStage.ArenaCenter,
                Mathf.Max(4f, Mathf.Abs(Player.Position.x - KarmaStage.ArenaCenter) / Mathf.Max(0.2f, 9.4f - t)));
            else if (t >= 34.5f && t < 39f) Player.EndingWalkTo(KarmaStage.Gate.x + 4, 3.1f);
            else Player.EndingWalkTo(Player.Position.x, 0);
            float ambience = t < 6 ? 1 - Mathf.SmoothStep(0, 1, t / 6)
                : t < 8 ? 0 : 0.08f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8, 10, t));
            endingAmbienceLevel = ambience;
            KarmaAudio.SetEndingAmbience(ambience * (1 - EndingFade));
            while (endingSoundStage < endingCueTimes.Length && t >= endingCueTimes[endingSoundStage])
            {
                KarmaAudio.Play(endingCues[endingSoundStage], lifetimeOwner: endingPresentation);
                endingSoundStage++;
            }
            if (t < EndingDuration) return;
            CompleteEnding();
        }
        void SkipEnding()
        {
            if (Phase != RunPhase.Ending || endingSkipping) return;
            endingSkipStartFade = EndingFade;
            endingSkipping = true; endingSkipTime = 0;
            Player.EndingWalkTo(Player.Position.x, 0);
        }
        void CompleteEnding()
        {
            EndingTime = ActiveEndingDuration;
            EndingCompleted = true; Phase = RunPhase.Victory;
            if (fallenGuardian != null) { fallenGuardian.gameObject.SetActive(false); Destroy(fallenGuardian.gameObject); fallenGuardian = null; }
            if (endingPresentation != null) { endingPresentation.gameObject.SetActive(false); Destroy(endingPresentation.gameObject); }
            endingPresentation = null;
            KarmaAudio.SetEndingAmbience(0);
        }
        void FrameEnding()
        {
            if (IsEndingReview) { FrameEndingAlternative(); return; }
            float t = EndingTime;
            var player = Player.Position + Vector2.up * 1.5f;
            Vector3 focus; float size;
            if (t < 6)
            {
                float p = Mathf.SmoothStep(0, 1, t / 6);
                var midpoint = Vector2.Lerp(player, (Vector2)fallenGuardianPosition + Vector2.up, 0.45f);
                focus = Vector3.Lerp(endingCameraStart, new Vector3(midpoint.x, midpoint.y + 1, -10), p);
                float bothActors = Mathf.Max(10, Mathf.Abs(player.x - fallenGuardianPosition.x) / (2 * GameCamera.aspect) + 3);
                size = Mathf.Lerp(endingCameraSize, bothActors, p);
            }
            else
            {
                focus = new Vector3(player.x - (t >= 20 && t < 30 ? 1.3f : 0), player.y + 0.8f, -10);
                size = t < 20 ? 7.4f : t < 30 ? 6.8f : Mathf.Lerp(7.4f,
                    Mathf.Max(endingCameraSize, 15.5f), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(33, 40, t)));
            }
            size = Mathf.Min(size, Mathf.Min(endingBackground.extents.y, endingBackground.extents.x / GameCamera.aspect));
            float width = size * GameCamera.aspect;
            focus.x = Mathf.Clamp(focus.x, endingBackground.min.x + width, endingBackground.max.x - width);
            focus.y = Mathf.Clamp(focus.y, endingBackground.min.y + size, endingBackground.max.y - size);
            float blend = 1 - Mathf.Exp(-2.8f * Time.deltaTime);
            GameCamera.orthographicSize = Mathf.Lerp(GameCamera.orthographicSize, size, blend);
            GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position, focus, blend);
            lastShake = Vector3.zero;
        }
        void CancelEnding()
        {
            if (IsEndingReview && Player != null) Player.EndReviewPresentation();
            EndingVersion = KarmaEndingVersion.Campaign;
            KarmaAudio.SetEndingMusic(-1);
            if (endingPresentation != null) { endingPresentation.gameObject.SetActive(false); Destroy(endingPresentation.gameObject); }
            endingPresentation = null; EndingTime = 0; EndingCompleted = false;
            endingSkipping = false; endingSkipTime = 0; endingAmbienceLevel = 1;
            ResetCinematicSkip();
            KarmaAudio.SetEndingAmbience(1);
        }
    }
}
