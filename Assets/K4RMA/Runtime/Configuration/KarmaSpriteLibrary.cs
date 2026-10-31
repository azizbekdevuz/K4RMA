using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Every cutout stays an ordinary, editable Unity Sprite subasset.
    [CreateAssetMenu(menuName = "K4RMA/스프라이트 라이브러리")]
    public sealed class KarmaSpriteLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Sheet
        {
            public string id, source;
            public Sprite[] sprites;
        }
        public Sheet[] sheets = Array.Empty<Sheet>();
        Dictionary<string, Sprite> lookup;
        public Sprite Get(string sheet, string pose)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var entry in sheets)
                    if (entry != null && entry.sprites != null)
                        foreach (var sprite in entry.sprites)
                            if (sprite != null) lookup[entry.id + "/" + sprite.name] = sprite;
            }
            lookup.TryGetValue(sheet + "/" + pose, out var result);
            if (result == null && (sheet == "ExtraWaveVFX" || sheet == "WaveGuardians"))
            {
                result = Resources.Load<Sprite>("K4RMAArtwork/" + sheet + "/" + pose);
                if (result != null) lookup[sheet + "/" + pose] = result;
            }
            return result;
        }
        public Sprite[] All(string sheet)
        {
            foreach (var entry in sheets) if (entry.id == sheet) return entry.sprites;
            return Array.Empty<Sprite>();
        }
        void OnEnable() { lookup = null; }
        void OnValidate() { lookup = null; }
    }
}
