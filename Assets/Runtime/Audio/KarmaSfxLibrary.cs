using UnityEngine;

namespace KarmaPrototype
{
    public sealed class KarmaSfxLibrary : ScriptableObject
    {
        public AudioClip[] Clips;
        [Range(0, 3)] public int DefaultMenuTrack = 1;
    }
}
