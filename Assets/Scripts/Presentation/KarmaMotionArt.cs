using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // One cache per performer so leaving a room releases that form's loaded sheets.
    internal sealed class KarmaMotionArt
    {
        public const string Root = "Motions12/";
        static Dictionary<string, int> counts;
        readonly Dictionary<(string form, string motion), Sprite[]> loaded = new Dictionary<(string, string), Sprite[]>();
        static KarmaMotionArt commonPlayer = new KarmaMotionArt();
        static readonly string[] playerOpening = {
            "JumpStart", "Run", "Jump", "Land", "Attack", "AirHorizontal", "Turn", "Fall",
            "Apex", "AirRecovery", "RunRecovery", "Recovery", "Walk", "Parry", "Guard"
        };
        static readonly string[] guardianOpening = {
            "Move", "Windup", "GroundStrike", "SWBasic", "UPBasic", "GDBasic",
            "Backstep", "FollowThrough", "Guard", "Hurt", "Stun"
        };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            commonPlayer = new KarmaMotionArt();
            counts = null;
        }

        internal static IEnumerator PreparePlayer() => commonPlayer.Prepare("Player", playerOpening);
        internal static bool TryPreparedPlayer(string motion, out Sprite[] frames)
            => commonPlayer.loaded.TryGetValue(("Player", motion), out frames);

        internal IEnumerator PrepareGuardian(string form) => Prepare(form, guardianOpening);
        IEnumerator Prepare(string form, string[] motions)
        {
            // Integrate one texture at a time. Keep real input responsive if it arrives before preparation finishes.
            foreach (string motion in motions)
            {
                if (!Has(form, motion) || loaded.ContainsKey((form, motion))) continue;
                var request = Resources.LoadAsync<Texture2D>(Root + form + "/" + motion);
                yield return request;
                if (request.asset != null) Frames(form, motion);
                yield return null;
            }
        }
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
            if (loaded.TryGetValue((form, motion), out var sequence)) return sequence;
            string key = form + "/" + motion;
            sequence = motion == "Idle" ? new[] { KarmaArtwork.StageIdle(form) } : Resources.LoadAll<Sprite>(Root + key);
            if (motion != "Idle" && (!Counts.TryGetValue(key, out int expected) || sequence.Length != expected))
                throw new InvalidOperationException("Missing motion drawings: " + key);
            Array.Sort(sequence, (a,b) => string.CompareOrdinal(a.name,b.name));
            return loaded[(form, motion)] = sequence;
        }
        public Sprite Sample(string form, string motion, float elapsed, bool loop = false)
        {
            var sequence = Frames(form, motion);
            int frame = Mathf.FloorToInt(Mathf.Max(0, elapsed) * KarmaBootstrap.FramesPerSecond + 0.0001f);
            return sequence[loop ? frame % sequence.Length : Mathf.Min(sequence.Length - 1, frame)];
        }
    }
}
