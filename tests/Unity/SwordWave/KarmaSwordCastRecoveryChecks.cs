#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class KarmaSwordCastRecoveryChecks
{
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/cast-downcut-review"));
    const string Key = "K4RMA.CastDowncutCheck.Weight.3";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    const string UltimateOutput = "Temp/sw05-final-downcut";
    static bool started, preparing, ultimateCheck, volleyCheck, highChargeCheck;
    static string CaptureFolder => highChargeCheck ? "Temp/sw02-wave-smooth-cast" : volleyCheck ? "Temp/sw01-cast-review" : ultimateCheck ? UltimateOutput : Output;
    static int capture;
    static string runtimeError;
    static readonly List<string> results = new List<string>();

    static KarmaSwordCastRecoveryChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!File.Exists(Output + "/run.flag")) return;
        string request = File.ReadAllText(Output + "/run.flag").Trim();
        ultimateCheck = request == "sw05-downcut";
        volleyCheck = request == "volley";
        highChargeCheck = request == "import-wave-downcut" || request == "wave-downcut" || request == "wave-aimed-slash" || request == "wave-high-charge";
        if (!highChargeCheck && !ultimateCheck && !volleyCheck && request != "run" && request != "import" && request != "cleanup" && request != "counter" && request != "counter-sw02"
            && !request.StartsWith("resume ", StringComparison.Ordinal)) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!highChargeCheck && !ultimateCheck && !volleyCheck && SessionState.GetString(Key, "") == "done") return;
        if (!EditorApplication.isPlaying)
        {
            if (preparing || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            preparing = true;
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)
            { Finish("FAIL: Save the modified open scene before running."); return; }
            SessionState.SetString(Key, "running");
            if (highChargeCheck || ultimateCheck || volleyCheck) Directory.CreateDirectory(CaptureFolder);
            try
            {
                // Rebuilding owned bindings is explicit; ordinary checks use the saved assets.
                if (request == "import") KarmaSwordBasicArtChecks.ImportCastWindups();
                if (request == "import-wave-downcut") KarmaSwordBasicArtChecks.ImportWaveDowncut();
            }
            catch (Exception error) { Finish("FAIL: artwork import " + error); return; }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!started && (!(highChargeCheck || ultimateCheck || volleyCheck) || SessionState.GetString(Key, "") == "running")
            && UnityEngine.Object.FindAnyObjectByType<KarmaGame>() != null) { started = true; Run(); }
    }
    static object Get(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) return field.GetValue(target); }
        throw new MissingFieldException(name);
    }
    static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) { field.SetValue(target, value); return; } }
        throw new MissingFieldException(name);
    }
    static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
    static Vector2 MeshPoint(Sprite drawing, Vector2 original)
    {
        Vector2 pixel = original * drawing.pixelsPerUnit + drawing.pivot;
        float x = Mathf.Clamp(pixel.x / drawing.rect.width * 16, 0, 15.999f);
        float y = Mathf.Clamp(pixel.y / drawing.rect.height * 28, 0, 27.999f);
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), a = iy * 17 + ix;
        var vertices = drawing.vertices;
        return Vector2.Lerp(Vector2.Lerp(vertices[a], vertices[a + 1], x - ix),
            Vector2.Lerp(vertices[a + 17], vertices[a + 18], x - ix), y - iy);
    }
    public static void CheckWeight(Sprite drawing, Sprite source, SpriteRenderer blade, int frame, int form, Vector2 grip)
    {
        if (frame == 7) { Check(drawing == source, "Final settle changed original drawing"); return; }
        Check(drawing != source && drawing.texture == source.texture, "Weight pose repainted the approved art");
        var vertices = drawing.vertices; var uv = drawing.uv;
        Check(vertices.Length == 17 * 29, "Missing articulated cast mesh");
        float height = form == 2 ? 2.79f : form == 3 ? 2.77f : 2.68f;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 pixel = new Vector2(i % 17 * source.rect.width / 16, i / 17 * source.rect.height / 28);
            Vector2 expectedUv = new Vector2((source.rect.x + pixel.x) / source.texture.width,
                (source.rect.y + pixel.y) / source.texture.height);
            Check(Vector2.Distance(uv[i], expectedUv) < 0.00001f, "Weight mesh changed the original UVs");
            Vector2 original = (pixel - source.pivot) / source.pixelsPerUnit;
            if (original.y <= height * 0.11f)
                Check(Vector2.Distance(vertices[i], original) < 0.0001f, "Sole or ankle slid during the cast");
        }
        var triangles = drawing.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 a = vertices[triangles[i + 1]] - vertices[triangles[i]];
            Vector2 b = vertices[triangles[i + 2]] - vertices[triangles[i]];
            Check(a.x * b.y - a.y * b.x > 0, "Body mesh folded over itself");
        }
        Vector2 chest = new Vector2(0, height * 0.8f), shoulder = new Vector2(-height * 0.1f, height * 0.7f);
        Check(Mathf.Abs(Vector2.Distance(MeshPoint(drawing, chest), MeshPoint(drawing, shoulder))
            - Vector2.Distance(chest, shoulder)) < 0.0002f, "Upper body/arm proportions stretched");
        Vector2 socket = MeshPoint(drawing, grip);
        Check(Vector2.Distance(socket, blade.transform.localPosition) < 0.001f, "Steel detached from the posed hand");
        if (frame == 2 || frame == 4)
        {
            Vector2 hips = MeshPoint(drawing, new Vector2(0, height * 0.54f));
            Check(frame == 2 ? hips.x < -0.04f : hips.x > 0.08f, "Missing rear-to-front weight transfer");
            Check(hips.y < height * 0.54f - 0.04f, "Knees did not absorb the stance");
            Vector2 knee = new Vector2(height * 0.12f, height * 0.22f);
            Check(Vector2.Distance(MeshPoint(drawing, knee), knee) > 0.01f, "Lower body stayed static");
        }
    }
    static Task Frame()
    {
        var complete = new TaskCompletionSource<bool>();
        game.StartCoroutine(FrameEnd(complete));
        return complete.Task;
    }
    static IEnumerator FrameEnd(TaskCompletionSource<bool> complete)
    { yield return new WaitForEndOfFrame(); complete.SetResult(true); }
    static async Task Seconds(float duration)
    { float until = Time.time + duration; while (Time.time < until) await Frame(); }
    static void Reset(int form, int side, bool melee)
    {
        game.StartRun();
        var order = new List<Essence> { Essence.SwordWave };
        if (form == 1 || form == 3) order.Add(Essence.RisingSlash);
        if (form == 2 || form == 3) order.Add(Essence.Guard);
        foreach (var kind in order) { game.Player.Essences.Preview(kind); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Count);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        var art = enemy.transform.Find("Artwork");
        art.localScale = new Vector3(side * Mathf.Abs(art.localScale.x), art.localScale.y, art.localScale.z);
        var playerBody = game.Player.GetComponent<Rigidbody2D>();
        playerBody.bodyType = RigidbodyType2D.Kinematic;
        playerBody.position = new Vector2(20 + side * (melee ? 3.2f : 15), KarmaSpace.PlayerHalfHeight);
        game.Player.transform.position = playerBody.position;
        Set(game.Player, "invulnerableUntil", Time.time + 100);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "running", true);
        Physics2D.SyncTransforms();
    }
    static void Force(EnemyAttack attack)
    {
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", attack);
        Set(enemy, "direction", game.Player.Position.x < enemy.Position.x ? -1f : 1f);
        Call(Get(enemy, "behavior"), "OnSelected");
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Capture(Transform follow = null)
    {
        var camera = game.GameCamera;
        var rt = RenderTexture.GetTemporary(1280, 720, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var rect = camera.rect; var aspect = camera.aspect;
        var cameraPosition = camera.transform.position; float cameraSize = camera.orthographicSize;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1280f / 720;
            camera.orthographicSize = 8;
            camera.transform.position = new Vector3(enemy.Position.x + 4, 7, cameraPosition.z);
            if (follow != null)
            {
                Vector3 centre = follow.TransformPoint(Vector3.up * (highChargeCheck ? 2.1f : 1.5f));
                camera.orthographicSize = (highChargeCheck ? 2.85f : 2.35f) * Mathf.Abs(follow.lossyScale.y);
                camera.transform.position = new Vector3(centre.x, centre.y, cameraPosition.z);
            }
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(CaptureFolder + "/frame-" + (capture++).ToString("D5") + ".jpg", image.EncodeToJPG(90));
        }
        finally
        { camera.targetTexture = previous; camera.rect = rect; camera.aspect = aspect; RenderTexture.active = active;
            camera.transform.position = cameraPosition; camera.orthographicSize = cameraSize;
            UnityEngine.Object.Destroy(image); RenderTexture.ReleaseTemporary(rt); }
    }
    static async Task CheckUltimateDowncut(int side, bool enraged)
    {
        Time.captureDeltaTime = 1f / 120;
        Reset(3, side, false);
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * (enraged ? 0.1f : 0.5f));
        await Seconds(.12f);
        var actor = (KarmaCharacterAnimator)Get(enemy, "animator");
        var picture = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
        var blade = picture.transform.Find("Cast rigid blade").GetComponent<SpriteRenderer>();
        var art = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/WaveFinal");
        var source = new Dictionary<int, (Sprite sprite, Vector3 grip, Quaternion turn)>();
        var groupFrames = new[] {new List<int>(), new List<int>(), new List<int>()};
        foreach (var attack in new[] {EnemyAttack.SW02, EnemyAttack.SW05})
        {
            // Sample the short SW02 pre-contact drawing before testing gameplay frame rates.
            if (attack == EnemyAttack.SW05)
            {
                Check(source.Count == 7, "Incomplete SW02 reference: " + string.Join(",", source.Keys));
                Time.captureDeltaTime = 1f / (enraged ? 30 : 60);
            }
            float start = Time.time, nextCapture = start;
            Force(attack);
            while ((bool)Get(enemy, "running"))
            {
                await Frame();
                Check(Time.time - start < 30, "Cast did not finish: " + attack);
                if (attack == EnemyAttack.SW02)
                {
                    if (blade.enabled) source[(int)Get(actor,"swordCastDrawing")] =
                        (picture.sprite, blade.transform.localPosition, blade.transform.localRotation);
                    continue;
                }
                string phase = (string)actor.GetType().GetProperty("SwordBodyAction", Flags).GetValue(actor);
                bool detonation = phase == "UltimateCharge" || phase == "UltimateFinish";
                bool striking = phase == "UltimateWindup" || phase == "UltimateCommand" || detonation;
                if (striking)
                {
                    int group = detonation ? groupFrames.Length - 1
                        : (int)actor.GetType().GetProperty("SwordStrikeIndex", Flags).GetValue(actor);
                    int frame = (int)Get(actor, "swordCastDrawing");
                    if (phase == "UltimateWindup" && group > 0 && frame >= 4) group--;
                    Check(Mathf.Sign(picture.transform.lossyScale.x) == side, "Wrong facing");
                    Check(blade.enabled == (frame >= 0), "Rigid steel visibility differs from the body");
                    if (frame >= 0)
                    {
                        var expected = source[frame];
                        Check(picture.sprite == expected.sprite && blade.sprite == art.blade, "Different SW02 body, shield or steel at " + frame);
                        Check(Vector3.Distance(blade.transform.localPosition, expected.grip) < .0001f
                            && Quaternion.Angle(blade.transform.localRotation, expected.turn) < .01f, "Different SW02 wrist or blade angle");
                        if (groupFrames[group].Count == 0 || groupFrames[group].Last() != frame)
                        {
                            groupFrames[group].Add(frame);
                            if (frame == 4) Check(phase == "UltimateCommand"
                                && game.World.GetComponentInChildren<KarmaSW05RainBlade>() != null, "Downcut missed the rain release");
                        }
                    }
                    // The windup coroutine may begin after this render's body update.
                    // Command recovery, however, must settle in the shared SW02 idle.
                    else if (phase != "UltimateWindup")
                        Check(picture.sprite == art.idle, "Wrong settled shield pose: " + picture.sprite.name);
                }
                else Check(!blade.enabled, "Cast blade leaked into " + phase);
                if (!enraged && side == 1 && Time.time >= nextCapture)
                { Capture(picture.transform); nextCapture += 1f/24; }
            }
            Set(enemy, "running", true);
        }
        foreach (var frames in groupFrames)
            Check(frames.SequenceEqual(new[]{1,2,3,4,5,6,7}), "Missing or repeated ultimate downcut: " + string.Join(",",frames));
        Check(!blade.enabled, "Ultimate left a second sword on landing");
        results.Add("PASS WaveFinal side=" + side + " enraged=" + enraged + ": all three rain volleys match the live SW02 body, shield and blade.");
    }
    static async Task CheckUltimateInterruption(int frame)
    {
        Reset(3, 1, false);
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.5f);
        await Seconds(0.12f);
        var actor = (KarmaCharacterAnimator)Get(enemy, "animator");
        var blade = enemy.transform.Find("Artwork/Guardian sprite/Cast rigid blade").GetComponent<SpriteRenderer>();
        Force(EnemyAttack.SW05);
        float deadline = Time.time + 10f;
        while (!blade.enabled || (int)Get(actor, "swordCastDrawing") != frame)
        { Check(Time.time < deadline, "No SW05 drawing before interrupt: " + frame); await Frame(); }
        Call(enemy, "InterruptPattern");
        Check(!blade.enabled && float.IsNegativeInfinity((float)Get(actor, "swordRainCutAt")),
            "Interrupted SW05 retained its weapon or release time");
        results.Add("PASS WaveFinal interrupted at SW05 drawing " + frame);
    }
    static async Task CheckVolley(string formName, int side)
    {
        var body = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
        var blade = body.transform.Find("Cast rigid blade").GetComponent<SpriteRenderer>();
        var frames = KarmaArtResource.Frames("SwordWave/Volley/" + formName);
        Check(frames.Length == 6, "Missing original volley for " + formName);
        var seen = new HashSet<KarmaBossAttack>();
        var drawings = new List<string>();
        float start = Time.time, nextCapture = start, firstRelease = -1;
        int contacts = 0;
        Force(EnemyAttack.SW01);
        while ((bool)Get(enemy, "running"))
        {
            await Frame();
            Check(Time.time - start < 7, "SW01 did not finish");
            foreach (var shot in game.World.GetComponentsInChildren<KarmaBossAttack>())
            {
                if (Get(shot, "owner") != (object)enemy || !seen.Add(shot)) continue;
                if (firstRelease < 0) firstRelease = Time.time - start;
                Check(((Vector2)Get(shot, "velocity")).x * side > 0, "SW01 fired backwards");
            }
            Check(!blade.enabled, "SW01 drew a second sword");
            if ((bool)Get(enemy, "running"))
            {
                Check(frames.Contains(body.sprite), "Unexpected SW01 drawing: " + body.sprite.name);
                string name = body.sprite.name;
                if (drawings.LastOrDefault() != name)
                {
                    drawings.Add(name);
                    if (name == "SW01_01") contacts++;
                }
            }
            if (side > 0 && Time.time >= nextCapture)
            { Capture(body.transform); nextCapture += 1f / 24; }
        }
        Check(seen.Count == 2 && contacts == 2 && drawings.Contains("Windup_00")
            && drawings.Contains("SW01_00") && drawings.Contains("FollowThrough_00"), "SW01 sequence: " + string.Join(", ", drawings));
        Check(Mathf.Abs(firstRelease - (float)Call(enemy, "Warning", 0.65f)) < 0.07f, "SW01 warning changed");
        results.Add("PASS " + formName + " SW01 side=" + side + ", two releases: " + string.Join(" > ", drawings));
    }
    static async void Run()
    {
        try
        {
            Application.runInBackground = true;
            Application.logMessageReceived += Error;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Check(game != null, "Missing game");
            string mode = File.ReadAllText(Output + "/run.flag").Trim();
            if (volleyCheck)
            {
                Time.captureDeltaTime = 1f / 60;
                string[] volleyForms = { "Wave", "WaveRise", "WaveWard", "WaveFinal" };
                for (int form = 0; form < volleyForms.Length; form++)
                foreach (int side in new[] { 1, -1 })
                {
                    Reset(form, side, false);
                    await Seconds(0.15f);
                    await CheckVolley(volleyForms[form], side);
                }
                Check(runtimeError == null, runtimeError);
                Finish("PASS: SW01 across four forms and both facings.");
                return;
            }
            if (ultimateCheck)
            {
                foreach (bool enraged in new[] {false, true})
                foreach (int side in new[] {1, -1}) await CheckUltimateDowncut(side, enraged);
                await CheckUltimateInterruption(2);
                await CheckUltimateInterruption(4);
                Check(runtimeError == null, runtimeError);
                Finish("PASS: 12 SW05 downcuts matched the SW02 reference. Captured frames=" + capture);
                return;
            }
            if (mode.StartsWith("counter", StringComparison.Ordinal))
            {
                Time.captureDeltaTime = 1f / 60;
                var attacks = mode == "counter-sw02" ? new[] { EnemyAttack.SW02 } : new[] { EnemyAttack.SW01, EnemyAttack.SW02 };
                foreach (var previousAttack in attacks)
                {
                    Reset(3, 1, false); await Seconds(0.1f);
                    Call(enemy, "InterruptPattern"); Set(enemy, "running", true);
                    Set(enemy, "selected", previousAttack);
                    Set(enemy, "state", Enum.Parse(Get(enemy, "state").GetType(), "Approach"));
                    var actor = (KarmaCharacterAnimator)Get(enemy, "animator");
                    Call(actor, "PrepareSwordBody", "Counter", 0.5f, 0);
                    actor.GuardianPose("Approach", previousAttack, false, false);
                    Call(actor, "ReleaseSwordBody", "Counter", 0);
                    await Frame();
                    var picture = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
                    Check(picture.sprite.texture.name == "SW04", "Previous " + previousAttack + " replaced the independent counter gesture");
                    Check(!picture.transform.Find("Cast rigid blade").GetComponent<SpriteRenderer>().enabled,
                        "Counter inherited a stale cast blade");
                    results.Add("PASS independent SW04 after " + previousAttack);
                }
                Check(runtimeError == null, runtimeError);
                Finish("PASS: " + attacks.Length + " independent counter presentation checks.");
                return;
            }
            bool cleanupOnly = File.ReadAllText(Output + "/run.flag").Trim() == "cleanup";
            int firstVariant = 0;
            if (mode.StartsWith("resume ", StringComparison.Ordinal))
            {
                var resume = mode.Split(' ');
                firstVariant = int.Parse(resume[1]); capture = int.Parse(resume[2]);
                Check(firstVariant > 0 && firstVariant <= 4 && capture > 0, "Invalid resume point");
                results.AddRange(File.ReadAllLines(Output + "/result.txt").Where(line => line.StartsWith("PASS ")));
                Check(results.Count == firstVariant * 4, "Resume is missing completed form checks");
                Check(File.Exists(Output + "/frame-" + (capture - 1).ToString("D5") + ".jpg"), "Resume is missing earlier captures");
            }
            string[] forms = { "Wave", "WaveRise", "WaveWard", "WaveFinal" };
            var attacksToCheck = highChargeCheck ? new[] { EnemyAttack.SW02 } : new[] { EnemyAttack.SW01, EnemyAttack.SW02 };
            for (int variant = firstVariant; variant < (cleanupOnly ? 0 : highChargeCheck ? 1 : 5); variant++)
            foreach (int side in variant == 4 ? new[] { 1 } : new[] { 1, -1 })
            foreach (var attack in attacksToCheck)
            {
                int fps = variant == 4 || highChargeCheck && side < 0 ? 30 : 60;
                Time.captureDeltaTime = 1f / fps;
                int form = Mathf.Min(variant, 3);
                Reset(form, side, false);
                if (variant == 4)
                {
                    typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.1f);
                    Check(enemy.Enraged, "Missing enraged final");
                }
                await Seconds(0.15f);
                if (attack == EnemyAttack.SW01)
                {
                    await CheckVolley(forms[form], side);
                    continue;
                }
                var actor = (KarmaCharacterAnimator)Get(enemy, "animator");
                var picture = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
                var blade = picture.transform.Find("Cast rigid blade").GetComponent<SpriteRenderer>();
                var idle = picture.sprite; var scale = picture.transform.localScale;
                Check(AssetDatabase.GetAssetPath(idle).EndsWith("/Idle.png"), "Idle changed");
                KarmaSwordBasicArtChecks.IdleBladePoints(form, picture, out var idleHeel, out var idleTip);
                float idleLength = Vector3.Distance(idleHeel, idleTip);
                var binding = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/" + forms[form]);
                Check(binding.poses.Length == (form == 0 ? 16 : 8) && binding.idle == idle, "Incomplete cast binding");
                if (highChargeCheck) Capture(picture.transform);
                else if (side == 1) { Capture(); Capture(); Capture(); }
                var seen = new HashSet<KarmaBossAttack>(); var drawings = new List<string>();
                var meshes = new Dictionary<string, Sprite>();
                float start = Time.time, nextCapture = start, firstRelease = -1, chargeAt = -1, swingAt = -1, lowAt = -1;
                int contacts = 0;
                string lastCapturePose = null;
                if (form == 0)
                {
                    var basic = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/Wave");
                    Check(binding.blade == basic.blade && binding.bladeLength == basic.bladeLength, "Wave steel differs from its basic sword");
                    for (int i = 1; i < binding.angles.Length; i++)
                        Check(Mathf.Abs(Mathf.DeltaAngle(binding.angles[i - 1], binding.angles[i])) <= 56f,
                            "Abrupt SW02 blade reversal at " + binding.poses[i].name);
                    foreach (string other in forms.Skip(1))
                    {
                        var otherCast = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/" + other);
                        Check(otherCast.poses.Length == 8 && otherCast.poses.All(p => p.texture != binding.poses[0].texture),
                            "Basic downcut leaked into " + other);
                    }
                }
                Force(attack);
                while ((bool)Get(enemy, "running"))
                {
                    await Frame();
                    Check(Time.time - start < 7, "Cast did not finish");
                    foreach (var shot in game.World.GetComponentsInChildren<KarmaBossAttack>())
                    {
                        if (Get(shot, "owner") != (object)enemy || !seen.Add(shot)) continue;
                        if (firstRelease < 0) firstRelease = Time.time - start;
                        var velocity = (Vector2)Get(shot, "velocity");
                        Check(velocity.x * side > 0 && Mathf.Abs(velocity.y) < 0.001f, "Projectile direction changed");
                        if (form == 0) Check(picture.sprite == binding.poses[11], "Shot missed the contact drawing");
                    }
                    string name = picture.sprite.name;
                    if (drawings.Count == 0 || drawings[drawings.Count - 1] != name)
                    { drawings.Add(name); if (name == (form == 0 ? "SW02Downcut_06" : "CastDowncut_04")) contacts++; }
                    if (form == 0 && picture.sprite != idle)
                        Check(binding.poses.Contains(picture.sprite) && blade.enabled, "Missing authored body or steel: " + name);
                    else if (form != 0)
                        Check(picture.sprite.texture.name == "CastDowncut" || picture.sprite.texture.name == "CastWindup" || picture.sprite == idle,
                            "Unexpected cast drawing: " + name);
                    Check(Vector3.Distance(picture.transform.localScale, scale) < 0.001f, "Cast changed body scale");
                    if (blade.enabled)
                    {
                        int drawing = (int)Get(actor, "swordCastDrawing");
                        if (form == 0 && drawing == 12 && lowAt < 0) lowAt = Time.time - start;
                        if (form == 0 && firstRelease < 0)
                        {
                            bool charging = game.World.GetComponentsInChildren<KarmaSwordWaveAnimation>()
                                .Any(fx => Get(fx, "owner") == (object)enemy && Get(fx, "playback").ToString() == "Charge");
                            Check(charging == (drawing >= 6), "Charge and overhead drawing disagree at " + name);
                            if (drawing == 7 && swingAt < 0) swingAt = Time.time - start;
                        }
                        if (name == "SW02Downcut_04")
                        {
                            Check(form == 0 && firstRelease < 0 && drawing == 6, "High charge outside basic SW02 windup");
                            Check(blade.transform.localPosition.y > 2.48f && blade.sprite == binding.blade,
                                "Basic blade not held above the helmet");
                            if (chargeAt < 0) chargeAt = Time.time - start;
                        }
                        var source = binding.poses[drawing];
                        Vector2 pixel = binding.sockets[drawing];
                        Vector2 grip = (new Vector2(pixel.x, source.rect.height - pixel.y) - source.pivot) / source.pixelsPerUnit;
                        if (form == 0)
                        {
                            Check(picture.sprite == source && Vector2.Distance(blade.transform.localPosition, grip) < .001f,
                                "Authored joints or two-handed grip changed: " + name);
                            Check(Mathf.Abs(source.pixelsPerUnit - 150) < .01f &&
                                Vector2.Distance(source.pivot, new Vector2(156, 17)) < .05f, "Body registration changed");
                            if (drawing == 11) Check(firstRelease >= 0, "Downcut before shot");
                        }
                        else if (!meshes.TryGetValue(name, out var cached))
                        { CheckWeight(picture.sprite, source, blade, drawing, form, grip); meshes.Add(name, picture.sprite); }
                        else Check(cached == picture.sprite, "Cast mesh was allocated again for the same drawing");
                        float length = blade.transform.TransformVector(Vector3.right * blade.sprite.bounds.size.x).magnitude;
                        Check(Mathf.Abs(length - idleLength) < 0.01f, "Blade length changed from Idle");
                        Check(!blade.flipX && !blade.flipY, "Blade edge flipped");
                        var tip = blade.transform.TransformPoint(Vector3.right * blade.sprite.bounds.size.x);
                        Check(tip.y >= picture.transform.position.y - 0.03f, "Blade crossed below feet: " + name);
                        if (name == "CastDowncut_02")
                        {
                            // Head crown measured on each new drawing; test actual world-space
                            // geometry, including the left-facing mirror and inherited forms.
                            float crown = new[] {275f,275f,268f,284f}[form];
                            float headLocalY = (picture.sprite.rect.height - crown - picture.sprite.pivot.y) / picture.sprite.pixelsPerUnit;
                            float headY = picture.transform.TransformPoint(MeshPoint(picture.sprite, new Vector2(0, headLocalY))).y;
                            // The hand is behind the guard on the hilt. A loaded
                            // down/back blade can put its guard below the crown
                            // while both hands remain correctly above the head.
                            float angle = binding.angles[drawing] * Mathf.Deg2Rad;
                            Vector2 hand = grip - new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.18f;
                            float handY = picture.transform.TransformPoint(MeshPoint(picture.sprite, hand)).y;
                            Check(handY > headY, "Windup hand did not rise above head");
                            Check((blade.transform.position.x - picture.transform.position.x) * side < 0,
                                "Windup grip did not move behind head");
                            Check((tip.x - blade.transform.position.x) * side < -length * 0.5f && tip.y < blade.transform.position.y,
                                "Sword did not load down behind the boss");
                        }
                        if (name == "SW02Downcut_06" || name == "CastDowncut_04" || name == "CastDowncut_05")
                            Check((tip.x - blade.transform.position.x) * side > 0, "Release points backwards");
                        object[] points = { picture, null, null, null };
                        var animation = typeof(KarmaEnemy).Assembly.GetType("KarmaPrototype.KarmaSwordWaveAnimation");
                        Check((bool)animation.GetMethod("BladePoints", BindingFlags.Static | Flags).Invoke(null, points), "Missing charge socket");
                        Check(Vector2.Distance((Vector2)points[1], blade.transform.position) < 0.001f
                            && Vector2.Distance((Vector2)points[3], tip) < 0.001f, "Charge does not follow actual steel");
                    }
                    if ((side == 1 || highChargeCheck) && Time.time >= nextCapture)
                    {
                        if (highChargeCheck && side == 1 && name != lastCapturePose)
                        {
                            results.Add("FRAME " + capture + " " + name);
                            lastCapturePose = name;
                        }
                        Capture(highChargeCheck ? picture.transform : null);
                        nextCapture += 1f / 24;
                    }
                }
                Set(enemy, "running", true);
                if (form == 0)
                {
                    Check(drawings.SequenceEqual(binding.poses.Select(p => p.name).Concat(new[] { idle.name })),
                        "Incorrect SW02 sequence: " + string.Join(" > ", drawings));
                    float tracking = 0.34f * (float)enemy.GetType().GetProperty("Tempo", Flags).GetValue(enemy);
                    Check(Mathf.Abs(chargeAt - tracking) <= 2f / fps, "Slow lift to the seventh drawing: " + chargeAt);
                    Check(swingAt > chargeAt && Mathf.Abs(firstRelease - swingAt - 4f / 30) <= 1f / fps + .001f,
                        "Incorrect downstroke to release: " + (firstRelease - swingAt));
                    Check(lowAt > firstRelease && Mathf.Abs(lowAt - firstRelease - 1f / 30) <= 1f / fps + .001f,
                        "Incorrect contact to low pose: " + (lowAt - firstRelease));
                }
                Check(seen.Count == 1 && contacts == 1, "Release count changed: " + seen.Count + "/" + contacts);
                Check(firstRelease >= 0.34f * (float)enemy.GetType().GetProperty("Tempo", Flags).GetValue(enemy) + 0.8f - 0.05f, "SW02 fired before warning");
                if (form != 0)
                    foreach (int frame in new[] { 1,2,3,4,5,6 })
                        Check(drawings.Contains("CastDowncut_" + frame.ToString("00")), "Missing action phase " + frame);
                Check(picture.sprite == idle && !blade.enabled, "Did not return to unchanged Idle");
                results.Add("PASS " + forms[form] + " " + attack + " side=" + side + " fps=" + fps
                    + " shots=" + seen.Count + " first=" + firstRelease.ToString("F3")
                    + (form == 0 ? " charge=" + chargeAt.ToString("F3") + " swing=" + swingAt.ToString("F3") : "")
                    + " " + string.Join(" > ", drawings));
            }
            Time.captureDeltaTime = 1f / 60;
            foreach (var attack in attacksToCheck)
            foreach (string ending in highChargeCheck ? new[] { "interrupt", "stun", "death" } : new[] { "windup", "interrupt", "stun", "death" })
            {
                Reset(highChargeCheck ? 0 : 3, 1, false); await Seconds(0.1f); Force(attack);
                if (highChargeCheck)
                {
                    var body = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
                    float deadline = Time.time + 4;
                    while (body.sprite.name != "SW02Downcut_04")
                    { Check(Time.time < deadline, "High charge not reached before " + ending); await Frame(); }
                }
                else if (ending == "windup") await Seconds(0.12f);
                else
                {
                    float deadline = Time.time + 3;
                    while (!game.World.GetComponentsInChildren<KarmaBossAttack>().Any(s => Get(s, "owner") == (object)enemy))
                    { Check(Time.time < deadline, "No release before cleanup"); await Frame(); }
                }
                var actor = (KarmaCharacterAnimator)Get(enemy, "animator");
                var blade = enemy.transform.Find("Artwork/Guardian sprite/Cast rigid blade").GetComponent<SpriteRenderer>();
                if (ending == "death") enemy.TakeDamage(enemy.MaxHealth * 10);
                else if (ending == "stun") Set(enemy, "pendingParry", true);
                else
                {
                    Call(enemy, "InterruptPattern"); Set(enemy, "running", true);
                    Set(enemy, "state", Enum.Parse(Get(enemy, "state").GetType(), "Approach"));
                }
                await Seconds(0.2f);
                Check(blade == null || !blade.enabled, ending + " left cast steel visible");
                if (ending == "stun") Check(enemy.Stunned, "Stun did not interrupt");
                if (ending == "death") Check(enemy == null || !enemy.Alive, "Death did not interrupt");
                if (highChargeCheck || ending == "windup")
                    Check(!game.World.GetComponentsInChildren<KarmaBossAttack>().Any(s => Get(s, "owner") == (object)enemy),
                        "Cancelled warning still fired");
                results.Add("PASS " + attack + " " + ending);
            }
            Check(runtimeError == null, runtimeError);
            if (highChargeCheck)
            {
                Finish("PASS: Wave-only SW02 two-handed downcut, sixteen poses, Idle scale and blade length, both facings, release and cancellation. Captured frames=" + capture);
                return;
            }
            Finish("PASS: 16 casts across four forms and both facings, 2 enraged final casts at 30 fps; planted feet, torso/hip/knee weight transfer, rigid arm proportions, unchanged UVs, hand/blade/VFX attachment and cached meshes; 8 windup/interruption/stun/death checks. Basic and Idle assets unchanged. Captured frames=" + capture);
        }
        catch (Exception error) { Finish("FAIL: " + error); Debug.LogException(error); }
        finally { Time.captureDeltaTime = 0; Application.logMessageReceived -= Error; EditorApplication.ExitPlaymode(); }
    }
    static void Error(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + stack; }
    static void Finish(string message)
    {
        SessionState.SetString(Key, "done");
        results.Insert(0, message);
        File.WriteAllLines(CaptureFolder + "/result.txt", results);
        File.WriteAllText(Output + "/run.flag", "done");
    }
}
#endif
