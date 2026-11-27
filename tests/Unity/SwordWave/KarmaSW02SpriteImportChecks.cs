#if UNITY_EDITOR
using System;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

public static class KarmaSW02SpriteImportChecks
{
    public static void CheckSheet(string form)
    {
        string path = "Assets/Arts/SwordWave/" + form + "/CastDowncut.png";
        var binding = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/" + form);
        if (binding == null || binding.blade == null || binding.idle == null)
            throw new InvalidOperationException("Missing SW02 cast binding: " + form);
        var frames = binding.poses;
        if (frames.Length != (form == "Wave" ? 16 : 8)) throw new InvalidOperationException("Unexpected SW02 frame count in " + path);
        if (form == "Wave")
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                if (!frame.name.StartsWith("SW02Downcut_", StringComparison.Ordinal) || (i > 0 && string.CompareOrdinal(frames[i-1].name, frame.name) >= 0) || AssetDatabase.GetAssetPath(frame) != "Assets/Arts/SwordWave/Wave/SW02Downcut.png"
                    || Vector2.Distance(frame.pivot, new Vector2(156,17)) > .05f)
                    throw new InvalidOperationException("Incorrect Wave downcut registration " + i);
            }
            return;
        }
        for (int i = 0; i < 8; i++)
        {
            string expected = i >= 1 && i <= 3 ? path.Replace("CastDowncut.png", "CastWindup.png") : path;
            if (frames[i].name != $"CastDowncut_{i:00}" || frames[i].pixelsPerUnit <= 0 || AssetDatabase.GetAssetPath(frames[i]) != expected)
                throw new InvalidOperationException("Unexpected SW02 drawing at " + path + ", frame " + i);
        }
    }

    public static void RunBatch()
    {
        try { CheckSheet("Wave"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
#endif
