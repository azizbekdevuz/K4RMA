using UnityEngine;

namespace KarmaPrototype
{
    // SwordWave-only drawings and attachment points; gameplay poses stay in the host.
    public sealed class KarmaSwordBasicArt : ScriptableObject
    {
        public Sprite idle;
        public Sprite[] poses;
        public Sprite blade;
        public Vector2[] sockets;
        public float[] angles;
        public float bladeLength;
        public Sprite shieldInside, shieldStrap;
    }
}
