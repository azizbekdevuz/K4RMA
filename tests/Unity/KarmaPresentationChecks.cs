#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

public static class KarmaPresentationChecks
{
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
    static int assertions;
    static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }
    static float Support(Sprite sprite) => (float)typeof(KarmaCharacterAnimator)
        .GetMethod("FallenSupportHeight", Hidden).Invoke(null, new object[] { sprite });

    [MenuItem("K4RMA/검증/배경과 사망 접지")]
    public static void RunFromMenu()
    {
        string result;
        try { Run(); result = "PASS: " + assertions + " background/original texture and fallen-body support assertions"; }
        catch (Exception error) { result = "FAIL: " + error; Debug.LogException(error); }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/presentation-ground.txt", result);
        Debug.Log(result);
    }
    public static void RunBatch()
    {
        RunFromMenu();
        EditorApplication.Exit(File.ReadAllText("output/validation/presentation-ground.txt").StartsWith("PASS:") ? 0 : 1);
    }
    static void Run()
    {
        assertions = 0;
        var stage = new GameObject("Presentation checks");
        try
        {
            var fallen = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Guardians/Revival/Neutral")
                .OrderBy(s => s.name).First();
            float silhouetteBottom = fallen.vertices.Min(v => v.y);
            float supportHeight = Support(fallen);
            Check(supportHeight > silhouetteBottom + 0.05f,
                "Actual fallen body's support must be higher than its protruding blade tip: " +
                supportHeight + " bottom=" + silhouetteBottom + " vertices=" + fallen.vertices.Length + " triangles=" + fallen.triangles.Length);

            var artwork = new GameObject("Detached fallen guardian"); artwork.transform.SetParent(stage.transform);
            artwork.transform.position = new Vector3(8, 12, 0);
            artwork.transform.rotation = Quaternion.Euler(0, 0, 18);
            artwork.transform.localScale = new Vector3(-1.7f, 1.7f, 1);
            var picture = new GameObject("Guardian sprite").AddComponent<SpriteRenderer>();
            picture.transform.SetParent(artwork.transform, false);
            picture.transform.localPosition = new Vector3(0, -KarmaSpace.GuardianGroundY / KarmaSpace.GuardianArtScale, 0);
            picture.transform.localScale = Vector3.one * KarmaSpace.Scale;
            var animator = artwork.AddComponent<KarmaCharacterAnimator>();
            animator.ConfigureSprites(picture, new[] { KarmaArtwork.Settings.neutralGuardian });
            animator.ConfigureSheet("Neutral");
            animator.Die();
            float support = Support(picture.sprite);
            Check(animator.CurrentPose == "Death", "Death pose is selected immediately");
            Check(Mathf.Abs(picture.transform.TransformPoint(new Vector3(0, support, 0)).y) < 0.001f,
                "Fallen body lands on the floor after an elevated, mirrored, tilted pose");
            Check(Quaternion.Angle(artwork.transform.rotation, Quaternion.identity) < 0.01f, "Death clears retained tilt");
            var settled = artwork.transform.position;
            animator.Pose(0, false);
            Check(Vector3.Distance(settled, artwork.transform.position) < 0.001f, "Repeated death updates do not drift");

            KarmaArtwork.Background(stage.transform, 0);
            var background = stage.transform.Find("Trial hall artwork");
            var renderer = background.GetComponent<SpriteRenderer>();
            Check(renderer.sprite == KarmaArtwork.Settings.dojoPanorama, "Stage zero uses the same extended temple");
            Check(background.GetComponentsInChildren<Renderer>().Length == 1, "Single plate has no overlay seam");
            Check(Mathf.Abs(renderer.bounds.size.x - KarmaStage.BackgroundWidth * 2) < 0.001f, "Two-width outpaint coverage");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stage);
        }
    }
}
#endif
