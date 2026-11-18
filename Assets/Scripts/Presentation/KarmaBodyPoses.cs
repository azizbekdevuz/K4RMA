using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KarmaPrototype
{
    // Logical pose names and collision measurements predate the new motion sheets.
    // The named Sprite shares the generated frame's texture and rendering geometry.
    internal static class KarmaBodyPoses
    {
        sealed class Pose
        {
            public string name, form, motion;
            public int frame;
            public Bounds bounds;
            public float floor;
            public Sprite sprite;
        }
        static readonly Dictionary<string, List<Pose>> groups = new Dictionary<string, List<Pose>>();
        static readonly Dictionary<Sprite, Pose> geometry = new Dictionary<Sprite, Pose>();
        static KarmaMotionArt art = new KarmaMotionArt();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var sprite in geometry.Keys)
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
            geometry.Clear();
            groups.Clear();
            art = new KarmaMotionArt();
        }
        static void Read()
        {
            if (groups.Count != 0) return;
            float Number(string n) => float.Parse(n, CultureInfo.InvariantCulture);
            foreach (var line in Resources.Load<TextAsset>("Motions12/BodyPoses").text.Split(new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = line.Split('\t');
                if (!groups.TryGetValue(f[0], out var poses)) groups.Add(f[0], poses = new List<Pose>());
                poses.Add(new Pose { name=f[1], form=f[2], motion=f[3], frame=int.Parse(f[4]),
                    bounds=new Bounds(new Vector3(Number(f[5]),Number(f[6]),0),new Vector3(Number(f[7]),Number(f[8]),0)), floor=Number(f[9]) });
            }
        }
        static Sprite Resolve(Pose pose)
        {
            if (pose.sprite != null) return pose.sprite;
            pose.sprite = UnityEngine.Object.Instantiate(art.Frames(pose.form,pose.motion)[pose.frame]);
            pose.sprite.name = pose.name;
            pose.sprite.hideFlags = HideFlags.DontSave;
            geometry.Add(pose.sprite,pose);
            return pose.sprite;
        }
        public static Sprite Get(string group, string name)
        {
            Read();
            if (groups.TryGetValue(group,out var poses))
                foreach (var pose in poses) if (pose.name == name) return Resolve(pose);
            return null;
        }
        public static Sprite[] All(string group)
        {
            Read();
            return groups.TryGetValue(group,out var poses) ? poses.ConvertAll(Resolve).ToArray() : Array.Empty<Sprite>();
        }
        public static Bounds HitBounds(Sprite s) => geometry.TryGetValue(s,out var pose) ? pose.bounds : s.bounds;
        public static bool TryFloor(Sprite s, out float y)
        {
            if (geometry.TryGetValue(s,out var pose)) { y=pose.floor; return true; }
            y=0; return false;
        }
    }
}
