using UnityEngine;

namespace KarmaPrototype
{
    // Cosmetic, confirmed-damage-only feedback. World ownership lets the short
    // flash finish if the attack enters recovery in the same frame.
    public static class KarmaSwordHitFeedback
    {
        public static void Show(Transform root, Vector2 at, bool boss)
        {
            var sprite = KarmaWaveArtwork.Sprite("Sword_HitBurst_v2");
            if (sprite == null) return;
            var flash = KarmaCombatVFX.Burst(root, at, sprite,
                boss ? "Sword boss confirmed hit" : "Sword player confirmed hit",
                Vector2.one * (boss ? 2.3f : 2.7f), boss ? 0.14f : 0.17f,
                0, KarmaSwordAttackPolish.Red, true);
            foreach (var renderer in flash.transform.parent.GetComponentsInChildren<SpriteRenderer>())
                renderer.sortingOrder += 18;
        }
    }

    // Sword-only overlay follows the rendered pose after secondary motion.
    // It never changes the source sprite, animator state, root or damage bounds.
    [DefaultExecutionOrder(450)]
    public sealed class KarmaSwordDamageFlash : MonoBehaviour
    {
        KarmaEnemy owner;
        SpriteRenderer source, overlay;
        float queuedHealth, age = 1, nextPulse;
        bool pending;
        public void Queue(KarmaEnemy enemy)
        {
            owner = enemy;
            queuedHealth = pending ? Mathf.Max(queuedHealth, enemy.Health) : enemy.Health;
            pending = true;
        }
        void LateUpdate()
        {
            if (owner == null || !owner.PresentationActive || owner.Stunned
                || owner.CurrentPattern == (int)EnemyAttack.SW05)
            { if (overlay != null) overlay.enabled = false; pending = false; age = 1; return; }
            if (pending)
            {
                if (owner.Health < queuedHealth && Time.time >= nextPulse)
                { age = 0; nextPulse = Time.time + 0.12f; }
                pending = false;
            }
            age += Time.deltaTime;
            if (age >= 0.18f) { if (overlay != null) overlay.enabled = false; return; }
            if (source == null)
            {
                var picture = transform.Find("Guardian sprite");
                if (picture == null) return;
                source = picture.GetComponent<SpriteRenderer>();
            }
            if (overlay == null)
            {
                var node = new GameObject("Sword confirmed damage silhouette");
                node.transform.SetParent(source.transform.parent, false);
                overlay = node.AddComponent<SpriteRenderer>();
                overlay.sharedMaterial = Resources.Load<Material>("KarmaSwordRetreatEcho");
            }
            overlay.enabled = source.enabled;
            overlay.sprite = source.sprite; overlay.flipX = source.flipX; overlay.flipY = source.flipY;
            overlay.sortingLayerID = source.sortingLayerID; overlay.sortingOrder = source.sortingOrder + 2;
            overlay.transform.localPosition = source.transform.localPosition;
            overlay.transform.localRotation = source.transform.localRotation;
            overlay.transform.localScale = source.transform.localScale;
            float alpha = age < 0.045f ? 0.88f : 0.88f * (1 - Mathf.Clamp01((age - 0.045f) / 0.135f));
            overlay.color = new Color(1, 0.75f, 0.65f, alpha);
        }
        void OnDisable() { if (overlay != null) overlay.enabled = false; }
        void OnDestroy() { if (overlay != null) Destroy(overlay.gameObject); }
    }
}
