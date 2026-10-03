using UnityEngine;

namespace K4RMA
{
    public class GuardianPose : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [SerializeField] Transform arm;
        [SerializeField] Transform core;
        [SerializeField] Color coreColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] float coreEmission = 1.5f;

        Quaternion armRest;
        Vector3 coreRest;
        Renderer coreRenderer;
        MaterialPropertyBlock block;
        BossState previous;
        float pose;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            if (boss == null)
                boss = GetComponentInParent<BossController>();
            if (arm != null)
                armRest = arm.localRotation;
            if (core != null)
            {
                coreRest = core.localScale;
                coreRenderer = core.GetComponent<Renderer>();
            }

            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (boss == null)
                return;
            if (boss.State != previous)
            {
                previous = boss.State;
                pose = 0f;
            }

            pose += Time.deltaTime;
            float raise = boss.State == BossState.MeleeTelegraph || boss.State == BossState.MeleeStrike ? 1f : 0f;
            float charge = boss.State == BossState.ProjectileTelegraph ? Mathf.Clamp01(pose / 0.95f) : 0f;
            if (arm != null)
                arm.localRotation = armRest * Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -70f, raise));
            if (core != null)
            {
                float pulse = 1f + charge * 0.45f + Mathf.Sin(Time.time * 6f) * 0.04f;
                core.localScale = coreRest * pulse;
            }

            if (coreRenderer == null)
                return;
            Color glow = coreColor * (coreEmission + charge * 3f);
            block.Clear();
            block.SetColor(BaseColorId, coreColor);
            block.SetColor(EmissionId, glow);
            coreRenderer.SetPropertyBlock(block);
        }
    }
}
