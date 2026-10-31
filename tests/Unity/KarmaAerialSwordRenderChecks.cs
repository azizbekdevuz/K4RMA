#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using KarmaPrototype;

// Copy to an isolated project's Assets/Editor and run with graphics enabled:
// Unity -batchmode -projectPath <copy> -executeMethod KarmaAerialSwordRenderChecks.Run
// Do not pass -nographics. This runner exits explicitly and never saves a scene.
[InitializeOnLoad]
public static class KarmaAerialSwordRenderChecks
{
    const string SessionKey = "K4RMA.AerialSwordRenderChecks";
    const string ErrorKey = SessionKey + ".Error";
    const int ProbeLayer = 31;
    static int assertions;
    static readonly List<string> checks = new List<string>();
    static readonly List<Texture2D> captures = new List<Texture2D>();

    [Serializable]
    sealed class Report
    {
        public string result, utc, graphicsDevice, graphicsApi, shader, failure;
        public int assertions, maximumAlphaDifference, sourceWarmPixels, remappedCoolPixels;
        public int visiblePixels, opaquePixels, magentaPixels;
        public float oldWarningEnergy, newWarningEnergy;
        public string[] checks, artifacts;
    }

    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Aerial sword GPU: " + message);
        checks.Add(message);
    }

    static KarmaAerialSwordRenderChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += CaptureError;
    }

    public static void Run()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!File.Exists(Path.Combine(folder, ".karma-isolated-project")))
            throw new InvalidOperationException("The GPU runner requires a disposable .karma-isolated-project.");
        SessionState.SetBool(SessionKey, true);
        SessionState.SetString(ErrorKey, "");
        Application.logMessageReceived -= CaptureError;
        Application.logMessageReceived += CaptureError;
        EditorApplication.delayCall += BeginPlay;
    }

    static void BeginPlay()
    {
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        { EditorApplication.delayCall += BeginPlay; return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        Application.logMessageReceived -= CaptureError;
        Application.logMessageReceived += CaptureError;
        Application.runInBackground = true;
        new GameObject("SW05 GPU Play Mode driver").AddComponent<KarmaAerialSwordRenderCheckDriver>().Step = RunProbes;
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (string.IsNullOrEmpty(SessionState.GetString(ErrorKey, "")))
            SessionState.SetString(ErrorKey, type + ": " + message + "\n" + stack);
    }

    static void RunProbes()
    {
        assertions = 0; checks.Clear(); captures.Clear();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var report = new Report {
            result = "FAIL", utc = DateTime.UtcNow.ToString("o"),
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            artifacts = new[] { "aerial-sword-render.png", "aerial-sword-directions.png", "aerial-sword-warning-overlap.png" }
        };
        GameObject root = null;
        bool originalAsyncCompilation = ShaderUtil.allowAsyncCompilation;
        int exitCode = 1;
        try
        {
            Check(File.Exists(Path.Combine(folder, ".karma-isolated-project")),
                "the runner is inside a disposable isolated project before replacing its untitled scene");
            Check(Application.isPlaying && Time.frameCount > 0,
                "geometry and GPU probes execute from an actual Play Mode Update");
            Check(string.IsNullOrEmpty(SessionState.GetString(ErrorKey, "")),
                "Play Mode startup has no unexpected Error, Exception or Assert logs");
            // A first-frame cyan async-compilation placeholder is not the blade.
            ShaderUtil.allowAsyncCompilation = false;
            Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
                "a real graphics device is available (this is not a headless structural test)");
            Sprite source = Resources.Load<Sprite>("K4RMAArtwork/ExtraWaveVFX/SW05_AerialSword");
            Material normal = Resources.Load<Material>("KarmaSpriteUnlit");
            Material mapped = Resources.Load<Material>("KarmaAerialSword");
            Check(source != null && source == KarmaArtwork.Sprite("ExtraWaveVFX", "SW05_AerialSword"),
                "Resources and the actual artwork library resolve the same supplied SW05 PNG");
            Check(normal != null && mapped != null && mapped.shader != null
                && mapped.shader.name == "K4RMA/AerialSword", "the dedicated material and expected shader load");
            report.shader = mapped.shader.name;
            Check(mapped.shader.isSupported, "the aerial sword shader is supported by this graphics device");
            ShaderChecks(mapped.shader);

            root = new GameObject("Temporary SW05 GPU probes (never saved)");
            var cameraObject = new GameObject("SW05 probe camera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 1.15f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 30;
            camera.allowHDR = false; camera.allowMSAA = false; camera.enabled = false;
            camera.cullingMask = 1 << ProbeLayer;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.requiresColorTexture = false;
            cameraData.requiresDepthTexture = false;

            SpriteRenderer blade = MakeBlade(root.transform, source);
            var dimensions = new Vector2(3.12f, 1.3f);
            KarmaWaveArtwork.Apply(blade, "SW05_AerialSword", dimensions, Vector2.right);
            blade.color = Color.white;
            Sprite beforeSprite = blade.sprite;
            Vector3 beforeScale = blade.transform.localScale;
            Vector3[] beforeVertices = WorldVertices(blade);
            blade.sharedMaterial = normal;
            Texture2D warm = Capture(camera, 1024, 512);
            blade.sharedMaterial = mapped;
            Texture2D cool = Capture(camera, 1024, 512);
            Check(blade.sprite == beforeSprite && blade.transform.localScale == beforeScale
                && SameVertices(beforeVertices, WorldVertices(blade)),
                "the palette material leaves the actual SpriteRenderer mesh, scale and world geometry unchanged");
            ComparePixels(warm, cool, report);
            SaveMontage(new[] { warm, cool }, 2, 1, Path.Combine(folder, report.artifacts[0]));

            blade.color = new Color(1, 1, 1, 0.35f);
            Texture2D faint = Capture(camera, 1024, 512);
            int fullMax = MaximumAlpha(cool), faintMax = MaximumAlpha(faint);
            Check(Mathf.Abs(faintMax - fullMax * 0.35f) <= 3,
                "SpriteRenderer alpha reaches the actual framebuffer without an opaque rectangular fallback");
            blade.color = Color.white;

            var directionImages = new Texture2D[8];
            camera.orthographicSize = 2.1f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45;
                Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
                KarmaWaveArtwork.Apply(blade, "SW05_AerialSword", dimensions, direction);
                Check(!blade.flipX && !blade.flipY && Vector2.Dot(blade.transform.right, direction) > 0.9999f,
                    angle + " degrees: unreflected authored tip follows the velocity");
                directionImages[i] = Capture(camera, 512, 512);
                Check(MaximumAlpha(directionImages[i]) >= 240 && BorderIsTransparent(directionImages[i], 4),
                    angle + " degrees: the complete blade renders clearly without reaching a capture edge");
                Texture2D directionCool = directionImages[i];
                blade.sharedMaterial = normal;
                Texture2D directionWarm = Capture(camera, 512, 512);
                Check(AlphaDifference(directionWarm, directionCool) <= 2,
                    angle + " degrees: material remapping retains the rendered source silhouette and alpha");
                blade.sharedMaterial = mapped;
            }
            SaveMontage(directionImages, 4, 2, Path.Combine(folder, report.artifacts[1]));
            checks.Add("Direction montage order: bottom row 0/45/90/135 degrees; top row 180/225/270/315 degrees.");
            blade.gameObject.SetActive(false);

            var diagonal = KarmaVisuals.Box(root.transform, "SW05 single diagonal fill probe", Vector2.zero, Vector2.one,
                new Color(1, 0.55f, 0.2f, 0.3f), 15);
            diagonal.gameObject.AddComponent<KarmaAttackWarning>().SetPath(diagonal,
                new Vector2(-6, -3), new Vector2(6, 3), dimensions, true, true, true);
            SetLayer(diagonal.transform, ProbeLayer);
            camera.orthographicSize = 6;
            Texture2D diagonalCapture = Capture(camera, 1024, 576);
            Check(AlphaAt(camera, diagonalCapture, new Vector2(0, 2.4f)) <= 1,
                "a diagonal warning leaves safe AABB corners outside its actual swept hull transparent on the GPU");
            Check(AlphaAt(camera, diagonalCapture, Vector2.zero) >= 3,
                "the same diagonal warning retains its translucent fill inside the real danger hull");
            diagonal.gameObject.SetActive(false);

            var oldWarnings = MakeWarnings(root.transform, false);
            var newWarnings = MakeWarnings(root.transform, true);
            SetLayer(root.transform, ProbeLayer);
            camera.orthographicSize = 12;
            oldWarnings.SetActive(true); newWarnings.SetActive(false);
            Texture2D oldOverlap = Capture(camera, 1024, 576);
            oldWarnings.SetActive(false); newWarnings.SetActive(true);
            Texture2D newOverlap = Capture(camera, 1024, 576);
            report.oldWarningEnergy = Energy(oldOverlap);
            report.newWarningEnergy = Energy(newOverlap);
            Check(report.oldWarningEnergy > 100 && report.newWarningEnergy > 0
                && report.newWarningEnergy < report.oldWarningEnergy * 0.85f,
                "fifteen actual warning renderers retain visible paths with lower accumulated screen coverage/glare");
            SaveMontage(new[] { oldOverlap, newOverlap }, 2, 1, Path.Combine(folder, report.artifacts[2]));
            ShaderChecks(mapped.shader);
            Check(root.GetComponentsInChildren<Collider2D>(true).Length == 0,
                "GPU probes create no gameplay colliders and run in a separate unsaved scene");
            Check(string.IsNullOrEmpty(SessionState.GetString(ErrorKey, "")),
                "all actual GPU probes complete without Error, Exception or Assert logs");
            report.result = "PASS"; exitCode = 0;
        }
        catch (Exception error)
        {
            report.failure = error.ToString();
            Debug.LogException(error);
        }
        finally
        {
            if (root != null)
            {
                // Dispose this one-frame fixture's generated sprites immediately
                // before the standalone Editor exits.
                var shapeField = typeof(KarmaAttackWarning).GetField("shape",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                foreach (var warning in root.GetComponentsInChildren<KarmaAttackWarning>(true))
                {
                    Sprite shape = (Sprite)shapeField.GetValue(warning);
                    shapeField.SetValue(warning, null);
                    if (shape != null) UnityEngine.Object.DestroyImmediate(shape);
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
            foreach (var capture in captures) if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
            captures.Clear();
            ShaderUtil.allowAsyncCompilation = originalAsyncCompilation;
            string loggedError = SessionState.GetString(ErrorKey, "");
            if (!string.IsNullOrEmpty(loggedError))
            {
                report.result = "FAIL"; exitCode = 1;
                report.failure = (report.failure ?? "") + "\nUnexpected Unity log:\n" + loggedError;
            }
            SessionState.SetBool(SessionKey, false);
            Application.logMessageReceived -= CaptureError;
            report.assertions = assertions; report.checks = checks.ToArray();
            File.WriteAllText(Path.Combine(folder, "aerial-sword-render-report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(folder, "aerial-sword-render-report.txt"),
                report.result + " — SW05 actual GPU render checks: " + assertions + " assertions\n"
                + "UTC: " + report.utc + "\nGPU: " + report.graphicsDevice + " / " + report.graphicsApi + "\n"
                + string.Join("\n", checks) + "\n" + report.failure);
            Debug.Log("SW05 GPU render checks: " + report.result + " / " + assertions + " assertions");
            EditorApplication.Exit(exitCode);
        }
    }

    static void ShaderChecks(Shader shader)
    {
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
            Check(message.severity.ToString() != "Error", "shader compiler: " + message.message);
        Check(!ShaderUtil.ShaderHasError(shader), "the shader has no compiler errors after actual rendering");
    }

    static SpriteRenderer MakeBlade(Transform parent, Sprite source)
    {
        var go = new GameObject("Actual SW05 source sprite");
        go.transform.SetParent(parent, false); go.layer = ProbeLayer;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = source; renderer.sortingOrder = 26;
        renderer.drawMode = SpriteDrawMode.Simple;
        return renderer;
    }

    static Texture2D Capture(Camera camera, int width, int height)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        target.antiAliasing = 1; target.Create();
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.aspect = (float)width / height;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); result.Apply(false, false);
            captures.Add(result);
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void ComparePixels(Texture2D warm, Texture2D cool, Report report)
    {
        Color32[] a = warm.GetPixels32(), b = cool.GetPixels32();
        report.maximumAlphaDifference = AlphaDifference(warm, cool);
        int coloredPixels = 0, neutralPixels = 0, emptyPixels = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].a < 2) emptyPixels++;
            if (a[i].a >= 64 && a[i].r > a[i].b + 12) report.sourceWarmPixels++;
            if (b[i].a < 64) continue;
            report.visiblePixels++;
            if (b[i].a >= 240) report.opaquePixels++;
            if (b[i].b >= b[i].r - 2 && b[i].b >= b[i].g - 2) report.remappedCoolPixels++;
            if (b[i].b > b[i].r + 8) coloredPixels++;
            if (Mathf.Abs(b[i].r - b[i].g) < 10 && Mathf.Abs(b[i].g - b[i].b) < 10) neutralPixels++;
            if (b[i].r > 190 && b[i].b > 190 && b[i].g < 60) report.magentaPixels++;
        }
        Check(report.visiblePixels > 1000 && report.opaquePixels > 100 && MaximumAlpha(cool) >= 240,
            "the actual remapped blade has a clearly visible opaque core");
        Check(report.sourceWarmPixels > 500 && report.remappedCoolPixels >= report.visiblePixels * 0.98f
            && coloredPixels > 100 && neutralPixels > 100,
            "the warm supplied PNG renders predominantly blue with retained neutral white highlights");
        Check(report.maximumAlphaDifference <= 2,
            "normal and remapped SpriteRenderer output have the same per-pixel alpha (maximum difference <= 2/255)");
        Check(emptyPixels > a.Length * 0.50f && BorderIsTransparent(cool, 4),
            "transparent source surroundings remain transparent instead of rendering an opaque rectangular background");
        Check(report.magentaPixels == 0, "the rendered blade contains no magenta error-shader pixels");
    }

    static int AlphaDifference(Texture2D a, Texture2D b)
    {
        var first = a.GetPixels32(); var second = b.GetPixels32();
        int maximum = 0;
        for (int i = 0; i < first.Length; i++) maximum = Mathf.Max(maximum, Mathf.Abs(first[i].a - second[i].a));
        return maximum;
    }
    static int MaximumAlpha(Texture2D image)
    {
        int maximum = 0;
        foreach (var pixel in image.GetPixels32()) maximum = Mathf.Max(maximum, pixel.a);
        return maximum;
    }
    static int AlphaAt(Camera camera, Texture2D image, Vector2 world)
    {
        Vector3 viewport = camera.WorldToViewportPoint(new Vector3(world.x, world.y, 0));
        int x = Mathf.Clamp(Mathf.FloorToInt(viewport.x * image.width), 0, image.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(viewport.y * image.height), 0, image.height - 1);
        return Mathf.RoundToInt(image.GetPixel(x, y).a * 255);
    }
    static bool BorderIsTransparent(Texture2D image, int border)
    {
        Color32[] pixels = image.GetPixels32();
        for (int y = 0; y < image.height; y++)
            for (int x = 0; x < image.width; x++)
                if ((x < border || x >= image.width - border || y < border || y >= image.height - border)
                    && pixels[y * image.width + x].a > 1) return false;
        return true;
    }
    static Vector3[] WorldVertices(SpriteRenderer renderer)
    {
        Vector2[] vertices = renderer.sprite.vertices;
        var result = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) result[i] = renderer.transform.TransformPoint(vertices[i]);
        return result;
    }
    static bool SameVertices(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if ((a[i] - b[i]).sqrMagnitude > 0.00000001f) return false;
        return true;
    }

    static GameObject MakeWarnings(Transform parent, bool aerial)
    {
        var group = new GameObject(aerial ? "New SW05 fifteen-path preview" : "Previous restrained fifteen-path preview");
        group.transform.SetParent(parent, false);
        for (int i = 0; i < 15; i++)
        {
            float radians = i * 360f / 15 * Mathf.Deg2Rad;
            Vector2 from = new Vector2(Mathf.Cos(radians) * 15, Mathf.Sin(radians) * 7);
            Vector2 to = -from;
            var marker = KarmaVisuals.Box(group.transform, "Path " + i, Vector2.zero, Vector2.one,
                new Color(1, 0.55f, 0.2f, 0.3f), 15);
            marker.gameObject.AddComponent<KarmaAttackWarning>().SetPath(marker, from, to, new Vector2(3.12f, 1.3f), true, true, aerial);
        }
        return group;
    }
    static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayer(child, layer);
    }
    static float Energy(Texture2D image)
    {
        float result = 0;
        foreach (Color32 pixel in image.GetPixels32()) result += (pixel.r + pixel.g + pixel.b) / 765f;
        return result;
    }
    static void SaveMontage(Texture2D[] panels, int columns, int rows, string path)
    {
        int width = panels[0].width, height = panels[0].height;
        var montage = new Texture2D(width * columns, height * rows, TextureFormat.RGBA32, false, false);
        captures.Add(montage);
        // Composite the captured alpha onto a dark neutral QA background only.
        // The imported source PNG and its importer are never written or changed.
        for (int i = 0; i < panels.Length; i++)
        {
            Color32[] pixels = panels[i].GetPixels32();
            for (int p = 0; p < pixels.Length; p++)
            {
                Color32 pixel = pixels[p];
                // GPU RGB already contains source-over-black blending; add the
                // remaining background rather than applying alpha a second time.
                float remaining = 1 - pixel.a / 255f;
                pixels[p] = new Color32((byte)Mathf.Clamp(pixel.r + 12 * remaining, 0, 255),
                    (byte)Mathf.Clamp(pixel.g + 16 * remaining, 0, 255),
                    (byte)Mathf.Clamp(pixel.b + 24 * remaining, 0, 255), 255);
            }
            montage.SetPixels32((i % columns) * width, (i / columns) * height, width, height, pixels);
        }
        montage.Apply(false, false);
        File.WriteAllBytes(path, montage.EncodeToPNG());
    }
}

public sealed class KarmaAerialSwordRenderCheckDriver : MonoBehaviour
{
    public Action Step;
    void Update()
    {
        Action step = Step;
        Step = null;
        if (step != null) step();
    }
}
#endif
