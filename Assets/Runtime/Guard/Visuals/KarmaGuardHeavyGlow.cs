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
        internal SpriteRenderer Overlay => overlay;
        internal Sprite BoundsDrawing { get; private set; }
        Sprite registeredBody, registeredBounds;
        internal void RegisterDrawing(Sprite body, Sprite bounds)
        {
            registeredBody = body;
            registeredBounds = bounds;
        }
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
            {new(98,118,141,137),new(41,33,81,7),new(99.0f,37.3f,68.8f,15.1f),new(112,45,81,16),new(115,119,151,136),new(107,115,148,137)},
            {new(52,120,98,140),new(49,32,106,8),new(61.0f,37.6f,7.4f,9.3f),new(110,94,144,123),new(109,110,149,127),new(89,109,139,125)},
            {new(85,107,133,138),new(24,43,84,17),new(66.0f,47.8f,20.6f,19.6f),new(81,66,35,28),new(106,121,150,136),new(81,109,140,130)},
            {new(65,113,22,143),new(53,34,10,9),new(81.0f,32.1f,39.6f,0.2f),new(87,68,44,21),new(108,117,151,133),new(101,110,146,132)},
            {new(106,112,151,135),new(54,36,10,15),new(51,39,8,50),new(111,51,77,14),new(110,116,150,130),new(99,113,142,131)}
        };
        static readonly Vector4[,] followupBlades = {
            {
                new(69.1f, 108.2f, 113.1f, 129.9f), new(67.9f, 107.4f, 111.6f, 129.9f), new(66.9f, 106.4f, 110.3f, 129.5f),
                new(67.7f, 106.6f, 111.4f, 129.1f), new(72.8f, 107.4f, 118.2f, 125.0f), new(74.0f, 107.0f, 119.7f, 123.2f),
                new(72.8f, 105.8f, 117.9f, 124.6f), new(71.0f, 104.4f, 115.0f, 126.7f), new(67.9f, 101.8f, 110.1f, 128.6f),
                new(73.6f, 102.2f, 112.8f, 135.7f), new(105.9f, 109.2f, 123.3f, 137.0f), new(107.3f, 108.0f, 125.4f, 137.0f)
            },
            {
                new(92.9f, 109.2f, 126.3f, 131.7f), new(91.7f, 108.4f, 124.8f, 131.5f), new(90.7f, 107.5f, 123.5f, 131.0f),
                new(91.5f, 107.7f, 124.7f, 130.7f), new(96.6f, 108.4f, 131.5f, 127.8f), new(97.7f, 108.1f, 133.0f, 126.3f),
                new(96.6f, 106.9f, 131.1f, 127.1f), new(94.8f, 105.5f, 128.3f, 128.4f), new(91.7f, 103.0f, 111.0f, 118.9f),
                new(63.6f, 104.4f, 36.2f, 129.9f), new(32.8f, 103.2f, 13.1f, 142.9f), new(29.9f, 101.0f, 8.8f, 143.2f)
            }
        };
        Sprite sampled;
        bool AlignBlade()
        {
            var sprite = source.sprite;
            if (sprite == null) return false;
            if (sprite == sampled) return true;
            string name = sprite.name;
            bool shieldAway = name.StartsWith("GD05Heavy_", System.StringComparison.Ordinal);
            bool followup = name.StartsWith("GD02Followup_", System.StringComparison.Ordinal);
            if (!followup && !shieldAway && !name.StartsWith("GD02_", System.StringComparison.Ordinal)) return false;
            int frame = name[name.Length - 1] - '0';
            if (followup) frame += (name[name.Length - 2] - '0') * 10;
            int drawingCount = followup ? followupBlades.GetLength(1) : blades.GetLength(1);
            if (frame < 0 || frame >= drawingCount) return false;
            int row = shieldAway ? 4 : form == "WardFinal" ? 3 : form == "WardWave" ? 2 : form == "WardRise" ? 1 : 0;
            var rect = sprite.rect;
            sampled = overlay.sprite = sprite;
            material.SetVector("_Blade", followup ? followupBlades[form == "WardFinal" ? 1 : 0, frame] : blades[row, frame]);
            material.SetVector("_PoseRect", new Vector4(rect.x, rect.y, rect.width, rect.height));
            material.SetVector("_TexSize", new Vector4(sprite.texture.width, sprite.texture.height, 0, 0));
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            return true;
        }
        void LateUpdate() { Sample(); }
        internal void Sample(bool updateBounds = true)
        {
            if (overlay == null) return;
            overlay.enabled = phase != 0 && source.enabled && AlignBlade();
            if (updateBounds)
                BoundsDrawing = overlay.enabled && source.sprite == registeredBody ? registeredBounds : null;
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
