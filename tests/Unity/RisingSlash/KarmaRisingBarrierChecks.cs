#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaRisingBarrierChecks
{
    const string Key = "K4RMA.RisingBarrierChecks";
    const string Output = "output/validation/rising-barrier";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started;
    static int checks;
    static string runtimeError;
    static int existingWarningCount;
    static RenderTexture target;
    static Texture2D image;
    static KarmaRisingBarrierChecks() { EditorApplication.update += Pump; }
    static object Get(object obj, string name) => obj.GetType().GetField(name, F).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, F).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static Transform Root => (Transform)Get(enemy, "effectsRoot");
    static KarmaRisingBarrier Barrier => enemy.GetComponentInChildren<KarmaRisingBarrier>();
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        EditorApplication.QueuePlayerLoopUpdate();
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }

    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Render()
    {
        var camera = game.GameCamera; var previous = camera.targetTexture; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; }
    }
    static void Capture(string name)
    { Render(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
    static async Task Until(Func<bool> done)
    {
        double end = EditorApplication.timeSinceStartup + 90;
        while (!done())
        { if (EditorApplication.timeSinceStartup > end) throw new Exception("Barrier timeout"); await Task.Delay(10); }
    }
    static void Reset(bool empowered)
    {
        game.StartRun();
        foreach (var essence in empowered ? new[] { Essence.RisingSlash, Essence.Guard, Essence.SwordWave }
            : new[] { Essence.RisingSlash })
        { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0];
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue);
        var body = game.Player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        body.position = game.Player.transform.position = new Vector2(32, KarmaSpace.Units(4));
        Set(game.Player, "invulnerableUntil", Time.time + 10000);
    }
    static void Force(EnemyAttack pattern)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", pattern); Set(enemy, "direction", 1f);
        Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static async Task CheckReflectedFragment(bool protectedBoss)
    {
        Reset(true);
        Set(Get(enemy, "behavior"), "PracticeHold", true);
        Set(enemy, "fullShield", protectedBoss);
        enemy.transform.position = new Vector2(25, KarmaSpace.GuardianGroundY);
        var patternType = typeof(KarmaEnemy).GetNestedType("UP04Pattern", BindingFlags.NonPublic);
        var pattern = Activator.CreateInstance(patternType, F, null, new object[] { enemy }, null);
        Call(pattern, "Burst", enemy.Position);
        var fragments = Root.GetComponentsInChildren<KarmaRisingFragmentAttack>();
        Check(fragments.Length == 14, "UP04 keeps fourteen damaging fragments");
        var shot = fragments[0];
        for (int i = 1; i < fragments.Length; i++) fragments[i].gameObject.SetActive(false);
        var point = new Vector2(15, KarmaSpace.Units(3));
        game.Player.GetComponent<Rigidbody2D>().position = point; game.Player.transform.position = point;
        shot.transform.position = point;
        var contact = (KarmaAttackContact)Get(shot, "fragmentContact");
        Check(contact.ParryWithoutRiposte, "Fragment disables instant counter riposte");
        float before = enemy.Health; int parries = game.Player.ParryCount;
        game.Player.BeginCounter();
        enemy.TryAttackHit(point, point, KarmaSpace.Units(new Vector2(1.05f, 0.675f)), 1, contact, 0);
        Check(game.Player.ParryCount == parries + 1 && shot.Reflected, "Actual parry reflects the contacted shard");
        Check(!enemy.Stunned && !(bool)Get(enemy, "pendingParry") && enemy.Health == before,
            "Parry does not instantly stun or damage boss");
        await Task.Yield();
        Check(!enemy.Stunned, "Boss remains unstunned while shard travels back");
        await Until(() => shot == null || !shot.gameObject.activeSelf);
        if (protectedBoss)
        {
            Check(!enemy.Stunned && !(bool)Get(enemy, "pendingParry") && enemy.Health == before,
                "UP05 immunity blocks returned shard damage and stun");
        }
        else
        {
            await Until(() => enemy.Stunned);
            Check(enemy.Health < before, "Returned shard damages boss only on impact");
            Check(enemy.Stunned, "Returned shard stuns boss on impact");
        }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (message == "Not allowed to override geometry on sprite ''" && stack.Contains("KarmaAttackWarning"))
        { existingWarningCount++; return; }
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            && stack.Contains("Assets/K4RMA/")) runtimeError = message;
    }
    static async void Run()
    {
        string verdict;
        try
        {
            Directory.CreateDirectory(Output);
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 24;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Application.logMessageReceived += Log;
            target = new RenderTexture(1280, 720, 24);
            image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            Reset(false); Force(EnemyAttack.UP02);
            bool unwanted = false;
            await Until(() => { unwanted |= Barrier != null; return !(bool)Get(enemy, "running"); });
            Check(!unwanted, "Uninherited UP02-UP01 never creates a barrier");
            Reset(true);
            for (int i = 0; i < 3; i++)
            {
                var sprite = KarmaRisingArt.Get("Fragment" + i);
                Check(sprite != null && sprite.texture.width <= 512 && sprite.texture.height <= 512,
                    "Fragment imports at display-appropriate resolution");
            }
            Force(EnemyAttack.UP02);
            var barrier = Barrier;
            Check(barrier != null && barrier.Opacity == 0 && !barrier.Formed, "UP02 starts with an invisible barrier");
            float formingHealth = enemy.Health; enemy.TakeDamage(10);
            Check(Mathf.Approximately(enemy.Health, formingHealth - 10), "Forming barrier does not reduce damage");
            bool checkedReduction = false;
            float peak = 0, previousY = enemy.Position.y, fragmentAt = 0;
            bool grew = false, falling = false, capturedFall = false, fragments = false, capturedFlight = false;
            await Until(() =>
            {
                if (Barrier != null)
                {
                    Check(Barrier == barrier, "UP02-UP01 retains the same sphere");
                    if (Barrier.Formed && !checkedReduction)
                    {
                        float protectedHealth = enemy.Health; enemy.TakeDamage(10);
                        Check(Mathf.Approximately(enemy.Health, protectedHealth - 7), "Completed sphere reduces damage by 30 percent");
                        checkedReduction = true;
                    }
                    float opacity = Barrier.Opacity;
                    grew |= opacity > 0 && opacity < 0.35f; peak = Mathf.Max(peak, opacity);
                    Check(enemy.GetComponentsInChildren<KarmaRisingBarrier>().Length == 1, "Only one barrier exists");
                    Check(Barrier.transform.parent == enemy.transform, "Sphere follows boss in the same transform hierarchy");
                    if (Get(enemy, "state").ToString() == "Attack" && enemy.Position.y < previousY - 0.01f)
                        falling = true;
                    previousY = enemy.Position.y;
                    var renderer = Barrier.GetComponent<SpriteRenderer>();
                    Check(Mathf.Abs(renderer.bounds.size.x - KarmaSpace.Units(5.3f)) < 0.01f,
                        "Sphere stays at the requested fixed diameter");
                    if (falling && !capturedFall && opacity >= 0.35f) { Capture("up02-up01-sphere"); capturedFall = true; }
                }
                var shots = Root.GetComponentsInChildren<KarmaBossAttack>();
                foreach (var shot in shots) if (shot.name == "방패 파편")
                {
                    if (!fragments) { fragmentAt = Time.time; Capture("up01-golden-fragments"); }
                    if (!capturedFlight && fragments && Time.time - fragmentAt >= 0.35f)
                    { Capture("fragments-translucent-flight"); capturedFlight = true; }
                    fragments = true;
                    Check(shot.Visual.sprite == KarmaRisingArt.Get("Fragment0")
                        || shot.Visual.sprite == KarmaRisingArt.Get("Fragment1")
                        || shot.Visual.sprite == KarmaRisingArt.Get("Fragment2"), "An irregular shard variant is used");
                    Check(shot.Visual.GetComponent<KarmaRisingFragmentVisual>() != null, "Shard artwork tumbles independently");
                    var material = shot.Visual.sharedMaterial;
                    Check(material != null && material.shader.name == "K4RMA/RisingTranslucentFragment"
                        && material.shader.isSupported, "Every shard uses the translucent lattice material");
                    Check(material.GetFloat("_MembraneAlpha") <= 0.2f
                        && material.GetFloat("_GridAlpha") < 1
                        && material.GetFloat("_GridAlpha") > material.GetFloat("_MembraneAlpha"),
                        "Membrane is translucent while lattice remains readable");
                    var local = shot.Visual.transform.localScale;
                    var bounds = shot.Visual.sprite.bounds.size;
                    Check(Mathf.Abs(local.x * bounds.x - KarmaSpace.Units(1.05f)) < 0.01f
                        && Mathf.Abs(local.y * bounds.y - KarmaSpace.Units(0.675f)) < 0.01f,
                        "Fragments match the original visual footprint");
                    Check(shot.Visual.color.r == 1 && shot.Visual.color.g == 1 && shot.Visual.color.b == 1,
                        "Shard keeps its golden color");
                }
                return !(bool)Get(enemy, "running");
            });
            Check(grew && peak >= 0.35f && checkedReduction, "Barrier gradually reaches full opacity and damage reduction");
            float recoveredHealth = enemy.Health; enemy.TakeDamage(10);
            Check(Mathf.Approximately(enemy.Health, recoveredHealth - 10), "Landing removes damage reduction");
            Check(falling && fragments && Barrier == null, "Landing clears barrier and emits new fragments");
            Check(capturedFlight, "Translucent fragments captured in flight after impact effects");
            Call(enemy, "InterruptPattern");
            Check(Root.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "Interruption removes all fragments");
            Reset(true); Force(EnemyAttack.UP05);
            Check(Barrier != null && enemy.Invulnerable, "UP05 begins sphere and invulnerability");
            Check(!((SpriteRenderer)Get(enemy, "shieldHalo")).gameObject.activeSelf,
                "Old white halo remains disabled during UP05");
            float health = enemy.Health; enemy.TakeDamage(1000000);
            Check(enemy.Health == health, "UP05 blocks lethal incoming damage");
            bool sawRecovery = false, capturedUltimate = false;
            await Until(() =>
            {
                string state = Get(enemy, "state").ToString();
                if (state == "Recover")
                { sawRecovery = true; Check(Barrier == null && !enemy.Invulnerable, "Recovery clears sphere and immunity"); }
                else if ((bool)Get(enemy, "running"))
                {
                    Check(Barrier != null && enemy.Invulnerable, "UP05 maintains sphere throughout motion");
                    if (!capturedUltimate && Barrier.Opacity > 0.35f) { Capture("up05-sphere"); capturedUltimate = true; }
                }
                return !(bool)Get(enemy, "running");
            });
            Check(sawRecovery && capturedUltimate, "UP05 reaches visible sphere and recovery");
            enemy.TakeDamage(1); Check(enemy.Health < health, "Damage resumes after UP05");
            Reset(true); Force(EnemyAttack.UP02); enemy.CancelCombat();
            Check(Barrier == null, "Cancellation removes forming sphere");
            Reset(true); Force(EnemyAttack.UP05); enemy.CancelCombat();
            Check(Barrier == null && !enemy.Invulnerable, "Cancellation clears UP05 sphere and immunity");
            await CheckReflectedFragment(false);
            await CheckReflectedFragment(true);
            Check(runtimeError == null, "No gameplay exceptions: " + runtimeError);
            verdict = "PASS: " + checks + " rising barrier, reflected-shard parry, cleanup and invulnerability assertions; existing attack-warning geometry messages: " + existingWarningCount;
        }
        catch (Exception ex) { verdict = "FAIL: " + ex; Debug.LogException(ex); }
        finally
        {
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (image != null) UnityEngine.Object.Destroy(image);
            Time.captureDeltaTime = 0;
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/result.txt", verdict);
        SessionState.SetBool(Key, false); Debug.Log(verdict); EditorApplication.Exit(verdict.StartsWith("PASS:") ? 0 : 1);
    }
}
#endif
