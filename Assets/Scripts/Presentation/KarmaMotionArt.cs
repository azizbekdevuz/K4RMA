using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // One cache per performer so leaving a room releases that form's loaded sheets.
    internal sealed class KarmaMotionArt
    {
        public const string Root = "Motions12/";
        static Dictionary<string, int> counts;
        readonly Dictionary<string, Sprite[]> loaded = new Dictionary<string, Sprite[]>();
        static Dictionary<string, int> Counts
        {
            get
            {
                if (counts != null) return counts;
                var catalog = Resources.Load<TextAsset>(Root + "Catalog");
                var result = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string line in catalog.text.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var columns = line.Split('\t');
                    result.Add(columns[0] + "/" + columns[1], int.Parse(columns[4]));
                }
                return counts = result;
            }
        }
        public static bool Has(string form, string motion) => motion == "Idle" || Counts.ContainsKey(form + "/" + motion);
        public Sprite[] Frames(string form, string motion)
        {
            string key = form + "/" + motion;
            if (loaded.TryGetValue(key, out var sequence)) return sequence;
            sequence = motion == "Idle" ? new[] { KarmaArtwork.StageIdle(form) } : Resources.LoadAll<Sprite>(Root + key);
            if (motion != "Idle" && (!Counts.TryGetValue(key, out int expected) || sequence.Length != expected))
                throw new InvalidOperationException("Missing motion drawings: " + key);
            Array.Sort(sequence, (a,b) => string.CompareOrdinal(a.name,b.name));
            return loaded[key] = sequence;
        }
        public Sprite Sample(string form, string motion, float elapsed, bool loop = false)
        {
            var sequence = Frames(form, motion);
            int frame = Mathf.FloorToInt(Mathf.Max(0, elapsed) * KarmaBootstrap.FramesPerSecond + 0.0001f);
            return sequence[loop ? frame % sequence.Length : Mathf.Min(sequence.Length - 1, frame)];
        }
    }
}
