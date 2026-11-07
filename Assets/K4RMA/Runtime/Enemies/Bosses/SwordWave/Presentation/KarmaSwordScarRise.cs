using UnityEngine;

namespace KarmaPrototype
{
    // The complete blade is visible from frame one. Ground anchoring and fitting
    // below the hall roof affect only this picture, never the attack root.
    public sealed class KarmaSwordScarRise : MonoBehaviour
    {
        SpriteRenderer picture;
        float age, life, damageHeight;
        static Material riseMaterial;
        static readonly int RiseProgress = Shader.PropertyToID("_RiseProgress");
        MaterialPropertyBlock properties;
        public void Initialize(float seconds, float worldHeight)
        {
            picture = GetComponent<SpriteRenderer>();
            life = seconds; damageHeight = worldHeight;
            if (riseMaterial == null) riseMaterial = Resources.Load<Material>("KarmaSwordRisePolish");
            if (riseMaterial != null) picture.sharedMaterial = riseMaterial;
            properties = new MaterialPropertyBlock();
            Apply(0);
        }
        void Update()
        {
            if (Time.deltaTime <= 0) return;
            age += Time.deltaTime; Apply(age);
        }
        public static void ShowHit(KarmaGame game, Transform effectsRoot, KarmaEnemy owner)
        {
            if (!game.IsCombat) return;
            var body = game.Player.GetComponent<Collider2D>();
            Vector2 at = body != null ? (Vector2)body.bounds.center : game.Player.Position;
            var sprite = KarmaArtwork.Sprite("SwordBossVFX", "SW03_HitCut_v7");
            if (sprite == null) return;
            var flash = KarmaCombatVFX.Burst(effectsRoot, at, sprite, "SW03 confirmed cut / no damage",
                Vector2.one * 3.8f, 0.14f, 0, KarmaSwordAttackPolish.Red, true);
            flash.name = "SW03 confirmed cut / no damage";
            foreach (var renderer in flash.transform.parent.GetComponentsInChildren<SpriteRenderer>()) renderer.sortingOrder += 18;
            if (owner != null) KarmaSwordVfxPool.For(game).Sparks(owner, at, Vector2.up, 3, 8);
        }
        void Apply(float elapsed)
        {
            var core = KarmaWaveArtwork.CorePixels("SW03_RisingBlade_v7");
            float ground = transform.parent.position.y - damageHeight / 2;
            var camera = Camera.main;
            // This hall's roof beam sits above y19. Fit the entire authored
            // rectangle (including transparent tip padding), not just its core.
            // The existing 30-unit hit rectangle remains owned by the parent.
            float ceiling = KarmaSpace.Units(9.5f);
            if (camera != null && camera.orthographic)
                ceiling = Mathf.Min(ceiling, camera.transform.position.y + camera.orthographicSize - 0.5f);
            float height = Mathf.Min(damageHeight, Mathf.Max(1, ceiling - ground));
            float ppu = picture.sprite.pixelsPerUnit;
            var scale = transform.localScale; scale.y = height * ppu / (picture.sprite.rect.height - core.yMin);
            transform.localScale = scale;
            Vector2 basePixel = new Vector2(core.center.x, core.yMin);
            Vector2 fromPivot = (basePixel - picture.sprite.pivot) / ppu;
            transform.localPosition = new Vector3(-fromPivot.x * scale.x,
                -damageHeight / 2 - fromPivot.y * scale.y, 0);
            properties.SetFloat(RiseProgress, -0.15f + elapsed / 0.09f);
            picture.SetPropertyBlock(properties);
            float fade = Mathf.Lerp(1, 0.35f, Mathf.Clamp01((elapsed - life + 0.1f) / 0.1f));
            picture.color = new Color(1, 1, 1, fade);
        }
    }
}
