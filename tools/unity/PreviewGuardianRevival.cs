using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using KarmaPrototype;

// Render the real animator, imported sprites, pivots and material at six moments.
public static class PreviewGuardianRevival
{
    public static string Main()
    {
        if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Preview in Edit Mode.");
        var root = new GameObject("Revival preview") { hideFlags = HideFlags.HideAndDontSave };
        var cameraObject = new GameObject("Revival preview camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        var target = new RenderTexture(1536, 1024, 24);
        var previous = RenderTexture.active;
        Texture2D capture = null;
        try
        {
            var forms = new[] { "Neutral", "Wave", "Rise", "Ward" };
            var times = new[] { 0.04f, 0.19f, 0.30f, 0.54f, 0.69f, 0.94f };
            for (int row = 0; row < forms.Length; row++)
                for (int col = 0; col < times.Length; col++)
                {
                    var actor = new GameObject(forms[row] + " " + col); actor.transform.SetParent(root.transform);
                    actor.transform.position = new Vector3((col - 2.5f) * 12, 12.8f - row * 12 + KarmaSpace.GuardianGroundY, 0);
                    var order = row == 0 ? new Essence[0] : new[] { (Essence)(row - 1) };
                    var art = KarmaArtwork.Guardian(actor.transform, order, false);
                    art.localScale = Vector3.one * KarmaSpace.GuardianArtScale;
                    var animator = art.GetComponent<KarmaCharacterAnimator>();
                    var fallen = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Guardians/Revival/" + forms[row]);
                    Array.Sort(fallen, (a, b) => string.CompareOrdinal(a.name, b.name));
                    animator.Empower(row == 0 ? Essence.SwordWave : (Essence)(row - 1), 3.2f, fallen[0]);
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(KarmaCharacterAnimator).GetField("empowermentAt", flags).SetValue(animator, Time.time - times[col] * 3.2f);
                    typeof(KarmaCharacterAnimator).GetField("empowermentUntil", flags).SetValue(animator, Time.time + (1 - times[col]) * 3.2f);
                    animator.GuardianPose("Idle", EnemyAttack.GroundStrike, false, false);
                }
            foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            camera.orthographic = true; camera.orthographicSize = 24; camera.aspect = 1.5f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f, 1);
            camera.cullingMask = 1 << 31; camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); capture.Apply();
            Directory.CreateDirectory("output/validation/revival");
            File.WriteAllBytes("output/validation/revival/guardian-get-up.png", capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root);
        }
        return "Rendered 24 actual guardian get-up poses to output/validation/revival/guardian-get-up.png";
    }
}
