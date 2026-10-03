using UnityEngine;

namespace K4RMA
{
    public class Health : MonoBehaviour
    {
        [SerializeField] int maxHealth = 100;

        public int MaxHealth => maxHealth;
        public int Current { get; private set; }
        public bool IsAlive => Current > 0;

        public event System.Action<Health> Changed;
        public event System.Action<Health> Died;

        void Awake()
        {
            Current = Mathf.Max(1, maxHealth);
            Changed?.Invoke(this);
        }

        public void ConfigureMax(int value)
        {
            maxHealth = Mathf.Max(1, value);
            Current = maxHealth;
            Changed?.Invoke(this);
        }

        public void ApplyDamage(int amount)
        {
            if (!IsAlive || amount <= 0)
                return;

            Current = Mathf.Max(0, Current - amount);
            Changed?.Invoke(this);
            if (Current == 0)
                Died?.Invoke(this);
        }

        public void RestoreFull()
        {
            if (maxHealth <= 0)
                return;
            Current = maxHealth;
            Changed?.Invoke(this);
        }
    }
}
