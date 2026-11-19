#if UNITY_EDITOR
using System;
using System.Linq;
using KarmaPrototype;
using KarmaPrototype.EditorTools;
using UnityEditor;
using UnityEngine;

public static class KarmaSW02SpriteImportChecks
{
    public static void CheckSheet(string form)
    {
        string path = KarmaMotionSheetImport.SheetPath(form, "SW02");
        string key = "Motions12/" + form + "/SW02";
        var frames = KarmaArtResource.Frames(key).OrderBy(sprite => sprite.name).ToArray();
        if (frames.Length != 6) throw new InvalidOperationException("Expected six SW02 frames in " + path);
        for (int i = 0; i < frames.Length; i++)
            if (frames[i].name != $"SW02_{i:00}" || frames[i].pixelsPerUnit <= 0 || AssetDatabase.GetAssetPath(frames[i]) != path)
                throw new InvalidOperationException("Unexpected SW02 drawing at " + path + ", frame " + i);
    }

    public static void RunBatch()
    {
        try { CheckSheet("Wave"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
#endif
