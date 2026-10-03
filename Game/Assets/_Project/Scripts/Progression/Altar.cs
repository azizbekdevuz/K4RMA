using UnityEngine;

namespace K4RMA
{
    public class Altar : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] RunDirector run;
        [SerializeField] string sacrificeAbilityId = PrototypeIds.Projectile;

        public bool PlayerInside { get; private set; }

        void OnTriggerEnter(Collider other)
        {
            if (other != null && other.GetComponentInParent<PlayerController>() != null)
                PlayerInside = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other != null && other.GetComponentInParent<PlayerController>() != null)
                PlayerInside = false;
        }

        void Update()
        {
            if (!PlayerInside || input == null || run == null)
                return;
            if (input.InteractPressed)
                run.BeginSacrifice(sacrificeAbilityId);
        }
    }
}
