#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;

public static class KarmaSwordCacheChecks
{
    public static void Run(Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        const string key = "BossArtwork/KarmaSwordBossSprites";
        var cache = (Dictionary<string, KarmaSpriteLibrary>)typeof(KarmaArtwork).GetField("loadedLibraries", flags).GetValue(null);
        bool hadLibrary = cache.TryGetValue(key, out var old);
        var library = UnityEngine.Object.Instantiate(Resources.LoadAll<KarmaSpriteLibrary>("BossArtwork")
            .Single(x => x.name == "KarmaSwordBossSprites"));
        var reset = typeof(KarmaSwordPackedVfx).GetMethod("ResetFrames", flags);
        try
        {
            cache[key] = library; reset.Invoke(null, null);
            var sheet = library.sheets.Single(x => x.id == "SwordBossVFX");
            // Exercise the failure/retry contract using a temporary library only.
            var sample = sheet.sprites[Array.IndexOf(sheet.poses, "SW03_GroundScar_5f_0")];
            sheet.poses = sheet.poses.Concat(Enumerable.Range(0, 6).Select(i => "SW03_GroundCharge_6f_" + i)).ToArray();
            sheet.sprites = sheet.sprites.Concat(Enumerable.Repeat(sample, 6)).ToArray();
            int missing = Array.IndexOf(sheet.poses, "SW03_GroundCharge_6f_3");
            check(missing >= 0, "Expected packed frame is registered");
            var sprite = sheet.sprites[missing]; sheet.sprites[missing] = null; typeof(KarmaSpriteLibrary).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(library, null);
            bool rejected = false;
            try { KarmaSwordPackedVfx.Frame(KarmaSwordPackedVfx.Clip.GroundCharge, 0); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Missing frame rejects the whole load");
            var root = new GameObject("Missing pack spawn fixture");
            try
            {
                rejected = false;
                try { KarmaSwordPackedVfx.Spawn(root.transform, null, KarmaSwordPackedVfx.Clip.GroundCharge,
                    Vector2.zero, Vector2.one, 1, 0); }
                catch (InvalidOperationException) { rejected = true; }
                check(rejected && root.transform.childCount == 0, "Failed load leaves no live effect object");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            sheet.sprites[missing] = sprite; typeof(KarmaSpriteLibrary).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(library, null);
            check(KarmaSwordPackedVfx.Frame(KarmaSwordPackedVfx.Clip.GroundCharge, 3) == sprite,
                "Retry after missing resource does not return a partially initialized cache");
            for (int effect = 0; effect < 3; effect++)
                for (int i = 0; i < 5; i++)
                    check(KarmaSwordScarRise.Frame(effect, i) != null, "Every SW03 frame resolves from the shipped library");
            rejected = false;
            try { KarmaSwordPackedVfx.Frame((KarmaSwordPackedVfx.Clip)99, 0); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Invalid clip is rejected explicitly");
        }
        finally
        {
            if (hadLibrary) cache[key] = old; else cache.Remove(key);
            reset.Invoke(null, null);
            UnityEngine.Object.DestroyImmediate(library);
        }
    }
}
#endif
