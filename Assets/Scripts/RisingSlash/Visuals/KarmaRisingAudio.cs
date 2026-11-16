namespace KarmaPrototype
{
    // Rising Slash presentation audio. Playback length never changes attack timing.
    static class KarmaRisingAudio
    {
        public static void Warn(float seconds) { KarmaAudio.Play(KarmaSfxId.RsTelegraph, seconds); }
        public static void EndWarning() { KarmaAudio.StopSlot("rs-warn"); }
        public static void Dash() { KarmaAudio.Play(KarmaSfxId.RsDashStart); }
        public static void Descent(float seconds) { KarmaAudio.Play(KarmaSfxId.RsUp01Descent, seconds); }
        public static void Pursuit() { KarmaAudio.Play(KarmaSfxId.RsUp02Pursuit); }
        public static void Wave() { KarmaAudio.Play(KarmaSfxId.RsUp03Wave); }
        public static void Charge(float seconds) { KarmaAudio.Play(KarmaSfxId.RsUp05Charge, seconds); }
        public static void Crossing() { KarmaAudio.Play(KarmaSfxId.RsUp05Crossing); }
        public static void Slash(int index) { KarmaAudio.Play(index == 0 ? KarmaSfxId.RsSlash01 : KarmaSfxId.RsSlash02); }
        public static void Knockback() { KarmaAudio.Play(KarmaSfxId.RsUp04Knockback); }

        public static void Landing()
        {
            KarmaAudio.StopSlot("rs-motion");
            KarmaAudio.Play(KarmaSfxId.RsUp01Landing);
        }

        public static void ShieldBurst()
        {
            KarmaAudio.Play(KarmaSfxId.RsUp04ShieldBurst);
            KarmaAudio.Play(KarmaSfxId.RsShardsRelease);
        }

        public static void StopPattern()
        {
            EndWarning();
            KarmaAudio.StopSlot("rs-motion");
            KarmaAudio.StopSlot("rs-action");
        }
    }
}
