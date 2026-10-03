using UnityEngine;

namespace K4RMA
{
    public class SparkBurst : MonoBehaviour
    {
        struct Shard
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Renderer Renderer;
        }

        Shard[] shards;
        float life = 0.28f;
        float age;
        Color color = Color.white;
        MaterialPropertyBlock block;

        public static void Play(Vector3 point, Color tint)
        {
            var root = new GameObject("Spark");
            root.transform.position = point;
            var burst = root.AddComponent<SparkBurst>();
            burst.color = tint;
            burst.Build();
        }

        void Build()
        {
            block = new MaterialPropertyBlock();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            shards = new Shard[6];
            for (int i = 0; i < shards.Length; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(shard.GetComponent<Collider>());
                shard.transform.SetParent(transform, false);
                shard.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);
                shard.GetComponent<Renderer>().sharedMaterial = material;
                float angle = i / 6f * Mathf.PI * 2f;
                shards[i] = new Shard
                {
                    Transform = shard.transform,
                    Velocity = new Vector3(Mathf.Cos(angle), Random.Range(0.4f, 1.4f), Mathf.Sin(angle)) * 3.5f,
                    Renderer = shard.GetComponent<Renderer>()
                };
            }
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float fade = 1f - Mathf.Clamp01(age / life);
            for (int i = 0; i < shards.Length; i++)
            {
                shards[i].Transform.position += shards[i].Velocity * Time.unscaledDeltaTime;
                shards[i].Velocity += Vector3.down * 8f * Time.unscaledDeltaTime;
                if (shards[i].Renderer != null && shards[i].Renderer.sharedMaterial != null && shards[i].Renderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    block.Clear();
                    var tint = color;
                    tint.a = fade;
                    block.SetColor("_BaseColor", tint);
                    shards[i].Renderer.SetPropertyBlock(block);
                }
            }

            if (age >= life)
                Destroy(gameObject);
        }
    }
}
