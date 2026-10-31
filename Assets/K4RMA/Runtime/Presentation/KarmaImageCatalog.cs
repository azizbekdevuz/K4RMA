using System;
using UnityEngine;

namespace KarmaPrototype
{
    // Only the images used by the current game, with physical paths and resource keys.
    [Serializable]
    public sealed class KarmaImageCatalog
    {
        [Serializable]
        public sealed class Entry
        {
            public string id, category, usage, path, resource;
            public string[] poses;
        }
        public Entry[] images;
        static KarmaImageCatalog current;
        public static KarmaImageCatalog Current
        {
            get
            {
                if (current == null)
                {
                    var file = Resources.Load<TextAsset>("KarmaImageCatalog");
                    if (file == null) throw new InvalidOperationException("이미지 목록 누락: Resources/KarmaImageCatalog.json");
                    current = JsonUtility.FromJson<KarmaImageCatalog>(file.text);
                }
                return current;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { current = null; }
    }
}
