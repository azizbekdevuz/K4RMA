using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public const float EndingDuration = 40f;
        internal const float EndingApproachAt = 5.4f, EndingKneelAt = 10f, EndingMasterAt = 10.6f;
        internal const float EndingStandAt = 16.8f, EndingGuardAt = 17.6f, EndingLiftAt = 18.8f, EndingWaveAt = 20f;
        internal const float EndingUnionAt = 21.6f, EndingSaluteAt = 30.5f, EndingTurnAt = 34.25f, EndingDepartAt = 34.5f;
        internal const float EndingMasterX = KarmaStage.ArenaCenter - 5.2f;
        internal const float EndingRelease = 3f / 24f;

        public float EndingTime { get; private set; }
        public bool EndingCompleted { get; private set; }
        public float EndingFade => EndingCompleted ? 1 : endingSkipping
            ? Mathf.Lerp(endingSkipStartFade, 1, EndingEase(0, 0.4f, endingSkipTime))
            : EndingEase(38.4f, EndingDuration, EndingTime);
        public float EndingMemoryDim => Phase == RunPhase.Ending
            ? 0.22f * EndingEase(EndingMasterAt, 12, EndingTime) * (1 - EndingEase(16, 17.5f, EndingTime)) : 0;

        readonly struct EndingCaption
        {
            public readonly float Start, End;
            public readonly string Speaker, Line;
            public EndingCaption(float start, float end, string speaker, string line)
            { Start = start; End = end; Speaker = speaker; Line = line; }
        }

        static readonly EndingCaption[] endingCaptions = {
            new EndingCaption(2.4f, 5.2f, "제자", "수호자는 쓰러졌는데… 제 검은 아직 떨립니다."),
            new EndingCaption(11, 13.8f, "스승", "배운 것을 그대로 쓰는 것만으로는 수련이 끝나지 않는다."),
            new EndingCaption(14.1f, 16.7f, "스승", "막아 내고, 일으키고, 흘려보내라. 그다음은 네가 정할 차례다."),
            new EndingCaption(23.8f, 26.5f, "스승", "방금 네가 이어 낸 그 한 수, 누가 가르쳤느냐?"),
            new EndingCaption(27, 30.3f, "제자", "시작은 스승님께 배웠습니다. 마지막은 제가 이었습니다."),
            new EndingCaption(31.4f, 33.8f, "스승", "그럼 가거라. 다음 길도 네가 정해라.")
        };

        EndingCaption CurrentEndingCaption
        {
            get
            {
                foreach (var caption in endingCaptions)
                    if (EndingTime >= caption.Start && EndingTime < caption.End) return caption;
                return default;
            }
        }

        public string EndingLine => CurrentEndingCaption.Line;
        public string EndingSpeaker => CurrentEndingCaption.Speaker;
        public float EndingLineAlpha
        {
            get
            {
                var caption = CurrentEndingCaption;
                if (caption.Line == null) return 0;
                return EndingEase(caption.Start, caption.Start + 0.3f, EndingTime)
                    * (1 - EndingEase(caption.End - 0.32f, caption.End, EndingTime));
            }
        }

        KarmaEndingPresentation endingPresentation;
        Vector3 endingCameraStart;
        float endingCameraSize;
        Bounds endingBackground;
        int endingSoundStage;
        bool endingSkipping;
        float endingSkipTime, endingSkipStartFade, endingAmbienceLevel = 1;
        static readonly KarmaSfxId[] endingCues = { KarmaSfxId.GdShieldActivate, KarmaSfxId.PlRisingSlash,
            KarmaSfxId.SwWaveLaunch, KarmaSfxId.GameAbilityInherit };
        static readonly float[] endingCueTimes = { EndingGuardAt + EndingRelease, EndingLiftAt + EndingRelease,
            EndingWaveAt + EndingRelease, EndingUnionAt };

        internal static float EndingEase(float start, float end, float time)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, time));

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

            float previous = EndingTime;
            EndingTime = Mathf.Min(EndingDuration, EndingTime + Time.deltaTime);
            float t = EndingTime;
            if (t >= EndingApproachAt && t < EndingKneelAt)
            {
                float distance = Mathf.Abs(Player.Position.x - KarmaStage.ArenaCenter);
                Player.EndingWalkTo(KarmaStage.ArenaCenter, Mathf.Clamp(distance / Mathf.Max(0.2f, 9.85f - t), 2.4f, 9f));
            }
            else if (t >= EndingDepartAt)
                Player.EndingWalkTo(KarmaStage.Gate.x + 2, 4.2f);
            else
                Player.EndingWalkTo(Player.Position.x, 0);

            if (previous < EndingMasterAt && t >= EndingMasterAt)
                KarmaAudio.SetEndingMusic(0); // K4RMA Main Theme, from its opening phrase.
            endingAmbienceLevel = t < EndingMasterAt
                ? 1 - EndingEase(0.7f, 5.8f, t)
                : EndingEase(EndingMasterAt + 0.3f, 14.5f, t)
                    * Mathf.Lerp(0.58f, 0.88f, EndingEase(16.4f, EndingUnionAt + 1f, t));
            KarmaAudio.SetEndingAmbience(endingAmbienceLevel * (1 - EndingFade));

            while (endingSoundStage < endingCueTimes.Length && t >= endingCueTimes[endingSoundStage])
                KarmaAudio.Play(endingCues[endingSoundStage++], lifetimeOwner: endingPresentation);

            if (t >= EndingDuration) CompleteEnding();
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
            EndingTime = EndingDuration;
            EndingCompleted = true; Phase = RunPhase.Victory;
            Player.EndEndingPresentation();
            if (fallenGuardian != null) { fallenGuardian.gameObject.SetActive(false); Destroy(fallenGuardian.gameObject); fallenGuardian = null; }
            if (endingPresentation != null) { endingPresentation.gameObject.SetActive(false); Destroy(endingPresentation.gameObject); }
            endingPresentation = null;
            KarmaAudio.SetEndingAmbience(0);
            KarmaAudio.SetEndingMusic(-1);
        }

        void FrameEnding()
        {
            float t = EndingTime;
            float playerX = Player.Position.x;
            Vector3 focus;
            float size;
            if (t < EndingApproachAt)
            {
                float bothActors = Mathf.Max(8.8f, Mathf.Abs(playerX - fallenGuardianPosition.x) / (2 * GameCamera.aspect) + 3);
                float settle = EndingEase(0, 3.5f, t);
                var fallenFocus = new Vector3(Mathf.Lerp(playerX, fallenGuardianPosition.x, 0.5f), 3.8f, -10);
                focus = Vector3.Lerp(endingCameraStart, fallenFocus, settle);
                size = Mathf.Lerp(endingCameraSize, bothActors, settle);
            }
            else
            {
                // Hold both figures for the lesson. Follow the blade through the
                // demonstration, then let the apprentice walk out of the two-shot.
                float lesson = EndingEase(8.5f, 11.4f, t);
                float demonstration = EndingEase(16.7f, 18.3f, t) * (1 - EndingEase(22.8f, 24.2f, t));
                float departure = EndingEase(EndingDepartAt, 39.6f, t);
                float conversationX = Mathf.Lerp(playerX, (playerX + EndingMasterX) * 0.5f, lesson);
                float focusX = Mathf.Lerp(conversationX, playerX + 2.2f, demonstration);
                focusX = Mathf.Lerp(focusX, KarmaStage.Gate.x + 2, departure);
                focus = new Vector3(focusX, Mathf.Lerp(3.65f + demonstration * 0.6f, 7.1f, departure), -10);
                size = Mathf.Lerp(Mathf.Lerp(8.1f, 5.8f, lesson), 7.4f, demonstration);
                size = Mathf.Lerp(size, Mathf.Max(endingCameraSize, 13.6f), departure);
            }

            float aspect = Mathf.Max(0.01f, GameCamera.aspect);
            size = Mathf.Min(size, Mathf.Min(endingBackground.extents.y, endingBackground.extents.x / aspect));
            float width = size * aspect;
            focus.x = Mathf.Clamp(focus.x, endingBackground.min.x + width, endingBackground.max.x - width);
            focus.y = Mathf.Clamp(focus.y, endingBackground.min.y + size, endingBackground.max.y - size);
            float blend = 1 - Mathf.Exp(-3.2f * Time.deltaTime);
            GameCamera.orthographicSize = Mathf.Lerp(GameCamera.orthographicSize, size, blend);
            GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position, focus, blend);
            lastShake = Vector3.zero;
        }

        void CancelEnding()
        {
            if (Player != null) Player.EndEndingPresentation();
            if (endingPresentation != null) { endingPresentation.gameObject.SetActive(false); Destroy(endingPresentation.gameObject); }
            endingPresentation = null; EndingTime = 0; EndingCompleted = false;
            endingSkipping = false; endingSkipTime = 0; endingAmbienceLevel = 1;
            ResetCinematicSkip();
            KarmaAudio.SetEndingAmbience(1);
            KarmaAudio.SetEndingMusic(-1);
        }
    }
}
