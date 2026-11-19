using UnityEngine;

namespace KarmaPrototype
{
    // Only the difference between the existing cold and molten sword art emits light.
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
            material = new Material(Resources.Load<Shader>("Guard/Effects/KarmaGuardHeavyGlow"));
            overlay.sharedMaterial = material;
            overlay.enabled = false;
        }
        public void Prepare(float seconds) { phase = 1; began = Time.time; chargeSeconds = Mathf.Max(0.001f, seconds); Sample(); }
        public void Cue() { phase = 2; pulseAt = Time.time; Sample(); }
        public void Strike() { phase = 3; pulseAt = Time.time; Sample(); }
        public void Stop() { phase = 0; Charge = Pulse = 0; if (overlay != null) overlay.enabled = false; }
        public void SetPose(Sprite cold, Sprite hot)
        {
            overlay.sprite = hot;
            material.SetTexture("_ColdTex", cold.texture);
            int pose = hot.name == "Windup" ? 0 : hot.name == "Downstroke" ? 1 : 2;
            Vector4 blade;
            if (form == "WardRise") blade = pose == 0 ? new Vector4(194,131,241,109) : pose == 1 ? new Vector4(216,280,270,316) : new Vector4(215,285,275,321);
            else if (form == "WardWave") blade = pose == 0 ? new Vector4(160,143,208,105) : pose == 1 ? new Vector4(155,275,199,316) : new Vector4(111,294,71,333);
            else if (form == "WardFinal") blade = pose == 0 ? new Vector4(181,145,237,108) : pose == 1 ? new Vector4(204,267,260,299) : new Vector4(213,285,263,321);
            else blade = pose == 0 ? new Vector4(145,116,210,82) : pose == 1 ? new Vector4(115,260,178,306) : new Vector4(164,284,222,328);
            // Blade endpoints measured in half-resolution, top-origin pose coordinates.
            material.SetVector("_Blade", new Vector4(blade.x*2,724-blade.y*2,blade.z*2,724-blade.w*2));
            material.SetVector("_PoseRect", new Vector4(hot.rect.x,hot.rect.y,hot.texture.width,hot.texture.height));
            // Both sheets retain the same slicing, pivot and dimensions.
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            Sample();
        }
        void LateUpdate() { Sample(); }
        void Sample()
        {
            if (overlay == null) return;
            overlay.enabled = phase != 0 && source.enabled;
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
