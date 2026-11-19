using UnityEngine;

namespace KarmaPrototype
{
    // A reusable visual layer sampled by the existing successful Riposte animation.
    // No hit box, counter window, damage or additional pose lifetime.
    public sealed class KarmaPlayerParryFlash : MonoBehaviour
    {
        SpriteRenderer glow;
        Material material;
        MaterialPropertyBlock properties;
        public bool Visible => glow != null && glow.enabled;

        public void Initialize(SpriteRenderer anchor)
        {
            var shader = Resources.Load<Shader>("Player/ParryFlash");
            if (shader == null) throw new System.InvalidOperationException("Player parry flash shader missing");
            material = new Material(shader) { name = "Player parry flash (runtime)" };
            properties = new MaterialPropertyBlock();
            // Existing 384x512 flash cells use a foot-level pivot (192,74),
            // while the contact star sits around pixel (192,235), bottom-up.
            glow = KarmaVisuals.Box(transform, "Successful parry radiance", new Vector2(0, 161f / 145),
                Vector2.one * 4f, Color.white, Mathf.Max(30, anchor.sortingOrder + 1));
            glow.sortingLayerID = anchor.sortingLayerID;
            glow.sharedMaterial = material;
            Hide();
        }

        public void Sample(float age)
        {
            glow.enabled = age >= 0 && age < 0.3f;
            properties.SetFloat("_Age", age);
            glow.SetPropertyBlock(properties);
        }
        public void Hide() { if (glow != null) glow.enabled = false; }
        void OnDisable() { Hide(); }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
