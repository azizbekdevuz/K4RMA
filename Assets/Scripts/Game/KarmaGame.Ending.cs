using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public const float EndingDuration = EndingDepartAt + 5.5f;
        internal const float EndingApproachAt = 5.4f, EndingKneelAt = 10f, EndingMasterAt = 10.6f;
        internal const float EndingStandAt = 20.5f, EndingGuardAt = 21.45f, EndingLiftAt = 22.53f, EndingWaveAt = 23.62f;
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
            : EndingEase(ActiveEndingDuration - 1.6f, ActiveEndingDuration, EndingTime);
        public float EndingMemoryDim => Phase == RunPhase.Ending
            ? 0.22f * EndingEase(EndingMasterAt, 12, EndingTime) * (1 - EndingEase(EndingMasterCloseEnd - 0.8f, EndingStandAt, EndingTime)) : 0;

        public float EndingImpactLight => Phase == RunPhase.Ending
            ? EndingEase(EndingUnionAt, EndingUnionAt + 0.08f, EndingTime)
                * (1 - EndingEase(EndingUnionAt + 0.10f, EndingUnionAt + 0.72f, EndingTime))
            : 0;

        const string DefaultEndingLocale = "ko";
        const string EndingDialogueResources = "EndingDialogue/";
        const float EndingVoiceDelay = 0.025f;

        readonly struct EndingCaption
        {
            public readonly string Id, Korean, KoreanContinuation;
            public readonly float Start, End, KoreanSplit;
            public readonly bool Master;

            public EndingCaption(string id, float start, float end, bool master, string korean,
                float koreanSplit = 0, string koreanContinuation = null)
            {
                Id = id; Start = start; End = end; Master = master; Korean = korean;
                KoreanSplit = koreanSplit; KoreanContinuation = koreanContinuation;
            }
        }

        [System.Serializable]
        sealed class EndingTranslation
        {
            public string master, apprentice;
            public EndingTranslationLine[] lines;
        }

        [System.Serializable]
        struct EndingTranslationLine
        {
            public string id, text;
        }

        static readonly EndingCaption[] endingCaptions = {
            new EndingCaption("apprentice_aftermath", 1.62f, 6.04f, false, "수호자는 쓰러졌는데… 제 검은 아직 떨립니다."),
            new EndingCaption("master_lesson", 10.84f, 14.85f, true, "배운 것을 그대로 쓰는 것만으로는 수련이 끝나지 않는다."),
            new EndingCaption("master_techniques", 14.91f, 20.84f, true, "막아 내고, 일으키고, 흘려보내라.",
                3.45f, "그다음은 네가 정할 차례다."),
            new EndingCaption("master_question", EndingMasterReplyAt, 30.95f, true, "방금 네가 이어 낸 그 한 수, 누가 가르쳤느냐?"),
            new EndingCaption("apprentice_answer", EndingApprenticeReplyAt, 36.80f, false, "시작은 스승님께 배웠습니다.",
                3.19f, "마지막은 제가 이었습니다."),
            new EndingCaption("master_farewell", EndingFarewellAt, 40.44f, true, "그럼 가거라.",
                1.52f, "다음 길도 네가 정해라.")
        };

        EndingCaption[] activeEndingCaptions = endingCaptions;

        internal float ActiveEndingApprenticeReplyAt => activeEndingCaptions[4].Start;
        internal float ActiveEndingFarewellAt => activeEndingCaptions[5].Start;
        internal float EndingDepartureTimeOffset => ActiveEndingFarewellAt - EndingFarewellAt;
        float ActiveEndingDuration => EndingDuration + EndingDepartureTimeOffset;

#if UNITY_EDITOR
        static readonly EndingCaption[] endingV0Captions = {
            new EndingCaption("apprentice_aftermath", 1.62f, 7.42f, false, "수호자는 쓰러졌는데… 아, 씨발. 손이 아직도 떨리네."),
            new EndingCaption("master_lesson", 10.68f, 14.51f, true, "검은 잘 쓰더라.", 1.72f, "근데 이 개판은 누가 치우냐?"),
            new EndingCaption("master_techniques", 14.57f, 21.55f, true, "막고, 띄우고, 날려라.", 2.80f,
                "적을 날리랬지, 마루를 뜯으랬냐, 이 새끼야."),
            new EndingCaption("master_question", EndingMasterReplyAt, 31.84f, true, "방금 그 좆같이 멋진 한 수…", 3.01f,
                "그건 누구한테 배웠냐?"),
            new EndingCaption("apprentice_answer", 31.92f, 37.78f, false, "처음은 스승님께 배웠습니다.", 2.98f,
                "마루 박살 낸 건 제 재능입니다."),
            new EndingCaption("master_farewell", 37.86f, 41.31f, true, "잘했다.", 1.89f, "씨발, 마루값은 내고 가라.")
        };

        AudioClip[] pendingEndingV0Clips;

        internal void QueueEndingV0Preview(AudioClip[] clips)
        {
            if (clips == null || clips.Length != endingV0Captions.Length)
                throw new System.ArgumentException("Ending v0 needs six dialogue recordings.", nameof(clips));
            for (int i = 0; i < clips.Length; i++)
            {
                var caption = endingV0Captions[i];
                if (clips[i] == null || clips[i].name != caption.Id || !EndingClipFits(caption, clips[i]))
                    throw new System.ArgumentException("Invalid Ending v0 recording: " + caption.Id, nameof(clips));
            }
            pendingEndingV0Clips = clips;
        }
#endif

        int CurrentEndingCaption
        {
            get
            {
                if (Phase != RunPhase.Ending) return -1;
                for (int i = 0; i < activeEndingCaptions.Length; i++)
                    if (EndingTime >= activeEndingCaptions[i].Start && EndingTime < activeEndingCaptions[i].End) return i;
                return -1;
            }
        }

        public string EndingLine
        {
            get
            {
                int index = CurrentEndingCaption;
                if (index < 0) return null;
                if (endingTranslatedLines != null) return endingTranslatedLines[index];
                var caption = activeEndingCaptions[index];
                return caption.KoreanContinuation != null && EndingTime >= caption.Start + EndingVoiceDelay + caption.KoreanSplit
                    ? caption.KoreanContinuation : caption.Korean;
            }
        }

        public string EndingSpeaker
        {
            get
            {
                int index = CurrentEndingCaption;
                if (index < 0) return null;
                return activeEndingCaptions[index].Master ? endingMasterLabel : endingApprenticeLabel;
            }
        }

        public float EndingLineAlpha
        {
            get
            {
                int index = CurrentEndingCaption;
                if (index < 0) return 0;
                var caption = activeEndingCaptions[index];
                return EndingEase(caption.Start, caption.Start + 0.10f, EndingTime)
                    * (1 - EndingEase(caption.End - 0.12f, caption.End, EndingTime));
            }
        }

        readonly AudioClip[] endingVoiceClips = new AudioClip[endingCaptions.Length];
        string[] endingTranslatedLines;
        string endingMasterLabel = "스승", endingApprenticeLabel = "제자";
        AudioSource endingDialogueSource;
        int endingSpokenCaption = -1;
        bool endingDialoguePaused;

        void BeginEndingDialogue()
        {
            endingMasterLabel = "스승";
            endingApprenticeLabel = "제자";
            endingTranslatedLines = null;
            activeEndingCaptions = endingCaptions;
#if UNITY_EDITOR
            if (pendingEndingV0Clips != null)
            {
                activeEndingCaptions = endingV0Captions;
                pendingEndingV0Clips.CopyTo(endingVoiceClips, 0);
                pendingEndingV0Clips = null;
                CreateEndingDialogueSource();
                return;
            }
#endif
            // A future settings screen can set a locale such as en-US or uz-Latn-UZ.
            // Each language pack supplies captions.json and WAVs keyed by the line IDs.
            string locale = PlayerPrefs.GetString("K4RMA.Options.Language", DefaultEndingLocale)
                .Trim().Split('-', '_')[0].ToLowerInvariant();
            endingTranslatedLines = LoadEndingTranslation(locale);
            if (endingTranslatedLines == null) locale = DefaultEndingLocale;

            string directory = EndingDialogueResources + locale + "/";
            for (int i = 0; i < endingCaptions.Length; i++)
            {
                var caption = endingCaptions[i];
                var clip = Resources.Load<AudioClip>(directory + caption.Id);
                endingVoiceClips[i] = EndingClipFits(caption, clip) ? clip : null;
            }
            CreateEndingDialogueSource();
        }

        static bool EndingClipFits(EndingCaption caption, AudioClip clip)
            => clip != null && clip.length <= caption.End - caption.Start - EndingVoiceDelay;

        void CreateEndingDialogueSource()
        {
            var dialogue = new GameObject("Ending dialogue");
            dialogue.transform.SetParent(endingPresentation.transform, false);
            endingDialogueSource = dialogue.AddComponent<AudioSource>();
            endingDialogueSource.playOnAwake = false;
            endingDialogueSource.spatialBlend = 0;
            endingDialogueSource.priority = 24;
            endingSpokenCaption = -1;
            endingDialoguePaused = false;
        }

        string[] LoadEndingTranslation(string locale)
        {
            if (locale == DefaultEndingLocale || locale.Length < 2 || locale.Length > 3) return null;
            foreach (char letter in locale)
                if (letter < 'a' || letter > 'z') return null;
            var asset = Resources.Load<TextAsset>(EndingDialogueResources + locale + "/captions");
            if (asset == null) return null;
            EndingTranslation pack;
            try { pack = JsonUtility.FromJson<EndingTranslation>(asset.text); }
            catch (System.ArgumentException) { return null; }
            if (pack?.lines == null || string.IsNullOrWhiteSpace(pack.master) || string.IsNullOrWhiteSpace(pack.apprentice))
                return null;

            var text = new string[endingCaptions.Length];
            for (int i = 0; i < endingCaptions.Length; i++)
            {
                foreach (var line in pack.lines)
                    if (line.id == endingCaptions[i].Id) { text[i] = line.text; break; }
                if (string.IsNullOrWhiteSpace(text[i])) return null;
            }
            endingMasterLabel = pack.master;
            endingApprenticeLabel = pack.apprentice;
            return text;
        }

        float EndingDialogueGain()
        {
            int index = CurrentEndingCaption;
            if (index < 0 || KarmaPreferences.Muted) return 1;
            var clip = endingVoiceClips[index];
            if (clip == null) return 1;
            float since = EndingTime - activeEndingCaptions[index].Start - EndingVoiceDelay;
            float speech = EndingEase(-0.08f, 0.07f, since) * (1 - EndingEase(clip.length - 0.14f, clip.length + 0.06f, since));
            return 1 - 0.54f * speech * KarmaPreferences.EffectsVolume;
        }

        void PresentEndingDialogue()
        {
            if (endingDialogueSource == null) return;
            float roleGain = endingSpokenCaption >= 0 && activeEndingCaptions[endingSpokenCaption].Master ? 0.74f : 0.94f;
            endingDialogueSource.volume = roleGain * KarmaPreferences.EffectsVolume * (1 - EndingFade);
            if (endingDialoguePaused != Paused)
            {
                endingDialoguePaused = Paused;
                if (Paused) endingDialogueSource.Pause(); else endingDialogueSource.UnPause();
            }
            if (Paused || endingSkipping) return;
            int index = CurrentEndingCaption;
            if (index == endingSpokenCaption) return;
            if (index >= 0 && EndingTime < activeEndingCaptions[index].Start + EndingVoiceDelay) return;

            endingDialogueSource.Stop();
            endingSpokenCaption = index;
            endingDialogueSource.clip = index >= 0 ? endingVoiceClips[index] : null;
            if (endingDialogueSource.clip == null) return;
            endingDialogueSource.volume = (activeEndingCaptions[index].Master ? 0.74f : 0.94f) * KarmaPreferences.EffectsVolume * (1 - EndingFade);
            float offset = EndingTime - activeEndingCaptions[index].Start - EndingVoiceDelay;
            if (offset >= endingDialogueSource.clip.length) return;
            endingDialogueSource.timeSamples = Mathf.FloorToInt(offset * endingDialogueSource.clip.frequency);
            endingDialogueSource.Play();
        }

        void EndEndingDialogue()
        {
            if (endingDialogueSource != null)
            {
                endingDialogueSource.Stop();
                endingDialogueSource.clip = null;
            }
            endingDialogueSource = null;
            endingTranslatedLines = null;
            System.Array.Clear(endingVoiceClips, 0, endingVoiceClips.Length);
        }

        KarmaEndingPresentation endingPresentation;
        Vector3 endingCameraStart;
        float endingCameraSize;
        int endingShot;
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
            endingShot = 0;
            endingCameraStart = GameCamera.transform.position;
            endingCameraSize = GameCamera.orthographicSize;
            var background = World.Find("Trial hall artwork") ?? World.Find("Mountain dojo artwork");
            endingBackground = background != null ? background.GetComponent<SpriteRenderer>().bounds
                : new Bounds(new Vector3(20, 8, 0), new Vector3(140, 56, 1));
            endingPresentation = KarmaEndingPresentation.Create(this, World, fallenGuardian, fallenGuardianPosition);
            BeginEndingDialogue();
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
            EndingTime = Mathf.Min(ActiveEndingDuration, EndingTime + Time.deltaTime);
            float t = EndingTime;
            if (t >= EndingApproachAt && t < EndingKneelAt)
            {
                float distance = Mathf.Abs(Player.Position.x - KarmaStage.ArenaCenter);
                Player.EndingWalkTo(KarmaStage.ArenaCenter, Mathf.Clamp(distance / Mathf.Max(0.18f, 9.55f - t), 2.4f, 11f));
            }
            else if (t >= EndingDepartAt + EndingDepartureTimeOffset)
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
            KarmaAudio.SetEndingAmbience(endingAmbienceLevel * (1 - EndingFade) * (1 - 0.24f * drawHush) * EndingDialogueGain());

            while (endingSoundStage < endingCueTimes.Length && t >= endingCueTimes[endingSoundStage])
                KarmaAudio.Play(endingCues[endingSoundStage++], lifetimeOwner: endingPresentation);

            if (t >= ActiveEndingDuration) CompleteEnding();
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
            EndEndingDialogue();
            Player.EndEndingPresentation();
            if (fallenGuardian != null) { fallenGuardian.gameObject.SetActive(false); Destroy(fallenGuardian.gameObject); fallenGuardian = null; }
            if (endingPresentation != null) { endingPresentation.gameObject.SetActive(false); Destroy(endingPresentation.gameObject); }
            endingPresentation = null;
            KarmaAudio.SetEndingAmbience(0);
            KarmaAudio.SetEndingMusic(-1);
        }

        void FrameEnding()
        {
            PresentEndingDialogue();
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
                float demonstration = EndingEase(EndingStandAt, EndingGuardAt, t)
                    * (1 - EndingEase(EndingUnionAt + 0.8f, EndingUnionAt + 1.55f, t));
                float departure = EndingEase(EndingDepartAt + EndingDepartureTimeOffset, ActiveEndingDuration - 0.4f, t);
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
                || t >= EndingMasterReplyAt && t < ActiveEndingApprenticeReplyAt)
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
            else if (t >= ActiveEndingApprenticeReplyAt && t < ActiveEndingFarewellAt)
            {
                shot = 6;
                float answer = EndingEase(ActiveEndingApprenticeReplyAt, ActiveEndingFarewellAt, t);
                focus = new Vector3(playerX - Mathf.Lerp(1.35f, 1.9f, answer), 2.65f, -10);
                size = Mathf.Lerp(4.65f, 4.45f, answer);
            }
            else if (t >= ActiveEndingFarewellAt) shot = 7;

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
#if UNITY_EDITOR
            pendingEndingV0Clips = null;
#endif
            activeEndingCaptions = endingCaptions;
            EndEndingDialogue();
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
