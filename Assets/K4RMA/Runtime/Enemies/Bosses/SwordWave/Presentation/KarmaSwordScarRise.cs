using UnityEngine;

namespace KarmaPrototype
{
    // The damage silhouette is visible in full from frame one. Only a faint
    // inner echo rises; animation must not hide the top of an active hitbox.
    public sealed class KarmaSwordScarRise : MonoBehaviour
    {
        SpriteRenderer picture;
        float age, life;
        static Material riseMaterial;
        static readonly int RiseProgress = Shader.PropertyToID("_RiseProgress");
        MaterialPropertyBlock properties;
        public void Initialize(float seconds, float worldHeight)
        {
            picture = GetComponent<SpriteRenderer>();
            life = seconds;
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
            if (owner != null) KarmaSwordAttackPolish.Hit(game, owner, at, true);
        }
        void Apply(float elapsed)
        {
            properties.SetFloat(RiseProgress, -0.15f + elapsed / 0.09f);
            picture.SetPropertyBlock(properties);
            float fade = Mathf.Lerp(1, 0.35f, Mathf.Clamp01((elapsed - life + 0.1f) / 0.1f));
            picture.color = new Color(1, 1, 1, fade);
        }
    }
}
