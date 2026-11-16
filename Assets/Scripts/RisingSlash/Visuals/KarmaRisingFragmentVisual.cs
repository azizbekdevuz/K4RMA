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
                fragmentMaterial = Resources.Load<Material>("RisingSlash/Effects/KarmaRisingTranslucentFragment");
            if (fragmentMaterial != null) picture.sharedMaterial = fragmentMaterial;
            spin = degreesPerSecond; life = seconds;
            initialAngle = transform.localEulerAngles.z;
        }
        public void Restart(float seconds)
        {
            age = 0; life = seconds; initialAngle = transform.localEulerAngles.z;
        }
        void Update()
        {
            if (picture == null || Time.deltaTime <= 0) return;
            age += Time.deltaTime;
            float sample = Mathf.Floor(age * 24f) / 24f;
            transform.localRotation = Quaternion.Euler(0, 0, initialAngle + spin * sample);
            picture.color = new Color(1, 1, 1, 1 - Mathf.InverseLerp(Mathf.Max(0, life - 0.2f), life, sample));
        }
    }
}
