using UnityEngine;

namespace K4RMA
{
    public class ImpactFeedback : MonoBehaviour
    {
        [SerializeField] float hitStopSeconds = 0.05f;
        [SerializeField] float meleeShake = 0.16f;
        [SerializeField] float deflectShake = 0.28f;

        static ImpactFeedback current;
        float stopUntil;
        float storedScale = 1f;
        bool stopping;

        void Awake()
        {
            current = this;
        }

        void OnDestroy()
        {
            if (current == this)
                current = null;
            if (stopping)
                Time.timeScale = storedScale;
        }

        public static void PlayHit(Vector3 point, bool victimIsPlayer)
        {
            if (current == null)
                return;
            current.Hit(point, victimIsPlayer);
        }

        public static void PlayDeflect(Vector3 point)
        {
            if (current == null)
                return;
            SparkBurst.Play(point, new Color(0.75f, 0.95f, 1f));
            current.Shake(current.deflectShake);
            current.BeginHitStop();
            AudioFeedback.Play(AudioCue.Deflect);
        }

        void Hit(Vector3 point, bool victimIsPlayer)
        {
            SparkBurst.Play(point, victimIsPlayer ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.92f, 0.7f));
            Shake(meleeShake);
            BeginHitStop();
            AudioFeedback.Play(victimIsPlayer ? AudioCue.PlayerHurt : AudioCue.Hit);
        }

        void Shake(float strength)
        {
            var camera = FindAnyObjectByType<SideViewCamera>();
            camera?.Shake(strength, 0.14f);
        }

        void BeginHitStop()
        {
            if (hitStopSeconds <= 0f || stopping)
                return;
            storedScale = Time.timeScale < 0.01f ? 1f : Time.timeScale;
            Time.timeScale = 0.02f;
            stopping = true;
            stopUntil = Time.unscaledTime + hitStopSeconds;
        }

        void Update()
        {
            if (!stopping || Time.unscaledTime < stopUntil)
                return;
            Time.timeScale = storedScale;
            stopping = false;
        }
    }
}
