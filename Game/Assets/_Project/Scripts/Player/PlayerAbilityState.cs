using UnityEngine;

namespace K4RMA
{
    public class PlayerAbilityState : MonoBehaviour
    {
        [SerializeField] AbilityDefinition[] catalog;

        public RunState State { get; private set; }
        public AbilityDefinition[] Catalog => catalog;

        public event System.Action Changed;

        public void Bind(RunState state)
        {
            State = state;
            Changed?.Invoke();
        }

        public void NotifyChanged()
        {
            Changed?.Invoke();
        }

        public bool HasActiveProjectile =>
            State != null && State.ActiveAbilityId == PrototypeIds.Projectile;

        public bool HasDeflect =>
            State != null && State.CounterAbilityId == PrototypeIds.Deflect;
    }
}
