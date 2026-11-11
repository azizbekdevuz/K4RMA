#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;

public static class KarmaSwordCacheChecks
{
    public static void Run(Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var cache = typeof(KarmaArtwork).GetField("bossLibraries", flags);
        var old = cache.GetValue(null);
        var library = UnityEngine.Object.Instantiate(Resources.LoadAll<KarmaSpriteLibrary>("BossArtwork")
            .Single(x => x.name == "KarmaSwordBossSprites"));
        var reset = typeof(KarmaSwordPackedVfx).GetMethod("ResetFrames", flags);
        try
        {
            cache.SetValue(null, new[] { library }); reset.Invoke(null, null);
            var sheet = library.sheets.Single(x => x.id == "SwordBossVFX");
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
            foreach (KarmaSwordPackedVfx.Clip clip in Enum.GetValues(typeof(KarmaSwordPackedVfx.Clip)))
                for (int i = 0; i < (clip == KarmaSwordPackedVfx.Clip.Detonation ? 8 : 6); i++)
                    check(KarmaSwordPackedVfx.Frame(clip, i) != null, "Every moved VFX frame resolves");
            rejected = false;
            try { KarmaSwordPackedVfx.Frame((KarmaSwordPackedVfx.Clip)99, 0); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Invalid clip is rejected explicitly");
        }
        finally { cache.SetValue(null, old); reset.Invoke(null, null); UnityEngine.Object.DestroyImmediate(library); }
    }
}
#endif
