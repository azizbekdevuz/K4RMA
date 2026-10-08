using UnityEngine;

namespace K4RMA
{
    public class BodyTint : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer[] renderers;

        MaterialPropertyBlock block;
        Color[] baseColors;
        Color persistentColor;
        Color flashColor;
        bool hasPersistent;
        float flashTimer;

        void Awake()
        {
            EnsureReady();
        }

        public void SetPersistent(Color color)
        {
            EnsureReady();
            hasPersistent = true;
            persistentColor = color;
            Apply();
        }

        public void ClearPersistent()
        {
            EnsureReady();
            hasPersistent = false;
            Apply();
        }

        public void Flash(Color color, float seconds)
        {
            EnsureReady();
            flashColor = color;
            flashTimer = seconds;
            Apply();
        }

        void Update()
        {
            if (flashTimer <= 0f)
                return;
            flashTimer -= Time.deltaTime;
            Apply();
        }

        void EnsureReady()
        {
            if (block == null)
                block = new MaterialPropertyBlock();
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            if (baseColors != null && baseColors.Length == renderers.Length)
                return;

            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                baseColors[i] = material != null && material.HasProperty(ColorId)
                    ? material.GetColor(ColorId)
                    : Color.white;
            }
        }

        void Apply()
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                Color color = flashTimer > 0f
                    ? flashColor
                    : hasPersistent ? persistentColor : baseColors[i];
                block.Clear();
                block.SetColor(ColorId, color);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
