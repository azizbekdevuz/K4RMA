using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    [InitializeOnLoad]
    public static class KarmaGuardDomeAssets
    {
        const string VerifyKey = "GD05.VerifyInPlayMode";
        static KarmaGuardDomeAssets()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(VerifyKey, false)) return;
                SessionState.SetBool(VerifyKey, false);
                new GameObject("GD05 Play Mode verification").AddComponent<KarmaGuardDomeVerificationRunner>();
            };
        }
        public static void ImportAndVerify()
        {
            Import("GD05_GoldenSeal", new Vector2(0.5f, 0.5f));
            Import("GD05_GoldenFoot", new Vector2(0.385f, 0.09f));
            Import("GD05_InternalBlast", new Vector2(0.5f, 0.145f));
            VerifyInPlayMode();
        }
        public static void VerifyInPlayMode()
        {
            SessionState.SetBool(VerifyKey, true);
            EditorApplication.isPlaying = true;
        }
        public static void VerifyRenders()
        {
            var parent = new GameObject("GD05 seal verification");
            var cameraNode = new GameObject("GD05 preview camera");
            Texture2D pixels = null; RenderTexture target = null; Sprite backdrop = null;
            try
            {
                var dome = KarmaGuardDome.Create(null, parent.transform, Vector2.zero, 20);
                var roof = dome.GetComponent<EdgeCollider2D>();
                if (roof.pointCount != 65 || dome.Radius != 20) throw new Exception("GD05 dimensions changed");
                for (int i = 0; i < 65; i++)
                {
                    float a = Mathf.PI * i / 64;
                    if (Vector2.Distance(roof.points[i], new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 20) > 0.0001f)
                        throw new Exception("GD05 collider changed");
                }
                var bg = Resources.Load<Texture2D>("Backgrounds/TrialHall/Background");
                if (bg == null) throw new Exception("Preview background missing");
                backdrop = Sprite.Create(bg, new Rect(0, 0, bg.width, bg.height), Vector2.one * 0.5f, bg.width / 48f);
                var node = new GameObject("Preview background"); node.transform.SetParent(parent.transform);
                node.transform.position = new Vector3(0, 7, 2);
                var renderer = node.AddComponent<SpriteRenderer>(); renderer.sprite = backdrop; renderer.sortingOrder = -10;
                KarmaVisuals.ApplyMaterial(renderer);
                var camera = cameraNode.AddComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = 15; camera.transform.position = new Vector3(0, 9, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
                target = new RenderTexture(1200, 750, 24); camera.targetTexture = target;
                pixels = new Texture2D(1200, 750, TextureFormat.RGBA32, false);
                var dir = System.IO.Path.GetFullPath("../output/gd05-golden-seal"); Directory.CreateDirectory(dir);
                void Capture(string name)
                {
                    camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0); pixels.Apply(); RenderTexture.active = previous;
                    File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), pixels.EncodeToPNG());
                }
                Capture("GD05_Normal");
                VerifyContinuousRim(dome);
                VerifyRimRendering(dome, camera, target, pixels);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var paint = typeof(KarmaGuardDome).GetMethod("Paint", flags);
                dome.Dissolve();
                if (roof.enabled) throw new Exception("Success did not release collision immediately");
                for (int frame = 0; frame <= 10; frame++)
                {
                    typeof(KarmaGuardDome).GetField("dissolveAge", flags).SetValue(dome, Mathf.Min(0.4f, frame / 24f));
                    paint.Invoke(dome, null); Capture("GD05_Success_" + frame.ToString("00"));
                }
                foreach (var sr in dome.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.color.a > 0.0001f) throw new Exception("Success sprite survived 0.4s");
                foreach (var line in dome.GetComponentsInChildren<LineRenderer>())
                    if (line.startColor.a > 0.0001f || line.endColor.a > 0.0001f)
                        throw new Exception("Success light survived 0.4s");
                VerifyNoMeshLight(dome);
                typeof(KarmaGuardDome).GetField("dissolving", flags).SetValue(dome, false);
                typeof(KarmaGuardDome).GetField("dissolveAge", flags).SetValue(dome, 0f);
                roof.enabled = true;
                dome.WarnFailure(); paint.Invoke(dome, null); Capture("GD05_Warning");
                if (!roof.enabled) throw new Exception("Warning disabled collision");
                for (int frame = 0; frame <= 12; frame++)
                {
                    typeof(KarmaGuardDome).GetField("warningAge", flags).SetValue(dome, frame / 24f);
                    paint.Invoke(dome, null); Capture("GD05_FailureWarning_" + frame.ToString("00"));
                    if (!roof.enabled) throw new Exception("Crack growth disabled collision");
                }
                VerifyFailureGeometry(dome);
                dome.Explode();
                if (roof.enabled) throw new Exception("Dissolve failed to disable collision");
                for (int frame = 0; frame <= 10; frame++)
                {
                    typeof(KarmaGuardDome).GetField("dissolveAge", flags).SetValue(dome, Mathf.Min(0.4f, frame / 24f));
                    paint.Invoke(dome, null); Capture("GD05_FailureBlast_" + frame.ToString("00"));
                }
                typeof(KarmaGuardDome).GetField("dissolveAge", flags).SetValue(dome, 0.2f);
                paint.Invoke(dome, null); Capture("GD05_Dissolve");
                typeof(KarmaGuardDome).GetField("dissolveAge", flags).SetValue(dome, 0.4f);
                paint.Invoke(dome, null);
                foreach (var sr in dome.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.color.a > 0.0001f) throw new Exception("Decoration remained after original fade time");
                foreach (var line in dome.GetComponentsInChildren<LineRenderer>())
                    if (line.startColor.a > 0.0001f || line.endColor.a > 0.0001f)
                        throw new Exception("Failure VFX survived 0.4s");
                VerifyNoMeshLight(dome);
                VerifyConcurrentPresentation(parent.transform);
                Debug.Log("GD05 golden seal PASS: continuous feathered 256-segment rim; radius 20, diameter 40, 65 unchanged collision points; shared traced seams, concave plate triangulation, fixed initial hairlines; success/failure collision shutdown and all mesh/sprite/line light gone within 0.4s.");
            }
            finally
            {
                // Release the captured objects and generated sprite geometry before the verifier exits.
                foreach (var dome in parent.GetComponentsInChildren<KarmaGuardDome>())
                {
                    var field = typeof(KarmaGuardDome).GetField("fillShape", BindingFlags.Instance | BindingFlags.NonPublic);
                    var shape = field.GetValue(dome) as Sprite;
                    field.SetValue(dome, null);
                    if (shape != null) UnityEngine.Object.DestroyImmediate(shape);
                }
                UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(cameraNode);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (backdrop != null) UnityEngine.Object.DestroyImmediate(backdrop);
            }
        }
        static void VerifyContinuousRim(KarmaGuardDome dome)
        {
            var field = typeof(KarmaGuardDome).GetField("rimShape", BindingFlags.Instance | BindingFlags.NonPublic);
            var shape = (Mesh)field.GetValue(dome); var vertices = shape.vertices; var colors = shape.colors;
            if (vertices.Length != 257 * 4 * 3 || shape.triangles.Length != 256 * 3 * 3 * 6) throw new Exception("Continuous rim topology changed");
            for (int i = 0; i <= 256; i++)
            {
                int index = 257 * 4 + i * 4;
                Vector3 center = (vertices[index + 1] + vertices[index + 2]) * 0.5f;
                Vector2 expected = new Vector2(Mathf.Cos(Mathf.PI * i / 256), Mathf.Sin(Mathf.PI * i / 256)) * 20;
                if (Vector2.Distance(center, expected) > 0.0001f) throw new Exception("Visual rim changed the dome radius");
                if (colors[index].a != 0 || colors[index + 3].a != 0 || colors[index + 1].a <= 0)
                    throw new Exception("Rim edge coverage has no feather");
            }
        }
        static void VerifyRimRendering(KarmaGuardDome dome, Camera camera, RenderTexture target, Texture2D pixels)
        {
            var rim = (MeshRenderer)typeof(KarmaGuardDome).GetField("rim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dome);
            var before = new Color[31]; var previous = RenderTexture.active;
            try
            {
                Color Sample(int index)
                {
                    float angle = Mathf.PI * (index + 0.5f) / 31;
                    Vector3 at = camera.WorldToViewportPoint(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * dome.Radius);
                    return pixels.GetPixel(Mathf.Clamp((int)(at.x * pixels.width), 0, pixels.width - 1), Mathf.Clamp((int)(at.y * pixels.height), 0, pixels.height - 1));
                }
                void Read()
                {
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0); pixels.Apply();
                }
                rim.enabled = false; Read(); for (int i = 0; i < before.Length; i++) before[i] = Sample(i);
                rim.enabled = true; Read(); int visible = 0;
                for (int i = 0; i < before.Length; i++)
                {
                    Color difference = Sample(i) - before[i];
                    if (difference.r + difference.g + difference.b > 0.015f) visible++;
                }
                if (visible < 27) throw new Exception("Continuous rim is missing from camera output: " + visible + "/31 samples visible");
                Debug.Log("GD05 camera output PASS: continuous rim visible at " + visible + "/31 sampled arc positions.");
            }
            finally { rim.enabled = true; RenderTexture.active = previous; }
        }
        static void VerifyNoMeshLight(KarmaGuardDome dome)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in dome.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                renderer.GetPropertyBlock(properties);
                float alpha = properties.GetColor("_Color").a;
                foreach (var color in renderer.GetComponent<MeshFilter>().sharedMesh.colors)
                    if (color.a * alpha > 0.0001f) throw new Exception("Mesh light survived 0.4s");
            }
        }
        static void VerifyConcurrentPresentation(Transform parent)
        {
            const int count = 8, samples = 60;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var paints = new Action[count];
            long meshBytes = 0;
            for (int i = 0; i < count; i++)
            {
                var dome = KarmaGuardDome.Create(null, parent, Vector2.zero, 20);
                dome.WarnFailure(); dome.Explode();
                typeof(KarmaGuardDome).GetField("dissolveAge", flags).SetValue(dome, 0.12f);
                paints[i] = (Action)Delegate.CreateDelegate(typeof(Action), dome, typeof(KarmaGuardDome).GetMethod("Paint", flags));
                paints[i]();
                foreach (var mesh in dome.GetComponentsInChildren<MeshFilter>())
                    meshBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh.sharedMesh);
            }
            var watch = new System.Diagnostics.Stopwatch();
            using (var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "GC.Alloc", 4096,
                Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                watch.Start();
                for (int sample = 0; sample < samples; sample++) foreach (var paint in paints) paint();
                watch.Stop(); recorder.Stop();
                Debug.Log("GD05 presentation CPU probe: 8 simultaneous explosions, " + (watch.Elapsed.TotalMilliseconds / samples).ToString("F3")
                    + " ms per group update, GC.Alloc events=" + recorder.Count + ", mesh references=" + meshBytes
                    + " bytes (shared flare mesh counted per renderer). GPU/full-game frame time not measured.");
            }
        }
        static void VerifyFailureGeometry(KarmaGuardDome dome)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(KarmaGuardDome);
            var paths = (Vector2[][])type.GetField("crackPaths", flags).GetValue(dome);
            var plates = (Vector2[][])type.GetField("platePaths", flags).GetValue(dome);
            var shapes = (Sprite[])type.GetField("fragmentShapes", flags).GetValue(dome);
            var cracks = (LineRenderer[])type.GetField("cracks", flags).GetValue(dome);
            foreach (var path in paths) foreach (var point in path)
                if (point.sqrMagnitude > 1.00001f || point.y < -0.00001f) throw new Exception("Crack outside dome");
            float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
            float totalArea = 0;
            for (int i = 0; i < plates.Length; i++)
            {
                var polygon = plates[i]; float area = 0, triangleArea = 0;
                for (int p = 0; p < polygon.Length; p++)
                {
                    if (polygon[p].sqrMagnitude > 1.00001f || polygon[p].y < -0.00001f) throw new Exception("Plate outside dome");
                    area += Cross(polygon[p], polygon[(p + 1) % polygon.Length]) * 0.5f;
                }
                var vertices = shapes[i].vertices; var triangles = shapes[i].triangles;
                if (triangles.Length != (polygon.Length - 2) * 3) throw new Exception("Incomplete plate triangulation");
                for (int t = 0; t < triangles.Length; t += 3)
                    triangleArea += Mathf.Abs(Cross(vertices[triangles[t + 1]] - vertices[triangles[t]],
                        vertices[triangles[t + 2]] - vertices[triangles[t]])) * 8;
                if (Mathf.Abs(triangleArea - Mathf.Abs(area)) > 0.0001f) throw new Exception("Concave plate triangles overlap or leave holes");
                totalArea += Mathf.Abs(area);
            }
            if (Mathf.Abs(totalArea - Mathf.PI * 0.5f) > 0.004f) throw new Exception("Plates do not cover the semicircle");
            for (int i = 0; i < 14; i++)
            {
                if (i == 1) continue; // A dangling hairline travels on its plate, rather than separating two plates.
                for (int p = 1; p < paths[i].Length; p++)
                {
                    int matches = 0; Vector2 a = paths[i][p - 1], b = paths[i][p];
                    foreach (var plate in plates) for (int q = 0; q < plate.Length; q++)
                    {
                        Vector2 c = plate[q], d = plate[(q + 1) % plate.Length];
                        if (((a - c).sqrMagnitude < 0.00000001f && (b - d).sqrMagnitude < 0.00000001f)
                            || ((a - d).sqrMagnitude < 0.00000001f && (b - c).sqrMagnitude < 0.00000001f)) matches++;
                    }
                    if (matches != 2) throw new Exception("Crack seam is not shared by exactly two plates: " + i);
                }
            }
            var paint = type.GetMethod("Paint", flags);
            // The first three roots and their tips are already fully drawn at 0.25s and never drift later.
            foreach (float time in new[] { 0.25f, 0.5f })
            {
                type.GetField("warningAge", flags).SetValue(dome, time); paint.Invoke(dome, null);
                foreach (int i in new[] { 0, 1, 2, 3, 5, 6 }) for (int p = 0; p < paths[i].Length; p++)
                    if (Vector2.Distance(cracks[i].GetPosition(p), paths[i][p] * dome.Radius) > 0.0001f)
                        throw new Exception("Initial crack moved during expansion");
            }
            if (dome.GetComponentsInChildren<Collider2D>().Length != 1) throw new Exception("Decoration added an attack collider");
        }
        static void Import(string name, Vector2 pivot)
        {
            string path = "Assets/Arts/Resources/Guard/Effects/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new Exception("Missing GD05 artwork: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            textureSettings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                throw new Exception("GD05 pivot editing unsupported");
            var rects = provider.GetSpriteRects();
            foreach (var rect in rects) { rect.alignment = SpriteAlignment.Custom; rect.pivot = pivot; }
            provider.SetSpriteRects(rects); provider.Apply(); importer.SaveAndReimport();
        }
    }
    public sealed class KarmaGuardDomeVerificationRunner : MonoBehaviour
    {
        void Update()
        {
            enabled = false;
            try { KarmaGuardDomeAssets.VerifyRenders(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
