#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaBossEntranceChecks
{
    const string Key = "K4RMA.EntranceChecks";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static bool started; static KarmaGame game; static KarmaHUD hud; static int checks; static string failure;
    static KarmaBossEntranceChecks() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!SessionState.GetBool(Key,false)) return;
        if (EditorApplication.isPlaying) { Application.runInBackground=true; EditorApplication.isPaused=false; }
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying && !started && Time.frameCount>3) { started=true;Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key,true);
        var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        view.position=new Rect(50,50,1280,760);view.Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    [MenuItem("K4RMA/Artwork/Validate inherited boss names")]
    public static void RunInEditor()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before validation.");
        SessionState.SetBool(Key+".Editor",true);
        SessionState.SetBool(Key+".Background",Application.runInBackground);
        RunBatch();
    }
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Check(bool ok,string message){ checks++;if(!ok)throw new Exception(message); }
    static async Task Until(Func<bool> done)
    {
        double limit=EditorApplication.timeSinceStartup+30;
        while(!done()){if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup>limit)throw new Exception("Timeout");await Task.Delay(10);}
    }
    static async Task Encounter(Essence[] order,float duration,bool capture,string expected)
    {
        game.StartRun();await Task.Delay(100);
        if(order.Length>1)
        {
            for(int i=0;i<order.Length-1;i++) { game.Player.Essences.Preview(order[i]);game.Player.Essences.Confirm(out _); }
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,order.Length-1);
            typeof(KarmaGame).GetMethod("LoadRoom",F).Invoke(game,new object[]{true});
        }
        game.Enemies[0].TakeDamage(1000000);
        await Until(()=>game.Phase==RunPhase.Choosing);
        game.Choose(order[order.Length-1]);game.ConfirmChoice();
        await Until(()=>game.GuardianInheriting);
        var enemy=game.Enemies[0];
        Check(ReferenceEquals(Get(hud,"entranceBoss"),enemy),"real transition hooks correct boss");
        Check((float)Get(hud,"entranceDuration")==duration,"first/repeat duration");
        Check((string)Get(hud,"entranceName")==expected,"inheritance-specific name: "+expected);
        var primary=order[0];
        var expectedColor=primary==Essence.SwordWave?new Color32(114,191,255,255)
            :primary==Essence.RisingSlash?new Color32(255,119,113,255):new Color32(242,207,99,255);
        Check(((Color32)(Color)Get(hud,"entranceAccent")).Equals(expectedColor),"primary blue/red/yellow accent");
        Check(!((string)Get(hud,"entranceName")).Contains("<color"),"Korean title remains plain white text");
        float at=(float)Get(hud,"entranceAt");
        float fight=enemy.FightSeconds;
        game.TogglePause();Check(!game.Paused,"existing transition pause policy unchanged");
        if(capture)
        {
            await Until(()=>Time.time-at>=Mathf.Min(1.8f,duration*0.65f));
            Check(enemy.FightSeconds==fight,"boss AI remains idle behind reveal");
            var style=(GUIStyle)Get(hud,"entranceTitle");
            Check(style!=null && style.font!=null && style.font.HasCharacter('형'),"Korean font present");
            Check(style.normal.textColor==new Color(0.94f,0.93f,0.87f),"Korean title retains its original white color");
            Check(((GUIStyle)Get(hud,"entranceLabel")).normal.textColor==(Color)expectedColor,"only existing accent positions use the boss color");
            Check((GUIStyle)Get(hud,"entranceLabel")!=null,"English encounter label remains present");
            string file="output/entrance-review/"+string.Join("-",order)+".png";
            if(File.Exists(file))File.Delete(file);
            ScreenCapture.CaptureScreenshot(file);await Until(()=>File.Exists(file));
        }
        await Until(()=>Time.time-at>=duration);
        Check(game.GuardianInheriting,"presentation finishes inside original safe window");
        await Until(()=>game.IsCombat);
        Check(Time.time-at<3.4f,"combat starts on unchanged inheritance clock");
        game.TogglePause();Check(game.Paused,"combat pause still works");game.TogglePause();
        game.ReturnToTitle();Check(game.Phase==RunPhase.Title,"title return");
    }
    static async void Run()
    {
        int code=0; string verdict;
        Application.LogCallback log=(m,s,t)=>{if(t==LogType.Exception||t==LogType.Error)failure=m;};
        try
        {
            Application.runInBackground=true;
            Application.logMessageReceived+=log;
            Directory.CreateDirectory("output/entrance-review");
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();hud=game.GetComponent<KarmaHUD>();
            foreach(var type in new[]{Essence.RisingSlash,Essence.SwordWave,Essence.Guard})
                await Encounter(new[]{type},2.8f,true,KarmaPatternCatalog.Name(type)+"형");
            await Encounter(new[]{Essence.RisingSlash,Essence.SwordWave},.9f,true,"상승베기형 : 검기 계승");
            await Encounter(new[]{Essence.RisingSlash,Essence.SwordWave,Essence.Guard},.9f,true,"상승베기형 : 검기·방어 계승");
            await Encounter(new[]{Essence.SwordWave,Essence.Guard},.9f,true,"검기형 : 방어 계승");
            await Encounter(new[]{Essence.SwordWave,Essence.Guard,Essence.RisingSlash},.9f,true,"검기형 : 방어·상승베기 계승");
            await Encounter(new[]{Essence.Guard,Essence.RisingSlash},.9f,true,"방어형 : 상승베기 계승");
            await Encounter(new[]{Essence.Guard,Essence.RisingSlash,Essence.SwordWave},.9f,true,"방어형 : 상승베기·검기 계승");
            // Same direct room-loading path used by existing isolated practice tools.
            game.StartRun();game.Player.Essences.Preview(Essence.Guard);game.Player.Essences.Confirm(out _);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,1);
            typeof(KarmaGame).GetMethod("LoadRoom",F).Invoke(game,new object[]{true});
            Check(game.IsCombat && !ReferenceEquals(Get(hud,"entranceBoss"),game.Enemies[0]),"practice room bypasses reveal");
            game.PlayerDied();await Until(()=>game.Phase==RunPhase.Defeat);game.RetryRoom();
            Check(game.IsCombat && !ReferenceEquals(Get(hud,"entranceBoss"),game.Enemies[0]),"retry does not introduce a combat overlay");
            game.ReturnToTitle();Check(failure==null,"no runtime errors");
            verdict="PASS: "+checks+" white Korean inheritance names, blue/red/yellow accents, retained English label, real revival, repeat timing, practice and retry checks";
        }
        catch(Exception e){code=1;verdict="FAIL: "+e;Debug.LogException(e);}
        Application.logMessageReceived-=log;Time.captureDeltaTime=0;
        File.WriteAllText("output/entrance-review/result.txt",verdict);Debug.Log(verdict);
        SessionState.SetBool(Key,false);
        if(SessionState.GetBool(Key+".Editor",false))
        {
            Application.runInBackground=SessionState.GetBool(Key+".Background",false);
            SessionState.EraseBool(Key+".Editor");EditorApplication.ExitPlaymode();
        }
        else EditorApplication.Exit(code);
    }
}
#endif
