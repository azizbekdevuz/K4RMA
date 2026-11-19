#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;

public static class KarmaSwordCacheChecks
{
    public static void Run(Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        const string key = "BossArtwork/KarmaSwordBossSprites";
        var libraries = (Dictionary<string, KarmaSpriteLibrary>)typeof(KarmaArtwork).GetField("loadedLibraries", flags).GetValue(null);
        var frames = (Sprite[,])typeof(KarmaSwordScarRise).GetField("frames", flags).GetValue(null);
        var previousFrames = (Sprite[,])frames.Clone();
        bool hadLibrary = libraries.TryGetValue(key, out var previousLibrary);
        var library = UnityEngine.Object.Instantiate(Resources.Load<KarmaSpriteLibrary>(key));
        try
        {
            libraries[key] = library;
            Array.Clear(frames, 0, frames.Length);
            var sheet = library.sheets.Single(entry => entry.id == "SwordBossVFX");
            const string pose = "SW03_GroundScar_5f_3";
            int index = Array.IndexOf(sheet.poses, pose);
            check(index >= 0 && sheet.sprites[index] != null, "Shipped ground warning contains its fourth frame");
            var sprite = sheet.sprites[index];
            sheet.sprites[index] = null;
            bool rejected = false;
            try { KarmaSwordScarRise.Frame(0, 3); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, "A missing ground warning frame rejects playback");

            sheet.sprites[index] = sprite;
            typeof(KarmaSpriteLibrary).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(library, null);
            check(KarmaSwordScarRise.Frame(0, 3) == sprite, "Retry resolves the restored ground warning frame");
            for (int effect = 0; effect < frames.GetLength(0); effect++)
                for (int frame = 0; frame < frames.GetLength(1); frame++)
                    check(KarmaSwordScarRise.Frame(effect, frame) != null, "Every SW03 frame resolves from the shipped library");
        }
        finally
        {
            if (hadLibrary) libraries[key] = previousLibrary;
            else libraries.Remove(key);
            Array.Copy(previousFrames, frames, frames.Length);
            UnityEngine.Object.DestroyImmediate(library);
        }
    }
}
#endif
