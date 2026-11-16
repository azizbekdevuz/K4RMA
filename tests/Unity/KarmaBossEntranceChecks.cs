#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
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
        double limit=EditorApplication.timeSinceStartup+15;
        while(!done()){if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup>limit)throw new Exception($"Timeout in {game.Phase}/{PromptProperty("CurrentEncounterPrompt")}, timer={Get(game, "transitionLeft")}, scale={Time.timeScale}, paused={game.Paused}, settings={game.SettingsOpen}");await Task.Delay(10);}
    }
    static object PromptProperty(string name) => typeof(KarmaGame).GetProperty(name, F).GetValue(game);
    static async Task SetSpace(Keyboard keyboard, bool held)
    {
        InputSystem.QueueStateEvent(keyboard, held ? new KeyboardState(UnityEngine.InputSystem.Key.Space) : new KeyboardState());
        await Until(() => keyboard.spaceKey.isPressed == held);
    }
    static async Task Encounter(Keyboard keyboard, Essence[] order, float duration, string expected)
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
        await Until(() => Time.time - at >= Mathf.Min(1.8f, duration * 0.65f));
        Check(enemy.FightSeconds == fight, "Boss AI remains idle behind reveal");
        var style = (GUIStyle)Get(hud, "entranceTitle");
        Check(style != null && style.font != null && style.font.HasCharacter('형'), "Korean font present");
        Check(style.normal.textColor == new Color(0.94f, 0.93f, 0.87f), "Korean title retains its original white color");
        Check(((GUIStyle)Get(hud, "entranceLabel")).normal.textColor == (Color)expectedColor, "Accent follows primary boss");

        await SetSpace(keyboard, true);
        await Until(() => (bool)PromptProperty("EncounterPromptReady"));
        float waitStarted = Time.time, runClock = game.Elapsed;
        await Until(() => Time.time > waitStarted + 0.24f);
        Check(PromptProperty("CurrentEncounterPrompt").ToString() == "BossName",
            "A Space press held from before the revival ended cannot dismiss the name");
        Check(!game.AllowsPlayerActions && enemy.FightSeconds == fight && Mathf.Abs(game.Elapsed - runClock) < 0.01f,
            "Waiting on the name cannot start combat or advance the run clock");
        await SetSpace(keyboard, false);
        await SetSpace(keyboard, true);
        await Until(() => PromptProperty("CurrentEncounterPrompt").ToString() == "SkillGuide");
        var essence = order[order.Length - 1];
        var guide = (string)typeof(KarmaHUD).GetMethod("PersonalizationGuideText", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { essence });
        var action = essence == Essence.SwordWave ? KarmaAction.Pierce
            : essence == Essence.RisingSlash ? KarmaAction.Attack : KarmaAction.Counter;
        string binding = KarmaInput.BindingLabel(action);
        string defaultKey = essence == Essence.SwordWave ? "L" : essence == Essence.RisingSlash ? "J" : "K";
        string markedKey = binding == defaultKey ? binding + (essence == Essence.SwordWave ? "을" : "를") : binding + " 키를";
        string expectedGuide = essence switch
        {
            Essence.SwordWave => $"방향키와 {markedKey} 함께 누르면 여덟 방향 중 원하는 방향으로 돌진해 적을 관통할 수 있다.",
            Essence.RisingSlash => $"공중에서 방향키와 {markedKey} 함께 누르면 여덟 방향 중 원하는 방향으로 베며 한 번 더 도약할 수 있다. 아래 방향을 선택하면 잠시 체공할 수 있다.",
            _ => $"적의 공격이 닿는 순간 {markedKey} 눌러 받아내면 그 힘을 되돌려줄 수 있다."
        };
        Check(guide == expectedGuide, "Skill guide shows the agreed Korean copy and bound key");
        Check(((GUIStyle)Get(hud, "text")).CalcHeight(new GUIContent(guide), 750) <= 64, "Skill guide fits the panel");
        waitStarted = Time.time;
        await Until(() => Time.time > waitStarted + 0.24f);
        Check(PromptProperty("CurrentEncounterPrompt").ToString() == "SkillGuide" && !game.IsCombat,
            "Holding the first press cannot dismiss the guide");
        await SetSpace(keyboard, false);
        Check(!game.IsCombat, "Releasing the first press keeps the guide on screen");
        await SetSpace(keyboard, true);
        await Until(() => PromptProperty("CurrentEncounterPrompt").ToString() == "WaitForRelease");
        Check(!game.IsCombat, "Second Space press cannot jump into combat");
        await SetSpace(keyboard, false);
        await Until(() => game.IsCombat);
        game.TogglePause();Check(game.Paused,"combat pause still works");game.TogglePause();
        game.ReturnToTitle();Check(game.Phase==RunPhase.Title,"title return");
    }
    static async void Run()
    {
        int code=0; string verdict;
        Application.LogCallback log=(m,s,t)=>{if(t==LogType.Exception||t==LogType.Error)failure=m;};
        Keyboard keyboard = null;
        try
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            Application.runInBackground=true;
            Application.logMessageReceived+=log;
            Directory.CreateDirectory("output/entrance-review");
            Time.captureDeltaTime = 1f / 30;
            game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();hud=game.GetComponent<KarmaHUD>();
            foreach(var type in new[]{Essence.RisingSlash,Essence.SwordWave,Essence.Guard})
                await Encounter(keyboard, new[]{type},2.8f,KarmaPatternCatalog.Name(type)+"형");
            await Encounter(keyboard, new[]{Essence.RisingSlash,Essence.SwordWave},.9f,"상승베기형 : 검기 계승");
            await Encounter(keyboard, new[]{Essence.RisingSlash,Essence.SwordWave,Essence.Guard},.9f,"상승베기형 : 검기·방어 계승");
            await Encounter(keyboard, new[]{Essence.SwordWave,Essence.Guard},.9f,"검기형 : 방어 계승");
            await Encounter(keyboard, new[]{Essence.SwordWave,Essence.Guard,Essence.RisingSlash},.9f,"검기형 : 방어·상승베기 계승");
            await Encounter(keyboard, new[]{Essence.Guard,Essence.RisingSlash},.9f,"방어형 : 상승베기 계승");
            await Encounter(keyboard, new[]{Essence.Guard,Essence.RisingSlash,Essence.SwordWave},.9f,"방어형 : 상승베기·검기 계승");
            // Same direct room-loading path used by existing isolated practice tools.
            game.StartRun();game.Player.Essences.Preview(Essence.Guard);game.Player.Essences.Confirm(out _);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,1);
            typeof(KarmaGame).GetMethod("LoadRoom",F).Invoke(game,new object[]{true});
            Check(game.IsCombat && !ReferenceEquals(Get(hud,"entranceBoss"),game.Enemies[0]),"practice room bypasses reveal");
            game.PlayerDied();await Until(()=>game.Phase==RunPhase.Defeat);game.RetryRoom();
            Check(game.IsCombat && !ReferenceEquals(Get(hud,"entranceBoss"),game.Enemies[0]),"retry does not introduce a combat overlay");
            game.ReturnToTitle();Check(failure==null,"no runtime errors");
            verdict="PASS: "+checks+" white Korean inheritance names, blue/red/yellow accents, retained English label, revival, two fresh Space presses, run clock, practice and retry checks";
        }
        catch(Exception e){code=1;verdict="FAIL: "+e;Debug.LogException(e);}
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
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
