using UnityEngine;

namespace K4RMA
{
    /// <summary>
    /// Brief airborne suspend while an aerial attack is active. Not RisingSlash.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PlayerAirHover : MonoBehaviour
    {
        readonly AirHoverState state = new AirHoverState();

        PlayerTuning tuning;
        PlayerController motor;
        Health health;
        Rigidbody body;
        BodyTint tint;
        Transform mark;
        bool gravityDisabled;

        public bool IsHovering => state.Hovering;

        void Awake()
        {
            Resolve();
        }

        void OnDisable()
        {
            ClearAll();
        }

        public void Resolve()
        {
            if (motor == null)
                motor = GetComponent<PlayerController>();
            if (tuning == null && motor != null)
                tuning = motor.Tuning;
            if (health == null)
                health = GetComponent<Health>();
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (tint == null)
                tint = GetComponent<BodyTint>();
        }

        public bool TryStartFromAirAttack()
        {
            Resolve();
            if (tuning == null || !tuning.previewAirHover || motor == null)
                return false;

            state.Duration = tuning.airHoverDurationSeconds;
            state.CooldownDuration = tuning.airHoverCooldownSeconds;
            bool alive = health == null || health.IsAlive;
            if (!state.TryBegin(alive, !motor.IsGrounded))
                return false;

            ApplySuspend();
            tint?.Flash(tuning.airHoverColor, Mathf.Max(0.05f, tuning.airHoverDurationSeconds));
            SparkBurst.Play(transform.position + Vector3.up * 0.4f, tuning.airHoverColor);
            AudioFeedback.Play(AudioCue.AirHover);
            ShowMark(true);
            return true;
        }

        void FixedUpdate()
        {
            Resolve();
            if (tuning == null || !tuning.previewAirHover)
            {
                if (state.Hovering || gravityDisabled)
                    ClearAll();
                return;
            }

            bool grounded = motor != null && motor.IsGrounded;
            bool knocked = motor != null && motor.InKnockback;
            bool alive = health == null || health.IsAlive;
            bool hoveringBefore = state.Hovering;
            state.Tick(Time.fixedDeltaTime, grounded, knocked, alive);
            if (state.Hovering)
                ApplySuspend();
            else if (gravityDisabled || hoveringBefore)
                ReleaseSuspend();

            if (hoveringBefore && !state.Hovering)
                ShowMark(false);
        }

        void ApplySuspend()
        {
            if (body == null || tuning == null)
                return;
            body.useGravity = false;
            gravityDisabled = true;
            var velocity = body.linearVelocity;
            velocity.y = Mathf.Min(0f, tuning.airHoverVerticalSpeed);
            velocity.z = 0f;
            body.linearVelocity = velocity;
        }

        void ReleaseSuspend()
        {
            if (body != null && gravityDisabled)
                body.useGravity = true;
            gravityDisabled = false;
            ShowMark(false);
        }

        void ClearAll()
        {
            state.Reset();
            ReleaseSuspend();
        }

        void ShowMark(bool visible)
        {
            if (visible && mark == null)
                CreateMark();
            if (mark == null)
                return;
            if (mark.gameObject.activeSelf != visible)
                mark.gameObject.SetActive(visible);
        }

        void CreateMark()
        {
            var markObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markObject.name = "AirHoverMark";
            Destroy(markObject.GetComponent<Collider>());
            mark = markObject.transform;
            mark.SetParent(transform, false);
            mark.localPosition = new Vector3(0f, 0.15f, 0f);
            mark.localScale = new Vector3(1.1f, 0.08f, 1.1f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                var color = tuning != null ? tuning.airHoverColor : new Color(0.75f, 0.9f, 1f);
                material.SetColor("_BaseColor", color);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.3f);
                markObject.GetComponent<Renderer>().sharedMaterial = material;
            }

            markObject.SetActive(false);
        }
    }
}
