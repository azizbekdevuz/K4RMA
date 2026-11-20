using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public static class KarmaPlayerTimeline
    {
        public const string Sheets = "Motions12/Player/";
        public const string Clips = "Player/Animation12/";

        public sealed class Motion
        {
            public readonly string Name, Source;
            public readonly float Duration;
            public readonly bool Loop;
            public string ResourcePath => Name == "Idle" ? KarmaArtwork.StageArt + "Player_Idle" : Sheets + Source;
            readonly string[] drawingNames;

            internal Motion(string name, int count, float duration = 0, bool loop = false)
            {
                Name = name;
                Source = name;
                Duration = duration > 0 ? duration : count * KarmaBootstrap.Step;
                Loop = loop;
                drawingNames = new string[count];
                for (int i = 0; i < count; i++) drawingNames[i] = Source + "_" + i.ToString("00");
            }

            public Sprite[] Sequence(Sprite[] drawings)
            {
                if (Name == "Idle")
                {
                    var idle = Array.Find(drawings, sprite => sprite != null && sprite.name == "Player_Idle");
                    if (idle == null) throw new InvalidOperationException("Missing StageStyle Player_Idle");
                    return new[] { idle };
                }
                var frames = new Sprite[drawingNames.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    string key = drawingNames[i];
                    frames[i] = Array.Find(drawings, sprite => sprite != null && sprite.name == key);
                    if (frames[i] == null) throw new InvalidOperationException(Name + " missing sprite " + key);
                }
                return frames;
            }

            public Sprite[] Load() => Sequence(KarmaArtResource.Frames(ResourcePath));
        }

        public static readonly Motion[] Motions = {
            new Motion("Idle", 1, duration: 1f, loop: true),
            new Motion("Walk", 8, loop: true),
            new Motion("Run", 7, duration: 0.5416667f, loop: true),
            new Motion("Turn", 2),
            new Motion("JumpStart", 2, duration: 0.125f),
            new Motion("Jump", 5),
            new Motion("Apex", 1),
            new Motion("Fall", 5, duration: 0.375f),
            new Motion("Land", 2, duration: 0.1f),
            new Motion("Crouch", 2, duration: 0.125f),
            new Motion("Attack", 3),
            new Motion("SwordWave", 3),
            new Motion("Rising", 3),
            new Motion("Pierce", 3),
            new Motion("PierceUp", 3),
            new Motion("PierceDiagonal", 3),
            new Motion("PierceDown", 3),
            new Motion("AirHorizontal", 2),
            new Motion("AirUp", 2),
            new Motion("AirDiagonal", 2),
            new Motion("AirHover", 4),
            new Motion("AirDown", 2),
            new Motion("Guard", 3),
            new Motion("Parry", 3),
            new Motion("Riposte", 4),
            new Motion("Recovery", 3),
            new Motion("RunRecovery", 4),
            new Motion("AirRecovery", 2),
            new Motion("Hurt", 2),
            new Motion("StunStart", 2),
            new Motion("StunLoop", 4, loop: true),
            new Motion("StunRecovery", 3),
            new Motion("ShockStart", 2),
            new Motion("ShockLoop", 4, loop: true),
            new Motion("ShockRecovery", 3),
            new Motion("Knockback", 3),
            new Motion("AirKnockback", 3),
            new Motion("AirborneRise", 2),
            new Motion("AirborneApex", 2),
            new Motion("AirborneFall", 2),
            new Motion("Knockdown", 4),
            new Motion("DownLoop", 2),
            new Motion("GetUp", 6),
            new Motion("Death", 8),
            new Motion("PersonalizeWave", 6),
            new Motion("PersonalizeRising", 6),
            new Motion("PersonalizeGuard", 6),
            new Motion("Kneel", 4),
            new Motion("StandFromKneel", 4),
            new Motion("Salute", 8),
        };

        static readonly Dictionary<string, Motion> byName = BuildLookup();
        static Dictionary<string, Motion> BuildLookup()
        {
            var result = new Dictionary<string, Motion>(StringComparer.Ordinal);
            foreach (var motion in Motions) result.Add(motion.Name, motion);
            result.Add("AirSlash", result["AirHorizontal"]);
            result.Add("Dash", result["Pierce"]);
            result.Add("Transition", result["PersonalizeWave"]);
            return result;
        }
        public static Motion Find(string name) => byName.TryGetValue(name, out var motion) ? motion : null;
    }
}
