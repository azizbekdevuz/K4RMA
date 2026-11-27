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
public static class KarmaSW03ReverseGripChecks
{
    const string Requests = "output/sw03-grip-hold-review";
    static string Output => plungeOnly ? "Temp/sw03-waverise-plunge" : shieldOnly ? "Temp/sw03-arm-review" : Requests;
    const string Key = "K4RMA.SW03Reverse.5";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static bool preparing, started, riseOnly, posesOnly, holdOnly, shieldOnly, plungeOnly;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static string runtimeError;
    static int capture;
    static readonly List<string> results = new();
    static KarmaSW03ReverseGripChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!File.Exists(Requests + "/run.flag")) return;
        string request = File.ReadAllText(Requests + "/run.flag").Trim();
        shieldOnly = request == "shield-arm";
        plungeOnly = request == "wave-rise-plunge";
        if (!plungeOnly && !shieldOnly && request != "run" && request != "import" && request != "import-rise" && request != "poses" && request != "hold") return;
        riseOnly = request == "import-rise" || plungeOnly;
        holdOnly = request == "hold";
        posesOnly = request == "poses" || holdOnly || shieldOnly;
        Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
        if (!plungeOnly && !shieldOnly && SessionState.GetString(Key, "") == "done") return;
        if (!EditorApplication.isPlaying)
        {
            if (preparing || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            preparing = true;
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)
            { Finish("FAIL: open scene has unsaved changes"); return; }
            try
            {
                Directory.CreateDirectory(Output);
                if (plungeOnly) KarmaSwordBasicArtChecks.BindWaveRisePlunge();
                if (request.StartsWith("import")) KarmaSwordBasicArtChecks.ImportGroundReverse();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
                if (shieldOnly) SessionState.SetBool(Key + ".shield", true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Finish("FAIL: import " + e); }
        }
        else if (!started && (!shieldOnly || SessionState.GetBool(Key + ".shield", false)) && UnityEngine.Object.FindAnyObjectByType<KarmaGame>() != null) { started = true; Run(); }
    }
    static object Get(object target, string name)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F); if (field != null) return field.GetValue(target); }
        throw new MissingFieldException(name);
    }
    static void Set(object target, string name, object value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F); if (field != null) { field.SetValue(target, value); return; } }
        throw new MissingFieldException(name);
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    // Painted off-hand wrist cuff in GroundPlunge source pixels.
    static readonly Vector2[] Wrist = {
        new(338,209), new(342,193), new(351,157), new(173,181), new(144,201),
        new(174,188), new(340,191), new(336,183), new(346,169)
    };
    static void CheckShield(SpriteRenderer body, SpriteRenderer shield, SpriteRenderer strap, KarmaSwordBasicArt art, int frame)
    {
        bool armBack = frame == 3 || frame == 4 || frame == 5;
        Check((shield.sprite == art.shieldInside) == armBack, "Wrong shield face on frame " + frame);
        Check((shield.sortingOrder < body.sortingOrder) == armBack, "Wrong forearm occlusion on frame " + frame);
        Check(strap.enabled == armBack, "Wrong strap visibility on frame " + frame);
        if (armBack)
        {
            Check(art.shieldInside.texture != art.idle.texture, "Reused the decorated shield face for its interior");
            Check(strap.sprite == art.shieldStrap && strap.sortingOrder > body.sortingOrder, "Strap must cross the visible arm");
            Check(strap.transform.parent == shield.transform && strap.transform.localPosition == Vector3.zero, "Strap slipped off its anchors");
        }
        var sprite = body.sprite;
        Vector2 pixel = Wrist[frame];
        Vector2 cuff = (new Vector2(pixel.x, sprite.rect.height - pixel.y) - sprite.pivot) / sprite.pixelsPerUnit;
        Vector2 local = shield.transform.InverseTransformPoint(body.transform.TransformPoint(cuff));
        var vertices = shield.sprite.vertices;
        var triangles = shield.sprite.triangles;
        if (!armBack) Check(vertices.Length > 4, "Shield still contains the rectangular idle artwork");
        float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        for (int i=0; i<triangles.Length; i+=3)
        {
            Vector2 a=vertices[triangles[i]], b=vertices[triangles[i+1]], c=vertices[triangles[i+2]];
            float ab=Cross(b-a,local-a), bc=Cross(c-b,local-b), ca=Cross(a-c,local-c);
            if (Mathf.Min(ab,bc,ca)>=0 || Mathf.Max(ab,bc,ca)<=0) return;
        }
        throw new Exception("Shield missed painted wrist on SW03 frame " + frame);
    }
    static object Ground => Get(Get(enemy, "behavior"), "ground");
    static bool GroundRunning => (bool)Ground.GetType().GetProperty("Running", F).GetValue(Ground);
    static Task Frame()
    { var result = new TaskCompletionSource<bool>(); game.StartCoroutine(FrameEnd(result)); return result.Task; }
    static IEnumerator FrameEnd(TaskCompletionSource<bool> result) { yield return new WaitForEndOfFrame(); result.SetResult(true); }
    static async Task Seconds(float seconds) { float until = Time.time + seconds; while (Time.time < until) await Frame(); }
    static void Reset(int form, int side)
    {
        game.StartRun(); var order = new List<Essence> { Essence.SwordWave };
        if (form == 1 || form == 3) order.Add(Essence.RisingSlash);
        if (form == 2 || form == 3) order.Add(Essence.Guard);
        foreach (var kind in order) { game.Player.Essences.Preview(kind); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, order.Count); Call(game, "LoadRoom", true);
        enemy = game.Enemies[0]; enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        var art = enemy.transform.Find("Artwork"); art.localScale = new Vector3(side * Mathf.Abs(art.localScale.x), art.localScale.y, art.localScale.z);
        var player = game.Player.GetComponent<Rigidbody2D>(); player.bodyType = RigidbodyType2D.Kinematic;
        player.position = new Vector2(20 + side * (plungeOnly || shieldOnly ? 12f : 3.2f), KarmaSpace.PlayerHalfHeight); game.Player.transform.position = player.position;
        Set(game.Player, "invulnerableUntil", Time.time + 100); Set(enemy, "engaged", true);
        Set(enemy, "timer", float.MaxValue); Set(enemy, "running", true); Physics2D.SyncTransforms();
    }
    static void Force(EnemyAttack attack = EnemyAttack.SW03)
    {
        Call(enemy, "InterruptPattern"); Set(enemy, "selected", attack);
        Set(enemy, "direction", game.Player.Position.x < enemy.Position.x ? -1f : 1f);
        Call(Get(enemy, "behavior"), "OnSelected"); Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void Capture()
    {
        var camera = game.GameCamera; var rt = RenderTexture.GetTemporary(1280,720,24);
        var target = camera.targetTexture; var active = RenderTexture.active; var rect = camera.rect; var aspect = camera.aspect;
        var position = camera.transform.position; float size = camera.orthographicSize;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = rt; camera.rect = new Rect(0,0,1,1); camera.aspect = 1280f/720;
            camera.orthographicSize = 8; camera.transform.position = new Vector3(enemy.Position.x+4,7,position.z);
            if (shieldOnly)
            {
                var body = enemy.transform.Find("Artwork/Guardian sprite");
                Vector3 centre = body.TransformPoint(Vector3.up * 1.4f);
                camera.orthographicSize = 2.05f * Mathf.Abs(body.lossyScale.y);
                camera.transform.position = new Vector3(centre.x, centre.y, position.z);
            }
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest {destination=rt});
            RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            File.WriteAllBytes(Output+(posesOnly?"/pose-frame-":riseOnly?"/rise-frame-":"/frame-")+(capture++).ToString("D5")+".jpg",image.EncodeToJPG(90));
        }
        finally
        { camera.targetTexture=target;camera.rect=rect;camera.aspect=aspect;camera.transform.position=position;camera.orthographicSize=size;
            RenderTexture.active=active;UnityEngine.Object.Destroy(image);RenderTexture.ReleaseTemporary(rt); }
    }
    static async void Run()
    {
        try
        {
            Application.logMessageReceived += Error; game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            string[] forms={"Wave","WaveRise","WaveWard","WaveFinal"};
            for(int variant=shieldOnly?3:riseOnly?1:0;variant<(holdOnly?1:riseOnly?2:5);variant++)
            foreach(int side in !shieldOnly && (variant==4||holdOnly)?new[]{1}:new[]{1,-1})
            {
                Time.captureDeltaTime=variant==4?1f/30:1f/60; int form=Mathf.Min(variant,3);
                Reset(form,side); if(variant==4) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*.1f);
                await Seconds(.12f);
                var actor=(KarmaCharacterAnimator)Get(enemy,"animator");
                var picture=enemy.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
                var blade=picture.transform.Find("Ground reverse rigid blade").GetComponent<SpriteRenderer>();
                var art=Resources.Load<KarmaSwordBasicArt>("SwordWave/Ground/"+forms[form]);
                var basic=Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/"+forms[form]);
                float standingScale=Mathf.Abs(((Vector3)Get(actor,"pictureScale")).x * picture.transform.parent.lossyScale.x);
                float longBlade=basic.bladeLength * standingScale;
                var shield = picture.transform.Find("SW03 forearm shield")?.GetComponent<SpriteRenderer>();
                var strap = shield != null ? shield.transform.Find("ForearmStrap")?.GetComponent<SpriteRenderer>() : null;
                Check((shield != null) == (form == 3), "Wrong SW03 shield owner");
                Check((strap != null) == (form == 3), "Wrong SW03 strap owner");
                if (form == 3 || plungeOnly)
                {
                    var plunge = Resources.Load<KarmaSwordBasicArt>("SwordWave/Ground/WaveRise");
                    Check(art.poses.SequenceEqual(plunge.poses) && art.poses.Distinct().Count() == 9
                        && art.poses.All(s => AssetDatabase.GetAssetPath(s) == "Assets/Arts/SwordWave/WaveRise/GroundPlunge.png"),
                        "Incorrect plunge drawings or frame order");
                    Check(art.blade == basic.blade && blade.sprite == basic.blade && art.idle == basic.idle,
                        "Plunge replaced the form's own equipment");
                    if (plungeOnly) Capture();
                }
                Check(art.poses.Length==9,"Missing intermediate grip poses");
                var seen=new HashSet<int>(); var rounds=new HashSet<string>(); int contacts=0,last=-1;
                float previousAngle=0; bool hadWindup=false;
                float released=-1,firstWarning=-1,warningDuration=0,firstDamage=-1,moveAt=-1,origin=enemy.Position.x,maxTravel=0,start=Time.time,nextCapture=start;
                Force(); Check(GroundRunning,"No independent ground timeline");
                var pool=KarmaSwordVfxPool.For(game);
                var cosmetics=((Transform)Get(pool,"poolRoot")).GetComponentsInChildren<SpriteRenderer>(true);
                var ring=KarmaWaveArtwork.Sprite("Sword_GuardRing_v6");
                while(GroundRunning && (!posesOnly || shieldOnly || rounds.Count<2))
                {
                    await Frame(); Check(Time.time-start<18,"SW03 timeline did not finish");
                    var scope=(Transform)Get(Ground,"groundRoot");
                    if(scope!=null)
                    foreach(var attack in scope.GetComponentsInChildren<KarmaBossAttack>())
                        rounds.Add(attack.transform.parent.name);
                    bool fired=(bool)Get(Ground,"swordPlanted");
                    if(fired && released<0) released=Time.time;
                    if(firstWarning<0 && scope!=null)
                    {
                        var round = (Transform)Get(Ground,"roundRoot");
                        var mark = round != null ? round.GetComponentInChildren<KarmaSwordScarRise>() : null;
                        if(mark != null)
                        {
                            firstWarning=Time.time;
                            warningDuration=(float)Get(mark,"life");
                            Check(released>=0 && firstWarning>released,"Warning preceded the planted sword");
                        }
                    }
                    if(firstDamage<0 && rounds.Count>0)
                    {
                        firstDamage=Time.time;
                        Check(firstWarning>=0 && firstDamage-firstWarning>=warningDuration-.035f,"First blades skipped the warning window");
                    }
                    if(released<0) Check(Mathf.Abs(enemy.Position.x-origin)<.01f,"Boss moved before first strike");
                    else
                    {
                        maxTravel=Mathf.Max(maxTravel,Mathf.Abs(enemy.Position.x-origin));
                        if(maxTravel>.1f && moveAt<0) moveAt=Time.time;
                        if(Time.time-released<.98f)
                        {
                            Check(maxTravel<.01f,"Boss moved during one-second planted hold");
                            Check(blade.enabled && (int)Get(actor,"swordGroundDrawing")==4,"Planted pose ended before one second");
                        }
                        if(Time.time-released>1.4f) Check(!blade.enabled,"Initial gesture reappeared during later ground waves");
                    }
                    foreach(var cosmetic in cosmetics)
                        Check(!cosmetic.gameObject.activeInHierarchy || cosmetic.sprite!=ring,"SW03 spawned the removed body bubble");
                    if (shield != null) Check(shield.enabled == blade.enabled, "Detached shield after SW03 pose changed");
                    if (strap != null && !blade.enabled) Check(!strap.enabled, "Strap visible after leaving SW03");
                    if(blade.enabled)
                    {
                        int frame=(int)Get(actor,"swordGroundDrawing"); bool first=seen.Add(frame);
                        if (form == 3 || plungeOnly)
                        {
                            Check(picture.sprite == art.poses[frame], "Plunge joint geometry was deformed");
                            Check(blade.color == picture.color, "Plunge blade tint differs from body renderer");
                            var source = art.poses[frame];
                            var p = art.sockets[frame];
                            var socket = (new Vector2(p.x, source.rect.height - p.y) - source.pivot) / source.pixelsPerUnit;
                            Check(Vector2.Distance(socket, blade.transform.localPosition) < .001f, "Blade left its painted guard");
                        }
                        if (shield != null && first) CheckShield(picture, shield, strap, art, frame);
                        if(frame==4 && last!=4) contacts++;
                        last=frame;
                        Vector3 heel=blade.transform.position;
                        Vector3 tip=blade.transform.TransformPoint(Vector3.right*art.blade.bounds.size.x);
                        float actual=Vector3.Distance(heel,tip);
                        Check(Mathf.Abs(actual-longBlade)<.001f,"Ground strike sword length differs from Idle");
                        Check(!blade.flipX && !blade.flipY,"Reverse steel flipped");
                        Check(picture.transform.localScale.x>0,"Display scale flipped independently");
                        if(form==3 || frame==4 || frame==6 || frame==7 && art.angles[frame]>=-100)
                            Check(blade.sortingOrder>picture.sortingOrder,"Front-held sword disappeared behind torso");
                        if(!fired && frame!=3)
                        {
                            float angle=blade.transform.localEulerAngles.z;
                            if(hadWindup) Check(Mathf.Abs(Mathf.DeltaAngle(previousAngle,angle))<=36,"Wrist/blade jumped during lift");
                            previousAngle=angle;hadWindup=true;
                        }
                        if(frame==4)
                        {
                            Check(fired,"Strike pose appeared before actual release");
                            Check(Mathf.Abs(tip.x-heel.x)<.01f,"Impact sword is not vertical");
                            Check(Mathf.Abs(tip.y-picture.transform.TransformPoint(Vector3.zero).y)<.02f,"Sword tip missed floor pivot");
                            Check(heel.y>tip.y,"Impact blade points upward");
                        }
                    }
                    else last=-1;
                    if(!holdOnly && (side==1 || shieldOnly || plungeOnly) && Time.time>=nextCapture && (Time.time-start<3.65f || !posesOnly && !plungeOnly && (variant==0 || riseOnly)))
                    { Capture(); nextCapture+=1f/24; }
                }
                Check(contacts==1 && firstWarning>released && firstDamage>firstWarning,"Incorrect plant / warning / damage order");
                Check(new[]{0,1,6,7,8,2,3,4,5}.All(seen.Contains),"Missing grip transition/lift/hold/drive/contact/recovery");
                Check(posesOnly && !shieldOnly?rounds.Count>=2:rounds.Count==5,"Incorrect ground rounds: "+rounds.Count);
                Check(maxTravel>.5f && moveAt-released>=1 && moveAt-released<1.4f,"Incorrect planted hold/movement: "+(moveAt-released)+" / "+maxTravel);
                Check(!blade.enabled,"Ground steel remained visible after cast");
                results.Add("PASS "+forms[form]+" facing="+side+" fps="+(variant==4?30:60)+" smooth grip; one plunge; one-second planted hold; "+rounds.Count+" ground rounds observed; no body bubble; movement after "+(moveAt-released).ToString("F3")+"s, travel="+maxTravel.ToString("F2"));
                File.WriteAllLines(Output+(posesOnly?"/pose-progress.txt":riseOnly?"/rise-progress.txt":"/progress.txt"),results);
                if(posesOnly) Call(enemy,"InterruptPattern");
                Set(enemy,"running",true);
            }
            if(posesOnly && !shieldOnly)
            {
                Check(runtimeError==null,runtimeError);
                Finish(holdOnly ? "PASS: SW03 one-second planted hold and movement resumption." : "PASS: 9 SW03 grip/plant/recovery runs with corrected steel layering. Captured frames="+capture);
                return;
            }
            if(riseOnly)
            {
                Check(runtimeError==null,runtimeError);
                Finish("PASS: corrected WaveRise both facings; one reverse-grip command then movement during all five ground rounds. Captured frames="+capture);
                return;
            }
            Time.captureDeltaTime=1f/60;
            foreach(bool afterRelease in new[]{false,true})
            foreach(string ending in new[]{"interrupt","stun","disable","death"})
            {
                Reset(3,1); await Seconds(.1f); Force();
                if(afterRelease) { float end=Time.time+2;while(!(bool)Get(Ground,"swordPlanted")){Check(Time.time<end,"No plant");await Frame();} }
                else await Seconds(.25f);
                var scope=(Transform)Get(Ground,"groundRoot");var blade=enemy.transform.Find("Artwork/Guardian sprite/Ground reverse rigid blade").GetComponent<SpriteRenderer>();
                var shield=blade.transform.parent.Find("SW03 forearm shield").GetComponent<SpriteRenderer>();
                var strap=shield.transform.Find("ForearmStrap").GetComponent<SpriteRenderer>();
                if(ending=="death") enemy.TakeDamage(enemy.MaxHealth*10);
                else if(ending=="disable") enemy.enabled=false;
                else if(ending=="stun") Set(enemy,"pendingParry",true);
                else {Call(enemy,"InterruptPattern");Set(enemy,"running",true);Set(enemy,"state",Enum.Parse(Get(enemy,"state").GetType(),"Approach"));}
                await Seconds(.25f);
                Check(scope==null||!scope.gameObject.activeSelf,ending+" kept ground attacks alive");
                Check(blade==null||!blade.enabled||!blade.gameObject.activeInHierarchy,ending+" kept reverse blade visible");
                Check(shield==null||!shield.enabled||!shield.gameObject.activeInHierarchy,ending+" kept SW03 shield visible");
                Check(strap==null||!strap.enabled||!strap.gameObject.activeInHierarchy,ending+" kept SW03 strap visible");
                results.Add("PASS "+ending+" "+(afterRelease?"released":"windup"));
            }
            if (shieldOnly)
            {
                Check(runtimeError==null,runtimeError);
                Finish("PASS: WaveFinal SW03 shield face, forearm occlusion and fastening strap match all nine poses, both facings and both tempos; 8 cancellation cases. Captured frames="+capture);
                return;
            }
            Reset(3,1);await Seconds(.1f);
            foreach(var attack in new[]{EnemyAttack.SWBasic,EnemyAttack.SW01,EnemyAttack.SW02,EnemyAttack.SW05})
            {
                Force(attack); await Seconds(.2f);
                var blade=enemy.transform.Find("Artwork/Guardian sprite/Ground reverse rigid blade").GetComponent<SpriteRenderer>();
                Check(!blade.enabled,"SW03 blade leaked into "+attack);
                Call(enemy,"InterruptPattern");Set(enemy,"running",true);
                results.Add("PASS separate "+attack+" presentation");
            }
            Check(runtimeError==null,runtimeError);
            Finish("PASS: 9 complete SW03 casts; smooth grip, one-second planted hold, no body bubble; 8 cancellation cases; 4 unrelated-pose checks. Captured frames="+capture);
        }
        catch(Exception e){Finish("FAIL: "+e);Debug.LogException(e);}
        finally{Time.captureDeltaTime=0;Application.logMessageReceived-=Error;EditorApplication.ExitPlaymode();}
    }
    static void Error(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message+"\n"+stack;}
    static void Finish(string message)
    {Directory.CreateDirectory(Output);File.WriteAllText(Output+(posesOnly?"/pose-result.txt":riseOnly?"/rise-result.txt":"/result.txt"),message+"\n"+string.Join("\n",results));File.WriteAllText(Requests+"/run.flag","done");SessionState.EraseBool(Key+".shield");SessionState.SetString(Key,"done");}
}
#endif
