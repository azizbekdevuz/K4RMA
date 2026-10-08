using UnityEngine;

namespace K4RMA
{
    public class DisciplePose : MonoBehaviour
    {
        [SerializeField] Transform torso;
        [SerializeField] Transform armLeft;
        [SerializeField] PlayerCombat combat;

        Quaternion torsoRest;
        Quaternion armRest;
        TrailRenderer trail;

        void Awake()
        {
            if (combat == null)
                combat = GetComponentInParent<PlayerCombat>();
            if (torso != null)
                torsoRest = torso.localRotation;
            if (armLeft != null)
                armRest = armLeft.localRotation;
            trail = GetComponentInChildren<TrailRenderer>();
        }

        void LateUpdate()
        {
            float swing = combat != null ? combat.Swing01 : 0f;
            float bob = Mathf.Sin(Time.time * 2.2f) * 2f;
            if (torso != null)
                torso.localRotation = torsoRest * Quaternion.Euler(0f, 0f, bob + swing * 8f);
            if (armLeft != null)
                armLeft.localRotation = armRest * Quaternion.Euler(0f, 0f, 18f + swing * 20f);
            if (trail != null)
                trail.emitting = combat != null && combat.IsSwinging;
        }
    }
}
