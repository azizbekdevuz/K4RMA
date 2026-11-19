using UnityEngine;

namespace KarmaPrototype
{
    // Mask the blade on the actual displayed frame so release and recovery stay aligned.
    // A separate renderer keeps the inherited shield material and damage silhouette untouched.
    public sealed class KarmaGuardHeavyGlow : MonoBehaviour
    {
        SpriteRenderer source, overlay;
        Material material;
        string form;
        float began, chargeSeconds, pulseAt;
        int phase;
        public float Charge { get; private set; }
        public float Pulse { get; private set; }
        public bool Visible => overlay != null && overlay.enabled;
        public void Initialize(SpriteRenderer body, string formKey)
        {
            source = body;
            form = formKey;
            var child = new GameObject("GD02 sword charge");
            child.transform.SetParent(body.transform, false);
            overlay = child.AddComponent<SpriteRenderer>();
            material = new Material(KarmaArtResource.Load<Shader>("Guard/Effects/KarmaGuardHeavyGlow"));
            overlay.sharedMaterial = material;
            overlay.enabled = false;
        }
        public void Prepare(float seconds) { phase = 1; began = Time.time; chargeSeconds = Mathf.Max(0.001f, seconds); Sample(); }
        public void Cue() { phase = 2; pulseAt = Time.time; Sample(); }
        public void Strike() { phase = 3; pulseAt = Time.time; Sample(); }
        public void Stop() { phase = 0; Charge = Pulse = 0; if (overlay != null) overlay.enabled = false; }
        // Grip and tip measured in each drawing on a normalized 160 by 160 cell, from the top left.
        static readonly Vector4[,] blades = {
            {new(98,118,141,137),new(41,33,81,7),new(41,47,82,32),new(112,45,81,16),new(115,119,151,136),new(107,115,148,137)},
            {new(52,120,98,140),new(49,32,106,8),new(48,36,107,11),new(110,94,144,123),new(109,110,149,127),new(89,109,139,125)},
            {new(85,107,133,138),new(24,43,84,17),new(22,50,80,21),new(81,66,35,28),new(106,121,150,136),new(81,109,140,130)},
            {new(65,113,22,143),new(53,34,10,9),new(68,29,18,14),new(87,68,44,21),new(108,117,151,133),new(101,110,146,132)},
            {new(106,112,151,135),new(54,36,10,15),new(51,39,8,50),new(111,51,77,14),new(110,116,150,130),new(99,113,142,131)}
        };
        Sprite sampled;
        bool AlignBlade()
        {
            var sprite = source.sprite;
            if (sprite == null) return false;
            if (sprite == sampled) return true;
            string name = sprite.name;
            bool shieldAway = name.StartsWith("GD05Heavy_", System.StringComparison.Ordinal);
            if (!shieldAway && !name.StartsWith("GD02_", System.StringComparison.Ordinal)) return false;
            int frame = name[name.Length - 1] - '0';
            if (frame < 0 || frame >= blades.GetLength(1)) return false;
            int row = shieldAway ? 4 : form == "WardFinal" ? 3 : form == "WardWave" ? 2 : form == "WardRise" ? 1 : 0;
            var rect = sprite.rect;
            sampled = overlay.sprite = sprite;
            material.SetVector("_Blade", blades[row, frame]);
            material.SetVector("_PoseRect", new Vector4(rect.x, rect.y, rect.width, rect.height));
            material.SetVector("_TexSize", new Vector4(sprite.texture.width, sprite.texture.height, 0, 0));
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            return true;
        }
        void LateUpdate() { Sample(); }
        void Sample()
        {
            if (overlay == null) return;
            overlay.enabled = phase != 0 && source.enabled && AlignBlade();
            if (phase == 0) return;
            Charge = phase == 1 ? Mathf.SmoothStep(0, 0.75f, Mathf.Clamp01((Time.time - began) / chargeSeconds)) : 1;
            Pulse = phase >= 2 ? Mathf.Pow(Mathf.Clamp01(1 - (Time.time - pulseAt) / (phase == 3 ? 0.16f : 0.12f)), 2) : 0;
            material.SetFloat("_Charge", Charge);
            material.SetFloat("_Pulse", Pulse * (phase == 3 ? 1.35f : 1));
            overlay.color = source.color;
        }
        void OnDisable() { Stop(); }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
