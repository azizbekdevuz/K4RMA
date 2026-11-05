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
public static class KarmaSW02AnimationChecks
{
    const string Key = "K4RMA.SW02Animation";
    const string Folder = "output/validation/sw02-animation-phase2-20261105";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static bool started, baseline, renderOnly; static KarmaGame game; static KarmaEnemy enemy;
    static string error; static int assertions; static readonly List<string> results = new List<string>();
    [Serializable] public sealed class Frame
    {
        public string key, state, pose, sprite; public int tick;
        public Vector3 center, size;
    }
    [Serializable] public sealed class Cast
    {
        public string key; public int releaseTick = -1, recoveryTick = -1, endTick = -1, hitTick = -1, shots;
        public float damage; public Vector2 origin, velocity, hitSize;
        public List<Frame> frames = new List<Frame>();
    }
    [Serializable] public sealed class Data { public List<Cast> casts = new List<Cast>(); }
    static KarmaSW02AnimationChecks() { EditorApplication.update += Pump; }
    public static void RunBaseline() => Begin(true);
    public static void RunBatch() => Begin(false);
    public static void RunRenderComparison() { Begin(false); SessionState.SetBool(Key + ".RenderOnly", true); }
    static void Begin(bool original)
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Baseline", original);
        SessionState.SetBool(Key + ".RenderOnly", false);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity"); EditorApplication.EnterPlaymode();
    }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }
    static FieldInfo Field(object target, string name)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f; }
        throw new MissingFieldException(name);
    }
    static object Get(object o, string n) => Field(o, n).GetValue(o);
    static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
    static async Task Until(Func<bool> done)
    {
        double deadline = EditorApplication.timeSinceStartup + 90;
        while (!done()) { Check(error == null, error); Check(EditorApplication.timeSinceStartup < deadline, "SW02 test timeout"); await Task.Delay(5); }
    }
    static Transform Art => (Transform)Get(enemy, "art");
    static Transform Root => (Transform)Get(enemy, "effectsRoot");
    static KarmaCharacterAnimator Animator => (KarmaCharacterAnimator)Get(enemy, "animator");
    static void Reset(int form, int side)
    {
        game.StartRun(); var order = new List<Essence> { Essence.SwordWave };
        if (form == 1 || form == 3) order.Add(Essence.RisingSlash);
        if (form == 2 || form == 3) order.Add(Essence.Guard);
        foreach (var e in order) { game.Player.Essences.Preview(e); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, form == 3 ? 3 : order.Count);
        Call(game, "LoadRoom", true); enemy = game.Enemies[0]; enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        Set(enemy, "engaged", true); Set(enemy, "timer", float.MaxValue); Set(enemy, "running", true);
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        game.Player.transform.position = new Vector3(20 + side * 15, KarmaSpace.PlayerHalfHeight + 0.02f, 0);
        Set(game.Player, "invulnerableUntil", 0f); Physics2D.SyncTransforms();
        // Capture-only A/B uses the same built runtime with the optional new
        // pictures disabled in A. The independent pre-change baseline above
        // already verified the gameplay timeline before any runtime edits.
        if (renderOnly && baseline) Set(Animator, "sw02Frames", null);
    }
    static void Force()
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", EnemyAttack.SW02); Set(enemy, "running", true);
        Set(enemy, "direction", game.Player.Position.x < enemy.Position.x ? -1f : 1f);
        Art.localScale = new Vector3(enemy.Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
        Set(enemy, "patternStarted", enemy.FightSeconds); enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Capture(string name)
    {
        var camera = game.GameCamera; var position = camera.transform.position; float size = camera.orthographicSize, aspect = camera.aspect;
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(768, 768, 24); var image = new Texture2D(768, 768, TextureFormat.RGBA32, false);
        try
        {
            camera.transform.position = new Vector3(enemy.Position.x, 5, -10); camera.orthographicSize = 9; camera.aspect = 1;
            camera.targetTexture = target;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); image.Apply();
            File.WriteAllBytes(Folder + (renderOnly ? "/renders/" : "/") + (baseline ? "before" : "after") + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; camera.transform.position = position; camera.orthographicSize = size; camera.aspect = aspect;
            RenderTexture.active = active; UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target);
        }
    }
    static async Task<Cast> Observe(int form, int side)
    {
        Reset(form, side); await Task.Delay(50); Set(enemy, "timer", float.MaxValue); Set(enemy, "running", true);
        var cast = new Cast { key = form + "/" + side }; int tick = 0; float health = game.Player.Health;
        var seen = new HashSet<KarmaBossAttack>(); var sprites = new HashSet<string>(); string lastSprite = null;
        var probe = new GameObject("SW02 actual render observer").AddComponent<KarmaSwordDashFrameProbe>();
        probe.After = () =>
        {
            if (cast.endTick >= 0) return;
            try
            {
                string state = Get(enemy, "state").ToString(); var picture = (SpriteRenderer)Get(Animator, "picture");
                var bounds = enemy.DamageBounds;
                cast.frames.Add(new Frame { key = cast.key, tick = tick, state = state, pose = Animator.CurrentPose,
                    sprite = picture.sprite.name, center = bounds.center, size = bounds.size }); sprites.Add(picture.sprite.name);
                foreach (var shot in Root.GetComponentsInChildren<KarmaBossAttack>()) if (seen.Add(shot))
                {
                    cast.shots++; cast.releaseTick = tick; cast.origin = shot.transform.position;
                    cast.velocity = (Vector2)Get(shot, "velocity"); cast.hitSize = (Vector2)Get(shot, "size");
                }
                if (game.Player.Health < health && cast.hitTick < 0) { cast.hitTick = tick; cast.damage = health - game.Player.Health; }
                if (state == "Recover" && cast.recoveryTick < 0) cast.recoveryTick = tick;
                if (!(bool)Get(enemy, "running")) { cast.endTick = tick; Set(enemy, "running", true); }
                if (form == 0 && side == 1 && (tick % 3 == 0 || picture.sprite.name != lastSprite)) Capture(tick.ToString("D3"));
                lastSprite = picture.sprite.name; tick++;
            }
            catch (Exception e) { error = e.ToString(); }
        };
        Force(); await Until(() => cast.endTick >= 0 || error != null);
        probe.After = null; UnityEngine.Object.Destroy(probe.gameObject); Check(error == null, error);
        Check(cast.shots == 1 && cast.hitTick >= 0 && cast.damage > 0, cast.key + " one actual shot and damage contact");
        if (!baseline && form == 0)
            foreach (var name in new[] { "Prepare01", "Prepare02", "Swing01", "Swing02", "Recover01", "Recover02" })
                Check(sprites.Contains(name), cast.key + " real-loop displayed " + name);
        results.Add("PASS: " + cast.key + " release=" + cast.releaseTick + ", hit=" + cast.hitTick + ", recovery=" + cast.recoveryTick + ", end=" + cast.endTick + ", damage=" + cast.damage);
        return cast;
    }
    static void Compare(Cast a, Cast b, int form)
    {
        Check(a.key == b.key && a.releaseTick == b.releaseTick && a.hitTick == b.hitTick && a.recoveryTick == b.recoveryTick
            && a.endTick == b.endTick && a.shots == b.shots && Mathf.Approximately(a.damage, b.damage), b.key + " unchanged combat ticks and damage");
        Check(Vector2.Distance(a.origin,b.origin)<0.0001f && a.velocity==b.velocity && a.hitSize==b.hitSize, b.key+" unchanged projectile geometry");
        Check(a.frames.Count == b.frames.Count, b.key + " unchanged frame count");
        for (int i = 0; i < a.frames.Count; i++)
        {
            Check(a.frames[i].state == b.frames[i].state && a.frames[i].pose == b.frames[i].pose, b.key + " unchanged logical state/pose " + i);
            Check(Vector3.Distance(a.frames[i].center,b.frames[i].center)<0.0001f && Vector3.Distance(a.frames[i].size,b.frames[i].size)<0.0001f, b.key+" unchanged DamageBounds " + i);
            if (form > 0) Check(a.frames[i].sprite==b.frames[i].sprite, b.key+" inherited sprites unchanged " + i);
        }
    }
    static async Task CleanupChecks()
    {
        Reset(0, 1); await Task.Delay(50); Force();
        await Until(() => ((SpriteRenderer)Get(Animator,"picture")).sprite.name=="Prepare01");
        var picture=(SpriteRenderer)Get(Animator,"picture"); var sprite=picture.sprite; float clock=Time.time, combat=enemy.FightSeconds;
        game.TogglePause(); await Task.Delay(180);
        Check(game.Paused && picture.sprite==sprite && Time.time==clock && enemy.FightSeconds==combat,"pause freezes actual frame and combat clock");
        game.TogglePause(); Set(enemy,"pendingParry",true); await Until(()=>enemy.Stunned);
        Check(!(bool)Get(Animator,"<SW02UsesIntermediateFrame>k__BackingField") && Get(Animator,"sw02VisualPhase").ToString()=="None","stun cancels SW02 display phase");
        Check(((SpriteRenderer)Get(Animator,"picture")).sprite.name=="Hurt","stun shows own Hurt pose");
        foreach(string phase in new[]{"Tracking","Locked","Released","Recovery"})
        {
            Reset(0,1); await Task.Delay(50); Force(); await Until(()=>Get(Animator,"sw02VisualPhase").ToString()==phase);
            Call(enemy,"InterruptPattern");
            Check(Get(Animator,"sw02VisualPhase").ToString()=="None" && !(bool)Get(Animator,"<SW02UsesIntermediateFrame>k__BackingField"),phase+" synchronous interruption cleanup");
            Check(!((SpriteRenderer)Get(Animator,"picture")).sprite.name.StartsWith("Prepare")
                && !((SpriteRenderer)Get(Animator,"picture")).sprite.name.StartsWith("Swing")
                && !((SpriteRenderer)Get(Animator,"picture")).sprite.name.StartsWith("Recover"),phase+" restores original display immediately");
        }
        Reset(0,1); await Task.Delay(50); Force(); await Until(()=>Get(Animator,"sw02VisualPhase").ToString()=="Locked");
        var dying=Animator; enemy.TakeDamage(enemy.MaxHealth*2);
        Check(Get(dying,"sw02VisualPhase").ToString()=="None","death clears frame phase");
        results.Add("PASS: pause/resume, real stun transition, all four phase interruptions and death cleanup");
    }
    static async void Run()
    {
        int exit = 0; baseline = SessionState.GetBool(Key + ".Baseline", false);
        renderOnly = SessionState.GetBool(Key + ".RenderOnly", false);
        Directory.CreateDirectory(Folder + "/" + (baseline ? "before" : "after"));
        if (renderOnly) { Directory.CreateDirectory(Folder+"/renders/before"); Directory.CreateDirectory(Folder+"/renders/after"); }
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); Application.targetFrameRate = 120; Time.captureDeltaTime = 1f/60;
            game.GameCamera.aspect = 1600f/700; var data = new Data();
            if (renderOnly)
            {
                baseline = true; var a = await Observe(0,1);
                baseline = false; var b = await Observe(0,1); Compare(a,b,0);
                data.casts.Add(a); data.casts.Add(b);
            }
            else
            {
            for (int form = 0; form < 4; form++) foreach (int side in new[] { 1, -1 }) data.casts.Add(await Observe(form, side));
            if (!baseline)
            {
                var old = JsonUtility.FromJson<Data>(File.ReadAllText(Folder + "/baseline.json"));
                for (int i = 0; i < data.casts.Count; i++) Compare(old.casts[i], data.casts[i], i/2);
                await CleanupChecks();
            }
            }
            File.WriteAllText(Folder + (renderOnly ? "/render-comparison.json" : baseline ? "/baseline.json" : "/updated.json"), JsonUtility.ToJson(data, true));
            results.Insert(0,"PASS: "+assertions+" assertions, "+(renderOnly ? "same-condition wide-camera A/B render" : "eight real SW02 casts; "+(baseline?"baseline":"before/after combat + bounds comparison")));
        }
        catch (Exception e) { results.Insert(0,"FAIL: "+e); Debug.LogException(e); exit=1; }
        File.WriteAllLines(Folder + (renderOnly ? "/render-result.txt" : baseline ? "/baseline-result.txt" : "/result.txt"),results);
        Time.captureDeltaTime=0; SessionState.SetBool(Key,false); EditorApplication.Exit(exit);
    }
}
#endif
