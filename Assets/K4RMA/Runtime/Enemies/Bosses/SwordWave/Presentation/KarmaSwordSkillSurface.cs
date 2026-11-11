using UnityEngine;

namespace KarmaPrototype
{
    // Only an existing renderer's material and property block. No transform,
    // collider, random stream, attack clock or source Sprite is ever changed.
    public sealed class KarmaSwordSkillSurface : MonoBehaviour
    {
        static Material material;
        static readonly int Style = Shader.PropertyToID("_Style");
        static readonly int Phase = Shader.PropertyToID("_Phase");
        SpriteRenderer picture;
        MaterialPropertyBlock properties;
        float age, style;
        public static int ProjectileStyle(int pattern)
        {
            if (pattern == (int)EnemyAttack.SW01) return 1;
            if (pattern == (int)EnemyAttack.SW02) return 2;
            if (pattern == (int)EnemyAttack.SW04) return 3;
            return 0;
        }
        public static void Apply(SpriteRenderer renderer, int mode)
        {
            if (renderer == null) return;
            if (material == null) material = Resources.Load<Material>("KarmaSwordSkillPolish");
            if (material == null) return;
            var surface = renderer.GetComponent<KarmaSwordSkillSurface>();
            if (surface == null) surface = renderer.gameObject.AddComponent<KarmaSwordSkillSurface>();
            surface.picture = renderer;
            if (surface.properties == null) surface.properties = new MaterialPropertyBlock();
            renderer.sharedMaterial = material;
            surface.Restart(mode);
        }
        internal void Restart(int mode)
        {
            age = 0; style = mode;
            Paint();
        }
        void Update()
        {
            if (picture == null || Time.deltaTime <= 0) return;
            age += Time.deltaTime; Paint();
        }
        void Paint()
        {
            picture.GetPropertyBlock(properties);
            properties.SetFloat(Style, style); properties.SetFloat(Phase, age);
            picture.SetPropertyBlock(properties);
        }
    }
}
