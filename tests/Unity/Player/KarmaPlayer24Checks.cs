#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InputKey = UnityEngine.InputSystem.Key;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaPlayer24Checks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string Key = "K4RMA.Player24";
    static bool started;
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static KarmaPlayer24Checks() { EditorApplication.update += () => {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.isPlaying) { EditorApplication.isPaused = false; Application.runInBackground = true; }
        EditorApplication.QueuePlayerLoopUpdate();
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }; }
    public static void RunBatch()
    {
        try { KarmaPrototype.EditorTools.KarmaPlayer24AnimationAssets.Rebuild(); }
        catch (Exception ex)
        {
            Directory.CreateDirectory("output/validation/player24");
            File.WriteAllText("output/validation/player24/result.txt", "FAIL asset quality gate: " + ex);
            Debug.LogException(ex); EditorApplication.Exit(1); return;
        }
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static async Task Await(Func<bool> condition, string message)
    {
        double deadline = EditorApplication.timeSinceStartup + 12;
        while (!condition()) { if (EditorApplication.timeSinceStartup > deadline) throw new Exception(message); await Task.Yield(); }
    }
    static async Task CheckControls(KarmaGame game, KarmaPlayer player, KarmaCharacterAnimator animator)
    {
        if (player.TestInvincible) player.ToggleTestInvincibility();
        async Task Ground()
        {
            player.ResetForRoom(new Vector2(12, KarmaSpace.PlayerHalfHeight + 0.05f), true);
            await Await(() => !player.Airborne, "Grounding timeout");
        }
        await Ground(); float health = player.Health;
        Check(player.ApplyShock(0.8f), "Shock accepted through existing CC API");
        await Await(() => animator.CurrentPose == "ShockLoop", "Shock loop missing");
        Check(player.ControlLocked && player.Health == health, "Shock presentation does not add damage");
        await Await(() => !player.Shocked, "Shock never releases");
        await Await(() => animator.CurrentPose == "ShockRecovery", "Shock recovery missing");
        await Ground(); player.Stun(0.8f);
        await Await(() => animator.CurrentPose == "StunLoop", "Stun loop missing");
        await Await(() => !player.Stunned, "Stun never releases");
        await Await(() => animator.CurrentPose == "StunRecovery", "Stun recovery missing");
        Check(!player.ControlLocked, "Stun ends without an animation input lock");
        await Ground();
        Check(player.ApplyAirborne(new Vector2(2, 14), 2), "Airborne accepted");
        await Await(() => animator.CurrentPose == "AirborneRise", "Launch pose missing");
        await Await(() => animator.CurrentPose == "AirborneApex", "Airborne apex missing");
        await Await(() => animator.CurrentPose == "AirborneFall", "Airborne fall phase missing");
        await Await(() => !player.KnockedAirborne && !player.Airborne, "Airborne landing never releases");
        await Ground(); float x = player.Position.x;
        Check(player.ApplyKnockback(new Vector2(-4, 0), 0.3f), "Opt-in ground knockback accepted");
        await Await(() => animator.CurrentPose == "Knockback", "Knockback pose missing");
        await Await(() => !player.KnockedBack, "Knockback never ends");
        Check(player.Position.x < x - 0.3f, "Knockback animation follows real displacement");
        await Ground();
        Check(player.ApplyKnockdown(0.6f), "Opt-in knockdown accepted on ground");
        await Await(() => animator.CurrentPose == "DownLoop", "Down hold missing");
        await Await(() => animator.CurrentPose == "GetUp", "Get up missing");
        await Await(() => !player.ControlLocked, "Get up never releases");
        Check(player.Health == health, "Presentation and standalone CC add no combat damage");
        player.ResetForRoom(KarmaStage.Spawn, true);
        Check(!player.ControlLocked, "Room reset clears all CC");
    }
    static async Task CheckGameplay(KarmaGame game, KarmaPlayer player, KarmaCharacterAnimator animator)
    {
        var keyboard=InputSystem.AddDevice<Keyboard>();
        var map=(InputActionMap)typeof(KarmaInput).GetField("map",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        string overrides=map.SaveBindingOverridesAsJson();map.RemoveAllBindingOverrides();
        async Task Keys(params InputKey[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            int frame=Time.frameCount;await Await(()=>Time.frameCount>frame+1,"Input update timeout");
        }
        async Task Ground()
        {
            await Keys();player.ResetForRoom(new Vector2(12,KarmaSpace.PlayerHalfHeight+0.05f),true);
            await Await(()=>!player.Airborne,"Gameplay grounding timeout");
        }
        async Task Advance(float seconds) { float end=Time.time+seconds;await Await(()=>Time.time>=end,"Gameplay clock timeout"); }
        try
        {
            await Ground();float start=player.Position.x;
            await Keys(InputKey.D);await Await(()=>animator.CurrentPose=="Run","Run input missing");await Advance(0.2f);
            var body=player.GetComponent<Rigidbody2D>();
            Check(Mathf.Abs(body.linearVelocity.x-game.Config.moveSpeed)<0.02f&&player.Position.x>start,"Run keeps configured velocity");
            await Keys(InputKey.A);await Await(()=>animator.CurrentPose=="Turn","Direction reversal uses authored turn");
            await Await(()=>animator.CurrentPose=="Run"&&player.Facing==-1,"Turn returns to leftward run without input lock");
            await Keys();await Await(()=>animator.CurrentPose=="Idle","Run to idle");
            typeof(KarmaCharacterAnimator).GetMethod("BeginPlayerTurn",F).Invoke(animator,null);
            var art=(Transform)typeof(KarmaPlayer).GetField("art",F).GetValue(player);
            art.localScale=new Vector3(-player.Facing,1,1);player.BeginSwordWave();
            Check(art.localScale.x==player.Facing,"Turn interruption faces the action on contact frame");
            await Ground();
            await Keys(InputKey.S);await Await(()=>animator.CurrentPose=="Crouch","Crouch input missing");await Keys();
            await Keys(InputKey.Space);await Await(()=>player.Airborne,"Jump input missing");await Keys();
            await Await(()=>animator.CurrentPose=="Jump","Rising jump motion missing");
            await Await(()=>animator.CurrentPose=="Fall","Falling jump motion missing");
            await Await(()=>animator.CurrentPose=="Land","Landing motion missing");
            await Await(()=>animator.CurrentPose=="Idle","Landing to idle");
            await Keys(InputKey.J);await Await(()=>animator.CurrentPose=="Attack","Basic attack input missing");
            Check((float)typeof(KarmaPlayer).GetField("nextSword",F).GetValue(player)-Time.time<=game.Config.swordCooldown+0.001f,"Attack cooldown remains independent of clip length");
            await Keys();await Await(()=>animator.CurrentPose=="Recovery","Attack recovery missing");
            await Keys(InputKey.D);await Await(()=>animator.CurrentPose=="RunRecovery"||animator.CurrentPose=="Run","Attack recovery accepts movement immediately");
            await Ground();await Keys(InputKey.L);await Await(()=>animator.CurrentPose=="SwordWave","Sword wave input missing");
            Check(player.Essences.Cooldown(Essence.SwordWave)>0,"Sword wave uses existing technique cooldown");await Keys();
            await Ground();await Keys(InputKey.LeftShift);await Await(()=>animator.CurrentPose=="Rising","Rising input missing");
            Check(body.linearVelocity.y>0,"Rising animation follows actual launch");await Keys();await Await(()=>!player.Airborne,"Rising landing timeout");
            await Ground();await Keys(InputKey.K);await Await(()=>animator.CurrentPose=="Guard","Guard input missing");
            float health=player.Health;player.TakeDamage(1);Check(player.Health==health&&player.Shielded,"Guard still blocks damage");
            await Ground();player.Essences.Preview(Essence.Guard);player.Essences.Confirm(out _);
            await Keys(InputKey.K);await Await(()=>animator.CurrentPose=="Parry","Counter input missing");
            int parries=player.ParryCount;player.TakeDamage(1);
            Check(player.ParryCount==parries+1&&animator.CurrentPose=="Riposte","Confirmed counter contact triggers riposte");await Keys();
            player.Essences.Preview(Essence.SwordWave);player.Essences.Confirm(out _);
            foreach(var direction in new[]{Vector2.right,Vector2.up,new Vector2(1,1).normalized,Vector2.down,Vector2.left})
            {
                await Keys();player.ResetForRoom(new Vector2(12,12),true);
                Vector2 from=player.Position;
                typeof(KarmaPlayer).GetMethod("BeginDirectionalSlash",F).Invoke(player,new object[]{direction,game.Config.Techniques.pierceDistance,Essence.SwordWave});
                string pose=direction.y>0.75f?"PierceUp":direction.y<-.25f?"PierceDown":direction.y>.25f?"PierceDiagonal":"Pierce";
                Check(animator.CurrentPose==pose,"Directional dash selects "+pose);
                Check(Vector2.Distance(player.Position,from+direction*game.Config.Techniques.pierceDistance)<0.06f,"Dash keeps configured atomic travel");
            }
            player.Essences.Preview(Essence.RisingSlash);player.Essences.Confirm(out _);
            foreach(var direction in new[]{Vector2.right,Vector2.up,new Vector2(1,1).normalized,Vector2.down,Vector2.left})
            {
                player.ResetForRoom(new Vector2(12,12),true);
                typeof(KarmaPlayer).GetMethod("BeginAirSlash",F).Invoke(player,new object[]{direction});
                string pose=direction.y<-.25f?"AirHover":direction.y>.75f?"AirUp":direction.y>.25f?"AirDiagonal":"AirHorizontal";
                Check(animator.CurrentPose==pose,"Directional airborne skill selects "+pose);
                if(direction.y<-.25f)await Await(()=>animator.CurrentPose=="AirDown","Downward hover never joins descent");
                await Await(()=>!(bool)typeof(KarmaPlayer).GetField("airSlashRunning",F).GetValue(player),"Air skill never ends");
                Check(!player.ControlLocked,"Air skill animation adds no CC lock");
            }
            foreach(Essence e in Enum.GetValues(typeof(Essence)))
            {
                player.ResetForRoom(KarmaStage.Spawn,true);animator.Empower(e,0.7f);
                await Await(()=>animator.CurrentPose==(e==Essence.SwordWave?"PersonalizeWave":e==Essence.RisingSlash?"PersonalizeRising":"PersonalizeGuard"),"Unique ceremony clip missing");
                await Advance(0.75f);
            }
            await Ground();
            if(player.TestInvincible)player.ToggleTestInvincibility();player.TakeDamage(1);
            await Await(()=>animator.CurrentPose=="Hurt","Ordinary hit motion missing");
            Check(player.Health<game.Config.playerHealth,"Ordinary hit damage unchanged");
            await Ground();player.TakeDamage(game.Config.playerHealth*2);
            await Await(()=>animator.CurrentPose=="Death","Death motion missing");
            game.RestartSession();await Task.Yield();
            Check(game.Phase==RunPhase.Tutorial&&game.Tutorial!=null&&game.Player.Health>0,"Death restart begins tutorial with restored health");
        }
        finally { map.LoadBindingOverridesFromJson(overrides);InputSystem.RemoveDevice(keyboard); }
    }
    static void CheckPlaybackCost(KarmaCharacterAnimator animator, KarmaSpriteLibrary native)
    {
        var sequences=(Dictionary<string,Sprite[]>)typeof(KarmaCharacterAnimator).GetField("playerSequences",F).GetValue(animator);
        var rate=typeof(KarmaCharacterAnimator).GetField("playerSampleRate",F);
        var sample=typeof(KarmaCharacterAnimator).GetMethod("SamplePlayerClip",F);
        var legacy=Resources.Load<KarmaSpriteLibrary>("Player/Animation/Frames");
        var saved=new Dictionary<string,Sprite[]>(sequences);
        double Measure(KarmaSpriteLibrary library,float fps)
        {
            sequences.Clear();foreach(var entry in library.sheets)sequences[entry.id]=entry.sprites;
            rate.SetValue(animator,fps);var args=new object[]{"Idle",false};
            for(int i=0;i<1000;i++)sample.Invoke(animator,args);
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<20000;i++)sample.Invoke(animator,args);
            watch.Stop();return watch.Elapsed.TotalMilliseconds;
        }
        double baseline=Measure(legacy,12), authored=Measure(native,24);
        sequences.Clear();foreach(var entry in saved)sequences[entry.Key]=entry.Value;rate.SetValue(animator,24f);
        long Bytes(KarmaSpriteLibrary library)=>library.sheets.SelectMany(s=>s.sprites).Select(s=>s.texture).Distinct()
            .Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
        Debug.Log("PLAYER24 COST: 20000 samples legacy="+baseline.ToString("F2")+"ms native="+authored.ToString("F2")
            +"ms; texture memory legacy="+(Bytes(legacy)/1048576f).ToString("F1")+"MiB native="+(Bytes(native)/1048576f).ToString("F1")+"MiB");
        Check(authored<baseline*3+5,"Native playback cost stays comparable to original sampling");
        int arcsBefore=animator.GetComponentsInParent<KarmaPlayer>()[0].GetComponentsInChildren<LineRenderer>(true).Length;
        Check(arcsBefore<16,"Electric arcs stay pooled with bounded renderer count");
    }
    static void CaptureNativeSheets(KarmaSpriteLibrary library, SpriteRenderer live)
    {
        const int cell = 224;
        var probe = new GameObject("Native sprite review"); probe.layer = 31;
        var sprite = probe.AddComponent<SpriteRenderer>(); sprite.sharedMaterial = live.sharedMaterial;
        probe.transform.localScale = live.transform.lossyScale;
        var cameraObject = new GameObject("Native sprite review camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
        camera.orthographicSize = 4.8f; camera.aspect = 1; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.22f,0.24f,0.28f,1);
        camera.transform.position = new Vector3(0, 2.3f, -10);
        var target = new RenderTexture(cell,cell,24); camera.targetTexture = target;
        var frame = new Texture2D(cell,cell,TextureFormat.RGB24,false);
        var active = RenderTexture.active;
        Directory.CreateDirectory("output/validation/player24/poses");
        try
        {
            foreach(var sheet in library.sheets)
            {
                if(Array.IndexOf(KarmaPrototype.EditorTools.KarmaPlayer24AnimationAssets.Names,sheet.id)<0)continue;
                int rows=(sheet.sprites.Length+3)/4;
                var plate=new Texture2D(cell*4,cell*rows,TextureFormat.RGB24,false);
                for(int i=0;i<sheet.sprites.Length;i++)
                {
                    sprite.sprite=sheet.sprites[i]; camera.Render(); RenderTexture.active=target;
                    frame.ReadPixels(new Rect(0,0,cell,cell),0,0);frame.Apply();
                    plate.SetPixels(i%4*cell,(rows-1-i/4)*cell,cell,cell,frame.GetPixels());
                }
                plate.Apply(); File.WriteAllBytes("output/validation/player24/poses/"+sheet.id+".png",plate.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(plate);
            }
        }
        finally
        {
            RenderTexture.active=active;camera.targetTexture=null;target.Release();
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(frame);
            UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(probe);
        }
    }
    static async void Run()
    {
        string result;
        try
        {
            Time.captureDeltaTime = 1f / 24; Application.runInBackground = true;
            var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); game.StartRun();
            var player = game.Player; player.ToggleTestInvincibility(); var animator = player.GetComponentInChildren<KarmaCharacterAnimator>();
            var picture = (SpriteRenderer)typeof(KarmaCharacterAnimator).GetField("picture", F).GetValue(animator);
            var sample = typeof(KarmaCharacterAnimator).GetMethod("SamplePlayerClip", F);
            var clock = typeof(KarmaCharacterAnimator).GetField("playerClipAt", F);
            var startupCache=(Dictionary<string,Sprite[]>)typeof(KarmaCharacterAnimator).GetField("playerSequences",F).GetValue(animator);
            Check(startupCache.Count<=5,"Normal startup loads only requested actions, not all CC/ending sheets");
            Debug.Log("PLAYER24 STARTUP: cached actions="+startupCache.Count);
            var library = Resources.Load<KarmaSpriteLibrary>("Player/Animation24/Frames");
            Check(library.sheets.Length == KarmaPrototype.EditorTools.KarmaPlayer24AnimationAssets.Names.Length + 3, "All native actions and compatibility aliases exist");
            foreach (var enemy in game.Enemies) enemy.gameObject.SetActive(false);
            player.enabled = false;
            var scale = picture.transform.localScale;
            foreach (var sheet in library.sheets)
            {
                var clip = Resources.Load<AnimationClip>("Player/Animation24/" + sheet.id);
                Check(clip.frameRate == 24, sheet.id + " is authored at 24 FPS");
                var setting = AnimationUtility.GetAnimationClipSettings(clip);
                Check(Mathf.Abs(setting.stopTime - sheet.sprites.Length / 24f) < 0.001f, sheet.id + " duration preserved");
                if (sheet.id == "Riposte")
                {
                    // Successful parry now owns an eight-frame player/flash pair,
                    // while this legacy native sheet remains available to authoring tools.
                    KarmaPrototype.EditorTools.KarmaPlayerAnimationAssets.VerifyParrySuccess();
                    Check(true, "Successful parry preserves 16 facing/frame samples, expiry and interruption");
                    continue;
                }
                sample.Invoke(animator, new object[] { sheet.id, true });
                for (int i = 0; i < sheet.sprites.Length; i++)
                {
                    clock.SetValue(animator, Time.time - (i + 0.1f) / 24);
                    sample.Invoke(animator, new object[] { sheet.id, false });
                    Check(AssetDatabase.GetAssetPath(sheet.sprites[i]).Contains("/Native24/"), "Every frame belongs to the freshly authored native library");
                    Check(picture.sprite == sheet.sprites[i] && picture.transform.localScale == scale, sheet.id + " frame " + i);
                    await Task.Yield();
                }
            }
            Directory.CreateDirectory("output/validation/player24");
            CaptureNativeSheets(library, picture);
            sample.Invoke(animator, new object[] { "Idle", true });
            clock.SetValue(animator, Time.time - 1.1f / 24); sample.Invoke(animator, new object[] { "Idle", false });
            ScreenCapture.CaptureScreenshot("output/validation/player24/idle.png"); await Task.Delay(150);
            player.enabled = true;
            foreach (int facing in new[] { -1, 1 })
            {
                typeof(KarmaPlayer).GetProperty("Facing").SetValue(player, facing);
                player.BeginGuard(1);
                var aura = (SpriteRenderer)typeof(KarmaPlayer).GetField("aura", F).GetValue(player);
                Check(aura.flipX == (facing < 0), "Guard follows facing");
                player.BeginRisingSlash();
                Transform root = null; foreach (Transform child in game.World) if (child.name == "PlayerVFX/Rising") root = child;
                Check(root != null && root.localScale.x == facing && root.localScale.y > 0, "Rising mirrors horizontally without inverting upward motion");
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
            await CheckControls(game, player, animator);
            CheckPlaybackCost(animator, library);
            await CheckGameplay(game, player, animator);
            game.ReturnToTitle(); game.StartOpening(); await Task.Delay(250);
            var tutorialPictures = game.GetComponentsInChildren<SpriteRenderer>();
            Check(Array.Exists(tutorialPictures, p => p.name == "Master" && p.sprite.texture.name == "Master"), "Master uses original static artwork");
            Check(Array.Exists(tutorialPictures, p => p.name == "TrainingDummy" && p.sprite.texture.name == "TrainingDummy"), "Dummy uses original static artwork");
            var master = Array.Find(tutorialPictures, p => p.name == "Master");
            var support = new Vector3(0, (17f - master.sprite.pivot.y) / master.sprite.pixelsPerUnit, 0);
            Check(Mathf.Abs(master.transform.TransformPoint(support).y) < 0.005f, "Master sandal support touches floor y=0");
            await Task.Delay(700);
            ScreenCapture.CaptureScreenshot("output/validation/player24/tutorial.png"); await Task.Delay(150);
            game.ReturnToTitle();
            result = "PASS: " + checks + " native player 24FPS, CC, facing and original tutorial artwork checks";
        }
        catch (Exception ex) { result = "FAIL: " + ex; Debug.LogException(ex); }
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false);
        Directory.CreateDirectory("output/validation/player24"); File.WriteAllText("output/validation/player24/result.txt", result);
        Debug.Log(result); EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 1);
    }
}
#endif
