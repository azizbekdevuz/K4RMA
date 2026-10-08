using UnityEngine;

namespace K4RMA
{
    public class SideViewCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 worldOffset = new Vector3(1.2f, 2.6f, -12f);
        [SerializeField] float followSharpness = 7f;
        [SerializeField] ArenaBounds arenaBounds;
        [SerializeField] float minX = -8.4f;
        [SerializeField] float maxX = 8.4f;

        Vector3 focusPoint;
        float focusWeight;
        float shakeTime;
        float shakeStrength;

        public void SetFocus(Vector3 worldPoint, float weight)
        {
            focusPoint = worldPoint;
            focusWeight = Mathf.Clamp01(weight);
        }

        public void Shake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            float left = arenaBounds != null ? arenaBounds.Left : minX;
            float right = arenaBounds != null ? arenaBounds.Right : maxX;
            float followX = Mathf.Clamp(target.position.x, left, right);
            float x = Mathf.Lerp(followX, focusPoint.x, focusWeight);
            Vector3 desired = new Vector3(x, target.position.y, 0f) + worldOffset;
            float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Vector3 position = Vector3.Lerp(transform.position, desired, blend);
            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                position += (Vector3)Random.insideUnitCircle * shakeStrength;
                if (shakeTime <= 0f)
                    shakeStrength = 0f;
            }

            transform.position = position;
        }
    }
}
