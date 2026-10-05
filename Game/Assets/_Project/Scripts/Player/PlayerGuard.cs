using UnityEngine;

namespace K4RMA
{
    [DefaultExecutionOrder(-50)]
    public class PlayerGuard : MonoBehaviour, IIncomingDamageFilter
    {
        readonly GuardTiming timing = new GuardTiming();

        PlayerInputReader input;
        PlayerTuning tuning;
        Health health;
        BodyTint tint;
        Transform ring;
        bool tintApplied;

        public bool IsGuarding => timing.IsGuarding;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<Health>();
            tint = GetComponent<BodyTint>();
            var motor = GetComponent<PlayerController>();
            tuning = motor != null ? motor.Tuning : null;
            if (tuning != null)
            {
                timing.ActiveDuration = tuning.guardActiveSeconds;
                timing.CooldownDuration = tuning.guardCooldownSeconds;
            }

            ring = CreateRing();
        }

        void OnDisable()
        {
            timing.Reset();
            ApplyPresentation(false);
        }

        void Update()
        {
            if (health != null && !health.IsAlive)
            {
                timing.Reset();
                ApplyPresentation(false);
                return;
            }

            bool wasGuarding = timing.IsGuarding;
            bool held = input != null && input.GuardHeld;
            timing.Tick(Time.deltaTime, held);
            if (timing.IsGuarding && !wasGuarding)
                AudioFeedback.Play(AudioCue.Guard);
            ApplyPresentation(timing.IsGuarding);
        }

        public bool TryBlockIncomingDamage(GameObject source)
        {
            // Source is kept for a later Counter. Guard does not use it.
            if (!timing.IsGuarding)
                return false;

            SparkBurst.Play(transform.position + Vector3.up * 0.9f, tuning != null ? tuning.guardColor : Color.yellow);
            AudioFeedback.Play(AudioCue.GuardBlock);
            tint?.Flash(Color.white, 0.06f);
            return true;
        }

        void ApplyPresentation(bool guarding)
        {
            if (ring != null && ring.gameObject.activeSelf != guarding)
                ring.gameObject.SetActive(guarding);
            if (guarding && ring != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 16f) * 0.04f;
                ring.localScale = new Vector3(1.35f * pulse, 0.03f, 1.35f * pulse);
            }

            if (tint == null)
                return;
            if (guarding && !tintApplied)
            {
                tint.SetPersistent(tuning != null ? tuning.guardColor : new Color(0.95f, 0.78f, 0.28f));
                tintApplied = true;
            }
            else if (!guarding && tintApplied)
            {
                tint.ClearPersistent();
                tintApplied = false;
            }
        }

        Transform CreateRing()
        {
            var ringObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObject.name = "GuardRing";
            Destroy(ringObject.GetComponent<Collider>());
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ringObject.transform.localScale = new Vector3(1.35f, 0.03f, 1.35f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                var color = tuning != null ? tuning.guardColor : new Color(0.95f, 0.78f, 0.28f);
                material.SetColor("_BaseColor", color);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
                ringObject.GetComponent<Renderer>().sharedMaterial = material;
            }

            ringObject.SetActive(false);
            return ringObject.transform;
        }
    }
}
