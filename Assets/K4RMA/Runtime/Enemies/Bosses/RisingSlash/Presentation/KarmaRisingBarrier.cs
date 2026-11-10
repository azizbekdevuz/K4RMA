using UnityEngine;

namespace KarmaPrototype
{
    // Boss-local presentation: no collider, damage, or invulnerability of its own.
    public sealed class KarmaRisingBarrier : MonoBehaviour
    {
        Transform follow;
        SpriteRenderer picture;
        float age, growSeconds;
        float diameter;
        public float Opacity => picture != null ? picture.color.a : 0;
        public bool Formed => Opacity >= 0.36f;

        public static KarmaRisingBarrier Begin(KarmaEnemy owner, float growSeconds)
        {
            var sprite = KarmaRisingArt.Get("Shield");
            if (owner == null || sprite == null) return null;
            var go = new GameObject("UP04 황금 구형 방어막");
            go.transform.SetParent(owner.transform, false);
            var barrier = go.AddComponent<KarmaRisingBarrier>();
            barrier.follow = owner.transform;
            barrier.growSeconds = Mathf.Max(0, growSeconds);
            barrier.picture = go.AddComponent<SpriteRenderer>();
            barrier.picture.sprite = sprite;
            barrier.picture.sortingOrder = 18;
            KarmaVisuals.ApplyMaterial(barrier.picture);
            barrier.diameter = KarmaSpace.Units(5.3f);
            barrier.Draw();
            return barrier;
        }
        void LateUpdate()
        {
            if (follow == null) { End(); return; }
            age += Time.deltaTime;
            Draw();
        }
        void Draw()
        {
            if (picture == null || follow == null) return;
            // Fixed body-centered size: weapon poses must not inflate the shield.
            transform.localPosition = Vector3.up * KarmaSpace.Units(0.75f);
            // Sprite presentation samples at 24 FPS without altering attack or physics clocks.
            float sample = Mathf.Floor(age * 24f) / 24f;
            float t = growSeconds <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(sample / growSeconds));
            float fit = diameter / Mathf.Max(picture.sprite.bounds.size.x, picture.sprite.bounds.size.y);
            transform.localScale = Vector3.one * fit;
            picture.color = new Color(1, 1, 1, 0.36f * t);
        }
        public void End()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
