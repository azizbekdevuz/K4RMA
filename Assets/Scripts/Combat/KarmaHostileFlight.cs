using UnityEngine;

namespace KarmaPrototype
{
    // Physics decides how many steps passed. Resolve them after dynamic input has
    // delivered every press and release from the same real-time interval.
    public abstract class KarmaHostileFlight : MonoBehaviour
    {
        int pendingTicks;

        protected void FixedUpdate() => pendingTicks++;

        protected void LateUpdate()
        {
            while (pendingTicks > 0 && isActiveAndEnabled)
            {
                float end = Time.fixedTime - (--pendingTicks) * Time.fixedDeltaTime;
                AdvanceFlight(end);
            }
        }

        protected abstract void AdvanceFlight(float tickEnd);
    }
}
