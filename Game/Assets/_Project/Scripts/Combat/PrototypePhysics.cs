using UnityEngine;

namespace K4RMA
{
    public static class PrototypePhysics
    {
        public static void Apply()
        {
            Ignore("Player", "PlayerShot");
            Ignore("Boss", "BossShot");
            Ignore("PlayerShot", "BossShot");
            Ignore("Player", "PlayerHit");
            Ignore("Boss", "BossHit");
        }

        static void Ignore(string first, string second)
        {
            int a = LayerMask.NameToLayer(first);
            int b = LayerMask.NameToLayer(second);
            if (a < 0 || b < 0)
                return;
            Physics.IgnoreLayerCollision(a, b, true);
        }
    }
}
