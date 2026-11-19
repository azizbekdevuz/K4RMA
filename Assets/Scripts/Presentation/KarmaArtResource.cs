using System;
using UnityEngine;

namespace KarmaPrototype
{
    // One resource per logical key keeps motion preloads independent of the physical art folders.
    public sealed class KarmaArtResource : ScriptableObject
    {
        public UnityEngine.Object asset;
        public Sprite[] frames;

        public static Sprite[] Frames(string key) => Resources.Load<KarmaArtResource>(key)?.frames ?? Array.Empty<Sprite>();
        public static Sprite First(string key)
        {
            var sprites = Frames(key);
            return sprites.Length > 0 ? sprites[0] : null;
        }
        public static T Load<T>(string key) where T : UnityEngine.Object => Resources.Load<KarmaArtResource>(key)?.asset as T;
    }
}
