using UnityEngine;

namespace KarmaPrototype
{
    public enum KarmaEndingVersion { Campaign, Legacy, Cycle, Lesson }

    public sealed partial class KarmaGame
    {
        public KarmaEndingVersion EndingVersion { get; private set; }
        public bool IsEndingReview => EndingVersion != KarmaEndingVersion.Campaign;
        public float ActiveEndingDuration => EndingVersion == KarmaEndingVersion.Legacy ? 28
            : EndingVersion == KarmaEndingVersion.Cycle ? 27 : EndingVersion == KarmaEndingVersion.Lesson ? 24 : EndingDuration;
        public string EndingReviewTitle => EndingVersion == KarmaEndingVersion.Legacy ? "계승의 완성"
            : EndingVersion == KarmaEndingVersion.Cycle ? "윤회의 끝" : "마지막 수업";

#if UNITY_EDITOR
        // Explicit preview setup only. It cannot be selected or called in a Player build.
        public void PreviewEnding(KarmaEndingVersion version)
        {
            if (version == KarmaEndingVersion.Campaign) { ReturnToTitle(); return; }
            StartRun();
            foreach (Essence essence in System.Enum.GetValues(typeof(Essence)))
            { Player.Essences.Preview(essence); Player.Essences.Confirm(out _); }
            StageIndex = 3; LoadRoom(true);
            Player.ResetForRoom(new Vector2(18, KarmaSpace.PlayerHalfHeight), true);
            EndingVersion = version;
            // Use the ordinary defeat/ending handoff, with preview identity visible throughout.
            Enemies[0].TakeDamage(Enemies[0].MaxHealth * 1000, Player.Position);
            KarmaAudio.SetEndingMusic(version == KarmaEndingVersion.Cycle ? -1 : 0);
        }
        public void SkipEndingReview() { if (IsEndingReview) SkipEnding(); }
#endif

        float AlternativeFade
        {
            get
            {
                float fade = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ActiveEndingDuration - 2, ActiveEndingDuration, EndingTime));
                if (EndingVersion == KarmaEndingVersion.Lesson)
                    fade = Mathf.Max(fade, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8.8f, 9.6f, EndingTime))
                        * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(11, 11.6f, EndingTime))));
                return fade;
            }
        }
        readonly struct EndingSpeech
        {
            public readonly float Start, End;
            public readonly string Text;
            public EndingSpeech(float start, float end, string text) { Start = start; End = end; Text = text; }
        }
        static readonly EndingSpeech[] legacySpeech = {
            new EndingSpeech(3, 5.8f, "제자: 배운 것은 세 가지. 이어가는 방식은 하나."),
            new EndingSpeech(17, 20.5f, "스승: 이제 그 기술들은 누구의 것이냐?"),
            new EndingSpeech(21, 25.5f, "제자: 배웠습니다. 하지만 이제는 제 방식으로 쓸 수 있습니다.") };
        static readonly EndingSpeech[] cycleSpeech = {
            new EndingSpeech(9, 12.5f, "배운 것을 버리는 것이 아니라,"),
            new EndingSpeech(16, 20.5f, "같은 자리에 머무르지 않는 것.") };
        static readonly EndingSpeech[] lessonSpeech = {
            new EndingSpeech(3, 6.3f, "스승: 오늘의 시련은 여기까지다."),
            new EndingSpeech(6.5f, 8.8f, "제자: 감사합니다, 스승님."),
            new EndingSpeech(10.2f, 13.8f, "스승: 잠깐. 마지막 수업이 남았다."),
            new EndingSpeech(14.2f, 16.7f, "제자: …아직 한 분 더 계십니까?"),
            new EndingSpeech(17, 19.8f, "스승: 아니다. 수련장 정리."),
            new EndingSpeech(20, 22.7f, "제자: …제 방식으로 해도 됩니까?") };
        string AlternativeLine(out float start, out float end)
        {
            var lines = EndingVersion == KarmaEndingVersion.Legacy ? legacySpeech
                : EndingVersion == KarmaEndingVersion.Cycle ? cycleSpeech : lessonSpeech;
            foreach (var line in lines)
                if (EndingTime >= line.Start && EndingTime < line.End)
                { start = line.Start; end = line.End; return line.Text; }
            start = end = 0; return null;
        }
        static readonly float[] legacyTimes = { 6, 8.7f, 11.2f, 15.5f };
        static readonly KarmaSfxId[] legacySounds = { KarmaSfxId.SwWaveLaunch, KarmaSfxId.PlRisingSlash,
            KarmaSfxId.GdShieldActivate, KarmaSfxId.GameAbilityInherit };
        static readonly float[] cycleTimes = { 13.5f };
        static readonly KarmaSfxId[] cycleSounds = { KarmaSfxId.GdDomeSuccess };
        static readonly float[] lessonTimes = { 7, 10.2f, 14.1f, 20 };
        static readonly KarmaSfxId[] lessonSounds = { KarmaSfxId.GameAbilityInherit, KarmaSfxId.UiCancel,
            KarmaSfxId.GdShieldActivate, KarmaSfxId.PlSwordSwing };

        void UpdateEndingAlternative()
        {
            float t = EndingTime;
            if (EndingVersion == KarmaEndingVersion.Legacy)
                Player.EndingWalkTo(t < 5.5f ? 20 : t > 23 ? 32 : Player.Position.x, t < 5.5f || t > 23 ? 2.4f : 0);
            else if (EndingVersion == KarmaEndingVersion.Cycle)
                Player.EndingWalkTo(t < 8 ? 20 : t > 18 ? 33 : Player.Position.x, t < 8 || t > 18 ? 1.7f : 0);
            else
            {
                bool depart = t >= 7 && t < 9;
                bool returnToMaster = t >= 18.5f && t < 19.7f || t >= 20.8f;
                Player.EndingWalkTo(depart ? 25 : returnToMaster ? 17 : Player.Position.x,
                    depart || returnToMaster ? 2.3f : 0);
            }
            endingAmbienceLevel = EndingVersion == KarmaEndingVersion.Cycle
                ? 1 - Mathf.SmoothStep(0, 1, t / 4)
                : EndingVersion == KarmaEndingVersion.Lesson && t >= 8.8f ? 0 : .55f;
            KarmaAudio.SetEndingAmbience(endingAmbienceLevel * (1 - EndingFade));
            var times = EndingVersion == KarmaEndingVersion.Legacy ? legacyTimes : EndingVersion == KarmaEndingVersion.Cycle ? cycleTimes : lessonTimes;
            var sounds = EndingVersion == KarmaEndingVersion.Legacy ? legacySounds : EndingVersion == KarmaEndingVersion.Cycle ? cycleSounds : lessonSounds;
            while (endingSoundStage < times.Length && t >= times[endingSoundStage])
                KarmaAudio.Play(sounds[endingSoundStage++], lifetimeOwner: endingPresentation);
        }
        void FrameEndingAlternative()
        {
            float t = EndingTime;
            float size = EndingVersion == KarmaEndingVersion.Cycle ? Mathf.Lerp(9, 12, Mathf.SmoothStep(0, 1, t / 18))
                : EndingVersion == KarmaEndingVersion.Lesson ? 7.4f : t < 14 ? 8 : Mathf.Lerp(7.3f, 10, Mathf.SmoothStep(0, 1, (t - 22) / 6));
            var focus = new Vector3(EndingVersion == KarmaEndingVersion.Cycle ? 20 : EndingVersion == KarmaEndingVersion.Lesson ? 20 : Player.Position.x - 1, 5.7f, -10);
            size = Mathf.Min(size, Mathf.Min(endingBackground.extents.y, endingBackground.extents.x / GameCamera.aspect));
            float width = size * GameCamera.aspect;
            focus.x = Mathf.Clamp(focus.x, endingBackground.min.x + width, endingBackground.max.x - width);
            focus.y = Mathf.Clamp(focus.y, endingBackground.min.y + size, endingBackground.max.y - size);
            float blend = 1 - Mathf.Exp(-2.8f * Time.deltaTime);
            GameCamera.orthographicSize = Mathf.Lerp(GameCamera.orthographicSize, size, blend);
            GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position, focus, blend);
            lastShake = Vector3.zero;
        }
    }
}
