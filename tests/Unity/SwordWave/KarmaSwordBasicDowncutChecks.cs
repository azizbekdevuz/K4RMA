#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class KarmaSwordBasicDowncutChecks
{
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/basic-tone-review"));
    const string Key = "K4RMA.BasicDowncutCheck.BodyWeight.1";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started, preparing;
    static int capture;
    static string runtimeError;
    static readonly List<string> results = new List<string>();

    static KarmaSwordBasicDowncutChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!File.Exists(Output + "/run.flag")) return;
        string request = File.ReadAllText(Output + "/run.flag").Trim();
        if (request != "run" && request != "import" && request != "cleanup") return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if (SessionState.GetString(Key, "") == "done") return;
        if (!EditorApplication.isPlaying)
        {
            if (preparing || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            preparing = true;
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)
            { Finish("FAIL: Save the modified open scene before running."); return; }
            SessionState.SetString(Key, "running");
            try
            {
                // Rebuilding owned bindings is explicit; ordinary checks use the saved assets.
                if (File.ReadAllText(Output + "/run.flag").Trim() == "import") KarmaSwordBasicArtChecks.Import();
            }
            catch (Exception error) { Finish("FAIL: artwork import " + error); return; }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!started && UnityEngine.Object.FindAnyObjectByType<KarmaGame>() != null) { started = true; Run(); }
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
    static void Force(EnemyAttack attack = EnemyAttack.SWBasic)
    {
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", attack);
        Set(enemy, "direction", game.Player.Position.x < enemy.Position.x ? -1f : 1f);
        Call(Get(enemy, "behavior"), "OnSelected");
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Capture()
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
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/frame-" + (capture++).ToString("D5") + ".jpg", image.EncodeToJPG(90));
        }
        finally
        { camera.targetTexture = previous; camera.rect = rect; camera.aspect = aspect; RenderTexture.active = active;
            camera.transform.position = cameraPosition; camera.orthographicSize = cameraSize;
            UnityEngine.Object.Destroy(image); RenderTexture.ReleaseTemporary(rt); }
    }
    static async void Run()
    {
        try
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 60;
            Application.logMessageReceived += Error;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Check(game != null, "Missing game");
            bool cleanupOnly = File.ReadAllText(Output + "/run.flag").Trim() == "cleanup";
            for (int variant = 0; variant < (cleanupOnly ? 0 : 5); variant++)
            foreach (int side in variant == 4 ? new[] { 1 } : new[] { 1, -1 })
            foreach (bool melee in new[] { true, false })
            {
                Time.captureDeltaTime = variant == 4 ? 1f / 30 : 1f / 60;
                int form = Mathf.Min(variant, 3);
                Reset(form, side, melee);
                if (variant == 4)
                {
                    typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * 0.1f);
                    Check(enemy.Enraged, "Missing enraged basic variant");
                }
                await Seconds(0.15f);
                var idlePicture = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
                Check(AssetDatabase.GetAssetPath(idlePicture.sprite).EndsWith("/Idle.png"), "Approved idle was replaced");
                var idleSprite = idlePicture.sprite;
                var idleScale = idlePicture.transform.localScale;
                var idleBlade = idlePicture.transform.Find("Basic rigid blade").GetComponent<SpriteRenderer>();
                Check(!idleBlade.enabled, "Idle unexpectedly uses the attack blade");
                KarmaSwordBasicArtChecks.IdleBladePoints(form, idlePicture, out var idleHeel, out var idleTip);
                float idleLength = Vector3.Distance(idleHeel, idleTip);
                Check((idleTip.x - idleHeel.x) * side > 0 && idleTip.y < idleHeel.y, "Idle blade must point down-forward");
                if (side == 1) { Capture(); Capture(); Capture(); }
                Force();
                var seen = new HashSet<KarmaBossAttack>(); var drawings = new List<string>();
                var picture = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
                var rigidBlade = picture.transform.Find("Basic rigid blade").GetComponent<SpriteRenderer>();
                var animator = Get(enemy, "animator");
                var binding = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/" + new[] {"Wave","WaveRise","WaveWard","WaveFinal"}[form]);
                var meshes = new Dictionary<int, Sprite>();
                float bladeLength = -1;
                float start = Time.time, nextCapture = start, firstRelease = -1, firstRecoveryAge = -1;
                while ((bool)Get(enemy, "running"))
                {
                    await Frame();
                    Check(Time.time - start < 6, "Basic attack did not finish");
                    foreach (var shot in game.World.GetComponentsInChildren<KarmaBossAttack>())
                    {
                        if (Get(shot, "owner") != (object)enemy) continue;
                        if (firstRelease < 0) firstRelease = Time.time - start;
                        seen.Add(shot);
                        Check(((Vector2)Get(shot, "velocity")).sqrMagnitude < 0.01f == melee, "Wrong basic mode");
                    }
                    var name = picture.sprite.name;
                    if (name == "BasicDowncut_05" && seen.Count > 0 && firstRecoveryAge < 0)
                        firstRecoveryAge = Time.time - (float)Get(Get(enemy, "animator"), "basicReleasedAt");
                    Check(Vector3.Distance(picture.transform.localScale, idleScale) < 0.001f,
                        "Idle and basic attack changed body scale");
                    if (rigidBlade.enabled)
                    {
                        int drawing = (int)Get(animator, "swordBasicDrawing");
                        int phase = (int)Get(animator, "swordBasicWeightPhase");
                        Sprite source = binding.poses[drawing];
                        Vector2 pixel = binding.sockets[drawing];
                        Vector2 grip = (new Vector2(pixel.x, source.rect.height - pixel.y) - source.pivot) / source.pixelsPerUnit;
                        if (!meshes.TryGetValue(phase, out var cached))
                        { KarmaSwordCastRecoveryChecks.CheckWeight(picture.sprite, source, rigidBlade, phase, form, grip); meshes.Add(phase, picture.sprite); }
                        else Check(cached == picture.sprite, "Basic body mesh was reallocated during the hold");
                        object[] points = { picture, null, null, null };
                        var animation = typeof(KarmaEnemy).Assembly.GetType("KarmaPrototype.KarmaSwordWaveAnimation");
                        Check((bool)animation.GetMethod("BladePoints", BindingFlags.Static | Flags).Invoke(null, points), "Missing basic blade VFX socket");
                        Check(Vector2.Distance((Vector2)points[1], rigidBlade.transform.position) < 0.001f,
                            "Basic charge detached from the posed hand");
                        float length = rigidBlade.transform.TransformVector(Vector3.right * rigidBlade.sprite.bounds.size.x).magnitude;
                        if (bladeLength < 0) bladeLength = length;
                        Check(Mathf.Abs(length - bladeLength) < 0.001f, "Rigid blade changed length during downcut");
                        Check(Mathf.Abs(length - idleLength) < 0.01f, "Blade length changed between idle and attack");
                        Check(!rigidBlade.flipX && !rigidBlade.flipY, "Blade edge flipped during downcut");
                        var tip = rigidBlade.transform.TransformPoint(Vector3.right * rigidBlade.sprite.bounds.size.x);
                        Check(tip.y >= picture.transform.position.y - 0.03f, "Basic blade tip passed below the planted feet: " + name);
                        if (name == "BasicDowncut_05")
                            Check((tip.x - rigidBlade.transform.position.x) * side > 0, "Recovery blade turned behind boss");
                        Check(Vector2.Distance(picture.bounds.center, rigidBlade.transform.position) < picture.bounds.size.magnitude,
                            "Blade detached from body");
                    }
                    if (drawings.Count == 0 || drawings[drawings.Count - 1] != name) drawings.Add(name);
                    if (side == 1 && Time.time >= nextCapture) { Capture(); nextCapture += 1f / 24; }
                }
                Set(enemy, "running", true);
                Check(seen.Count == 1, "Expected one basic contact/projectile, got " + seen.Count);
                float expectedWarning = (float)Call(enemy, "Warning", 0.4f);
                Check(Mathf.Abs(firstRelease - expectedWarning) < 0.06f, "Basic warning changed");
                Check(Mathf.Abs(firstRecoveryAge - 2f / 12 / 2f) < 0.025f && firstRecoveryAge < 2f / 12 - 0.003f,
                    "Downstroke was not faster: recovery at " + firstRecoveryAge);
                Check(bladeLength > 0, "Rigid basic blade was never displayed");
                Check(drawings.Contains("BasicDowncut_01") && drawings.Contains("BasicDowncut_03") && drawings.Contains("BasicDowncut_04") && drawings.Contains("BasicDowncut_05"),
                    "Missing two-handed downcut drawing " + string.Join(",", drawings));
                Check(picture.sprite == idleSprite && !rigidBlade.enabled, "Basic did not return to the approved idle");
                foreach (int phase in new[] {1,2,3,4,5,6,7})
                    Check(meshes.ContainsKey(phase), "Missing basic body weight/settle phase " + phase);
                results.Add("PASS form=" + form + " facing=" + side + " melee=" + melee + " stroke=" + firstRecoveryAge
                    + " one release; " + string.Join(" > ", drawings));
            }
            Time.captureDeltaTime = 1f / 60;
            foreach (bool melee in new[] { true, false })
            {
                Reset(3, 1, melee); await Seconds(0.1f); Force(); await Seconds(0.1f);
                Call(enemy, "InterruptPattern"); Set(enemy, "running", true); await Seconds(0.3f);
                Check(((Transform)Get(enemy, "effectsRoot")).GetComponentsInChildren<KarmaBossAttack>().Length == 0,
                    "Interrupted windup left a basic attack");
                results.Add("PASS windup interruption melee=" + melee);
            }
            foreach (string ending in new[] { "interrupt", "stun", "death" })
            foreach (bool melee in new[] { true, false })
            {
                Reset(0, 1, melee); await Seconds(0.1f); Force();
                float deadline = Time.time + 2;
                while (Get(enemy, "state").ToString() != "Attack")
                { Check(Time.time < deadline, "No basic release before cleanup check"); await Frame(); }
                var effects = (Transform)Get(enemy, "effectsRoot");
                var animator = Get(enemy, "animator");
                if (ending == "death") enemy.TakeDamage(enemy.MaxHealth * 10);
                else if (ending == "stun") Set(enemy, "pendingParry", true);
                else { Call(enemy, "InterruptPattern"); Set(enemy, "running", true); }
                await Seconds(0.2f);
                Check(effects == null || effects.GetComponentsInChildren<KarmaBossAttack>().Length == 0,
                    ending + " left a live basic contact/projectile");
                Check(!(bool)Get(animator, "basicPresentationActive"), ending + " kept the basic pose active");
                var blade = enemy != null ? enemy.transform.Find("Artwork/Guardian sprite/Basic rigid blade") : null;
                Check(blade == null || !blade.GetComponent<SpriteRenderer>().enabled, ending + " left the cancelled strike blade visible");
                if (ending == "interrupt")
                {
                    // InterruptPattern clears the cast; its caller owns the next state.
                    Set(enemy, "state", Enum.Parse(Get(enemy, "state").GetType(), "Approach"));
                    await Seconds(0.15f);
                    Check(blade != null && !blade.GetComponent<SpriteRenderer>().enabled
                        && AssetDatabase.GetAssetPath(blade.parent.GetComponent<SpriteRenderer>().sprite).EndsWith("/Idle.png"),
                        "Interruption did not restore the approved idle");
                }
                if (ending == "stun") Check(enemy.Stunned, "Basic did not yield to stun");
                if (ending == "death") Check(enemy == null || !enemy.Alive, "Basic prevented death");
                results.Add("PASS released " + ending + " melee=" + melee);
            }
            if (!cleanupOnly)
            {
                // The same component owns basic and skill meshes. Check that
                // switching back to skills keeps their approved weight poses.
                Reset(3, 1, false); await Seconds(0.15f);
                foreach (var attack in new[] {EnemyAttack.SW01, EnemyAttack.SW02})
                {
                    var picture = enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
                    var blade = picture.transform.Find("Cast rigid blade").GetComponent<SpriteRenderer>();
                    var binding = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/WaveFinal");
                    var seenFrames = new HashSet<int>();
                    Force(attack); float deadline = Time.time + 7;
                    while ((bool)Get(enemy, "running"))
                    {
                        await Frame(); Check(Time.time < deadline, "Skill did not finish after basic mesh setup");
                        if (!blade.enabled) continue;
                        int frame = int.Parse(picture.sprite.name.Substring(picture.sprite.name.Length - 2));
                        if (!seenFrames.Add(frame)) continue;
                        Sprite source = binding.poses[frame]; Vector2 pixel = binding.sockets[frame];
                        Vector2 grip = (new Vector2(pixel.x, source.rect.height - pixel.y) - source.pivot) / source.pixelsPerUnit;
                        KarmaSwordCastRecoveryChecks.CheckWeight(picture.sprite, source, blade, frame, 3, grip);
                    }
                    Check(seenFrames.Count >= 6 && picture.sprite == binding.idle && !blade.enabled, "Skill weight/Idle regression");
                    Set(enemy, "running", true);
                    results.Add("PASS existing " + attack + " weight, attachments and recovery");
                }
            }
            Check(runtimeError == null, runtimeError);
            Finish(cleanupOnly ? "PASS: 6 released interrupt/stun/death checks, including return to approved idle."
                : "PASS: 16 basic casts, both facings and four forms; 2 enraged final casts at 30 fps; planted feet, torso/hip/knee motion, rigid arms, original UVs and hand/blade/VFX alignment; 2 windup interruptions; 6 released interrupt/stun/death checks; 2 existing SW01/SW02 weight checks. Captured frames=" + capture);
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
        File.WriteAllLines(Output + "/result.txt", results);
        File.WriteAllText(Output + "/run.flag", "done");
    }
}
#endif
