#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KarmaPrototype;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class KarmaSwordPatternPerformanceChecks
{
    const string Key="K4RMA.SwordPatternPerformance";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] sealed class Measurement
    {
        public int fps,seed,shots,peakShots,peakWarnings,peakObjects,collections,frames;
        public bool enraged,baseline;
        public float duration,p95Ms,maxMs,shieldDuration;
        public long allocatedBytes,maxAllocatedBytes;
    }
    [Serializable] sealed class Report
    {
        public string result,utc,gpu,api,failure;
        public Measurement[] cases;
    }
    static KarmaGame game; static KarmaEnemy enemy;
    static int index; static bool started;
    static float startedAt;
    static float shieldStarted;
    static Measurement current;
    static readonly List<Measurement> measurements=new List<Measurement>();
    static readonly List<float> samples=new List<float>();
    static readonly HashSet<KarmaBossAttack> shots=new HashSet<KarmaBossAttack>();
    static ProfilerRecorder allocation,frame;
    static int collections;
    static double deadline;
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private).Invoke(o,args);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static KarmaSwordPatternPerformanceChecks() { EditorApplication.playModeStateChanged+=OnState; }
    public static void Run()
    {
        if(!File.Exists(".karma-isolated-project")) throw new Exception("Isolated project required");
        SessionState.SetBool(Key,true); EditorApplication.delayCall+=Begin;
    }
    static void Begin()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall+=Begin;return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); EditorApplication.EnterPlaymode();
    }
    static void OnState(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
        try
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) throw new Exception("Actual graphics required");
            Application.runInBackground=true;
            Application.logMessageReceived+=OnLog;
            game=new GameObject("Actual SW05 performance game").AddComponent<KarmaGame>();
            game.Config.playerHealth=1000000; game.StartRun();
            game.Player.Essences.Sacrifice(Essence.SwordWave); game.Player.Essences.Sacrifice(Essence.RisingSlash); game.Player.Essences.Sacrifice(Essence.Guard);
            Call(game,"LoadRoom",true);
            deadline=EditorApplication.timeSinceStartup+600;
            new GameObject("SW05 performance actual loop").AddComponent<KarmaSwordPatternPerformanceDriver>().Step=Tick;
            EditorApplication.update+=Queue;
        }
        catch(Exception e) { Finish(e); }
    }
    static void Queue() { EditorApplication.QueuePlayerLoopUpdate(); }
    static void OnLog(string message,string stack,LogType kind)
    { if(kind==LogType.Error || kind==LogType.Exception || kind==LogType.Assert) Finish(new Exception(message+"\n"+stack)); }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Performance deadline");
            if(!started)
            {
                if(index==6) { Finish(null);return; }
                enemy=game.Enemies[0]; Call(enemy,"InterruptPattern");
                current=new Measurement{fps=index%2==0?30:60,enraged=index>=4,baseline=index<2,seed=20261029};
                Time.captureDeltaTime=1f/current.fps;
                typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*(current.enraged?.45f:1));
                enemy.transform.position=new Vector3(20,KarmaSpace.GuardianGroundY,0);
                var body=game.Player.GetComponent<Rigidbody2D>(); body.bodyType=RigidbodyType2D.Kinematic;
                body.position=new Vector2(13,2);game.Player.transform.position=body.position;
                Set(enemy,"selected",EnemyAttack.SW05);Set(enemy,"direction",-1f);Set(enemy,"engaged",true);
                Set(enemy,"running",true);Set(enemy,"patternStarted",enemy.FightSeconds);Set(enemy,"nextShield",float.MaxValue);
                game.Config.BossTuning.aerialSwordSeed=current.seed;
                game.Config.BossTuning.aerialSwordVolleyMin=current.baseline?4:15;
                game.Config.BossTuning.aerialSwordVolleyMax=current.baseline?8:15;
                shieldStarted=-1;
                samples.Clear();shots.Clear();collections=GC.CollectionCount(0);
                allocation=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
                frame=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");
                startedAt=enemy.FightSeconds; started=true;
                enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern")); return;
            }
            Set(game.Player,"invulnerableUntil",Time.time+100);
            current.frames++;
            if(enemy.Invulnerable && shieldStarted<0) shieldStarted=enemy.FightSeconds;
            if(!enemy.Invulnerable && shieldStarted>=0 && current.shieldDuration==0) current.shieldDuration=enemy.FightSeconds-shieldStarted;
            if(allocation.Valid) { current.allocatedBytes+=allocation.LastValue; current.maxAllocatedBytes=Math.Max(current.maxAllocatedBytes,allocation.LastValue); }
            if(frame.Valid && frame.LastValue>0) samples.Add(frame.LastValue/1000000f);
            var effects=Get<Transform>(enemy,"effectsRoot");
            var attacks=effects.GetComponentsInChildren<KarmaBossAttack>();
            foreach(var attack in attacks) shots.Add(attack);
            current.peakShots=Mathf.Max(current.peakShots,attacks.Length);
            current.peakWarnings=Mathf.Max(current.peakWarnings,effects.GetComponentsInChildren<KarmaAttackWarning>().Length);
            int active=0;foreach(Transform child in effects) if(child.gameObject.activeInHierarchy) active++;
            current.peakObjects=Mathf.Max(current.peakObjects,active);
            if(Get<bool>(enemy,"running")) return;
            current.duration=enemy.FightSeconds-startedAt;current.shots=shots.Count;
            if(current.shots!=(current.baseline?47:105) || current.peakWarnings!=(current.baseline?8:15) || attacks.Length!=0 || enemy.Invulnerable || enemy.Armored)
                throw new Exception("105-shot count/formation/cleanup regression");
            if(!allocation.Valid || !frame.Valid || samples.Count==0) throw new Exception("Profiler data unavailable");
            samples.Sort();current.p95Ms=samples[Mathf.Min(samples.Count-1,(int)(samples.Count*.95f))];current.maxMs=samples[samples.Count-1];
            current.collections=GC.CollectionCount(0)-collections;
            allocation.Dispose();frame.Dispose();
            measurements.Add(current); Debug.Log("SW05 ACTUAL GPU PERFORMANCE "+JsonUtility.ToJson(current));
            index++;started=false;
        }
        catch(Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        Application.logMessageReceived-=OnLog;EditorApplication.update-=Queue;SessionState.SetBool(Key,false);
        if(allocation.Valid) allocation.Dispose();if(frame.Valid) frame.Dispose();
        var report=new Report{result=error==null?"PASS":"FAIL",utc=DateTime.UtcNow.ToString("o"),gpu=SystemInfo.graphicsDeviceName,
            api=SystemInfo.graphicsDeviceType.ToString(),failure=error?.ToString(),cases=measurements.ToArray()};
        File.WriteAllText("sword-performance.json",JsonUtility.ToJson(report,true));
        EditorApplication.Exit(error==null?0:1);
    }
}
public sealed class KarmaSwordPatternPerformanceDriver:MonoBehaviour
{
    public Action Step; void Update() { Step?.Invoke(); }
}
#endif
