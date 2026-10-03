using UnityEngine;

namespace K4RMA
{
    public class AltarPresence : MonoBehaviour
    {
        [SerializeField] Altar altar;
        [SerializeField] Light glow;
        [SerializeField] float idleIntensity = 3.2f;
        [SerializeField] float nearIntensity = 9f;
        [SerializeField] SacrificeSequence sequence;

        void Awake()
        {
            if (altar == null)
                altar = GetComponent<Altar>();
            if (sequence == null)
                sequence = FindAnyObjectByType<SacrificeSequence>();
        }

        void Update()
        {
            if (glow == null)
                return;
            bool active = (altar != null && altar.PlayerInside) || (sequence != null && sequence.IsPlaying);
            float target = active ? nearIntensity : idleIntensity;
            glow.intensity = Mathf.Lerp(glow.intensity, target, Time.deltaTime * 5f);
        }
    }
}
