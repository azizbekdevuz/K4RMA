using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using KarmaPrototype;

[InitializeOnLoad]
static class SwordGuardGrowthReview
{
    const string RiseFolder = "Temp/sword-rise-inheritance-review";
    static string Folder => File.Exists(RiseFolder + "/state") ? RiseFolder : "output/sword-guard-inheritance-review";
    static string State => Folder + "/state";
    static bool running;
    static SwordGuardGrowthReview() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (File.Exists(State) && File.ReadAllText(State).Trim() == "restart") { if (EditorApplication.isPlaying) { File.WriteAllText(State,"start"); EditorApplication.ExitPlaymode(); return; } if (!EditorApplication.isPlayingOrWillChangePlaymode) File.WriteAllText(State,"start"); }
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (running || !File.Exists(State) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string request = File.ReadAllText(State).Trim();
        if (request == "refresh-wave" && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.WriteAllText(State,"start");
            foreach (string file in Directory.GetFiles("Assets/Arts/SwordWave/Wave", "*.png"))
                AssetDatabase.ImportAsset(file.Replace('\\','/'), ImportAssetOptions.ForceUpdate);
            return;
        }
        if (request == "start")
        {
            running = true;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { running=false; return; }
            if (SceneManager.GetActiveScene().isDirty ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/KarmaPrototype.unity")
            { File.WriteAllText(State,"FAIL Editor in use"); return; }
            File.WriteAllText(State,"play"); EditorApplication.EnterPlaymode();
        }
        if (request == "play" && EditorApplication.isPlaying && UnityEngine.Object.FindAnyObjectByType<KarmaGame>() != null)
        { running = true; Play(); }
    }
    static KarmaGame director;
    static Task Frame() { var done=new TaskCompletionSource<bool>(); director.StartCoroutine(AtEnd(done)); return done.Task; }
    static IEnumerator AtEnd(TaskCompletionSource<bool> done) { yield return new WaitForEndOfFrame(); done.SetResult(true); }
    static async Task Until(Func<bool> condition)
    { for (int i=0; !condition(); i++) { if (i>1800) throw new Exception("Timed out in game transition"); await Frame(); } }
    static string N(float a) => a.ToString("F5",CultureInfo.InvariantCulture);
    static string Geometry(SpriteRenderer body)
    {
        var sprite = body.sprite;
        var v = sprite.vertices; var u = sprite.uv;
        // Record the actual affine mapping from top-left source pixels to world height.
        int i0=0,i1=1,i2=2;
        for(; i2<v.Length; i2++)
        {
            Vector2 e1=u[i1]-u[i0],e2=u[i2]-u[i0];
            if (Mathf.Abs(e1.x*e2.y-e2.x*e1.y)>0.000001f) break;
        }
        var info = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite.texture));
        info.GetSourceTextureWidthAndHeight(out int width,out int height);
        var all=new List<string>();
        foreach(int n in new[]{i0,i1,i2})
        { all.Add(N(u[n].x*width)); all.Add(N((1-u[n].y)*height)); all.Add(N(body.transform.TransformPoint(v[n]).y)); }
        return string.Join(",",all);
    }
    static async void Play()
    {
        var game=director=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        var stamp=File.ReadAllText(Folder+"/run").Trim();
        
        float delta=Time.captureDeltaTime; bool bg=Application.runInBackground;
        string error=null;
        void OnLog(string msg,string trace,LogType type) { if (type == LogType.Error || type == LogType.Exception) error??=msg; }
        try
        {
            Application.logMessageReceived+=OnLog;
            Application.runInBackground=true; Time.captureDeltaTime=1f/36;
            for (int route=2;route<=3;route++)
            {
            var dir=Folder+"/"+stamp+route; Directory.CreateDirectory(dir);
            File.WriteAllText(Folder+"/progress",stamp+" route "+route+" setup");
            game.StartRun();
            foreach (var kind in route==2 ? new[]{Essence.SwordWave} : new[]{Essence.SwordWave,stamp=="rise"?Essence.Guard:Essence.RisingSlash})
            {game.Player.Essences.Preview(kind); game.Player.Essences.Confirm(out _);}
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,route-1);
            KarmaPrototype.EditorTools.KarmaBossPractice.Invoke(game,"LoadRoom",true);
            await Until(()=>game.IsCombat && game.Enemies.Count>0);
            File.WriteAllText(Folder+"/progress",stamp+" route "+route+" combat");
            var old=game.Enemies[0];
            var art=old.transform.Find("Artwork"); var sc=art.localScale; sc.x=(route==2?-1:1)*Mathf.Abs(sc.x);art.localScale=sc;
            old.TakeDamage(10000000); await Until(()=>game.Phase==RunPhase.Choosing);
            File.WriteAllText(Folder+"/progress",stamp+" route "+route+" choosing"); var previous=((Transform)KarmaPrototype.EditorTools.KarmaBossPractice.Get(game,"fallenGuardian")).GetComponentInChildren<SpriteRenderer>();
            File.AppendAllText(dir+"/poses.tsv",string.Join("\t",N(Time.time),"Fallen","Death",AssetDatabase.GetAssetPath(previous.sprite.texture),previous.sprite.name,Geometry(previous))+"\n");
            game.Choose(stamp=="rise" ? Essence.RisingSlash : Essence.Guard); game.ConfirmChoice(); await Until(()=>game.GuardianInheriting);
            var growth=UnityEngine.Object.FindAnyObjectByType<KarmaGuardianGrowth>(); var boss=game.Enemies[0];
            var actor=boss.GetComponentInChildren<KarmaCharacterAnimator>();
            var body=boss.transform.Find("Artwork/Guardian sprite").GetComponent<SpriteRenderer>();
            if (growth.Stage!=route || boss.PrimaryType!=Essence.SwordWave) throw new Exception("Wrong inheritance route");
            File.WriteAllText(Folder+"/progress",stamp+" route "+route+" growth");
            var fixedPos=boss.Position;
            var seen=new HashSet<string>();
            int tick=0,frames=0;
            void Sample(string beat) {
                string key=beat+" "+body.sprite.name+" "+body.transform.lossyScale.ToString("F3")+" "+body.transform.position.y.ToString("F3")+" "+body.transform.localEulerAngles.z.ToString("F2");
                if(!seen.Add(key)) return;
                File.AppendAllText(dir+"/poses.tsv",string.Join("\t",N(Time.time),beat,actor.CurrentPose,AssetDatabase.GetAssetPath(body.sprite.texture),body.sprite.name,Geometry(body))+"\n");
            }
            while (game.GuardianInheriting && growth!=null)
            {
                if (tick++>1600 || error!=null || (boss.Position-fixedPos).sqrMagnitude>0.0001f) throw new Exception(error??"Boss origin moved or growth stalled");
                var beat=growth.CurrentBeat;
                Sample(beat.ToString());
                if (stamp == "after") {
                    if(beat==KarmaGuardianGrowth.Beat.Ready && body.sprite.name!="Idle") throw new Exception("Missing combat idle "+body.sprite.name);
                    if(route==2 && beat==KarmaGuardianGrowth.Beat.Imitate && body.transform.localScale.y<2.9f) throw new Exception("Undersized guard rehearsal "+body.transform.localScale);
                    if(route==2 && beat==KarmaGuardianGrowth.Beat.Adapt && body.sprite.texture.name!="ShieldRaise") throw new Exception("Incorrect shield "+body.sprite.name);
                }
                if (stamp == "rise") {
                    if (beat==KarmaGuardianGrowth.Beat.Ready && body.sprite.name!="Idle") throw new Exception("Missing inherited combat idle");
                    if (route==2 && beat==KarmaGuardianGrowth.Beat.Adapt && body.sprite.texture.name!="GroundPlunge")
                        throw new Exception("Wrong rise demonstration "+body.sprite.name);
                    if (route==2 && beat==KarmaGuardianGrowth.Beat.Inspect && actor.CurrentPose=="Growth/InspectWeapon" &&
                        (!AssetDatabase.GetAssetPath(body.sprite).EndsWith("/Wave/Guard.png") || body.transform.localScale.y<2.9f))
                        throw new Exception("Incorrect inspection artwork or scale");
                }
                if (tick%3==0 && (stamp=="after" || stamp=="rise" && route==2)) {
                    var shot=ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(dir+"/"+(frames++).ToString("D4")+".jpg",shot.EncodeToJPG(88)); }
                    finally { UnityEngine.Object.Destroy(shot); }
                }
                await Frame();
            }
            Sample("After");
            if(stamp=="after" && route==2 && (!seen.Any(x=>x.StartsWith("Adapt ShieldRaise_08 ")) || !seen.Any(x=>x.StartsWith("Adapt ShieldRaise_00 ")))) throw new Exception("Incomplete shield raise");
            if (!seen.Any(x=>x.StartsWith("Ready")) || !seen.Any(x=>x.StartsWith(route==2 ? "Adapt" : "Combine"))) throw new Exception("Missing guard demonstration");
            }
            File.WriteAllText(State,"PASS "+stamp+" both inheritance routes");
        }
        catch (Exception ex) { File.WriteAllText(State,"FAIL "+ex); }
        finally { Application.logMessageReceived-=OnLog; Application.runInBackground=bg; Time.captureDeltaTime=delta; EditorApplication.ExitPlaymode(); }
    }
}
