using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public const float EndingDuration = EndingDepartAt + 5.5f;
        internal const float EndingApproachAt = 5.4f, EndingKneelAt = 10f, EndingMasterAt = 10.6f;
        internal const float EndingStandAt = 20.5f, EndingCounterAt = 21.45f, EndingAirSlashAt = 22.53f, EndingPierceAt = 23.62f;
        internal const float EndingAirJumpAt = EndingAirSlashAt - 0.20f, EndingAirLandAt = EndingAirSlashAt + 0.72f;
        internal const float EndingPierceReleaseAt = EndingPierceAt + EndingRelease;
        internal const float EndingUnionAt = 25.55f;
        internal const float EndingSaluteAt = EndingFarewellAt + 0.1f;
        internal const float EndingDepartAt = EndingFarewellAt + 3.72f, EndingTurnAt = EndingDepartAt - 0.25f;
        internal const float EndingTurnToMasterAt = EndingUnionAt + 1.25f;
        internal const float EndingMasterCloseAt = 12f, EndingMasterCloseEnd = 20.48f;
        internal const float EndingDrawShotAt = 24.15f;
        internal const float EndingDrawAt = EndingUnionAt - 0.79f;
        internal const float EndingSheatheAt = EndingUnionAt + 0.61f;
        internal const float EndingSheatheDuration = 0.49f;
        internal const float EndingMasterReplyAt = 27.1f, EndingApprenticeReplyAt = 31.02f;
        internal const float EndingFarewellAt = 36.91f;
        internal const float EndingMasterX = KarmaStage.ArenaCenter - 5.2f;
        internal const float EndingRelease = 3f / 24f;

        public float EndingTime { get; private set; }
        public bool EndingCompleted { get; private set; }
        public float EndingFade => EndingCompleted ? 1 : endingSkipping
            ? Mathf.Lerp(endingSkipStartFade, 1, EndingEase(0, 0.4f, endingSkipTime))
            : EndingEase(EndingDuration - 1.6f, EndingDuration, EndingTime);
        public float EndingMemoryDim => Phase == RunPhase.Ending
            ? 0.22f * EndingEase(EndingMasterAt, 12, EndingTime) * (1 - EndingEase(EndingMasterCloseEnd - 0.8f, EndingStandAt, EndingTime)) : 0;

        public float EndingImpactLight => Phase == RunPhase.Ending
            ? EndingEase(EndingUnionAt, EndingUnionAt + 0.08f, EndingTime)
                * (1 - EndingEase(EndingUnionAt + 0.10f, EndingUnionAt + 0.72f, EndingTime))
            : 0;

        readonly struct EndingCaption
        {
            public readonly float Start, End;
            public readonly string Speaker, Text;

            public EndingCaption(float start, float end, string speaker, string text)
            { Start = start; End = end; Speaker = speaker; Text = text; }
        }

        static readonly EndingCaption[] endingCaptions = {
            new EndingCaption(1.62f, 6.04f, "제자", "마지막 수호자까지 넘어섰습니다."),
            new EndingCaption(10.84f, 14.85f, "스승", "내게 배운 세 기술의 원형은 이제 네 손에 없구나."),
            new EndingCaption(14.91f, 18.35f, "스승", "그래도 그 가르침은 네 검에 남아 있다."),
            new EndingCaption(18.45f, 20.84f, "스승", "어떻게 네 것이 되었는지 보여 다오."),
            new EndingCaption(EndingCounterAt - 0.18f, EndingAirJumpAt - 0.04f, "고유화", KarmaPatternCatalog.UniqueName(Essence.Guard)),
            new EndingCaption(EndingAirJumpAt, EndingPierceAt - 0.12f, "고유화", KarmaPatternCatalog.UniqueName(Essence.RisingSlash)),
            new EndingCaption(EndingPierceAt - 0.08f, EndingDrawShotAt, "고유화", KarmaPatternCatalog.UniqueName(Essence.SwordWave)),
            new EndingCaption(EndingMasterReplyAt, 30.95f, "스승", "이제 그 기술들은 누구의 것이냐?"),
            new EndingCaption(EndingApprenticeReplyAt, 34.15f, "제자", "스승님께 배웠습니다."),
            new EndingCaption(34.25f, 36.80f, "제자", "하지만 이제는 제 방식으로 쓸 수 있습니다."),
            new EndingCaption(EndingFarewellAt, 38.40f, "스승", "그렇다면 수련은 헛되지 않았구나."),
            new EndingCaption(38.50f, 40.44f, "스승", "이제 네 길을 가거라.")
        };

        EndingCaption? CurrentEndingCaption
        {
            get
            {
                if (Phase == RunPhase.Ending)
                    foreach (var caption in endingCaptions)
                        if (EndingTime >= caption.Start && EndingTime < caption.End) return caption;
                return null;
            }
        }

        public string EndingLine => CurrentEndingCaption?.Text;
        public string EndingSpeaker => CurrentEndingCaption?.Speaker;
        public float EndingLineAlpha => CurrentEndingCaption is EndingCaption caption
            ? EndingEase(caption.Start, caption.Start + 0.10f, EndingTime)
                * (1 - EndingEase(caption.End - 0.12f, caption.End, EndingTime))
            : 0;

        KarmaEndingPresentation endingPresentation;
        Vector3 endingCameraStart;
        float endingCameraSize;
        int endingShot;
        Bounds endingBackground;
        int endingSoundStage;
        bool endingSkipping;
        float endingSkipTime, endingSkipStartFade, endingAmbienceLevel = 1;
        static readonly KarmaSfxId[] endingCues = { KarmaSfxId.CombatPerfectParry, KarmaSfxId.PlSwordSwing,
            KarmaSfxId.PlDash, KarmaSfxId.GameAbilityInherit };
        static readonly float[] endingCueTimes = { EndingCounterAt + EndingRelease, EndingAirSlashAt + EndingRelease,
            EndingPierceReleaseAt, EndingUnionAt };

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
            endingShot = 0;
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
                Player.EndingWalkTo(KarmaStage.ArenaCenter, Mathf.Clamp(distance / Mathf.Max(0.18f, 9.55f - t), 2.4f, 11f));
            }
            else if (t >= EndingDepartAt)
                Player.EndingWalkTo(KarmaStage.Gate.x + 2, 4.8f);
            else
                Player.EndingWalkTo(Player.Position.x, 0);

            if (previous < EndingMasterAt && t >= EndingMasterAt)
                KarmaAudio.SetEndingMusic(0); // K4RMA Main Theme, from its opening phrase.
            endingAmbienceLevel = t < EndingMasterAt
                ? 1 - EndingEase(0.7f, 5.8f, t)
                : EndingEase(EndingMasterAt + 0.3f, 14.5f, t)
                    * Mathf.Lerp(0.58f, 0.88f, EndingEase(16.4f, EndingUnionAt + 1f, t));
            // Leave a short pocket for the draw before the release cue and wide shot.
            float drawHush = EndingEase(EndingDrawShotAt, EndingDrawAt, t)
                * (1 - EndingEase(EndingUnionAt, EndingUnionAt + 0.66f, t));
            KarmaAudio.SetEndingAmbience(endingAmbienceLevel * (1 - EndingFade) * (1 - 0.24f * drawHush));

            if (previous < EndingPierceReleaseAt && t >= EndingPierceReleaseAt) Player.EndingPierce();

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
                float lesson = EndingEase(8.5f, 11.4f, t);
                float demonstration = EndingEase(EndingStandAt, EndingCounterAt, t)
                    * (1 - EndingEase(EndingUnionAt + 0.8f, EndingUnionAt + 1.55f, t));
                float departure = EndingEase(EndingDepartAt, EndingDuration - 0.4f, t);
                float follow = EndingEase(EndingUnionAt, EndingUnionAt + 0.66f, t)
                    * (1 - EndingEase(EndingUnionAt + 0.86f, EndingMasterReplyAt, t));
                float conversationX = Mathf.Lerp(playerX, (playerX + EndingMasterX) * 0.5f, lesson);
                float focusX = Mathf.Lerp(conversationX, playerX + 3.15f + follow * 3.45f, demonstration);
                focusX = Mathf.Lerp(focusX, KarmaStage.Gate.x + 2, departure);
                focus = new Vector3(focusX, Mathf.Lerp(3.55f + demonstration * 0.55f, 7.1f, departure), -10);
                size = Mathf.Lerp(Mathf.Lerp(8.1f, 4.9f, lesson), 6.65f + follow * 1.25f, demonstration);
                size = Mathf.Lerp(size, Mathf.Max(endingCameraSize, 13.6f), departure);
            }

            // Cut on the teacher's gesture, the draw, and the exchange after the strike.
            // These shots share the same screen direction and the same world-space actors.
            int shot = 0;
            if (t >= EndingMasterCloseAt && t < EndingMasterCloseEnd
                || t >= EndingMasterReplyAt && t < EndingApprenticeReplyAt)
            {
                shot = t < EndingMasterCloseEnd ? 1 : 5;
                float start = shot == 1 ? EndingMasterCloseAt : EndingMasterReplyAt;
                float push = EndingEase(start, start + 2.1f, t);
                focus = new Vector3(EndingMasterX + 1.05f, Mathf.Lerp(2.95f, 3.2f, push), -10);
                size = Mathf.Lerp(3.05f, 2.63f, push);
            }
            else if (t >= EndingMasterCloseEnd && t < EndingDrawShotAt) shot = 2;
            else if (t >= EndingDrawShotAt && t < EndingUnionAt)
            {
                shot = 3;
                float draw = EndingEase(EndingDrawShotAt, EndingUnionAt - 0.13f, t);
                focus = new Vector3(playerX + Mathf.Lerp(0.7f, 1.8f, draw), Mathf.Lerp(2.48f, 2.8f, draw), -10);
                // The medium shot keeps the full stance in frame and limits sprite enlargement.
                size = Mathf.Lerp(5.05f, 4.45f, draw);
            }
            else if (t >= EndingUnionAt && t < EndingMasterReplyAt) shot = 4;
            else if (t >= EndingApprenticeReplyAt && t < EndingFarewellAt)
            {
                shot = 6;
                float answer = EndingEase(EndingApprenticeReplyAt, EndingFarewellAt, t);
                focus = new Vector3(playerX - Mathf.Lerp(1.35f, 1.9f, answer), 2.65f, -10);
                size = Mathf.Lerp(4.65f, 4.45f, answer);
            }
            else if (t >= EndingFarewellAt) shot = 7;

            float aspect = Mathf.Max(0.01f, GameCamera.aspect);
            size = Mathf.Min(size, Mathf.Min(endingBackground.extents.y, endingBackground.extents.x / aspect));
            float width = size * aspect;
            focus.x = Mathf.Clamp(focus.x, endingBackground.min.x + width, endingBackground.max.x - width);
            focus.y = Mathf.Clamp(focus.y, endingBackground.min.y + size, endingBackground.max.y - size);
            float blend = shot != endingShot ? 1 : 1 - Mathf.Exp(-5f * Time.deltaTime);
            endingShot = shot;
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
