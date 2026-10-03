using UnityEngine;

namespace K4RMA
{
    public class AltarGuide : MonoBehaviour
    {
        [SerializeField] RunDirector director;
        [SerializeField] Transform arrowRoot;
        [SerializeField] float height = 2.05f;
        [SerializeField] float pulseAmount = 0.035f;

        SacrificeSequence sequence;
        Vector3 baseScale = Vector3.one;

        void Awake()
        {
            if (director == null)
                director = FindAnyObjectByType<RunDirector>();
            sequence = FindAnyObjectByType<SacrificeSequence>();
            if (arrowRoot == null)
                arrowRoot = CreateArrow();
            baseScale = arrowRoot.localScale;
            arrowRoot.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (arrowRoot == null)
                return;
            if (!ShouldShow())
            {
                if (arrowRoot.gameObject.activeSelf)
                    arrowRoot.gameObject.SetActive(false);
                return;
            }

            var player = FindAnyObjectByType<PlayerController>();
            var altar = director.Altar.transform;
            if (player == null)
            {
                arrowRoot.gameObject.SetActive(false);
                return;
            }

            if (!arrowRoot.gameObject.activeSelf)
                arrowRoot.gameObject.SetActive(true);

            Vector3 toAltar = altar.position - player.transform.position;
            toAltar.z = 0f;
            arrowRoot.position = player.transform.position + Vector3.up * height;
            if (toAltar.sqrMagnitude > 0.04f)
            {
                float angle = Mathf.Atan2(toAltar.y, toAltar.x) * Mathf.Rad2Deg;
                arrowRoot.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            float pulse = 1f + Mathf.Sin(Time.time * 2.2f) * pulseAmount;
            arrowRoot.localScale = baseScale * pulse;
        }

        bool ShouldShow()
        {
            if (director == null || director.State == null || director.Altar == null)
                return false;
            if (director.State.Phase != RunPhase.Altar || director.State.StageIndex != 1)
                return false;
            if (director.Altar.PlayerInside)
                return false;
            if (sequence != null && sequence.IsPlaying)
                return false;
            return true;
        }

        static Transform CreateArrow()
        {
            var root = new GameObject("PlayerAltarArrow").transform;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.45f));
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.6f, 0.4f, 0.1f));
            }

            Shard(root, material, new Vector3(0.1f, 0.05f, 0f), -32f);
            Shard(root, material, new Vector3(0.1f, -0.05f, 0f), 32f);
            return root;
        }

        static void Shard(Transform parent, Material material, Vector3 position, float zAngle)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(shard.GetComponent<Collider>());
            shard.transform.SetParent(parent, false);
            shard.transform.localPosition = position;
            shard.transform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
            shard.transform.localScale = new Vector3(0.34f, 0.055f, 0.03f);
            shard.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
