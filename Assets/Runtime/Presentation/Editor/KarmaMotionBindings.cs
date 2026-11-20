using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaMotionBindings
    {
        [MenuItem("K4RMA/Animation/Rebind character artwork")]
        public static void Apply()
        {
            var frames = new Dictionary<(string, string), Sprite[]>();
            Sprite Frame(string form, string motion, int index)
            {
                if (!frames.TryGetValue((form, motion), out var sequence))
                {
                    string resource = motion == "Idle" ? KarmaArtwork.StageArt + form + "_Idle" : "Motions12/" + form + "/" + motion;
                    sequence = KarmaArtResource.Frames(resource).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                    frames.Add((form, motion), sequence);
                }
                return sequence[index];
            }
            var bindings = new Dictionary<(string, string), Sprite>();
            foreach (var row in Resources.Load<TextAsset>("Motions12/BodyPoses").text.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = row.Split('\t');
                if (!f[0].StartsWith("@", StringComparison.Ordinal)) bindings.Add((f[0], f[1]), Frame(f[2], f[3], int.Parse(f[4])));
            }
            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:KarmaSpriteLibrary", new[] {"Assets/Data/Resources"}))
            {
                var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(AssetDatabase.GUIDToAssetPath(guid));
                bool dirty = false;
                foreach (var sheet in library.sheets)
                {
                    int matched = 0;
                    var names = new string[sheet.sprites.Length];
                    for (int i = 0; i < names.Length; i++)
                    {
                        names[i] = sheet.poses != null && i < sheet.poses.Length && !string.IsNullOrEmpty(sheet.poses[i])
                            ? sheet.poses[i] : sheet.sprites[i].name;
                        if (!bindings.TryGetValue((sheet.id, names[i]), out var sprite)) continue;
                        sheet.sprites[i] = sprite;
                        matched++;
                    }
                    if (matched == 0) continue;
                    sheet.poses = names;
                    if (matched == names.Length) sheet.source = "Assets/Arts/Resources/Motions12/BodyPoses.txt";
                    assigned += matched;
                    dirty = true;
                }
                if (dirty) EditorUtility.SetDirty(library);
            }
            if (assigned != bindings.Count) throw new InvalidOperationException("Unbound body poses: " + (bindings.Count - assigned));
            var settings = KarmaArtwork.Settings;
            settings.playerIdle = Frame("Player", "Idle", 0);
            settings.playerMove1 = Frame("Player", "Run", 0);
            settings.playerMove2 = Frame("Player", "Run", 3);
            settings.playerSlash1 = Frame("Player", "Attack", 0);
            settings.playerSlash2 = Frame("Player", "Attack", 1);
            settings.playerRising = Frame("Player", "Rising", 1);
            settings.playerFalling = Frame("Player", "AirDown", 1);
            settings.playerHurt = Frame("Player", "Hurt", 0);
            settings.playerDeath = Frame("Player", "Death", 7);
            settings.neutralGuardian = Frame("Neutral", "Idle", 0);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Bound " + assigned + " named body poses and 10 artwork settings slots.");
        }
    }
}
