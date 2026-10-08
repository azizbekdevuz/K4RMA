using UnityEngine;

namespace K4RMA
{
    /// <summary>
    /// Short timing window that stops one incoming hit before HP changes, then retaliates once.
    /// Not Guard and not legacy Deflect.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class PlayerCounter : MonoBehaviour, IIncomingDamageFilter
    {
        readonly CounterTiming timing = new CounterTiming();

        PlayerTuning tuning;
        PlayerController motor;
        PlayerInputReader input;
        Health health;
        BodyTint tint;
        Transform mark;
        bool wasOpen;

        public bool IsWindowOpen => timing.WindowOpen;

        void Awake()
        {
            Resolve();
        }

        void OnDisable()
        {
            timing.Reset();
            wasOpen = false;
            ShowMark(false);
        }

        public void Resolve()
        {
            if (motor == null)
                motor = GetComponent<PlayerController>();
            if (input == null)
                input = GetComponent<PlayerInputReader>();
            if (health == null)
                health = GetComponent<Health>();
            if (tint == null)
                tint = GetComponent<BodyTint>();
            if (tuning == null && motor != null)
                tuning = motor.Tuning;
        }

        public void Configure(PlayerTuning source, Health self, PlayerInputReader reader)
        {
            tuning = source;
            health = self;
            input = reader;
            CopyDurations();
        }

        void Update()
        {
            Resolve();
            if (!PreviewActive || (health != null && !health.IsAlive))
            {
                if (timing.WindowOpen || timing.CooldownRemaining > 0f)
                    timing.Reset();
                wasOpen = false;
                ShowMark(false);
                return;
            }

            CopyDurations();
            bool opened = input != null && input.GuardPressed && timing.TryOpen();
            timing.Tick(Time.deltaTime);
            if (opened && timing.WindowOpen)
                AudioFeedback.Play(AudioCue.CounterWindow);

            bool open = timing.WindowOpen;
            if (wasOpen && !open && !timing.LastCloseWasSuccess)
            {
                tint?.Flash(new Color(0.55f, 0.62f, 0.7f), 0.08f);
                AudioFeedback.Play(AudioCue.CounterWhiff);
            }

            wasOpen = open;
            ShowMark(open);
            if (open && mark != null)
            {
                int facing = motor != null && motor.Facing < 0 ? -1 : 1;
                mark.localPosition = new Vector3(facing * 0.55f, 1f, 0f);
                float pulse = 1f + Mathf.Sin(Time.time * 28f) * 0.12f;
                mark.localScale = new Vector3(0.12f, 1.15f * pulse, 0.12f);
            }
        }

        public bool TryOpenWindow()
        {
            Resolve();
            if (!PreviewActive)
                return false;
            CopyDurations();
            return timing.TryOpen();
        }

        public bool TryBlockIncomingDamage(GameObject source)
        {
            Resolve();
            if (!PreviewActive)
                return false;
            if (!timing.TryReceiveHit(out bool retaliate))
                return false;

            wasOpen = false;
            ShowMark(false);
            if (retaliate)
                Retaliate(source);
            return true;
        }

        bool PreviewActive => tuning != null && tuning.previewCounter;

        void CopyDurations()
        {
            if (tuning == null)
                return;
            timing.WindowDuration = tuning.counterWindowSeconds;
            timing.WhiffCooldown = tuning.counterWhiffCooldownSeconds;
            timing.SuccessCooldown = tuning.counterSuccessCooldownSeconds;
        }

        void Retaliate(GameObject source)
        {
            DamageAttacker(source);
            Vector3 point = transform.position + Vector3.up * 1f;
            var color = tuning != null ? tuning.counterColor : new Color(0.85f, 0.95f, 1f);
            SparkBurst.Play(point, color);
            tint?.Flash(Color.white, 0.08f);
            ImpactFeedback.PlayCounter(point);
        }

        void DamageAttacker(GameObject source)
        {
            if (source == null || tuning == null)
                return;
            if (source.transform.root == transform.root)
                return;

            var attackerHealth = source.GetComponent<Health>();
            if (attackerHealth == null)
                attackerHealth = source.GetComponentInParent<Health>();
            if (attackerHealth == null || attackerHealth == health || !attackerHealth.IsAlive)
                return;

            if (!attackerHealth.ApplyDamage(tuning.counterDamage, gameObject))
                return;

            float sign = Mathf.Sign(attackerHealth.transform.position.x - transform.position.x);
            if (Mathf.Abs(sign) < 0.01f)
                sign = 1f;
            attackerHealth.GetComponentInParent<IKnockback>()?.ApplyKnockback(
                sign * tuning.counterKnockback,
                tuning.counterKnockbackSeconds);
            attackerHealth.GetComponentInParent<BodyTint>()?.Flash(Color.white, 0.08f);

            var boss = source.GetComponent<BossController>();
            if (boss == null)
                boss = source.GetComponentInParent<BossController>();
            if (boss != null && attackerHealth.IsAlive)
                boss.ApplyCounterStagger(tuning.counterStaggerSeconds);
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
            var markObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            markObject.name = "CounterMark";
            Destroy(markObject.GetComponent<Collider>());
            mark = markObject.transform;
            mark.SetParent(transform, false);
            mark.localPosition = new Vector3(0.55f, 1f, 0f);
            mark.localScale = new Vector3(0.12f, 1.15f, 0.12f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                var color = tuning != null ? tuning.counterColor : new Color(0.85f, 0.95f, 1f);
                material.SetColor("_BaseColor", color);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.8f);
                markObject.GetComponent<Renderer>().sharedMaterial = material;
            }

            markObject.SetActive(false);
        }
    }
}
