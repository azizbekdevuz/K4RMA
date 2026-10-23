#if UNITY_EDITOR
using UnityEngine;
namespace KarmaPrototype.Editor
{
    public sealed class KarmaArtInventory:ScriptableObject
    {
        [System.Serializable] public struct Entry { public string source; public string usage; public Texture2D image; }
        public Entry[] images;
    }
}
#endif
