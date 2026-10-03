using System.Collections;
using UnityEngine;

namespace K4RMA
{
    public class SacrificeSequence : MonoBehaviour
    {
        [SerializeField] float leaveSeconds = 0.9f;
        [SerializeField] float orbitSeconds = 0.8f;
        [SerializeField] float travelSeconds = 1.05f;
        [SerializeField] float revealSeconds = 0.7f;

        public bool IsPlaying { get; private set; }

        public void Play(RunDirector director, string abilityId)
        {
            if (IsPlaying || director == null)
                return;
            StartCoroutine(Run(director, abilityId));
        }

        IEnumerator Run(RunDirector director, string abilityId)
        {
            IsPlaying = true;
            director.SetControlsLocked(true);
            if (director.StageOneBoss != null)
                director.StageOneBoss.enabled = false;

            var camera = FindAnyObjectByType<SideViewCamera>();
            var altar = director.Altar != null ? director.Altar.transform : null;
            Vector3 altarPoint = altar != null ? altar.position + Vector3.up * 1.8f : Vector3.up * 2f;
            camera?.SetFocus(altarPoint, 0.65f);
            FindAnyObjectByType<HudPresenter>()?.ShowMessage("The shot leaves you.");
            AudioFeedback.Play(AudioCue.Altar);

            var orb = CreateOrb();
            Vector3 from = director.StageOneBoss != null
                ? FindPlayerPoint(director)
                : altarPoint;
            yield return Move(orb.transform, from, altarPoint, leaveSeconds);

            float spun = 0f;
            Vector3 center = altarPoint;
            while (spun < orbitSeconds)
            {
                spun += Time.deltaTime;
                float angle = spun * 5.5f;
                orb.transform.position = center + new Vector3(Mathf.Cos(angle) * 0.7f, Mathf.Sin(angle * 2f) * 0.25f, 0f);
                yield return null;
            }

            Vector3 guardianPoint = director.StageTwoBoss != null
                ? director.StageTwoBoss.transform.position + Vector3.up * 0.8f
                : altarPoint + Vector3.left * 4f;
            FindAnyObjectByType<HudPresenter>()?.ShowMessage("The altar returns it.");
            yield return Move(orb.transform, orb.transform.position, guardianPoint, travelSeconds);
            AudioFeedback.Play(AudioCue.Transfer);

            director.TrySacrifice(abilityId);
            if (director.StageTwoBoss != null)
                director.StageTwoBoss.enabled = false;
            camera?.SetFocus(guardianPoint, 0.35f);
            SparkBurst.Play(guardianPoint, new Color(0.4f, 0.9f, 1f));
            Destroy(orb);
            yield return new WaitForSeconds(revealSeconds);

            camera?.SetFocus(guardianPoint, 0f);
            director.SetControlsLocked(false);
            if (director.StageTwoBoss != null)
                director.StageTwoBoss.enabled = true;
            IsPlaying = false;
        }

        static Vector3 FindPlayerPoint(RunDirector director)
        {
            var player = FindAnyObjectByType<PlayerController>();
            return player != null ? player.transform.position + Vector3.up * 0.6f : Vector3.up;
        }

        static IEnumerator Move(Transform target, Vector3 from, Vector3 to, float duration)
        {
            float time = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                target.position = Vector3.Lerp(from, to, t);
                target.localScale = Vector3.one * Mathf.Lerp(0.28f, 0.42f, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
        }

        static GameObject CreateOrb()
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "SurrenderedShot";
            Destroy(orb.GetComponent<Collider>());
            orb.transform.localScale = Vector3.one * 0.32f;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", new Color(0.45f, 0.9f, 1f));
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.3f, 0.85f, 1f) * 2.5f);
                orb.GetComponent<Renderer>().sharedMaterial = material;
            }

            return orb;
        }
    }
}
