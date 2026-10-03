using UnityEngine;

namespace K4RMA
{
    public class ArenaBounds : MonoBehaviour
    {
        [SerializeField] float left = -8.4f;
        [SerializeField] float right = 8.4f;

        public float Left => Mathf.Min(left, right);
        public float Right => Mathf.Max(left, right);

        public float ClampX(float x, float padding)
        {
            return Mathf.Clamp(x, Left + padding, Right - padding);
        }
    }
}
