using UnityEngine;

namespace KarmaPrototype
{
    // Only the artwork tumbles. Projectile motion, contact and parry remain on its parent.
    public sealed class KarmaRisingFragmentVisual : MonoBehaviour
    {
        static Material fragmentMaterial;
        SpriteRenderer picture;
        float age, spin, life, initialAngle;
        public void Initialize(float degreesPerSecond, float seconds)
        {
            picture = GetComponent<SpriteRenderer>();
            if (fragmentMaterial == null)
                fragmentMaterial = KarmaArtResource.Load<Material>("RisingSlash/Effects/KarmaRisingTranslucentFragment");
            if (fragmentMaterial != null) picture.sharedMaterial = fragmentMaterial;
            spin = degreesPerSecond; life = seconds;
            initialAngle = transform.localEulerAngles.z;
        }
        bool returning;
        public void BeginReturn(float seconds)
        {
            age = 0; life = seconds; initialAngle = transform.localEulerAngles.z;
            returning = true;
            Draw();
        }
        void Update()
        {
            if (picture == null || Time.deltaTime <= 0) return;
            age += Time.deltaTime;
            Draw();
        }
        void Draw()
        {
            float sample = Mathf.Floor(age * KarmaBootstrap.FramesPerSecond) * KarmaBootstrap.Step;
            transform.localRotation = Quaternion.Euler(0, 0, initialAngle + spin * sample);
            var tint = returning ? new Color(0.37f, 0.96f, 1.28f) : Color.white;
            tint.a = 1 - Mathf.InverseLerp(Mathf.Max(0, life - 0.2f), life, sample);
            picture.color = tint;
        }
    }
}
