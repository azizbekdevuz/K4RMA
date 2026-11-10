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
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Check(bool ok,string message){ checks++;if(!ok)throw new Exception(message); }
    static async Task Until(Func<bool> done)
    {
        double limit=EditorApplication.timeSinceStartup+30;
        while(!done()){if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup>limit)throw new Exception("Timeout");await Task.Delay(10);}
    }
    static async Task Encounter(Essence type,float duration,bool capture)
    {
        game.StartRun();await Task.Delay(100);
        game.Enemies[0].TakeDamage(1000000);
        await Until(()=>game.Phase==RunPhase.Choosing);
        game.Choose(type);game.ConfirmChoice();
        await Until(()=>game.GuardianInheriting);
        var enemy=game.Enemies[0];
        Check(ReferenceEquals(Get(hud,"entranceBoss"),enemy),"real transition hooks correct boss");
        Check((float)Get(hud,"entranceDuration")==duration,"first/repeat duration");
        Check((string)Get(hud,"entranceName")==KarmaPatternCatalog.TypeName(game.Player.Essences.PersonalizationOrder),"canonical name");
        float at=(float)Get(hud,"entranceAt");
        float fight=enemy.FightSeconds;
        game.TogglePause();Check(!game.Paused,"existing transition pause policy unchanged");
        if(capture)
        {
            await Until(()=>Time.time-at>=1.8f);
            Check(enemy.FightSeconds==fight,"boss AI remains idle behind reveal");
            var style=(GUIStyle)Get(hud,"entranceTitle");
            Check(style!=null && style.font!=null && style.font.HasCharacter('형'),"Korean font present");
            string file="output/entrance-review/"+type+".png";
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
        int code=0;
        try
        {
            Application.runInBackground=true;
            Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Exception||t==LogType.Error)failure=m;};
            Directory.CreateDirectory("output/entrance-review");
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();hud=game.GetComponent<KarmaHUD>();
            foreach(var type in new[]{Essence.RisingSlash,Essence.SwordWave,Essence.Guard})await Encounter(type,2.8f,true);
            await Encounter(Essence.RisingSlash,.9f,false);
            // Same direct room-loading path used by existing isolated practice tools.
            game.StartRun();game.Player.Essences.Preview(Essence.Guard);game.Player.Essences.Confirm(out _);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,1);
            typeof(KarmaGame).GetMethod("LoadRoom",F).Invoke(game,new object[]{true});
            Check(game.IsCombat && !ReferenceEquals(Get(hud,"entranceBoss"),game.Enemies[0]),"practice room bypasses reveal");
            game.PlayerDied();await Until(()=>game.Phase==RunPhase.Defeat);game.RetryRoom();
            Check(game.IsCombat && !ReferenceEquals(Get(hud,"entranceBoss"),game.Enemies[0]),"retry does not introduce a combat overlay");
            game.ReturnToTitle();Check(failure==null,"no runtime errors");
            Debug.Log("PASS: "+checks+" boss entrance checks; three real inheritance encounters, repeat, practice path, retry, pause and title");
        }
        catch(Exception e){code=1;Debug.LogException(e);}
        SessionState.SetBool(Key,false);EditorApplication.Exit(code);
    }
}
#endif
