using UnityEngine;

namespace K4RMA
{
    public enum AudioCue
    {
        Swing,
        Hit,
        PlayerHurt,
        Projectile,
        DeflectReady,
        Deflect,
        Altar,
        Transfer,
        RisingSlash,
        Guard,
        GuardBlock
    }

    public class AudioFeedback : MonoBehaviour
    {
        static AudioFeedback current;
        AudioSource source;
        AudioClip swing;
        AudioClip hit;
        AudioClip hurt;
        AudioClip shot;
        AudioClip ready;
        AudioClip deflect;
        AudioClip altar;
        AudioClip transfer;
        AudioClip risingSlash;
        AudioClip guard;
        AudioClip guardBlock;

        void Awake()
        {
            current = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            swing = Tone(180f, 0.09f, 18f);
            hit = Tone(90f, 0.12f, 14f);
            hurt = Tone(140f, 0.16f, 8f);
            shot = Tone(520f, 0.1f, 10f);
            ready = Tone(740f, 0.08f, 12f);
            deflect = Tone(880f, 0.18f, 6f);
            altar = Tone(220f, 0.35f, 3f);
            transfer = Tone(360f, 0.45f, 2.5f);
            risingSlash = Tone(280f, 0.16f, 8f);
            guard = Tone(160f, 0.12f, 6f);
            guardBlock = Tone(640f, 0.08f, 14f);
        }

        void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        public static void Play(AudioCue cue)
        {
            if (current == null || current.source == null)
                return;
            AudioClip clip = cue switch
            {
                AudioCue.Swing => current.swing,
                AudioCue.Hit => current.hit,
                AudioCue.PlayerHurt => current.hurt,
                AudioCue.Projectile => current.shot,
                AudioCue.DeflectReady => current.ready,
                AudioCue.Deflect => current.deflect,
                AudioCue.Altar => current.altar,
                AudioCue.Transfer => current.transfer,
                AudioCue.RisingSlash => current.risingSlash,
                AudioCue.Guard => current.guard,
                AudioCue.GuardBlock => current.guardBlock,
                _ => null
            };
            if (clip != null)
                current.source.PlayOneShot(clip, 0.35f);
        }

        static AudioClip Tone(float frequency, float duration, float decay)
        {
            const int rate = 22050;
            int count = Mathf.Max(1, Mathf.RoundToInt(rate * duration));
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)rate;
                float envelope = Mathf.Exp(-time * decay);
                data[i] = Mathf.Sin(Mathf.PI * 2f * frequency * time) * envelope * 0.4f;
            }

            var clip = AudioClip.Create("cue", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
