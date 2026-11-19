#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Focused checks for the SwordWave skill refresh; no unrelated regression suites.
[InitializeOnLoad]
public static class KarmaSwordSkillRefreshChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string Key = "K4RMA.SkillRefresh";
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started;
    static string error;
    static Transform Root => (Transform)Get(enemy, "effectsRoot");
    static object Get(object o, string n)
    { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f.GetValue(o); } throw new MissingFieldException(n); }
    static void Set(object o, string n, object v)
    { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) { f.SetValue(o, v); return; } } throw new MissingFieldException(n); }
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Pass(string message) => Debug.Log("REFRESH PASS: " + message);
    static KarmaSwordSkillRefreshChecks() { EditorApplication.update += Pump; }
    public static void RunBatch()
    {
        SessionState.SetBool(Key + ".RequestedChanges", false);
        SessionState.SetBool(Key + ".RainOnly", false);
        SessionState.SetBool(Key + ".PackedRenderOnly", false);
        SessionState.SetBool(Key + ".VfxOnly", false);
        SessionState.SetBool(Key + ".Grand", false);
        SessionState.SetBool(Key + ".WardFinal", false);
        SessionState.SetBool(Key + ".WardOnly", false);
        SessionState.SetBool(Key + ".ContinuousAimOnly", false);
        SessionState.SetBool(Key + ".ActiveSW03Only", false);
        SessionState.SetBool(Key + ".ElectrocutionOnly", false);
        SessionState.SetBool(Key + ".ShockAndDetonation", false);
        SessionState.SetBool(Key + ".FrequencyOnly", false);
        SessionState.SetBool(Key + ".EvasionAndWard", false);
        SessionState.SetBool(Key + ".SW03Only", false);
        SessionState.SetBool(Key + ".ScarPolish", false);
        SessionState.SetBool(Key + ".SkillVisualReview", false);
        SessionState.SetBool(Key + ".GroundedVfxReview", false);
        SessionState.SetBool(Key + ".VisualCleanup", false);
        SessionState.SetBool(Key + ".AreaRecording", false);
        SessionState.SetBool(Key + ".AppearanceOnly", false);
        SessionState.SetBool(Key + ".GroundEntryReview", false);
        SessionState.SetInt(Key + ".Stage", 0);
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunRetreatAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 1); }
    public static void RunScarAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 2); }
    public static void RunJumpAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 3); }
    public static void RunRainAndLater() { RunBatch(); SessionState.SetInt(Key + ".Stage", 4); }
    public static void RunExplosionAndRender() { RunBatch(); SessionState.SetInt(Key + ".Stage", 5); }
    public static void RunRenderOnly() { RunBatch(); SessionState.SetInt(Key + ".Stage", 6); }
    public static void RunSW03Only() { RunBatch(); SessionState.SetBool(Key + ".SW03Only", true); SessionState.SetInt(Key + ".SW03Step", 0); }
    public static void RunSW03MovementOnly() { RunSW03Only(); SessionState.SetInt(Key + ".SW03Step", 1); }
    public static void RunSW03VisualOnly() { RunSW03Only(); SessionState.SetInt(Key + ".SW03Step", 2); }
    public static void RunRequestedChanges() { RunBatch(); SessionState.SetBool(Key + ".RequestedChanges", true); }
    public static void RunRequestedRainOnly() { RunRequestedChanges(); SessionState.SetBool(Key + ".RainOnly", true); }
    public static void RunPackedRenderOnly() { RunBatch(); SessionState.SetBool(Key + ".PackedRenderOnly",true); }
    public static void RunRequestedVfxOnly() { RunRequestedChanges(); SessionState.SetBool(Key + ".VfxOnly",true); }
    public static void RunGrandOverhaul() { RunRequestedChanges(); SessionState.SetBool(Key+".Grand",true); }
    public static void RunWardAndFinal() { RunRequestedChanges(); SessionState.SetBool(Key+".WardFinal",true); }
    public static void RunWardOnly() { RunWardAndFinal(); SessionState.SetBool(Key+".WardOnly",true); }
    public static void RunWardAndRainVisuals() { RunWardAndFinal(); SessionState.SetBool(Key+".RainOnly",true); SessionState.SetBool(Key+".AreaRecording",true); }
    public static void RunRainVisuals() { RunRequestedRainOnly(); SessionState.SetBool(Key+".AreaRecording",true); }
    public static void RunAppearanceOnly() { RunBatch(); SessionState.SetBool(Key+".AppearanceOnly",true); }
    public static void RunCommandAppearanceReview() { RunAppearanceOnly(); SessionState.SetBool(Key+".GroundEntryReview",true); }
    public static void RunContinuousAimOnly() { RunBatch(); SessionState.SetBool(Key+".ContinuousAimOnly",true); }
    public static void RunWardParryOnly() { RunVisualCleanup(); SessionState.SetInt(Key+".Stage",1); }
    public static void RunVisualCleanup() { RunBatch(); SessionState.SetBool(Key+".VisualCleanup",true); }
    public static void RunSkillVisualReview() { RunBatch(); SessionState.SetBool(Key+".SkillVisualReview",true); SessionState.SetBool(Key+".ScarPolish",true); SessionState.SetBool(Key+".AreaRecording",true); }
    public static void RunGroundedVfxReview() { RunSkillVisualReview();SessionState.SetBool(Key+".SkillVisualReview",false);SessionState.SetBool(Key+".GroundedVfxReview",true); }
    public static void RunScarPolish() { RunBatch(); SessionState.SetBool(Key+".ActiveSW03Only",true); SessionState.SetBool(Key+".ScarPolish",true); }
    public static void RunActiveSW03Only() { RunBatch(); SessionState.SetBool(Key+".ActiveSW03Only",true); }
    public static void RunShockAndDetonation() { RunBatch(); SessionState.SetBool(Key+".ShockAndDetonation",true); }
    public static void RunElectrocutionOnly() { RunBatch(); SessionState.SetBool(Key+".ElectrocutionOnly",true); }
    public static void RunFrequencyOnly() { RunBatch(); SessionState.SetBool(Key+".FrequencyOnly",true); }
    public static void RunEvasionAndWard() { RunBatch(); SessionState.SetBool(Key+".EvasionAndWard",true); }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        if (!started && Time.frameCount > 2) { started = true; Run(); }
    }
    static void Reset()
    {
        typeof(KarmaSwordPlayChecks).GetField("game", F).SetValue(null, game);
        typeof(KarmaSwordPlayChecks).GetMethod("Reset", F).Invoke(null, new object[] { Essence.SwordWave });
        enemy = (KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy", F).GetValue(null);
    }
    static void Force(EnemyAttack pattern) => typeof(KarmaSwordPlayChecks).GetMethod("Force", F).Invoke(null, new object[] { pattern });
    static async Task Until(Func<bool> done, Action observe = null, float timeout = 25)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!done()) { observe?.Invoke(); Check(EditorApplication.timeSinceStartup < end, "focused check timeout"); await Task.Delay(5); }
        observe?.Invoke();
    }
    static async Task ObserveEveryFrame(Func<bool> done, Action observe, float timeout = 25)
    {
        Exception failure = null;
        var probe = game.gameObject.AddComponent<KarmaSwordAimProbe>();
        probe.Tick = () => {
            if (failure != null || done()) return;
            try { observe(); } catch (Exception ex) { failure = ex; }
        };
        try
        {
            probe.Tick();
            await Until(() => failure != null || done(), timeout: timeout);
            if (failure != null) throw failure;
        }
        finally { probe.Tick = null; UnityEngine.Object.Destroy(probe); }
    }
    static Task Seconds(float seconds) { float end = enemy.FightSeconds + seconds; return Until(() => enemy.FightSeconds >= end); }
    static float OriginalWaitAt60(float seconds)
    {
        float elapsed=0;int frames=0;
        while(elapsed<seconds){elapsed+=1f/60;frames++;}
        return frames/60f;
    }
    static async Task BasicLinkOnly()
    {
        foreach(int side in new[]{-1,1})foreach(bool landFirst in new[]{false,true})
        {
            Reset();var player=game.Player;var rb=player.GetComponent<Rigidbody2D>();
            Vector2 close=enemy.Position+Vector2.right*side*3;
            close.y=KarmaSpace.PlayerHalfHeight+0.02f;
            player.transform.position=rb.position=close;Physics2D.SyncTransforms();
            Set(player,"invulnerableUntil",0f);
            float previousHealth=player.Health;
            var contacts=new HashSet<KarmaBossAttack>();var releases=new List<float>();var damageTimes=new List<float>();
            Force(EnemyAttack.SWBasic);
            var probe=game.gameObject.AddComponent<KarmaSwordElectricRecorder>();
            probe.Tick=()=>
            {
                foreach(var attack in Root.GetComponentsInChildren<KarmaBossAttack>())
                    if(contacts.Add(attack))releases.Add(enemy.FightSeconds);
                if(player.Health<previousHealth){damageTimes.Add(enemy.FightSeconds);previousHealth=player.Health;}
                Vector2 position=!landFirst && releases.Count<1 ? close+Vector2.right*side*20 : close;
                if(!landFirst && releases.Count==1 && enemy.FightSeconds<releases[0]+3f/24)
                    position=close+Vector2.right*side*20;
                player.transform.position=rb.position=position;rb.linearVelocity=Vector2.zero;
                Physics2D.SyncTransforms();
            };
            try { await Until(()=>!(bool)Get(enemy,"running")); }
            finally {probe.Tick=null;UnityEngine.Object.Destroy(probe);}
            Check(releases.Count==2,"both authored melee release events occur");
            Check(damageTimes.Count==(landFirst?2:1),"real Health falls independently on each connected cut");
            float interval=releases[1]-releases[0];
            if(landFirst)Check(interval<0.75f && damageTimes[1]-damageTimes[0]>game.Config.hitInvulnerability,
                "confirmed opener shortens link without losing second hit to immunity");
            else Check(interval<0.25f,"missed opener follows with a prompt return cut");
            Pass("BASIC LINK facing="+side+" firstHit="+landFirst+" releaseInterval="+interval.ToString("F3")+" healthLosses="+damageTimes.Count);
        }
    }
    static async Task ContinuousAimOnly()
    {
        // captureDeltaTime takes effect on the next player-loop frame. Avoid
        // feeding the editor's long scene-loading delta into Execute's first step.
        int clockFrame=Time.frameCount;
        await Until(()=>Time.frameCount>clockFrame && Mathf.Abs(Time.deltaTime-1f/60)<0.00001f);
        var args=Environment.GetCommandLineArgs();int filterAt=Array.IndexOf(args,"-swAimPattern");
        string filter=filterAt>=0&&filterAt+1<args.Length?args[filterAt+1]:null;
        var orders = new HashSet<bool>();
        foreach (var pattern in new[] { EnemyAttack.SW01, EnemyAttack.SW02 })
        for (int trial=0;trial<2;trial++)
        {
            if(filter!=null && pattern.ToString()!=filter)continue;
            Reset();
            using var pathRecording=trial==0 && SessionState.GetBool(Key+".GroundedVfxReview",false) ? new SkillRecording(pattern.ToString().ToLowerInvariant()+"-path",false) : null;
            if (trial==1) typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*0.1f);
            Action<float> place = side =>
            {
                var p=new Vector2(enemy.Position.x+side*12,KarmaSpace.PlayerHalfHeight+0.02f);
                game.Player.transform.position=p;game.Player.GetComponent<Rigidbody2D>().position=p;Physics2D.SyncTransforms();
            };
            float initialSide=trial==0?1:-1;
            place(initialSide);
            if(pattern==EnemyAttack.SW01)
            {
                for(int seed=0;seed<100;seed++)
                {
                    UnityEngine.Random.InitState(seed);bool high=UnityEngine.Random.value<0.5f;
                    if(high==(trial==1)){UnityEngine.Random.InitState(seed);break;}
                }
            }
            int expected=pattern==EnemyAttack.SW01?2:1, count=0;
            var seen=new Dictionary<KarmaBossAttack,Vector2>();
            var launches=new List<float>();var heights=new List<float>();
            var trackingSides=new HashSet<int>();
            float advertisedFacing=initialSide;
            Vector2 advertisedOrigin=Vector2.zero;
            float tempo=trial==0?1:Mathf.Clamp(game.Config.BossTuning.enrageTempo,0.65f,1);
            float gap=OriginalWaitAt60(Mathf.Max(0.42f,0.54f*tempo));
            float recovery=Mathf.Max(0.38f,Mathf.Clamp(game.Config.BossTuning.recoveryMultiplier,0.4f,2)*tempo*game.Config.BossTuning.sword.recoveryScale);
            float firstRelease=OriginalWaitAt60((float)Call(enemy,"Warning",0.65f));
            float total=firstRelease+gap+OriginalWaitAt60(0.12f)+OriginalWaitAt60(recovery);
            float aimedTracking=OriginalWaitAt60(0.34f*tempo);
            bool aimedWarningCaptured=false;
            if(pattern==EnemyAttack.SW02)
            {
                var aimed=Pattern(pattern);
                var origin=(Vector2)Call(aimed,"AimedSlashOrigin",enemy.Position,initialSide);
                var size=(Vector2)Call(Get(enemy,"behavior"),"SwordSize",pattern);
                firstRelease=OriginalWaitAt60(0.34f*tempo)+OriginalWaitAt60((float)Call(aimed,"AimedSlashWarningTime",origin,initialSide,size));
                total=firstRelease+OriginalWaitAt60(0.10f)+OriginalWaitAt60(Mathf.Max(0.38f,1.1f*Mathf.Clamp(game.Config.BossTuning.recoveryMultiplier,0.4f,2)*tempo*game.Config.BossTuning.sword.recoveryScale));
            }
            bool crossed=false,returned=false,lastFrameCrossed=false;
            float crossedAt=-1;
            Exception failure=null;
            var probe=game.gameObject.AddComponent<KarmaSwordAimProbe>();
            float began=Time.time;
            Force(pattern);
            probe.Tick=()=>
            {
                if(failure!=null)return;
                try
                {
                    float now=Time.time-began;
                    var animator=(KarmaCharacterAnimator)Get(enemy,"animator");
                    if(pattern==EnemyAttack.SW02 && !aimedWarningCaptured && Get(animator,"sw02VisualPhase").ToString()=="Locked")
                    {
                        // The safety countdown is chosen after tracking/dashing.
                        // Read the duration supplied to the visible preparation,
                        // then require release on its final frame with no extra wait.
                        // Execute's first tracking increment can run in Force's
                        // frame, before the first LateUpdate observation.
                        Check(Mathf.Abs(now-aimedTracking)<=1f/60+0.002f,
                            "existing SW02 tracking duration retained: "+now.ToString("F4")+" expected "+aimedTracking.ToString("F4"));
                        float warning=(float)Get(animator,"sw02PhaseSeconds");
                        firstRelease=now+OriginalWaitAt60(warning);
                        total=firstRelease+OriginalWaitAt60(0.10f)+OriginalWaitAt60(Mathf.Max(0.38f,1.1f*Mathf.Clamp(game.Config.BossTuning.recoveryMultiplier,0.4f,2)*tempo*game.Config.BossTuning.sword.recoveryScale));
                        aimedWarningCaptured=true;
                    }
                    foreach(var shot in Root.GetComponentsInChildren<KarmaBossAttack>())
                    {
                        var velocity=(Vector2)Get(shot,"velocity");
                        if(seen.ContainsKey(shot)){Check(velocity==seen[shot],"released wave never homes after later player crossings");continue;}
                        float release=firstRelease+count*gap;
                        Check(Mathf.Abs(now-release)<0.002f,"original release timing without an added wait: "+now.ToString("F4")+" expected "+release.ToString("F4"));
                        Check(lastFrameCrossed && now-crossedAt>0 && now-crossedAt<0.018f,"player crosses behind on the last frame before each release");
                        float side=advertisedFacing;
                        Check(Mathf.Sign(velocity.x)==side,"release keeps the last visible warning direction after a late crossing");
                        Check(Vector2.Distance(shot.transform.position,advertisedOrigin)<0.002f,
                            "release origin is the last advertised start, including the projectile half length");
                        // SW01 can already be aiming its next warning in the other direction.
                        Check(Mathf.Sign(((Transform)Get(enemy,"art")).localScale.x)==enemy.Facing,"art facing matches the current boss aim");
                        Check(trackingSides.Count==2,"warning/boss track both sides before each projectile");
                        float speed=pattern==EnemyAttack.SW02
                            ? Mathf.Max(0.1f,game.Config.BossTuning.sword.swordSpeed*1.25f*1.3f)
                            : Mathf.Max(KarmaSpace.Scale,game.Config.BossTuning.sword.swordSpeed*1.3f);
                        Check(Mathf.Abs(velocity.x-side*speed)<0.001f && velocity.y==0,"existing projectile speed retained");
                        Check((Vector2)Get(shot,"size")== (Vector2)Call(Get(enemy,"behavior"),"SwordSize",pattern),"existing hitbox retained");
                        Check(shot.GetComponent<KarmaSwordWaveAnimation>()!=null,"existing wave VFX retained");
                        seen.Add(shot,velocity);launches.Add(now);heights.Add(shot.transform.position.y);
                        count++;crossed=returned=lastFrameCrossed=false;trackingSides.Clear();
                        initialSide=side;
                    }
                    if(count>=expected)return;
                    foreach(var mark in Root.GetComponentsInChildren<KarmaAttackWarning>().Where(m=>m.gameObject.activeInHierarchy))
                    {
                        Check(Mathf.Sign(mark.transform.position.x-enemy.Position.x)==enemy.Facing,"warning tracks current boss firing direction");
                        Check(enemy.Facing==Mathf.Sign(game.Player.Position.x-enemy.Position.x),"continuous tracking throughout preparation");
                        trackingSides.Add(enemy.Facing);
                        advertisedFacing=enemy.Facing;
                        var size=(Vector2)Call(Get(enemy,"behavior"),"SwordSize",pattern);
                        var outline=mark.transform.Find("Locked danger outline").GetComponent<LineRenderer>();
                        Check(outline.enabled && outline.loop && outline.positionCount>=4 && mark.GetComponentInChildren<MeshFilter>()==null,
                            "Original visible warning outline follows the advertised path");
                        Vector3 Vertex(int i) => outline.useWorldSpace ? outline.GetPosition(i) : outline.transform.TransformPoint(outline.GetPosition(i));
                        var bounds=new Bounds(Vertex(0),Vector3.zero);
                        for (int i=1;i<outline.positionCount;i++) bounds.Encapsulate(Vertex(i));
                        advertisedOrigin=new Vector2(advertisedFacing>0?bounds.min.x+size.x/2:bounds.max.x-size.x/2,bounds.center.y);
                    }
                    float phaseStart=count==0?0:launches[0];
                    if(!crossed && now-phaseStart>=0.07f){place(-initialSide);crossed=true;}
                    if(!returned && now-phaseStart>=0.16f){place(initialSide);returned=true;}
                    if(!lastFrameCrossed && now>=firstRelease+count*gap-1f/60-0.001f)
                    {place(-initialSide);lastFrameCrossed=true;crossedAt=now;}
                }
                catch(Exception ex){failure=ex;}
            };
            await Until(()=>failure!=null || !(bool)Get(enemy,"running"),timeout:20);
            probe.Tick=null;UnityEngine.Object.Destroy(probe);
            if(failure!=null)throw failure;
            Check(count==expected,"exactly the existing projectile count");
            Check(Mathf.Abs(Time.time-began-total)<0.002f,"original total duration and recovery: "+(Time.time-began).ToString("F4")+" expected "+total.ToString("F4"));
            if(pattern==EnemyAttack.SW01)
            {
                Check(Mathf.Abs(heights[0]-heights[1])>1,"high/low lanes preserved");
                orders.Add(heights[0]>heights[1]);
                Check(Mathf.Abs(gap-(trial==0?0.55f:0.45f))<0.002f,"original normal/enraged two-shot rhythm");
                Check(Mathf.Abs(total-(trial==0?1.683333f:1.45f))<0.002f,"original overall SW01 duration");
            }
            Pass(pattern+" "+(trial==0?"normal":"enraged")+": releases ["+string.Join(",",launches.Select(t=>t.ToString("F4")))
                +"] s; complete="+(Time.time-began).ToString("F4")
                +(pattern==EnemyAttack.SW01?"; gap="+(launches[1]-launches[0]).ToString("F4"):"")
                +" s; continuous facing/warning tracking, last-frame crossings retain the advertised origin/direction, no added delay, fixed spawned velocity, original speed/hitboxes/VFX passed");
        }
        if(filter==null || filter=="SW01")Check(orders.Count==2,"both randomized SW01 firing orders preserved");
    }
    static async Task FrequencyOnly()
    {
        string[] args=Environment.GetCommandLineArgs();
        Func<string,string> option=name=>{int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:null;};
        string label=option("-sw04Case")??"current";
        float? overrideWeight=option("-sw04Weight") is string w?float.Parse(w,System.Globalization.CultureInfo.InvariantCulture):(float?)null;
        Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
        foreach(int seed in new[]{17,43,91})
        {
            Reset();var behavior=Get(enemy,"behavior");var counter=Get(behavior,"counter");
            if(overrideWeight.HasValue)game.Config.BossTuning.sword.Weights.sw04Weight=overrideWeight.Value;
            game.GameCamera.enabled=false;
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,2);
            game.Player.enabled=false;
            var body=game.Player.GetComponent<Rigidbody2D>();
            UnityEngine.Random.InitState(seed);
            Set(enemy,"running",false);Set(enemy,"timer",0f);Set(enemy,"patternStarted",-1f);
            float start=enemy.FightSeconds,lastStarted=-1,nextHit=0;
            var times=new List<float>();var counts=new Dictionary<EnemyAttack,int>();
            var categories=new Dictionary<string,float>();float sampledAt=-1;
            Exception failure=null;
            var probe=game.gameObject.AddComponent<KarmaSwordElectricRecorder>();
            probe.Tick=()=>
            {
                if(failure!=null)return;
                try
                {
                    float t=enemy.FightSeconds-start;
                    float cast=(float)Get(enemy,"patternStarted");
                    if(cast!=lastStarted && cast>=0)
                    {
                        lastStarted=cast;var pattern=(EnemyAttack)Get(enemy,"selected");
                        counts[pattern]=counts.TryGetValue(pattern,out int n)?n+1:1;
                        if(pattern==EnemyAttack.SW04)times.Add(t);
                    }
                    // Identical kinematic ground-running path and HP schedule in every version.
                    float target=KarmaStage.ArenaCenter+24*Mathf.Sin(t*0.12f+seed*0.07f);
                    float x=Mathf.MoveTowards(body.position.x,target,game.Config.moveSpeed*0.75f*Time.deltaTime);
                    var point=new Vector2(Mathf.Clamp(x,KarmaStage.ArenaLeft+2,KarmaStage.ArenaRight-2),KarmaSpace.PlayerHalfHeight+0.02f);
                    body.position=point;game.Player.transform.position=point;Physics2D.SyncTransforms();
                    if(t>=45 && enemy.Health>enemy.MaxHealth*0.45f)typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*0.45f);
                    if(t>=85 && enemy.Health>enemy.MaxHealth*0.20f)typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*0.20f);
                    if(t>=nextHit)
                    {
                        nextHit=t+0.75f;
                        if(Vector2.Distance(enemy.ClosestDamagePoint(point),point)<8)enemy.TakeDamage(0.001f);
                    }
                    if(t-sampledAt>=0.1f)
                    {
                        float dt=sampledAt<0?0:t-sampledAt;sampledAt=t;
                        bool formed=(bool)counter.GetType().GetProperty("Formed",F).GetValue(counter);
                        bool ground=GroundRunning;
                        float ready=(float)counter.GetType().GetProperty("NextReadyAt",F).GetValue(counter);
                        string reason=ground?"SW03":formed?"formed":enemy.FightSeconds<ready?"cooldown"
                            :(bool)Call(behavior,"SwordCandidateAvailable",EnemyAttack.SW04,enemy.Position,Mathf.Abs(x-enemy.Position.x),true,true)?"eligible":"rangeOrEligibility";
                        categories[reason]=categories.TryGetValue(reason,out float total)?total+dt:dt;
                    }
                }
                catch(Exception ex){failure=ex;}
            };
            await Until(()=>failure!=null||enemy.FightSeconds-start>=120,timeout:240);
            probe.Tick=null;UnityEngine.Object.Destroy(probe);game.GameCamera.enabled=true;game.Player.enabled=true;
            if(failure!=null)throw failure;
            Check(enemy.Alive && game.IsCombat,"controlled combat completes full 120 s");
            float avg=times.Count>1?(times.Last()-times.First())/(times.Count-1):0;
            Pass("FREQUENCY "+label+" seed="+seed+" weight="+game.Config.BossTuning.sword.Weights.sw04Weight.ToString("F2")
                +" sw04="+times.Count+" first="+(times.Count>0?times[0].ToString("F3"):"none")+" meanInterval="+avg.ToString("F3")
                +" times=["+string.Join(",",times.Select(t=>t.ToString("F3")))+"] counts="+string.Join(",",counts.OrderBy(p=>p.Key).Select(p=>p.Key+":"+p.Value))
                +" statusSeconds="+string.Join(",",categories.OrderBy(p=>p.Key).Select(p=>p.Key+":"+p.Value.ToString("F2"))));
        }
    }
    static async Task SW01()
    {
        Reset(); await Seconds(0.1f);
        bool low = false, high = false, repeated = false; bool? previous = null;
        for (int cast = 0; cast < 8 && !(low && high && repeated); cast++)
        {
            // The first two casts replay the same random sample on the SAME boss.
            // A forced alternation would fail even though both lanes still fire.
            UnityEngine.Random.InitState(cast < 2 ? 0 : cast - 1);
            var seen = new HashSet<KarmaBossAttack>(Root.GetComponentsInChildren<KarmaBossAttack>()); var heights = new List<float>();
            float cue = -1;
            Force(EnemyAttack.SW01);
            await Until(() => !(bool)Get(enemy, "running"), () =>
            {
                if (cue < 0)
                {
                    var mark = Root.GetComponentsInChildren<KarmaAttackWarning>().FirstOrDefault();
                    if (mark != null) cue = mark.transform.position.y;
                }
                foreach (var shot in Root.GetComponentsInChildren<KarmaBossAttack>()) if (seen.Add(shot))
                {
                    heights.Add(shot.transform.position.y);
                    Check(Mathf.Abs(((Vector2)Get(shot, "velocity")).x - game.Config.BossTuning.sword.swordSpeed * 1.3f) < 0.01f, "SW01 speed +30%");
                    Check((Vector2)Get(shot, "size") == (Vector2)Call(Get(enemy, "behavior"), "SwordSize", EnemyAttack.SW01), "SW01 shape unchanged");
                }
            });
            Check(heights.Count == 2, "SW01 exactly two shots");
            Check(Mathf.Abs(heights[0] - cue) < 0.05f, "first height matches visible lane cue");
            low |= heights[0] < heights[1]; high |= heights[0] > heights[1];
            bool highFirst = heights[0] > heights[1];
            repeated |= previous.HasValue && previous.Value == highFirst; previous = highFirst;
        }
        Check(low && high && repeated, "both SW01 orders and consecutive repeated order execute on same boss");
        Pass("SW01 independent cast order, consecutive repeats, locked cue, two shots, +30% speed, unchanged shape");
    }
    static object Pattern(EnemyAttack attack) => ((System.Collections.IDictionary)Get(Get(enemy, "behavior"), "patterns"))[attack];
    static async Task SW02()
    {
        Reset(); await Seconds(0.1f);
        var pattern = Pattern(EnemyAttack.SW02);
        var behavior = Get(enemy, "behavior");
        Vector2 size = (Vector2)Call(behavior, "SwordSize", EnemyAttack.SW02);
        Vector2 origin = (Vector2)Call(pattern, "AimedSlashOrigin", enemy.Position, 1f);
        float locked = (float)Call(pattern, "AimedSlashLockTime", origin, 1f, size);
        float safeX = origin.x - size.x / 2 - game.Player.GetComponent<Collider2D>().bounds.extents.x - 0.15f;
        float travel = Mathf.Max(0, game.Player.GetComponent<Collider2D>().bounds.center.x - safeX);
        float oldLock = Mathf.Max(0.9f, travel / (game.Config.moveSpeed * 0.9f) + 0.35f + 2 * Time.fixedDeltaTime);
        Check(locked < oldLock, "SW02 direction lock shortened with walking escape budget retained");
        float start = enemy.FightSeconds, released = -1;
        var seen = new HashSet<KarmaBossAttack>();
        Force(EnemyAttack.SW02);
        await Until(() => !(bool)Get(enemy, "running"), () =>
        {
            foreach (var shot in Root.GetComponentsInChildren<KarmaBossAttack>()) if (seen.Add(shot))
            {
                released = enemy.FightSeconds - start;
                Check(Mathf.Abs(((Vector2)Get(shot, "velocity")).x - Mathf.Max(0.1f, game.Config.BossTuning.sword.swordSpeed * 1.25f * 1.3f)) < 0.01f, "SW02 original configured speed restored");
                Check((Vector2)Get(shot, "size") == size, "SW02 original hit rectangle");
                Check(shot.Visual.sprite == KarmaWaveArtwork.Sprite("Sword_Crescent_v6"), "SW01/SW02 source sharing preserved");
            }
        });
        Check(seen.Count == 1 && Mathf.Abs(released - (0.34f + locked)) < 0.12f, "SW02 one release at shorter advertised timing");
        Pass("SW02 original configured speed, existing tracking/release and hit rectangle");
    }
    static void Capture(string name)
    {
        var rt = new RenderTexture(1600, 700, 24); var picture = new Texture2D(1600, 700, TextureFormat.RGBA32, false);
        var active = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(game.GameCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; picture.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0); picture.Apply();
            string folder = SessionState.GetBool(Key + ".ActiveSW03Only",false) ? "output/active-sw03" : SessionState.GetBool(Key + ".RequestedChanges",false) ? "output/requested-sw03-sw05" : "output/skill-refresh";
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(folder + "/" + name + ".png", picture.EncodeToPNG());
        }
        finally { RenderTexture.active = active; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(picture); }
    }
    static void Retreat()
    {
        Reset(); var move = Get(Get(enemy, "behavior"), "movement");
        Check(Mathf.Abs((float)Call(move, "SwordRetreatDistance") - game.Config.Techniques.pierceDistance * 1.75f) < 0.01f, "retreat 1.75 dash");
        Set(move, "swordAttackedSinceRetreat", true);
        Call(move, "RetreatSword", 1f / 60, 5f);
        float remaining = (float)Get(move, "nextSwordRetreat") - enemy.FightSeconds;
        Check(remaining > 2.1f, "live retreat cooldown exceeds previous 1.5 cap");
        Check((float)Get(move, "swordRetreatDirection") < 0, "retreat away from player");
        Call(move, "Interrupt", false);
        enemy.transform.position = new Vector2(KarmaStage.BossLeftWall + 0.5f, KarmaSpace.GuardianGroundY);
        object[] args = { 5f, 0f, 0f };
        Check((bool)move.GetType().GetMethod("ChooseRetreat", F).Invoke(move, args) && (float)args[1] > 0, "wall escape points inward");
        Pass("retreat 12 units, live cooldown 2.4 seconds, away priority and wall escape");
    }
    static async Task SW03()
    {
        await SW03Active(null, true);
    }
    static List<Vector2> SafeGaps(IEnumerable<Vector2> danger)
    {
        var gaps = new List<Vector2>(); float cursor = KarmaStage.ArenaLeft;
        foreach (var interval in danger.OrderBy(x => x.x))
        {
            float start = Mathf.Max(KarmaStage.ArenaLeft, interval.x), end = Mathf.Min(KarmaStage.ArenaRight, interval.y);
            if (start > cursor + 0.001f) gaps.Add(new Vector2(cursor, start));
            cursor = Mathf.Max(cursor, end);
        }
        if (cursor < KarmaStage.ArenaRight - 0.001f) gaps.Add(new Vector2(cursor, KarmaStage.ArenaRight));
        return gaps;
    }
    static Bounds RenderedBounds(SpriteRenderer source)
    {
        var node = new GameObject("SW03 focused world height render"); var camera = node.AddComponent<Camera>();
        node.AddComponent<UniversalAdditionalCameraData>(); camera.orthographic = true; camera.orthographicSize = 9;
        camera.aspect = 1; camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.transform.position = new Vector3(source.transform.position.x, 8, -10);
        var sample = new GameObject("supplied sprite render sample"); sample.layer = 31;
        var sr = sample.AddComponent<SpriteRenderer>(); sr.sprite = source.sprite; sr.sharedMaterial = source.sharedMaterial;
        sr.flipX = source.flipX; sr.flipY = source.flipY; sr.color = source.color;
        sample.transform.position = source.transform.position; sample.transform.rotation = source.transform.rotation; sample.transform.localScale = source.transform.lossyScale;
        var rt = new RenderTexture(512, 512, 24); var image = new Texture2D(512, 512, TextureFormat.RGBA32, false); var old = RenderTexture.active;
        try
        {
            Check(!UnityEditor.ShaderUtil.ShaderHasError(sr.sharedMaterial.shader), "supplied sprite material renders");
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
            var pixels = image.GetPixels32(); int x0 = 512, y0 = 512, x1 = -1, y1 = -1, blue = 0;
            for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
            {
                var c = pixels[y * 512 + x]; if (c.a < 128) continue;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                if (c.b > c.r * 1.2f && c.b > 70) blue++;
            }
            Check(y1 >= y0 && blue > 20, "visible supplied blue/cyan frame");
            float pixel = 18f / 512;
            return new Bounds(new Vector3(camera.transform.position.x + ((x0 + x1 + 1) / 2f - 256) * pixel, -1 + (y0 + y1 + 1) / 2f * pixel, 0),
                new Vector3((x1 - x0 + 1) * pixel, (y1 - y0 + 1) * pixel, 0));
        }
        finally { RenderTexture.active = old; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(sample); UnityEngine.Object.Destroy(node); }
    }
    static async Task JumpAcrossBlade()
    {
        Reset(); Call(enemy, "InterruptPattern"); Set(enemy, "selected", EnemyAttack.SW03); Set(enemy, "running", true);
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic; body.position = new Vector2(21f, KarmaSpace.PlayerHalfHeight + 0.03f);
        player.transform.position = body.position; body.linearVelocity = new Vector2(14, game.Config.jumpSpeed);
        Set(player, "invulnerableUntil", 0f); Physics2D.SyncTransforms();
        float before = player.Health;
        Vector2 scarSize = (Vector2)Pattern(EnemyAttack.SW03).GetType().GetProperty("ScarSize", F).GetValue(Pattern(EnemyAttack.SW03));
        var shot = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(35, scarSize.y / 2), Vector2.zero,
            scarSize, 2, 10, Color.white, null, 0, separateLaunch: true,
            extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(shot); shot.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, shot, scarSize, 2);
        var neighbor = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(43.75f, scarSize.y / 2), Vector2.zero,
            scarSize, 2, 10, Color.white, null, 0, separateLaunch: true, extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(neighbor); neighbor.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, neighbor, scarSize, 2);
        var drive = player.gameObject.AddComponent<KarmaSwordRefreshJumpDriver>(); drive.player = player;
        player.enabled = false;
        await Until(() => body.position.x > 39.5f || drive.age > 1.5f);
        Check(drive.slashed && body.position.x > 39.5f && player.Health == before, "jump + air slash crosses full-height blade without damage");
        Set(player, "move", 0f); UnityEngine.Object.Destroy(drive); player.enabled = true;
        await Until(() => body.position.y < KarmaSpace.PlayerHalfHeight + 0.10f, timeout: 6);
        var landed = player.GetComponent<Collider2D>().bounds;
        Check(landed.min.x > 35 + scarSize.x / 2 && landed.max.x < 43.75f - scarSize.x / 2 && player.Health == before, "safe landing between two live cells");
        Pass("real player jump attack crosses full SW03 cell into opposite safe side without damage");
    }
    static async Task DashAcrossBlade()
    {
        Reset(); Call(enemy, "InterruptPattern"); Set(enemy, "selected", EnemyAttack.SW03); Set(enemy, "running", true);
        Vector2 size = (Vector2)Pattern(EnemyAttack.SW03).GetType().GetProperty("ScarSize", F).GetValue(Pattern(EnemyAttack.SW03));
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        body.position = new Vector2(35 - size.x / 2 - KarmaSpace.PlayerHalfWidth - 0.03f, KarmaSpace.PlayerHalfHeight + 0.03f);
        player.transform.position = body.position; Set(player, "invulnerableUntil", 0f); Physics2D.SyncTransforms();
        var shot = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(35, size.y / 2), Vector2.zero, size, 1, 10,
            Color.white, null, 0, separateLaunch: true, extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(shot); shot.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, shot, size, 1);
        var neighbor = KarmaBossAttack.Spawn(game, enemy, Root, new Vector2(43.75f, size.y / 2), Vector2.zero, size, 1, 10,
            Color.white, null, 0, separateLaunch: true, extraWavePose: "SW03_RisingBlade_v5", sourcePattern: EnemyAttack.SW03);
        KarmaSwordAttackPolish.Core(neighbor); neighbor.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(game, enemy, neighbor, size, 1);
        float before = player.Health;
        Call(player, "BeginDirectionalSlash", Vector2.right, game.Config.Techniques.pierceDistance, Essence.SwordWave);
        Physics2D.SyncTransforms();
        Check(player.GetComponent<Collider2D>().bounds.min.x > 35 + size.x / 2, "real dash lands whole body beyond SW03 cell");
        Check(player.GetComponent<Collider2D>().bounds.max.x < 43.75f - size.x / 2, "dash arrival fits safe gap before next cell");
        await Seconds(0.25f); Check(player.Health == before, "dash avoids SW03 without lingering damage after invulnerability");
        Pass("SW03 real ground dash crosses the active cell and lands safely");
    }
    static async Task SW04Sequence()
    {
        Reset(); game.Player.transform.position = new Vector2(35,12); Physics2D.SyncTransforms();
        Force(EnemyAttack.SW04); var counter = Get(Get(enemy,"behavior"),"counter");
        await Until(() => Get(counter,"activeVolley") != null);
        var volley=Get(counter,"activeVolley");
        await Seconds(0.24f);
        foreach(var blade in (SpriteRenderer[])Get(volley,"Blades"))
            Check(blade.color.a>=0.96f,"fully formed front/rear SW04 blades remain opaque");
        Check(!ShaderUtil.ShaderHasError(Resources.Load<Material>("SwordWave/Shared/KarmaSwordSkillPolish").shader),"SW04 core shader compiles");
        Capture("sw04-solid-orbit");
        using var recording=SessionState.GetBool(Key+".AreaRecording",false)?new SkillRecording("sw04",false):null;
        float absorbed=enemy.FightSeconds; enemy.TakeDamage(1);
        Check((bool)Get(counter,"Guarding"),"absorbing guard retains its defensive state");
        var released = new float[4];
        await Until(() => released[3]>0, () =>
        {
            var shots=(KarmaBossAttack[])Get(volley,"Shots");
            for(int i=0;i<4;i++) if(shots[i]!=null && released[i]==0)
            {
                released[i]=enemy.FightSeconds;
                var picture=shots[i].Visual;
                Check(picture.color.a>=0.96f,"released SW04 blade preserves opaque core");
                Check(!shots[i].GetComponentsInChildren<TrailRenderer>().Any(t=>t.enabled && t.emitting),"no lines trail a released counter blade");
                if(i==0)Capture("sw04-solid-flight");
                Check(picture.sprite==KarmaArtwork.Sprite("SwordBossVFX","SW05_HeavyJudgementSword_optional"),"SW04 flight uses the same straight blade as its orbit");
                var axis=((Vector2)Get(shots[i],"velocity")).normalized;
                Check(Vector2.Dot(-(Vector2)picture.transform.up,axis)>.999f,"straight sword tip follows actual projectile velocity");
                var size=(Vector2)Get(shots[i],"size");
                Check(Mathf.Abs(picture.transform.lossyScale.y*968/100-size.x*0.88f)<.001f
                    && Mathf.Abs(picture.transform.lossyScale.x*396/100-size.y)<.001f,"counter silhouette is twelve percent shorter within the original damage footprint");
            }
        });
        float tracking=(float)counter.GetType().GetProperty("GuardTrackingTime",F).GetValue(counter);
        float locked=(float)counter.GetType().GetProperty("GuardLockTime",F).GetValue(counter);
        float gap=(float)counter.GetType().GetProperty("GuardShotGap",F).GetValue(counter);
        Check(Mathf.Abs(released[0]-absorbed-tracking-locked)<0.08f,"first counter keeps tracking and lock timing");
        for(int i=1;i<4;i++) Check(Mathf.Abs(released[i]-released[i-1]-tracking-locked-gap)<0.09f,"all four counter shots retain their cadence");
        await Until(() => Get(counter,"activeVolley")==null);
        await Seconds(0.15f); // SW04's three-frame cosmetic fade owns no attack slot.
        Check(Root.GetComponentInChildren<KarmaSwordWardVfx>()==null,"ward cleans up after all four projectiles finish");
        Pass("SW04 full four-shot sequence, original tracking/lock/gap timing, defensive state and normal cleanup");
    }
    static async Task SW04()
    {
        Reset(); Force(EnemyAttack.SW04); var counter = Get(Get(enemy, "behavior"), "counter");
        await Until(() => Get(counter, "activeVolley") != null);
        var ward=Root.GetComponentInChildren<KarmaSwordWardVfx>();Check(ward!=null,"one native SW04 barrier exists");
        int initialFrame=ward.FrameIndex;await Seconds(0.24f);
        using var recording = SessionState.GetBool(Key+".AreaRecording",false) ? new SkillRecording("sw04",false) : null;
        Check(ward.FrameIndex!=initialFrame && Root.GetComponentsInChildren<KarmaSwordWardVfx>().Length==1,"one 24fps dome advances without duplicate formations");
        foreach(var blade in (SpriteRenderer[])Get(Get(counter,"activeVolley"),"Blades"))
        {
            Check(blade.enabled,"four orbit blades remain visible on the single dome");
            Check(blade.sprite==KarmaArtwork.Sprite("SwordBossVFX","SW05_HeavyJudgementSword_optional"),"orbit reuses silver/cyan straight sword artwork");
        }
        Capture("sw04-grand-orbit");enemy.TakeDamage(1);
        await Seconds(0.06f);Capture("sw04-grand-absorb");
        await Seconds(0.34f);Capture("sw04-grand-locked-warning");
        await Until(() => ((KarmaBossAttack[])Get(Get(counter, "activeVolley"), "Shots"))[0] != null);
        Capture("sw04-counter");
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 2);
        var volley = Get(counter, "activeVolley");
        var shot = ((KarmaBossAttack[])Get(volley, "Shots"))[0];
        var contacts = (KarmaAttackContact[])Get(volley, "Contacts"); var slots = (Transform[])Get(volley, "Slots");
        Set(game.Player, "invulnerableUntil", 0f); game.Player.BeginCounter();
        shot.transform.position = game.Player.GetComponent<Collider2D>().bounds.center; Set(shot, "velocity", Vector2.zero);
        int parries = game.Player.ParryCount; Call(shot, "AdvanceFlight", Time.time + Time.fixedDeltaTime);
        Check(game.Player.ParryCount == parries + 1, "SW04 actual contact parried");
        for (int i = 1; i < 4; i++) Check(contacts[i].Consumed && !slots[i].gameObject.activeInHierarchy, "SW04 successors synchronously canceled");
        Check(game.World.GetComponentInChildren<KarmaSwordParryFeedback>() != null, "SW04 success feedback active"); Capture("sw04-parry");
        Check(!enemy.Stunned && !(bool)Get(enemy,"pendingParry"), "parry contact alone does not electrocute boss");
        Check(ward != null && !(bool)Get(ward,"collapsed") && (bool)Get(volley,"Returning"), "barrier survives while reflected blade is flying");
        await Until(() => !(bool)Get(volley,"Returning"));
        Check(ward == null || (bool)Get(ward,"collapsed"), "reflected hit breaks barrier");
        await Until(() => enemy.Stunned); await Seconds(0.1f);
        float left = (float)Get(enemy, "stunnedUntil") - enemy.FightSeconds;
        Check(left > 1.8f && left <= 2, "SW04 two-second stun");
        Check(game.World.GetComponentsInChildren<KarmaSwordElectrocution>().Any(e => e.IsBoss), "electrical stun visible"); Capture("sw04-stun");
        await Seconds(2); Check(!enemy.Stunned, "SW04 stun ends after two seconds");
        Reset(); typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 2); Force(EnemyAttack.SW05);
        Call(Get(enemy, "behavior"), "OnAttackParried", true); await Seconds(0.1f);
        Check(!enemy.Stunned && !(bool)Get(enemy, "pendingParry") && (bool)Get(enemy, "running"), "SW05 ignores parry stun even before final stage");
        Pass("SW04 actual parry cancels successors, visible success/stun, two seconds; SW05 parry stun immunity retained");
    }
    static KarmaSwordElectrocution PlayerElectric => game.World.GetComponentsInChildren<KarmaSwordElectrocution>().FirstOrDefault(e=>!e.IsBoss);
    static KarmaAttackContact PlayerContact(EnemyAttack pattern, float damage, KarmaAttackContact contact = null)
    {
        contact = contact ?? new KarmaAttackContact();
        Physics2D.SyncTransforms();
        Vector2 at = game.Player.GetComponent<Collider2D>().bounds.center;
        enemy.TryAttackHit(at,at,Vector2.one,damage,contact,0,sourcePattern:pattern);
        return contact;
    }
    static async Task ShockRouting()
    {
        foreach (var pattern in new[] { EnemyAttack.SW04, EnemyAttack.SW05 })
        foreach (string defense in new[] { "guard", "invulnerable", "test", "parry", "parryWithoutRiposte" })
        {
            ResetElectric();var player=game.Player;Set(player,"invulnerableUntil",0f);
            float before=player.Health, taken=game.DamageTaken, bossHealth=enemy.Health;
            int parries=player.ParryCount;
            var contact=new KarmaAttackContact(parryWithoutRiposte:defense=="parryWithoutRiposte");
            if(pattern==EnemyAttack.SW05)Call(Get(enemy,"behavior"),"RegisterFinalDetonation",contact);
            if(defense=="guard")player.BeginGuard(1);
            else if(defense=="invulnerable")Set(player,"invulnerableUntil",Time.time+1);
            else if(defense=="test")player.ToggleTestInvincibility();
            else
            {
                player.BeginCounter();
                if(defense=="parryWithoutRiposte")Set(player,"invulnerableUntil",Time.time+1);
            }
            PlayerContact(pattern,5,contact);
            Check(contact.Consumed && !contact.DealtDamage && player.Health==before && game.DamageTaken==taken,
                pattern+" / "+defense+" consumes contact without damage");
            Check(!player.Shocked && PlayerElectric==null,pattern+" / "+defense+" rejects Shock and presentation");
            if(defense.StartsWith("parry"))Check(player.ParryCount==parries+1,"parry option survives CC dispatch");
            if(defense=="parryWithoutRiposte")Check(enemy.Health==bossHealth,"no riposte damage through special parry contract");
        }
        foreach(var pattern in new[] {EnemyAttack.SWBasic,EnemyAttack.SW01,EnemyAttack.SW02,EnemyAttack.SW03,EnemyAttack.SW05})
        {
            ResetElectric();Set(game.Player,"invulnerableUntil",0f);
            // Even a registered final blast must not classify another contact.
            Call(Get(enemy,"behavior"),"RegisterFinalDetonation",new KarmaAttackContact());
            float before=game.Player.Health;
            var contact=PlayerContact(pattern,5);
            Check(contact.DealtDamage && Mathf.Abs(before-game.Player.Health-5)<0.001f && !game.Player.Shocked && PlayerElectric==null,
                pattern+" ordinary contact retains damage without Shock");
        }
        // Real falling geometry goes through the same dispatcher, with its own ledger.
        ResetElectric();Set(enemy,"selected",EnemyAttack.SW05);Set(game.Player,"invulnerableUntil",0f);
        Vector2 center=game.Player.GetComponent<Collider2D>().bounds.center;
        float health=game.Player.Health;
        var spec=new KarmaAerialSwordPlanner.Shot {origin=center+Vector2.up*0.25f,
            velocity=Vector2.down*20,size=new Vector2(2,1),life=0.1f};
        var falling=KarmaSW05RainBlade.Spawn(game,enemy,Root,spec,5,null);
        Call(falling,"AdvanceFlight", Time.time + Time.fixedDeltaTime);
        Check(Mathf.Abs(health-game.Player.Health-5)<0.001f && !game.Player.Shocked,"real falling blade damages without Shock");

        // A later entry during the original active window still receives CC.
        ResetElectric();var player2=game.Player;Set(player2,"invulnerableUntil",0f);
        var body=player2.GetComponent<Rigidbody2D>();
        body.position=new Vector2(40,KarmaSpace.PlayerHalfHeight+0.02f);player2.transform.position=body.position;
        Physics2D.SyncTransforms();
        spec=new KarmaAerialSwordPlanner.Shot {origin=new Vector2(35,5.05f),velocity=Vector2.down*20,size=new Vector2(6,1),life=0.1f};
        var blades=new List<KarmaSW05RainBlade>();
        for(int i=0;i<2;i++)blades.Add(KarmaSW05RainBlade.Spawn(game,enemy,Root,spec,10,null));
        await Until(()=>blades.All(b=>b.Landed));
        var ledger=new KarmaAttackContact();Call(Get(enemy,"behavior"),"RegisterFinalDetonation",ledger);
        foreach(var blade in blades)blade.Explode(ledger);
        Check(!ledger.Consumed,"missed initial blast remains live for later entry");
        await Seconds(0.035f);
        body.position=new Vector2(35,KarmaSpace.PlayerHalfHeight+0.02f);player2.transform.position=body.position;Physics2D.SyncTransforms();
        health=player2.Health;float takenBefore=game.DamageTaken;
        foreach(var blade in blades)Call(blade,"AdvanceFlight", Time.time + Time.fixedDeltaTime);
        float expected=10/game.Config.BossTuning.sword.aerialSwordDamageMultiplier;
        Check(ledger.DealtDamage && Mathf.Abs(health-player2.Health-expected)<0.001f
            && Mathf.Abs(game.DamageTaken-takenBefore-expected)<0.001f && player2.Shocked,"late entry gets exactly one blast damage and Shock");
        float deadline=(float)Get(player2,"shockUntil");var fx=PlayerElectric;
        Check(fx!=null && Mathf.Abs(deadline-Time.time-0.75f)<0.001f,"SW05 shares real 0.75-second player electricity");
        await Until(()=>blades.All(b=>b==null),()=>
        {
            Set(player2,"invulnerableUntil",0f);
            Check((float)Get(player2,"shockUntil")==deadline && Mathf.Abs(health-player2.Health-expected)<0.001f,"overlap/active frames cannot refresh Shock or repeat damage");
        });
        await Seconds(0.04f);Check(Get(Get(enemy,"behavior"),"finalDetonationContact")==null,"contact registration expires after damaging window");

        // A legitimate new hit refreshes the status, retaining one visual instance.
        await Seconds(0.15f);Set(player2,"invulnerableUntil",0f);PlayerContact(EnemyAttack.SW04,5);
        float refreshed=(float)Get(player2,"shockUntil");
        Check(refreshed>deadline && Mathf.Abs(refreshed-Time.time-0.75f)<0.001f && PlayerElectric==fx,"Shock refresh reuses existing visual");
        Set(player2,"invulnerableUntil",0f);player2.ApplyShock(0.1f);
        Check((float)Get(player2,"shockUntil")==refreshed,"shorter Shock cannot shorten existing deadline");
        await Seconds(0.05f);
        var picture=(SpriteRenderer)Get(fx,"picture");
        var home=(Vector3)Get(fx,"home");var rotation=(Quaternion)Get(fx,"rotation");var tint=(Color)Get(fx,"color");
        Call(game,"TogglePause");Call(fx,"LateUpdate");
        try
        {
            Check(PlayerElectric==fx && !(bool)Get(fx,"painted") && picture.transform.localPosition==home
                && picture.transform.localRotation==rotation && picture.color==tint,"pause restores sprite without destroying live effect");
            Check(fx.GetComponentsInChildren<LineRenderer>().All(l=>!l.enabled),"paused electrical lines hidden");
            float pausedTime=Time.time;await Task.Delay(120);
            Check(Time.time==pausedTime && player2.Shocked,"pause freezes the real Shock deadline");
        }
        finally { if(game.Paused)Call(game,"TogglePause"); }
        await Seconds(0.05f);Check(PlayerElectric==fx && (bool)Get(fx,"painted"),"same effect resumes without duplication");
        Call(Get(enemy,"behavior"),"RegisterFinalDetonation",new KarmaAttackContact());
        Call(enemy,"InterruptPattern");
        Check(Get(Get(enemy,"behavior"),"finalDetonationContact")==null && player2.Shocked,"interrupt clears blast registration without shortening applied Shock");
        await Until(()=>!player2.Shocked);await Seconds(0.04f);
        Check(PlayerElectric==null && !player2.ControlLocked,"effect and control lock end together after refresh");

        foreach(string ending in new[]{"interrupt","cancel","death"})
        {
            ResetElectric();Call(Get(enemy,"behavior"),"RegisterFinalDetonation",new KarmaAttackContact());
            if(ending=="interrupt")Call(enemy,"InterruptPattern");
            else if(ending=="cancel")enemy.CancelCombat();
            else enemy.TakeDamage(enemy.MaxHealth*2);
            Check(Get(Get(enemy,"behavior"),"finalDetonationContact")==null,"blast registration clears on "+ending);
        }
        // CC's false return on a lethal hit must not suppress contact accounting.
        ResetElectric();Set(game.Player,"invulnerableUntil",0f);
        var lethal=PlayerContact(EnemyAttack.SW04,game.Player.Health*2);
        Check(game.Player.Health==0 && lethal.DealtDamage && !game.Player.Shocked && PlayerElectric==null,"lethal damage recorded although CC is rejected");

        foreach(var essence in new[]{Essence.Guard,Essence.RisingSlash})
        {
            typeof(KarmaSwordPlayChecks).GetField("game",F).SetValue(null,game);
            typeof(KarmaSwordPlayChecks).GetMethod("Reset",F).Invoke(null,new object[]{essence});
            enemy=(KarmaEnemy)typeof(KarmaSwordPlayChecks).GetField("enemy",F).GetValue(null);
            var behavior=Get(enemy,"behavior");
            Check(behavior.GetType().GetMethod("TryPlayerCrowdControl",F).DeclaringType==behavior.GetType().BaseType,"other boss inherits disabled CC hook");
            Set(game.Player,"invulnerableUntil",0f);health=game.Player.Health;
            PlayerContact(EnemyAttack.GroundStrike,5);
            Check(Mathf.Abs(health-game.Player.Health-5)<0.001f && !game.Player.Shocked,"other boss ordinary damage unchanged");
            Set(game.Player,"invulnerableUntil",0f);
            Check(game.Player.TakeCrowdControlHit(1,KarmaPlayer.CrowdControlKind.Airborne,0.2f,Vector2.up*5)
                && game.Player.KnockedAirborne,"existing public damage-plus-Airborne API remains compatible");
        }
        Pass("Shock routing, rejected contacts, one-hit ledger, refresh, pause, interruption, lethal accounting and default-disabled other-boss hook");
    }
    static void ResetElectric()
    {
        Reset();
        var behavior=Get(enemy,"behavior");
        Set(behavior,"PracticeHold",true);
        Set(Get(behavior,"ultimate"),"thresholdsUsed",2);
    }
    static async Task Electrocution()
    {
        foreach(bool enraged in new[]{false,true})
        {
            ResetElectric();
            if(enraged)typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*0.1f);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,2);
            var player=game.Player;var body=player.GetComponent<Rigidbody2D>();
            Force(EnemyAttack.SW04);var counter=Get(Get(enemy,"behavior"),"counter");
            await Until(()=>Get(counter,"activeVolley")!=null);
            var volley=Get(counter,"activeVolley");enemy.TakeDamage(1);
            await Until(()=>((KarmaBossAttack[])Get(volley,"Shots"))[0]!=null);
            var shot=((KarmaBossAttack[])Get(volley,"Shots"))[0];
            float damage=(float)Get(shot,"damage"),health=player.Health;
            Check(Mathf.Abs(((Vector2)Get(shot,"velocity")).magnitude-game.Config.BossTuning.sword.swordSpeed)<0.001f,"counter speed retained");
            shot.transform.position=body.position;Set(shot,"velocity",Vector2.zero);
            // An immune contact consumes its ledger without producing electricity.
            Call(shot,"AdvanceFlight", Time.time + Time.fixedDeltaTime);Check(PlayerElectric==null && player.Health==health,"immune contact does not electrify");
            // Exercise actual hit resolution, with a fresh ledger, after immunity.
            Set(player,"invulnerableUntil",0f);var hit=new KarmaAttackContact();float began=Time.time;
            enemy.TryAttackHit(body.position,body.position,Vector2.one,damage,hit,0,sourcePattern:EnemyAttack.SW04);
            var fx=PlayerElectric;
            Check(fx!=null && Mathf.Abs(health-player.Health-damage)<0.001f,"only confirmed unchanged SW04 damage triggers player effect");
            float invulnerable=(float)Get(player,"invulnerableUntil");
            Check(Mathf.Abs(invulnerable-Time.time-game.Config.hitInvulnerability)<0.001f,"normal hit immunity remains exactly unchanged");
            Check(player.Shocked && player.ControlLocked && !player.Stunned,"Shock locks controls without stun immunity");
            Check(Mathf.Abs((float)Get(player,"shockUntil")-Time.time-0.75f)<0.001f,"exact 0.75-second Shock deadline");
            Check(hit.Consumed && hit.DealtDamage,"one consumed and damaging contact");
            // Isolate the duration check from later shots; the full four-shot
            // cadence and parry-cancellation trials below remain unchanged.
            Call(counter,"Interrupt");
            var frames=new HashSet<int>();bool moved=false,attacked=false;
            ElectricRecording recording=enraged?null:new ElectricRecording(game,player.transform,"player",1.15f);
            await Until(()=>fx==null || !fx.gameObject.activeInHierarchy,()=>
            {
                if(fx==null || !player.Shocked)return;
                frames.Add(fx.FrameIndex);
                Check(player.ControlLocked && !player.Stunned,"real Shock remains the sole new control lock");
                if(Time.time-began>=0.23f && !moved)
                {
                    moved=true;Set(player,"move",1f);Call(player,"FixedUpdate");
                    Check(body.linearVelocity.x==0,"Shock blocks horizontal movement");
                    player.transform.localScale=new Vector3(-1,1,1);
                }
                if(Time.time-began>=0.3f && !attacked)
                {attacked=true;Set(player,"nextSword",0f);Call(player,"Attack");Check((float)Get(player,"nextSword")==0,"Shock blocks sword attacks");}
            });
            float duration=(float)(fx.EndedAt-fx.StartedAt);
            Check(duration>=0.74f && duration<0.79f && moved && attacked && !player.Shocked,"visual ends with 0.75-second Shock");
            Check(frames.Count>=8,"12 FPS electrical frames advance during real Shock");
            player.transform.localScale=Vector3.one;
            if(recording!=null){await Seconds(0.17f);recording.Dispose();}
            Pass("SW04 player "+(enraged?"enraged":"normal")+": one damage event, exact 0.75 s Shock, unchanged hit immunity and synchronized visual duration="+duration.ToString("F4"));

            // A separate real four-shot cast demonstrates no redesign of SW04.
            ResetElectric();if(enraged)typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*0.1f);
            var far=new Vector2(35,12);game.Player.transform.position=far;game.Player.GetComponent<Rigidbody2D>().position=far;Physics2D.SyncTransforms();
            Force(EnemyAttack.SW04);counter=Get(Get(enemy,"behavior"),"counter");
            await Until(()=>Get(counter,"activeVolley")!=null);volley=Get(counter,"activeVolley");enemy.TakeDamage(1);
            var released=new float[4];
            await ObserveEveryFrame(()=>released[3]>0,()=>
            {var shots=(KarmaBossAttack[])Get(volley,"Shots");for(int i=0;i<4;i++)if(shots[i]!=null&&released[i]==0)released[i]=enemy.FightSeconds;});
            float cadence=(float)counter.GetType().GetProperty("GuardTrackingTime",F).GetValue(counter)
                +(float)counter.GetType().GetProperty("GuardLockTime",F).GetValue(counter)
                +(float)counter.GetType().GetProperty("GuardShotGap",F).GetValue(counter);
            for(int i=1;i<4;i++)Check(Mathf.Abs(released[i]-released[i-1]-cadence)<0.07f,"all four counter releases retain existing cadence: actual=" + (released[i]-released[i-1]) + " expected=" + cadence);
            Pass("SW04 "+(enraged?"enraged":"normal")+": all four counters and original cadence passed");

            ResetElectric();if(enraged)typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*0.1f);
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,2);
            Force(EnemyAttack.SW04);counter=Get(Get(enemy,"behavior"),"counter");
            await Until(()=>Get(counter,"activeVolley")!=null);volley=Get(counter,"activeVolley");enemy.TakeDamage(1);
            await Until(()=>((KarmaBossAttack[])Get(volley,"Shots"))[0]!=null);
            shot=((KarmaBossAttack[])Get(volley,"Shots"))[0];
            var slots=(Transform[])Get(volley,"Slots");var contacts=(KarmaAttackContact[])Get(volley,"Contacts");
            Set(game.Player,"invulnerableUntil",0f);game.Player.BeginCounter();int parries=game.Player.ParryCount;
            shot.transform.position=game.Player.GetComponent<Collider2D>().bounds.center;Set(shot,"velocity",Vector2.zero);Call(shot,"AdvanceFlight", Time.time + Time.fixedDeltaTime);
            Check(game.Player.ParryCount==parries+1 && PlayerElectric==null && !game.Player.Shocked,"real parry never electrocutes player");
            for(int i=1;i<4;i++)Check(contacts[i].Consumed && !slots[i].gameObject.activeInHierarchy,"same-frame successor cancellation preserved");
            await Until(()=>enemy.Stunned);float stunStart=enemy.FightSeconds;
            Check(Mathf.Abs((float)Get(enemy,"stunnedUntil")-stunStart-2)<=2*Time.fixedDeltaTime+0.001f,"host owns exact existing 2 second deadline");
            var position=enemy.Position;bool activeElectrical=false;var bossFrames=new HashSet<int>();
            recording=enraged?null:new ElectricRecording(game,enemy.transform,"boss",2.2f);
            await Until(()=>!enemy.Stunned,()=>
            {
                if(!enemy.Stunned)return;
                Check(!(bool)Get(enemy,"running") && enemy.Position==position,"boss cannot act or move during existing stun");
                Check(Root.GetComponentsInChildren<KarmaBossAttack>().Length==0,"no cancelled follow-up projectile respawns");
                var electric=game.World.GetComponentsInChildren<KarmaSwordElectrocution>().FirstOrDefault(e=>e.IsBoss);
                if(electric!=null){activeElectrical=true;bossFrames.Add(electric.FrameIndex);}
                Check(!game.World.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="Stun orbit" && l.gameObject.activeInHierarchy),"old SW04 dizzy orbit replaced");
            });
            float stunDuration=enemy.FightSeconds-stunStart;
            Check(Mathf.Abs(stunDuration-2)<=2*Time.fixedDeltaTime+0.002f && activeElectrical && bossFrames.Count>=18,"electricity follows unchanged host stun duration");
            await Seconds(0.22f);if(recording!=null)recording.Dispose();
            Check(game.World.GetComponentsInChildren<KarmaSwordElectrocution>().Length==0,"no electric effect lingers after stun");
            Check(((KarmaCharacterAnimator)Get(enemy,"animator")).CurrentPose!="Hurt","normal AI presentation resumes");
            Set(Get(enemy,"behavior"),"PracticeHold",false);Set(enemy,"timer",0f);
            await Until(()=>(bool)Get(enemy,"running"));Check(!enemy.Stunned,"original AI can select an action after existing stun");
            Pass("SW04 boss "+(enraged?"enraged":"normal")+": actual parry stun="+stunDuration.ToString("F4")+" s, exact original timer/action block, successor cleanup/no respawn, electrical animation and recovery passed");
        }
        foreach(string ending in new[]{"death","disable","combat","status"})
        {
            ResetElectric();Set(game.Player,"invulnerableUntil",0f);game.Player.ApplyShock(0.75f);KarmaSwordElectrocution.ShowPlayer(game);await Seconds(0.05f);
            Check(PlayerElectric!=null,"live visual before "+ending);
            if(ending=="death")game.Player.TakeDamage(game.Player.Health*2); // clear initial test immunity below when needed
            if(ending=="death"){Set(game.Player,"invulnerableUntil",0f);game.Player.TakeDamage(game.Player.Health*2);}
            else if(ending=="disable")game.Player.enabled=false;
            else if(ending=="combat")game.PlayerDied();
            else { game.Player.Stun(0.12f); Call(game.Player,"ClearStatus"); }
            if(ending=="status") {await Seconds(0.05f);Check(PlayerElectric==null,"cleared Shock removes visual despite overlapping ordinary stun");}
            else await Until(()=>PlayerElectric==null,timeout:3);
        }
        foreach(string ending in new[]{"death","disable","combat"})
        {
            ResetElectric();var feedback=enemy.GetComponent<KarmaSwordStunFeedback>();
            var state=enemy.GetType().GetField("state",F);state.SetValue(enemy,Enum.Parse(state.FieldType,"Stunned"));
            Set(enemy,"stunnedUntil",enemy.FightSeconds+2);feedback.ArmElectrocution();
            await Seconds(0.05f);Check(game.World.GetComponentsInChildren<KarmaSwordElectrocution>().Any(e=>e.IsBoss),"boss electric before "+ending);
            if(ending=="death")enemy.TakeDamage(enemy.MaxHealth*2);
            else if(ending=="disable")enemy.enabled=false;
            else enemy.CancelCombat();
            await Until(()=>game.World.GetComponentsInChildren<KarmaSwordElectrocution>().Length==0,timeout:3);
        }
        ResetElectric();typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,2);
        Set(enemy,"pendingParry",true);await Until(()=>enemy.Stunned);await Seconds(0.05f);
        Check(!enemy.GetComponent<KarmaSwordStunFeedback>().Electrocuted && game.World.GetComponentsInChildren<KarmaSwordElectrocution>().Length==0,"unrelated sword stuns keep their existing presentation");
        // Immune phases continue to use their original policy.
        ResetElectric();Force(EnemyAttack.SW05);Call(Get(enemy,"behavior"),"OnAttackParried",true);await Seconds(0.1f);
        Check(!enemy.Stunned && !enemy.GetComponent<KarmaSwordStunFeedback>().Electrocuted,"SW05 immunity preserved");
        Pass("SW04 death/disable/combat/overlapping-status cleanup and SW05 immunity passed");
    }
    sealed class SkillRecording : IDisposable
    {
        readonly RenderTexture target=new RenderTexture(960,420,24);
        readonly Texture2D pixels=new Texture2D(960,420,TextureFormat.RGBA32,false);
        readonly KarmaSwordElectricRecorder recorder;
        readonly string folder;
        readonly bool waitForRain;
        readonly string waitForKind;
        readonly List<double> costs=new List<double>();
        float began=-1,last=-1;int frames;
        public SkillRecording(string name,bool rain,string kind=null)
        {
            folder="output/area-vfx/"+name;waitForRain=rain;waitForKind=kind;
            System.IO.Directory.CreateDirectory(folder);
            recorder=game.gameObject.AddComponent<KarmaSwordElectricRecorder>();recorder.Tick=Paint;
        }
        void Paint()
        {
            if(began<0)
            {
                if(waitForRain&&!game.World.GetComponentsInChildren<SpriteRenderer>()
                    .Any(e=>e.enabled && e.name=="SW05 낙하 지면 전조"))return;
                if(waitForKind!=null&&!game.World.GetComponentsInChildren<KarmaSW05Energy>()
                    .Any(e=>Get(e,"kind").ToString()==waitForKind))return;
                began=Time.time;
            }
            float age=Time.time-began;
            if(age>1.8f||age-last<1f/60-0.001f)return;
            last=age;var clock=System.Diagnostics.Stopwatch.StartNew();
            RenderPipeline.SubmitRenderRequest(game.GameCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            clock.Stop();costs.Add(clock.Elapsed.TotalMilliseconds);
            var old=RenderTexture.active;
            try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,960,420),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(folder+"/"+(frames++).ToString("D3")+".png",pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;}
        }
        public void Dispose()
        {
            recorder.Tick=null;UnityEngine.Object.Destroy(recorder);
            UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);
            costs.Sort();
            if(frames<50){Debug.LogWarning(folder+" incomplete recording: "+frames+" frames");return;}
            Pass(folder+" frames="+frames+" render-request CPU median="+costs[costs.Count/2].ToString("F3")+"ms");
        }
    }
    sealed class ScarRecording : IDisposable
    {
        Camera camera; RenderTexture target; Texture2D pixels; KarmaSwordElectricRecorder recorder;
        float began,last=-1; string folder; int count; bool sawDelay,sawIgnition,sawResidual,sawDescent; readonly List<double> costs=new List<double>();
        public ScarRecording(bool enraged)
        {
            began=Time.time;folder="output/sw03-polish/"+(enraged?"enraged":"normal");System.IO.Directory.CreateDirectory(folder);
            camera=new GameObject("SW03 visual QA camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=14.5f;camera.aspect=960f/360;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.025f,0.045f,0.09f);
            camera.transform.position=new Vector3(KarmaStage.ArenaCenter,9,-10);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            target=new RenderTexture(960,360,24);pixels=new Texture2D(960,360,TextureFormat.RGBA32,false);
            recorder=game.gameObject.AddComponent<KarmaSwordElectricRecorder>();recorder.Tick=Paint;
        }
        void Paint()
        {
            float age=Time.time-began;if(age>4.5f || age-last<1f/60-0.001f)return;last=age;
            foreach(var aura in game.World.GetComponentsInChildren<KarmaSwordScarAura>())
            {
                float auraAge=(float)Get(aura,"age"),tail=(float)Get(aura,"tailAge");
                var properties=new MaterialPropertyBlock();aura.GetComponent<SpriteRenderer>().GetPropertyBlock(properties);
                float opacity=properties.GetFloat("_Fade");
                if(auraAge<KarmaSwordScarRise.AuraDelay && opacity==0)sawDelay=true;
                if(auraAge>0.12f && tail==0 && opacity>0.1f)sawIgnition=true;
                if(tail>KarmaSwordScarRise.BladeFadeSeconds+0.02f && opacity>0.01f)sawResidual=true;
            }
            foreach(var blade in game.World.GetComponentsInChildren<KarmaSwordScarFade>())
            {
                float tail=(float)Get(blade,"age"),height=(float)Get(blade,"bladeHeight");
                var anchor=(Vector3)Get(blade,"anchor");
                float descent=KarmaSwordScarRise.RetractionDistance(tail,height);
                Check(Mathf.Abs(blade.transform.position.y-(anchor.y-descent))<0.001f,"residual sword follows continuous descent");
                Check(blade.GetComponent<KarmaBossAttack>()==null,"descending sword is cosmetic only");
                if(tail>0.06f && tail<0.16f && descent>height*0.1f)sawDescent=true;
            }
            var layers=game.World.GetComponentsInChildren<KarmaSW05Energy>();
            Check(layers.All(l=>!l.CapacityExceeded),"all simultaneously planned swords fit energy mesh budgets");
            var clock=System.Diagnostics.Stopwatch.StartNew();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            clock.Stop();
            if(SW03PlannedCount>0 && Root.GetComponentsInChildren<KarmaSwordScarRise>().Count(v=>Get(v,"sequence").ToString()=="Blade")==SW03PlannedCount)costs.Add(clock.Elapsed.TotalMilliseconds);
            var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,960,360),0,0);pixels.Apply();RenderTexture.active=old;
            System.IO.File.WriteAllBytes(folder+"/"+(count++).ToString("D3")+".png",pixels.EncodeToPNG());
        }
        public void Dispose()
        {
            Check(sawDelay && sawIgnition && sawResidual && sawDescent,"delayed aura ignition, continuous contact-free sinking and final fade");
            Check(!ShaderUtil.ShaderHasError(Resources.Load<Material>("SwordWave/SW03/KarmaSwordRisePolish").shader),"split sword/aura shader compiles");
            Pass("SW03: delayed aura, continuous cosmetic descent and residual cleanup verified in Play Mode");
            Check(count>150 && costs.Count>10,"real-time warning/rise/active/fade and repeated-wave footage captured");
            costs.Sort();Pass("SW03 RENDER "+folder+" frames="+count+" maximum-density SubmitRenderRequest CPU ms median="+costs[costs.Count/2].ToString("F3")+" p95="+costs[(int)(costs.Count*0.95)].ToString("F3")+" (readback/PNG excluded; not GPU frame time)");
            recorder.Tick=null;UnityEngine.Object.Destroy(recorder);UnityEngine.Object.Destroy(camera.gameObject);UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);
        }
    }
    sealed class ElectricRecording : IDisposable
    {
        Camera camera;RenderTexture target;Texture2D pixels;KarmaSwordElectricRecorder recorder;
        float began,last=-1,duration;Transform subject;string folder;int count;
        public ElectricRecording(KarmaGame game,Transform subject,string name,float duration)
        {
            this.subject=subject;this.duration=duration;began=Time.time;
            folder="output/sw04-electrocution/"+name;System.IO.Directory.CreateDirectory(folder);
            camera=new GameObject("Electrical visual QA camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=name=="boss"?4.5f:2.5f;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.025f,0.045f,0.09f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            target=new RenderTexture(512,512,24);pixels=new Texture2D(512,512,TextureFormat.RGBA32,false);
            recorder=game.gameObject.AddComponent<KarmaSwordElectricRecorder>();recorder.Tick=Paint;
        }
        void Paint()
        {
            float age=Time.time-began;if(age>duration || age-last<1f/60-0.001f || subject==null)return;last=age;
            var picture=subject.Find(subject.GetComponent<KarmaEnemy>()!=null?"Artwork/Guardian sprite":"Artwork/Player sprite").GetComponent<SpriteRenderer>();
            var bounds=picture.bounds;camera.orthographicSize=Mathf.Max(2,bounds.size.y*0.52f);
            camera.transform.position=new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*0.43f,-10);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply();RenderTexture.active=old;
            System.IO.File.WriteAllBytes(folder+"/"+(count++).ToString("D3")+".png",pixels.EncodeToPNG());
        }
        public void Dispose(){recorder.Tick=null;UnityEngine.Object.Destroy(recorder);UnityEngine.Object.Destroy(camera.gameObject);UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);}
    }
    static void CheckPooledSplinters()
    {
        var emit=typeof(KarmaSW05Energy).GetMethod("EmitSplinters",F);
        object previous=null;
        for(int cycle=0;cycle<4;cycle++)
        {
            var root=new GameObject("pooled splinter test scope");
            try
            {
                for(int site=0;site<54;site++)
                    emit.Invoke(null,new object[]{root.transform,game,new Vector2(0,0.12f),-1f,1f,3f,14,site});
                var fx=root.GetComponentInChildren<KarmaSW05Energy>();
                var storage=Get(fx,"buffer");
                if(previous!=null)Check(ReferenceEquals(previous,storage),"fragment mesh and storage reused between scopes");
                Check((int)Get(fx,"splinterCount")==384,"54 impacts saturate fixed 384 fragment cap");
                Check(root.GetComponentsInChildren<MeshRenderer>().Length==1,"all fragments share one draw");
                var draw=(Action)Delegate.CreateDelegate(typeof(Action),fx,typeof(KarmaSW05Energy).GetMethod("SplinterEnergy",F));
                for(int frame=0;frame<=30;frame++)
                {
                    Set(fx,"age",frame/60f);Set(fx,"count",0);draw();
                    Check(!fx.CapacityExceeded,"fragment mesh capacity");
                    var vertices=(Vector3[])Get(storage,"vertices");
                    for(int v=0;v<(int)Get(fx,"count")*4;v++)
                        Check(vertices[v].x>=-1 && vertices[v].x<=1 && vertices[v].y>=0 && vertices[v].y<=3.12f,"fragment facets stay inside attack bounds");
                }
                Check((int)Get(fx,"splinterCount")==0,"all fragments expire within 500ms");
                // Measure the warmed drawing path with a full batch, excluding reflection.
                emit.Invoke(null,new object[]{root.transform,game,new Vector2(0,0.12f),-1f,1f,3f,384,5});
                Set(fx,"count",0);draw();Set(fx,"count",0);
                long before=GC.GetAllocatedBytesForCurrentThread();draw();
                Check(GC.GetAllocatedBytesForCurrentThread()==before,"zero fragment drawing allocations");
                previous=storage;root.SetActive(false);
                Check(Get(fx,"buffer")==null && fx.GetComponent<MeshFilter>().sharedMesh==null,"interruption immediately returns mesh to pool");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        Pass("SW03/SW05 pooled fragments: 384 cap, one draw, bounds, expiry, reuse and interruption");
    }
    static void CheckSW05RestoredContracts()
    {
        var type=typeof(KarmaSW05Energy);
        foreach (string removed in new[] {"MagneticFieldBlast","PressurePetal","ResidualMagneticField","LocalResidualElectricity"})
            Check(type.GetMethod(removed,F)==null,"obsolete final pass removed: "+removed);
        Check(type.GetMethod("HazardVolumes",F)!=null,"historical full-region warning builder restored");
        Check(Mathf.Abs(KarmaSW05RainBlade.ExplosionActiveSeconds-0.12f)<0.00001f,"damage interval remains 120ms");
    }
    static void CheckSW05RestoredWarning(KarmaSW05Energy energy,KarmaSW05RainBlade[] blades)
    {
        Check(!energy.Detonated && Mathf.Abs((float)Get(energy,"life")-0.60f)<0.0001f,
            "original warning uses current 600ms charge, without another delay");
        foreach (var blade in blades)
        {
            var warning=(SpriteRenderer)Get(blade,"warning");
            Check(warning==null || !warning.gameObject.activeInHierarchy,
                "planted explosion range marker removed; charge and blast retained");
        }
        Check(energy.GetComponentsInChildren<KarmaSwordPackedVfx>().Length == 0,
            "sigils and helixes are not obscured by the old packed charge layer");
        var sites=(Vector3[])Get(energy,"sites");
        var feet=(Vector2[])Get(energy,"chargeFeet");
        var hilts=(Vector2[])Get(energy,"chargeHilts");
        Check(sites.Length==blades.Length && feet.Length==sites.Length && hilts.Length==sites.Length,
            "every planted blade retains its charge anchors");
        // Observations can run before LateUpdate while a just-landed sword is
        // still settling. Compare exact anchors once that 100ms settle ends.
        if(energy.Age<0.12f)return;
        foreach(var blade in blades)
            Check(Enumerable.Range(0,sites.Length).Any(i=>
                Mathf.Abs(sites[i].x-blade.ElectricFoot.x)<0.001f
                && Vector2.Distance(feet[i],blade.ElectricFoot)<0.002f
                && Vector2.Distance(hilts[i],blade.ElectricHilt)<0.002f),
                "charge follows the actual embedded blade foot and hilt");
    }

    static void CheckSW05RestoredRegions(KarmaSW05Energy energy)
    {
        var zones=(Vector3[])Get(energy,"zones");
        float savedAge=energy.Age;
        try
        {
            foreach(float time in new[] {0f,1f/30,0.12f,0.16f,0.24f,0.42f,0.48f})
            {
                Set(energy,"age",time);Set(energy,"count",0);Call(energy,"FinalEnergy");
                int quads=(int)Get(energy,"count");
                Check(!energy.CapacityExceeded && quads<=16384,"historical composition fits the existing pool");
                if(time>=0.48f) { Check(quads==0,"original fade ends without delayed lightning");continue; }
                Check(quads>0,"restored explosion submits geometry throughout its original fade");
                var vertices=(Vector3[])Get(Get(energy,"buffer"),"vertices");
                for(int q=0;q<quads;q++)
                {
                    float left=float.PositiveInfinity,right=float.NegativeInfinity,top=0,bottom=float.PositiveInfinity;
                    for(int n=0;n<4;n++)
                    {
                        var v=energy.transform.TransformPoint(vertices[q*4+n]);
                        left=Mathf.Min(left,v.x);right=Mathf.Max(right,v.x);top=Mathf.Max(top,v.y);bottom=Mathf.Min(bottom,v.y);
                    }
                    Check(bottom>=-0.001f && zones.Any(z=>left>=z.x-0.001f && right<=z.y+0.001f && top<=z.z+0.001f),
                        "restored mesh stays in current locked damage regions");
                }
            }
        }
        finally { Set(energy,"age",savedAge);Set(energy,"count",0);Call(energy,"FinalEnergy");Call(energy,"Upload"); }
    }
    static void CheckSW05ImmediateDischarge(KarmaSW05Energy energy,int swords)
    {
        var mesh = energy.GetComponent<MeshFilter>().sharedMesh;
        int quads = (int)Get(energy,"count");
        Check(energy.Detonated && energy.Age == 0 && (int)Get(energy,"detonationFrame") == Time.frameCount,
            "all sword flashes start on the trigger frame with one zero-age clock");
        Check(quads >= swords && mesh != null && mesh.GetIndexCount(0) == (uint)(quads*6)
            && !energy.CapacityExceeded,"Ignite uploads nonempty flash geometry immediately without a dead frame");
        Call(energy,"LateUpdate");
        Check(energy.Age == 0 && (int)Get(energy,"count") == quads,
            "the trigger frame LateUpdate preserves the full flash peak");
        Check(energy.GetComponentsInChildren<Collider2D>(true).Length == 0,
            "discharge branches and supporting glow add no damage colliders");
        var renderer = energy.GetComponent<MeshRenderer>();
        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
        Check(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sortingOrder == 4,
            "release remains visible below character bodies");
        Check(renderer.sharedMaterial == Resources.Load<Material>("SwordWave/SW05/SW05Celestial")
            && block.GetFloat("_Mode") == 6,"procedural vertex-color shader path is selected");
        Check(energy.transform.parent == Root && energy.GetComponentInParent<KarmaSW05RainBlade>() == null
            && Mathf.Abs((float)Get(energy,"life")-0.48f)<0.001f,
            "historical 480ms fade is independent of 120ms sword damage cleanup");
        var layers=energy.GetComponentsInChildren<KarmaSwordPackedVfx>();
        Check(layers.Length==0,
            "procedural release has no overlapping packed detonation layer");
        Check(((Vector2[])Get(energy,"chargeFeet")).Length == swords,
            "release retains every planted anchor through the shared fade");
        CheckSW05RestoredRegions(energy);
        CheckSW05EruptionPixels(energy);
    }
    // Explicit Unity-only render check. This is not executed by the offline
    // compiler or mesh probe; it must run in the actual URP project.
    static void CheckSW05EruptionPixels(KarmaSW05Energy energy)
    {
        float savedAge = energy.Age; int savedLayer = energy.gameObject.layer;
        var cameraNode = new GameObject("SW05 eruption renderer check");
        var camera = cameraNode.AddComponent<Camera>();
        cameraNode.AddComponent<UniversalAdditionalCameraData>();
        camera.enabled = false; camera.orthographic = true; camera.aspect = 1.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        var target = new RenderTexture(384,256,24,RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(384,256,TextureFormat.RGBA32,false);
        var old = RenderTexture.active;
        var packed=energy.GetComponentsInChildren<KarmaSwordPackedVfx>();
        var packedLayers=packed.Select(p=>p.gameObject.layer).ToArray();
        try
        {
            energy.gameObject.layer = 31;
            foreach(var layer in packed)layer.gameObject.layer=31;
            foreach (float time in new[] {0f,0.10f,0.15f,0.30f})
            {
                Set(energy,"age",time); Set(energy,"count",0);
                Call(energy,"FinalEnergy"); Call(energy,"Upload");
                foreach(var layer in packed)layer.SetProgress(time/(8f/24));
                var vertices = (Vector3[])Get(Get(energy,"buffer"),"vertices");
                int count = (int)Get(energy,"count");
                var bounds = new Bounds(energy.transform.TransformPoint(vertices[0]),Vector3.zero);
                for (int i = 1; i < count*4; i++) bounds.Encapsulate(energy.transform.TransformPoint(vertices[i]));
                camera.orthographicSize = Mathf.Max(bounds.size.y*0.56f,bounds.size.x/(camera.aspect*1.8f));
                camera.transform.position = new Vector3(bounds.center.x,bounds.center.y,-10);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,384,256),0,0); pixels.Apply();
                int left=384,right=-1,bottom=256,top=-1,lit=0;
                var colors = pixels.GetPixels32();
                for (int y = 0; y < 256; y++)
                for (int x = 0; x < 384; x++)
                {
                    var c = colors[y*384+x];
                    if (Mathf.Max(c.r,c.g,c.b) < (time < 0.12f ? 80 : 20)) continue;
                    left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);lit++;
                }
                float perUnit = 256/(2*camera.orthographicSize);
                // Restored burst retains full-height ridges and distributed fading fragments.
                Check((top-bottom >= bounds.size.y*perUnit*0.65f) && right-left >= bounds.size.x*perUnit*0.55f,
                    "actual eruption image at "+time+"s: span="+(right-left)+"x"+(top-bottom)+", expected="+(bounds.size.x*perUnit)+"x"+(bounds.size.y*perUnit)+", lit="+lit);
                // A wide arena occupies a shallow strip in this 3:2 camera.
                // Measure coverage against the effect bounds, not empty sky.
                float projectedArea=bounds.size.x*bounds.size.y*perUnit*perUnit;
                if (time <= 0.10f) Check(lit > projectedArea*0.18f,"sword fan illuminates its actual footprint: time="+time+", pixels="+lit+", area="+projectedArea);
                else Check(lit>30,"restored burst fragments remain readable through the final fade");
                Pass("SW05 GPU "+time.ToString("F2")+"s: lit="+lit+", span="+(right-left)+"x"+(top-bottom));
            }
        }
        finally
        {
            RenderTexture.active = old; energy.gameObject.layer = savedLayer;
            for(int i=0;i<packed.Length;i++)
            {
                packed[i].gameObject.layer=packedLayers[i];
                packed[i].SetProgress((float)Get(packed[i],"age")/(float)Get(packed[i],"seconds"));
            }
            Set(energy,"age",savedAge); Set(energy,"count",0); Call(energy,"FinalEnergy"); Call(energy,"Upload");
            UnityEngine.Object.Destroy(cameraNode); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
        }
    }
    static async Task SW05()
    {
        CheckSW05RestoredContracts();
        CheckPooledSplinters();
        Reset(); game.Player.transform.position = new Vector2(35, 12); Physics2D.SyncTransforms(); Force(EnemyAttack.SW05);
        bool warned = false, formationWarned = false, exploded = false, retained = false, dimmed = false, finalWarnCaptured = false;
        bool distinctionCaptured = false, rainCaptured = false, impactCaptured = false, blastCaptured = false, tailCaptured = false, echoSeen = false;
        using var recording = SessionState.GetBool(Key+".AreaRecording",false) ? new SkillRecording("sw05",true) : null;
        using var explosionRecording = SessionState.GetBool(Key+".GroundedVfxReview",false) ? new SkillRecording("sw05-explosion",false,"Charge") : null;
        var played = new Dictionary<KarmaSwordPackedVfx.Clip,HashSet<int>>();
        var plantedPoses = new Dictionary<KarmaSW05VFX,Matrix4x4>();
        int lastCount = 0; float lastLanding = -1, detonation = -1;
        var waves = new List<float>(); var launches = new HashSet<KarmaSW05RainBlade>();
        await Until(() => !(bool)Get(enemy, "running"), () =>
        {
            var shots = Root.GetComponentsInChildren<KarmaSW05RainBlade>();
            foreach (var shot in shots) launches.Add(shot);
            if(shots.Length>0)
            {
                var halo=(SpriteRenderer)Get(enemy,"shieldHalo");
                Check(halo==null || !halo.enabled || halo.color.a==0,"SW05 invulnerability never draws the bright boss halo");
            }
            if (launches.Count > lastCount) { if (waves.Count == 0 || enemy.FightSeconds - waves.Last() > 0.5f) waves.Add(enemy.FightSeconds); lastCount = launches.Count; }
            retained |= shots.Count(s => s.Landed) >= 32;
            var sprites = Root.GetComponentsInChildren<SpriteRenderer>();
            var fallBoxes = sprites.Where(s=>s.enabled && s.name=="SW05 낙하 지면 전조").ToArray();
            foreach (var box in fallBoxes)
                Check(!box.forceRenderingOff && box.sprite != null && box.GetComponent<Collider2D>() == null,
                    "SW05 uses the original visible, non-damaging landing box");
            formationWarned |= shots.Length==0 && fallBoxes.Length>0;
            foreach (var layer in Root.GetComponentsInChildren<KarmaSwordPackedVfx>())
            {
                if (!played.TryGetValue(layer.ClipId,out var frames)) played[layer.ClipId] = frames = new HashSet<int>();
                frames.Add(layer.FrameIndex);
                Check(layer.GetComponent<Collider2D>() == null,"pack layers add no damaging collider");
                if (layer.ClipId == KarmaSwordPackedVfx.Clip.Detonation)
                    Check(shots.Length == 0 || shots.All(s => (bool)Get(s,"detonating")),"packed detonation starts at the real shared damage event");
            }
            var energies = Root.GetComponentsInChildren<KarmaSW05Energy>();
            foreach(var energy in energies)
            {
                Check(!energy.CapacityExceeded,"bounded VFX meshes do not truncate");
                var mesh = energy.GetComponent<MeshFilter>().sharedMesh;
                int quads = (int)Get(energy,"count"), capacity = (int)Get(Get(energy,"buffer"),"capacity");
                Check(mesh != null && quads <= capacity && mesh.GetIndexCount(0) == (uint)(quads*6),
                    "every emitted energy quad reaches the bounded mesh draw");

            }
            Check(energies.Count(e=>e.name=="SW05 planted electrical lattice / no damage") <= 1,
                "all planted swords share one idle electrical mesh");
            foreach (var shot in shots.Where(s=>s.Landed && !(bool)Get(s,"detonating")))
            {
                var planted = (KarmaSW05VFX)Get(shot,"planted");
                var renderer = planted.GetComponent<SpriteRenderer>();
                Vector2 center = renderer.transform.position, axis = -renderer.transform.up;
                Vector2 foot = center+axis*((0.12f-center.y)/axis.y);
                Vector2 hilt = renderer.transform.TransformPoint(new Vector3(0,2.30f,0));
                Check(Vector2.Distance(shot.ElectricFoot,foot) < 0.002f
                    && Vector2.Distance(shot.ElectricHilt,hilt) < 0.002f,
                    "electric roots and crossguards follow measured heavy-sword geometry at every landing angle");
                if ((float)Get(planted,"age") <= 0.10f) continue;
                Matrix4x4 pose = renderer.transform.localToWorldMatrix;
                if (plantedPoses.TryGetValue(planted,out var previous))
                    Check(pose == previous,"settled planted swords never drift, rotate or change proportion");
                else plantedPoses.Add(planted,pose);
            }
            if(energies.Any(e=>e.Detonated))
                Check(!sprites.Any(s=>s.gameObject.activeInHierarchy && s.name.Contains("planted explosion warning")),
                    "historical warnings retire at the shared release event");
            warned |= shots.Any(s=>!s.Landed) && fallBoxes.Length>0;
            dimmed |= sprites.Any(s => s.name.Contains("wood-embedded sword") && s.color.a >= 0.4f && s.color.a <= 0.55f && s.sortingOrder == 15);
            echoSeen |= sprites.Any(s=>s.name=="SW05 compact blade echo / no damage");
            if (!rainCaptured && shots.Any(s => !s.Landed && (float)Get(s,"age") > 0.025f))
            { rainCaptured = true; Capture("sw05-falling"); }
            var landings=energies.Where(e=>Get(e,"kind").ToString()=="Landing").ToArray();
            Check(landings.Length<=1,"simultaneous landings share one draw layer");
            foreach(var landingFx in landings)
            {
                Check(!landingFx.CapacityExceeded,"all landing sites fit the fixed mesh");
                int vertices=(int)Get(landingFx,"count")*4;
                var points=(Vector3[])Get(Get(landingFx,"buffer"),"vertices");
                for(int v=0;v<vertices;v++)Check(points[v].y>=0.0149f&&points[v].y<0.39f
                    &&points[v].x>=KarmaStage.ArenaLeft&&points[v].x<=KarmaStage.ArenaRight,
                    "landing sparks stay low and inside the arena");
            }
            if (!impactCaptured && waves.Count == 1 && shots.Any(s => s.Landed))
            { impactCaptured = true; Capture("sw05-impact"); }
            if (!distinctionCaptured && dimmed && sprites.Any(s => s.name == "Forming aerial sword / no damage"))
            { distinctionCaptured = true; Capture("sw05-next-strike"); }
            if (waves.Count == 3 && shots.Length > 0 && shots.All(s => s.Landed && !(bool)Get(s,"detonating")))
            {
                if (lastLanding < 0) lastLanding = enemy.FightSeconds;
                var charge=Root.GetComponentsInChildren<KarmaSW05Energy>().FirstOrDefault(e=>Get(e,"kind").ToString()=="Charge");
                Check(charge!=null && !charge.Detonated,"restored area warning precedes damage");
                Check(Mathf.Abs((float)Get(charge,"life")-0.60f) < 0.0001f
                    && ((Array)Get(charge,"sites")).Length == shots.Length,
                    "all 48-54 planted anchors share the existing 0.60-second charge clock");
                var finalZones=(Vector3[])Get(charge,"zones");
                Check(finalZones.Length==1,"all gaps between planted swords belong to one final warning");
                var zone=finalZones[0];
                foreach(var blade in shots)
                {
                    Check(Mathf.Abs(blade.ExplosionCenter.x-blade.ExplosionSize.x/2-zone.x)<0.001f
                        && Mathf.Abs(blade.ExplosionCenter.x+blade.ExplosionSize.x/2-zone.y)<0.001f
                        && Mathf.Abs(blade.ExplosionSize.y-zone.z)<0.001f,
                        "every final damage check uses the same complete advertised volume");
                }
                CheckSW05RestoredWarning(charge,shots);
                if(charge.Age>0.05f)Check((int)Get(charge,"count")>0,"charge mesh displays the locked damage region");
                if (!finalWarnCaptured && enemy.FightSeconds-lastLanding >= 0.35f)
                { finalWarnCaptured = true; Capture("sw05-explosion-range-warning"); }
            }
            if (energies.Any(e=>e.Detonated))
            {
                var sharedDischarge = energies.Single(e=>e.Detonated);
                Check(!energies.Any(e=>Get(e,"kind").ToString()=="Summon"),
                    "summoning mesh retires at final release");
                Check(((Array)Get(sharedDischarge,"sites")).Length == launches.Count
                    && (int)Get(Get(sharedDischarge,"buffer"),"capacity") == 16384,
                    "every sword uses one shared detonation clock within the existing 16384-quad budget");
                Check((int)Get(sharedDischarge,"count") <= 16384,
                    "restored historical mesh fits its current bounded pool");
                if (sharedDischarge.Age >= 0.18f && sharedDischarge.Age < 0.46f)
                    Check(shots.Length == 0 && sharedDischarge.GetComponent<MeshRenderer>().enabled
                        && (int)Get(sharedDischarge,"count") > 0,
                        "original explosion fade survives damage-owner cleanup");
                if (!exploded)
                {
                    detonation = enemy.FightSeconds;
                    Capture("sw05-final-ignition");
                    CheckSW05RestoredRegions(sharedDischarge);
                    CheckSW05EruptionPixels(sharedDischarge);
                }
                exploded = true;
                if (!blastCaptured && enemy.FightSeconds-detonation >= 0.035f)
                { blastCaptured = true; Capture("sw05-final-explosion"); }
                if (!tailCaptured && enemy.FightSeconds-detonation >= 0.15f)
                { tailCaptured = true; Capture("sw05-final-explosion-tail"); }
                Check(shots.All(s => (bool)Get(s, "detonating")), "every planted sword detonates synchronously");
                if (shots.Length > 0)
                {
                    object contact = Get(shots[0],"explosionContact");
                    Check(contact != null && shots.All(s=>ReferenceEquals(contact,Get(s,"explosionContact"))),
                        "every simultaneous explosion retains one shared damage ledger");
                    Check(Mathf.Abs(KarmaSW05RainBlade.ExplosionActiveSeconds-0.12f) < 0.0001f,
                        "final electrical burst preserves the 0.12-second damage window");
                }
            }
        }, timeout: 60); // Full rain plus GPU captures can exceed 25 wall-clock seconds.
        Check(waves.Count == 3 && waves[2] - waves[1] < 2.8f, "three existing volleys with faster impact cadence");
        Check(retained && formationWarned && warned && exploded && dimmed && distinctionCaptured, "formation/falling warnings remain; old swords recede below new strike cues");
        Check(finalWarnCaptured && detonation - lastLanding >= 0.58f && detonation-lastLanding <= 0.68f, "electric charge retains the 0.60-second final countdown");
        Check(launches.Count >= 48 && launches.Count <= 54, "all three complete rain waves retained");
        Check(echoSeen && !played.ContainsKey(KarmaSwordPackedVfx.Clip.FallingAura),
            "compact blade echoes replace the duplicate broad falling aura");
        Check(!played.ContainsKey(KarmaSwordPackedVfx.Clip.LandingShock),
            "one grounded landing batch replaces the overlapping tall impact sprites");
        await Seconds(0.05f);
        Check(Root.GetComponentsInChildren<KarmaSW05RainBlade>(true).Length == 0
            && Root.GetComponentsInChildren<KarmaSW05VFX>(true).Length == 0
            && Root.GetComponentsInChildren<KarmaSW05Energy>(true).Length == 0
            && Root.GetComponentsInChildren<KarmaSwordPackedVfx>(true).Length == 0, "no lingering explosion, sprite animation or energy mesh after completion");
        Force(EnemyAttack.SW05); await Until(() => Root.GetComponentsInChildren<KarmaSW05RainBlade>().Length > 0);
        Call(enemy, "InterruptPattern"); await Seconds(0.05f);
        Check(!enemy.Invulnerable && Root.GetComponentsInChildren<KarmaSW05Energy>(true).Length == 0, "SW05 interruption clears energy and shield");
        Force(EnemyAttack.SW05);
        await Until(()=>Root.GetComponentsInChildren<KarmaSW05Energy>().Any(e=>Get(e,"kind").ToString()=="Charge"),timeout:60);
        Call(enemy,"InterruptPattern");await Seconds(0.05f);
        Check(Root.GetComponentsInChildren<KarmaSW05Energy>(true).Length==0
            && Root.GetComponentsInChildren<KarmaSW05RainBlade>(true).Length==0
            && Get(Get(enemy,"behavior"),"finalDetonationContact")==null,"final charge cancellation clears pooled electricity and pending detonation");
        Pass("SW05 three full rain waves, heavy sword silhouette, all animated layers, 0.60-second synchronized recharge/detonation, completion and interruption cleanup");
    }
    // Reuse the current SW03 assertions from older regression entry points.
    public static async Task CheckSW03Redesign(bool? enraged = null, bool verifyEscape = false)
    {
        game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        Check(game != null, "live game required for SW03 runtime checks");
        await SW03Active(enraged, verifyEscape);
    }
    static int SW03PlannedCount
    {
        get { var plan=Get(GroundPhase,"<CurrentPlan>k__BackingField");return plan==null?0:((float[])Get(plan,"Positions")).Length; }
    }
    static void SW03Different(float[] previous,float[] current,bool varietyFallback=false)
    {
        Check(!previous.SequenceEqual(current),"maximum-density planning never replays the identical visible row");
        if(previous.Length==current.Length && !varietyFallback)
            Check(previous.Zip(current,(a,b)=>Mathf.Abs(a-b)).Max()>0.1f,"continuous corridor packing changes physical sword sites");
    }
    // Runs only when explicitly invoked in an already playing game. Exercise
    // the real normal-seed Execute path, not just a test-seeded planner call.
    public static async Task CheckSW03PlacementCasts()
    {
        game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        Check(game!=null,"live game required for SW03 placement checks");
        var seeds=new HashSet<int>();float[] priorBattle=null;
        foreach(float x in new[]{KarmaStage.ArenaLeft+0.75f,KarmaStage.ArenaCenter,KarmaStage.ArenaRight-0.75f})
        {
            Reset();float[] previous=null;
            for(int cast=0;cast<3;cast++)
            {
                var body=game.Player.GetComponent<Rigidbody2D>();
                var at=new Vector2(x,KarmaSpace.PlayerHalfHeight+0.02f);
                body.position=at;body.linearVelocity=Vector2.zero;game.Player.transform.position=at;Physics2D.SyncTransforms();
                Set(GroundPhase,"seedOverride",null);Force(EnemyAttack.SW03);
                var phase=GroundPhase;var scope=(Transform)Get(phase,"groundRoot");
                await Until(()=>SW03PlannedCount>0 && scope.GetComponentsInChildren<KarmaSwordScarRise>().Count(v=>Get(v,"sequence").ToString()=="Warning")==SW03PlannedCount);
                int seed=(int)phase.GetType().GetProperty("Seed",F).GetValue(phase);
                Check(seeds.Add(seed),"normal casts/battle resets never reuse a cast seed");
                var plan=Get(phase,"<CurrentPlan>k__BackingField");
                var positions=(float[])((float[])Get(plan,"Positions")).Clone();
                Check(positions.Length>=24 && positions.Length<=26,"normal cast reaches corridor-first density target");
                if(previous!=null)SW03Different(previous,positions,(bool)Get(plan,"Fallback"));
                else if(priorBattle!=null)Check(!priorBattle.SequenceEqual(positions),"battle reset does not replay the same first layout");
                var warnings=scope.GetComponentsInChildren<KarmaSwordScarRise>().Where(v=>Get(v,"sequence").ToString()=="Warning")
                    .ToDictionary(v=>v,v=>(Vector2)Get(v,"ground"));
                Check(warnings.Values.Select(p=>p.x).OrderBy(p=>p).SequenceEqual(positions),"every planned coordinate reaches one real warning");
                await Seconds(0.08f);
                Check(warnings.All(p=>p.Key!=null && (Vector2)Get(p.Key,"ground")==p.Value && (Vector2)p.Key.transform.position==p.Value),"real normal-cast warnings remain fixed");
                previous=positions;priorBattle=positions;
                Call(enemy,"InterruptPattern");await Until(()=>scope==null);
            }
        }
        Pass("SW03 nine normal-seed casts across arena edges/center and battle resets: unique seeds, meaningful layout/density changes and locked matching warnings");
    }
    public static async Task CheckSW03ContactAndCancellation()
    {
        game=UnityEngine.Object.FindAnyObjectByType<KarmaGame>();Reset();Force(EnemyAttack.SW03);
        var scope=(Transform)Get(GroundPhase,"groundRoot");
        await Until(()=>SW03PlannedCount>0 && scope.GetComponentsInChildren<KarmaBossAttack>().Length==SW03PlannedCount);
        var shots=scope.GetComponentsInChildren<KarmaBossAttack>();var shot=shots[0];
        var ledger=(KarmaAttackContact)Get(shot,"contact");
        var plan=Get(GroundPhase,"<CurrentPlan>k__BackingField");
        int site=System.Array.IndexOf((float[])Get(plan,"Positions"),shot.transform.position.x);
        int cluster=((int[])Get(plan,"ContactIndices"))[site];
        Check(shots.Count(s=>ReferenceEquals(Get(s,"contact"),ledger))==((int[])Get(plan,"ClusterSizes"))[cluster],"all overlapping blades in one cluster share one real ledger");
        var at=(Vector2)shot.transform.position;var size=(Vector2)Get(shot,"size");
        var position=new Vector2(at.x,KarmaSpace.PlayerHalfHeight+0.02f);
        game.Player.transform.position=position;game.Player.GetComponent<Rigidbody2D>().position=position;Physics2D.SyncTransforms();
        Set(game.Player,"invulnerableUntil",0f);Set(game.Player,"guardUntil",0f);
        float before=game.Player.Health;
        enemy.TryAttackHit(at,at,size,10,ledger,0,sourcePattern:EnemyAttack.SW03);
        float after=game.Player.Health;
        Check(after<before && ledger.DealtDamage && ledger.Consumed,"real SW03 contact deals one hit");
        Set(game.Player,"invulnerableUntil",0f);
        enemy.TryAttackHit(at,at,size,10,ledger,0,sourcePattern:EnemyAttack.SW03);
        Check(game.Player.Health==after,"cluster overlap/repeated frames cannot reuse a consumed contact");
        Call(enemy,"InterruptPattern");await Until(()=>scope==null);
        Check(!GroundRunning,"interruption clears the synchronized round");
    }
    static object GroundPhase => Get(Get(enemy,"behavior"),"ground");
    static bool GroundRunning => (bool)GroundPhase.GetType().GetProperty("Running",F).GetValue(GroundPhase);
    static async Task SW03Active(bool? onlyState = null, bool verifyEscape = false)
    {
        foreach(bool enraged in onlyState.HasValue ? new[] { onlyState.Value } : new[] { false,true })
        {
            Reset();enemy.transform.position=new Vector2(25,KarmaSpace.GuardianGroundY);
            var behavior=Get(enemy,"behavior");var movement=Get(behavior,"movement");
            var ultimate=Get(behavior,"ultimate");
            if(enraged)
            {
                typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy,enemy.MaxHealth*0.4f);
                Set(ultimate,"thresholdsUsed",1);
            }
            var body=game.Player.GetComponent<Rigidbody2D>();
            Action<float> place=gap=>
            {
                float side=enemy.Position.x<KarmaStage.ArenaCenter?1:-1;
                var p=new Vector2(Mathf.Clamp(enemy.Position.x+side*gap,KarmaStage.ArenaLeft+1,KarmaStage.ArenaRight-1),KarmaSpace.PlayerHalfHeight+0.02f);
                body.position=p;game.Player.transform.position=p;Physics2D.SyncTransforms();
            };
            place(3.5f);UnityEngine.Random.InitState(enraged?8414:8413);
            var recording=SessionState.GetBool(Key+".ScarPolish",false)?new ScarRecording(enraged):null;
            float castBegan=enemy.FightSeconds;
            Set(GroundPhase,"seedOverride",(int?)(enraged ? 8414 : 8413));
            float escapeHealth=game.Player.Health;
            KarmaSwordRefreshJumpDriver escape=null;
            Force(EnemyAttack.SW03);
            if(verifyEscape)
            {
                Set(game.Player,"invulnerableUntil",0f);Set(game.Player,"guardUntil",0f);
                escape=game.Player.gameObject.AddComponent<KarmaSwordRefreshJumpDriver>();
                escape.player=game.Player;escape.WalkOnly=true;escape.MoveSpeed=game.Config.moveSpeed*0.8f;
                game.Player.enabled=false;
            }
            Check(GroundRunning && !enemy.Invulnerable,"SW03 begins without a shield");
            var phaseRoot=(Transform)Get(GroundPhase,"groundRoot");
            var duplicate=(System.Collections.IEnumerator)Call(GroundPhase,"Execute");
            Check(!duplicate.MoveNext() && (Transform)Get(GroundPhase,"groundRoot")==phaseRoot,"duplicate SW03 execution cannot restart the timeline");
            var warningRoots=new HashSet<Transform>();var activeRoots=new HashSet<Transform>();
            var blades=new HashSet<KarmaSwordScarRise>();
            var roundRoots=new HashSet<Transform>();
            var warningTimes=new Dictionary<Transform,float>();
            var warningDurations=new Dictionary<Transform,float>();
            var releaseFrames=new Dictionary<Transform,int>();
            var recycledBeforeRelease=new Dictionary<Transform,int>();
            var ledgerCounts=new Dictionary<Transform,Dictionary<object,int>>();
            var clusterSizesByRound=new Dictionary<Transform,int[]>();
            var clusterLedgers=new Dictionary<Transform,Dictionary<int,object>>();
            Transform escapeRound=null;
            var warningPositions=new Dictionary<KarmaSwordScarRise,Vector2>();
            var warningSites=new Dictionary<Transform,Dictionary<int,Vector2>>();
            var releasedSites=new Dictionary<Transform,HashSet<int>>();
            var layouts=new List<float[]>();
            var strikes=new HashSet<KarmaSW05Energy>();
            var centers=new List<float>();var chargeFrames=new HashSet<int>();var surgeFrames=new HashSet<int>();
            bool retreated=false,thresholdCrossed=false;int retreatCount=0;float approachTravel=0;
            bool retreatCapture=false,groundCapture=false;
            float previousTime=enemy.FightSeconds,previousX=enemy.Position.x,travel=0,warningSeconds=0,lastRelease=-1;
            float active=Mathf.Clamp(game.Config.BossTuning.sword.scarActiveSeconds,0.25f,0.8f);
            float retreatEnd=0;bool wasRetreating=false;
            await ObserveEveryFrame(()=>!GroundRunning,()=>
            {
                if(!GroundRunning)return;
                foreach(var fx in phaseRoot.GetComponentsInChildren<KarmaSW05Energy>())
                    if(fx.name=="SW03 first ground strike / no damage")
                    { strikes.Add(fx);Check(fx.GetComponent<KarmaBossAttack>()==null,"ground strike cracks are cosmetic");
                      Check(!fx.CapacityExceeded,"ground strike stays in its recycled mesh capacity"); }
                Check(!enemy.Invulnerable,"boss remains vulnerable through warnings, rising swords and gaps");
                Check(phaseRoot!=null && phaseRoot.position==Vector3.zero,"SW03 root stays fixed in world space");
                float dt=enemy.FightSeconds-previousTime,step=Mathf.Abs(enemy.Position.x-previousX);
                Check(step<=48*dt+0.1f,"existing smooth movement has no teleport");
                travel+=step;previousX=enemy.Position.x;previousTime=enemy.FightSeconds;
                bool retreating=(float)Get(movement,"swordRetreatRemaining")>0;
                if(retreating)
                {
                    if(!wasRetreating)retreatCount++;
                    retreated=true;retreatEnd=(float)Get(movement,"swordRetreatTarget");
                    var pose=((KarmaCharacterAnimator)Get(enemy,"animator")).CurrentPose;
                    Check(pose=="Backstep","SW03 cannot overwrite the existing retreat pose");
                    if(!enraged && !retreatCapture){retreatCapture=true;Capture("active-sw03-retreat");}
                }
                if(wasRetreating && !retreating && !(bool)Get(enemy,"running"))
                    Check(Mathf.Abs(enemy.Position.x-retreatEnd)<0.15f,"existing retreat completes its original displacement");
                if(!retreating && ((KarmaCharacterAnimator)Get(enemy,"animator")).CurrentPose=="Move")approachTravel+=step;
                wasRetreating=retreating;
                if((bool)Get(enemy,"running"))
                    Check((EnemyAttack)Get(enemy,"selected")==EnemyAttack.SW03,"only the initial SW03 cast may occupy the action slot");
                foreach(var candidate in new[] {EnemyAttack.SWBasic,EnemyAttack.SW01,EnemyAttack.SW02,EnemyAttack.SW03,EnemyAttack.SW04,EnemyAttack.SW05})
                    Check(!(bool)Call(behavior,"SwordCandidateAvailable",candidate,enemy.Position,12f,true,true),"all other specials and repeated SW03 blocked");
                object[] selection={12f,true,true,default(EnemyAttack)};
                bool selected=(bool)Call(behavior,"TrySelect",selection);
                Check(!selected,"all attack selection, including both basic modes, is blocked during SW03");
                var views=phaseRoot.GetComponentsInChildren<KarmaSwordScarRise>();
                var warnings=views.Where(v=>Get(v,"sequence").ToString()=="Warning").ToArray();
                foreach(var set in warnings.GroupBy(v=>v.transform.parent))
                {
                    var warningRoot=set.Key;var groupRoot=warningRoot.parent;var roundRoot=groupRoot.parent;
                    Check(set.Select(v=>(float)Get(v,"life")).Distinct().Count()==1
                        && set.Select(v=>v.FrameIndex).Distinct().Count()==1,"all warnings share one duration and countdown frame");
                    if(warningRoots.Add(warningRoot))
                    {
                        Check(set.Count()==SW03PlannedCount && set.Select(v=>((Vector2)Get(v,"ground")).x).Distinct().Count()==set.Count(),
                            "one locked warning per sword in the common countdown");
                        warningTimes[groupRoot]=Time.time;
                        warningDurations[groupRoot]=(float)Get(set.First(),"life");
                        Check(warningDurations[groupRoot]>=0.8f,"minimum readable warning retained");
                        if(roundRoots.Add(roundRoot))
                        {
                            recycledBeforeRelease[roundRoot]=KarmaSwordVfxPool.For(game).RecycledLive;
                            if(lastRelease>=0)
                                Check(Mathf.Abs(Time.time-lastRelease-active-Mathf.Clamp(game.Config.BossTuning.sword.scarRoundGap,0.3f,0.8f))<0.06f,
                                    "gap follows the common unchanged active duration: actual=" + (Time.time-lastRelease-active) + " expected=" + game.Config.BossTuning.sword.scarRoundGap);
                            warningSeconds=(float)Get(set.First(),"life");
                            var plan=Get(GroundPhase,"<CurrentPlan>k__BackingField");
                            centers=((float[])Get(plan,"Positions")).ToList();
                            if(layouts.Count>0)SW03Different(layouts[layouts.Count-1],centers.ToArray(),(bool)Get(plan,"Fallback"));
                            layouts.Add(centers.ToArray());
                            Check(centers.Count>=24 && centers.Count<=26,"corridor-first layout reaches useful sword density");
                            var clusterSizes=(int[])Get(plan,"ClusterSizes");
                            Check(clusterSizes.All(c=>c==1||c==3||c==4) && clusterSizes.Sum()==centers.Count,"only one/three/four sword spatial clusters");
                            Check((float)Get(plan,"SafeClearance")>=0.25f-0.0001f,"safe center retains body clearance and positive tolerance");
                            foreach(var corridor in (System.Array)Get(plan,"Corridors"))
                            {
                                float left=(float)Get(corridor,"Left"),right=(float)Get(corridor,"Right");
                                Check(right-left>=game.Player.GetComponent<Collider2D>().bounds.size.x*2-0.001f,"physical corridor is at least two actual player widths");
                                Check(left>=KarmaStage.ArenaLeft && right<=KarmaStage.ArenaRight,"no off-arena corridor space counted");
                            }
                            clusterSizesByRound[roundRoot]=clusterSizes;
                            clusterLedgers[roundRoot]=new Dictionary<int,object>();
                            warningSites[roundRoot]=new Dictionary<int,Vector2>();
                            releasedSites[roundRoot]=new HashSet<int>();
                            ledgerCounts[roundRoot]=new Dictionary<object,int>();
                            float health=enemy.Health;enemy.TakeDamage(1);
                            Check(Mathf.Abs(enemy.Health-health+1)<0.001f,"boss vulnerable during warnings");
                            if(verifyEscape && escapeRound!=roundRoot)
                            {
                                escapeRound=roundRoot;escape.TargetX=(float)Get(plan,"SafeX");
                                escape.ReactAt=Time.time+0.28f;
                            }
                            if(enraged && roundRoots.Count==2)
                            {
                                enemy.TakeDamage(enemy.MaxHealth*0.16f);thresholdCrossed=true;
                                Check((bool)ultimate.GetType().GetProperty("ThresholdPending",F).GetValue(ultimate),"threshold remains pending");
                            }
                        }
                    }
                    foreach(var warning in set)
                    {
                        var ground=(Vector2)Get(warning,"ground");
                        int site=int.Parse(warning.name.Substring(warning.name.LastIndexOf(' ')+1));
                        Check(ground==new Vector2(centers[site],0),"planner coordinates reach the matching warning unchanged");
                        if(!warningPositions.ContainsKey(warning))
                        {
                            warningPositions.Add(warning,ground);
                            Check(!warningSites[roundRoot].ContainsKey(site),"one warning per planned site");
                            warningSites[roundRoot].Add(site,ground);
                        }
                        Check(ground==warningPositions[warning] && (Vector2)warning.transform.position==ground,"telegraphs never move after lock");
                    }
                }
                var rising=views.Where(v=>Get(v,"sequence").ToString()=="Blade").ToArray();
                if(rising.Length>0)
                {
                    foreach(var set in rising.GroupBy(v=>v.transform.parent.parent))
                    {
                        var groupRoot=set.Key;var roundRoot=groupRoot.parent;
                        Check(set.Max(v=>(float)Get(v,"age"))-set.Min(v=>(float)Get(v,"age"))<0.0001f,
                            "all round swords share identical rise age");
                        Check(set.Select(v=>Get(v,"roundClock")).Distinct().Count()==1 && Get(set.First(),"roundClock")!=null
                            && set.Select(v=>v.FrameIndex).Distinct().Count()==1,"one shared clock and reveal frame across all round swords");
                        var shots=groupRoot.GetComponentsInChildren<KarmaBossAttack>();
                        Check(shots.Max(s=>(float)Get(s,"remaining"))-shots.Min(s=>(float)Get(s,"remaining"))<0.0001f,
                            "all active damage lifetimes remain synchronized");
                        Check(groupRoot.GetComponentsInChildren<KarmaSW05Energy>().All(fx=>!fx.CapacityExceeded),"simultaneous ignition fits every existing mesh buffer");
                        Check(set.All(v=>Mathf.Abs((float)Get(v,"life")-active)<0.0001f)
                            && set.Select(v=>((Vector2)Get(v,"footprint")).y).Distinct().Count()==1,
                            "every sword retains identical active duration and vertical travel");
                        if(!activeRoots.Add(groupRoot))continue;
                        Check(set.Count()==SW03PlannedCount,"all planned swords are visible together on the first release frame");
                        Check(KarmaSwordVfxPool.For(game).RecycledLive==recycledBeforeRelease[roundRoot],"simultaneous release does not recycle live pooled effects");
                        var impact=groupRoot.GetComponentsInChildren<KarmaSW05Energy>().Single(fx=>fx.name=="SW03 grouped ground discharge / no damage");
                        Check((int)Get(impact,"scarSiteCount")==SW03PlannedCount,"every simultaneous sword is present in the ground-impact buffer");
                        Check(!groupRoot.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Guardian launch/")),"no launch copies");
                        var plan=Get(GroundPhase,"<CurrentPlan>k__BackingField");
                        Check(groupRoot.GetComponentsInChildren<KarmaBossAttack>().Select(shot=>Get(shot,"contact")).Distinct().Count()==((int[])Get(plan,"ClusterSizes")).Length,
                            "one independent ledger per spatial cluster at the common release");
                        Check(Mathf.Abs(Time.time-warningTimes[groupRoot]-warningDurations[groupRoot])<0.04f,"full warning for each group: elapsed=" + (Time.time-warningTimes[groupRoot]) + " expected=" + warningDurations[groupRoot]);
                        Check(!releaseFrames.ContainsKey(roundRoot),"exactly one activation event in a round");
                        releaseFrames[roundRoot]=Time.frameCount;lastRelease=Time.time;
                        float health=enemy.Health;enemy.TakeDamage(1);
                        Check(Mathf.Abs(enemy.Health-health+1)<0.001f,"boss vulnerable during active swords");
                    }
                    foreach(var view in rising)
                    {
                        var viewScope=view.transform.parent.parent;
                        if(blades.Add(view))
                        {
                            Check(Time.frameCount==releaseFrames[viewScope.parent],"every round sword first appears on the same observed frame");
                            int site=int.Parse(view.name.Substring(view.name.LastIndexOf(' ')+1));
                            Check(releasedSites[viewScope.parent].Add(site),"each planned site releases exactly one sword");
                            Check(warningSites[viewScope.parent].TryGetValue(site,out var warned)
                                && (Vector2)Get(view,"ground")==warned
                                && Mathf.Abs(view.GetComponentInParent<KarmaBossAttack>().transform.position.x-warned.x)<0.001f,
                                "warning, rising sword and damage contact use the same locked planner coordinate");
                            var ledger=Get(view.GetComponentInParent<KarmaBossAttack>(),"contact");
                            var plan=Get(GroundPhase,"<CurrentPlan>k__BackingField");
                            int cluster=((int[])Get(plan,"ContactIndices"))[site];
                            var mapping=clusterLedgers[viewScope.parent];
                            if(mapping.TryGetValue(cluster,out var shared))Check(ReferenceEquals(shared,ledger),"cluster blades share the same accepted contact");
                            else {Check(!mapping.Values.Contains(ledger),"unrelated clusters never share a global ledger");mapping[cluster]=ledger;}
                            var counts=ledgerCounts[viewScope.parent];
                            counts[ledger]=counts.TryGetValue(ledger,out int count)?count+1:1;
                        }
                        var ground=(Vector2)Get(view,"ground");var size=(Vector2)Get(view,"footprint");
                        Check(centers.Count(x=>Mathf.Abs(x-ground.x)<0.001f)==1,"one planned warning per fixed sword origin");
                        var sr=view.GetComponent<SpriteRenderer>();
                        Check(sr.sprite==KarmaArtwork.Sprite("SwordBossVFX","SW03_RisingBlade_v7") && !sr.flipX,"exact static artwork is the only blade sprite");
                        var props=new MaterialPropertyBlock();sr.GetPropertyBlock(props);
                        Check(props.GetFloat("_EnergyLayer")==0,"sword uses isolated core mask");
                        var aura=viewScope.GetComponentsInChildren<KarmaSwordScarAura>().FirstOrDefault(a=>(SpriteRenderer)Get(a,"source")==sr);
                        Check(aura!=null && aura.transform.parent==viewScope && aura.GetComponent<Collider2D>()==null,"separate sibling aura renderer without contact");
                        var auraProps=new MaterialPropertyBlock();aura.GetComponent<SpriteRenderer>().GetPropertyBlock(auraProps);
                        Check(auraProps.GetFloat("_EnergyLayer")==1,"aura uses complementary energy mask");
                        float auraAge=(float)Get(aura,"age");
                        // This observer may run before the aura's LateUpdate.
                        Check(ReferenceEquals(Get(aura,"roundClock"),Get(view,"roundClock"))
                            && Mathf.Abs(auraAge-(float)Get(view,"age"))<=Time.deltaTime+0.0001f,"aura samples the same common clock at LateUpdate");
                        if(auraAge<KarmaSwordScarRise.AuraDelay)Check(auraProps.GetFloat("_Fade")==0,"sword emerges before aura ignition");
                        float originalFit=size.y*sr.sprite.pixelsPerUnit/KarmaSwordScarRise.BladePixels.height;
                        float widthFit=KarmaSwordScarRise.VisualSize(size).x*sr.sprite.pixelsPerUnit/KarmaSwordScarRise.BladePixels.width;
                        Check(Mathf.Abs(sr.transform.localScale.x-widthFit)<0.0001f
                            && Mathf.Abs(sr.transform.localScale.y-originalFit)<0.0001f,"new silver artwork preserves the established visible width and height");
                        Check(aura.transform.localScale==sr.transform.lossyScale,"independent aura matches slimmer blade fit");
                        var electric=Get(view,"electricalBlade");var electricSize=(Vector2)Get(electric,"size");
                        Check(Mathf.Abs(electricSize.x-size.y*704f/1354f*0.76f)<0.0001f
                            && Mathf.Abs(electricSize.y-size.y)<0.0001f,"lightning follows visual width without changing height");
                        Vector2 foot=sr.transform.TransformPoint((KarmaSwordScarRise.FloorPixel-sr.sprite.pivot)/sr.sprite.pixelsPerUnit);
                        float visibleHeight=KarmaSwordScarRise.VisualRiseHeight((float)Get(view,"age"),size.y);
                        Check(Vector2.Distance(foot,ground-Vector2.up*(size.y-visibleHeight))<0.001f,"crossguard keeps its original ground/rise alignment");
                        var rects=(Rect[])typeof(KarmaSwordScarRise).GetField("BladeCore",F).GetValue(null);
                        var shot=sr.GetComponentInParent<KarmaBossAttack>();var actual=(Vector2)Get(shot,"size");
                        Check(Mathf.Abs(actual.x-size.x*rects[view.FrameIndex].width/rects[4].width)<0.0001f,"original damage width retained at every rise stage");
                        Check(Mathf.Abs(actual.y-Mathf.Min(size.y*rects[view.FrameIndex].height/rects[4].height,KarmaSwordScarRise.VisibleTipHeight((float)Get(view,"age"),size.y)))<0.0001f,"damage height follows the visible tip at every rise stage");
                        Check(Mathf.Abs(shot.transform.position.y-actual.y/2)<0.001f,"damage remains grounded while sprite rises");
                    }
                    if(!enraged && !groundCapture && rising.Any(v=>v.FrameIndex==4)){groundCapture=true;Capture("active-sw03-ground");}
                }
                foreach(var fx in phaseRoot.GetComponentsInChildren<KarmaSwordPackedVfx>())
                {
                    if(fx.ClipId==KarmaSwordPackedVfx.Clip.GroundCharge)chargeFrames.Add(fx.FrameIndex);
                    if(fx.ClipId==KarmaSwordPackedVfx.Clip.BladeSurge)surgeFrames.Add(fx.FrameIndex);
                }
                foreach(var shot in Root.GetComponentsInChildren<KarmaBossAttack>())
                {
                    var source=(EnemyAttack)Get(shot,"sourcePattern");
                    if(source==EnemyAttack.SW03)
                    {
                        Check(shot.transform.IsChildOf(phaseRoot),"ground contacts have independent scope and SW03 attribution");
                        continue;
                    }
                    Check(false,"zero basic melee/ranged projectiles or other special contacts during SW03: "+source);
                }
                Check(phaseRoot.GetComponentsInChildren<Transform>().Count(t=>t.name=="SW03 independent ground timeline")==1,"one live SW03 instance");
                float gap=roundRoots.Count%2==0?18f:3.5f;
                if(!verifyEscape)place(gap);
            // Synchronous GPU readback and PNG encoding can dominate wall time
            // on low-memory editors; the simulated combat clock is unchanged.
            },timeout:recording != null ? 180 : 55);
            Check(strikes.Count==1,"exactly one initial ground strike for all five rising rounds");
            Check(roundRoots.Count==5 && warningRoots.Count==5 && activeRoots.Count==5 && blades.Count==layouts.Sum(p=>p.Length) && warningPositions.Count==blades.Count,
                "five rounds, five synchronized releases, one warning per planned sword");
            Check(ledgerCounts.All(pair=>pair.Value.Values.OrderBy(n=>n).SequenceEqual(clusterSizesByRound[pair.Key].OrderBy(n=>n))),"one contact ledger per cluster with exactly its assigned sword count");
            Check(layouts.Count==5 && layouts.All(p=>p.Length>=24 && p.Length<=26)
                && warningSites.All(pair=>releasedSites[pair.Key].SetEquals(pair.Value.Keys)),
                "five corridor-first rounds release all matching warnings/swords");
            if(verifyEscape)
            {
                UnityEngine.Object.Destroy(escape);game.Player.enabled=true;
                Check(game.Player.Health==escapeHealth,"reaction-delayed walking survives the simultaneous active window");
            }
            if(!verifyEscape)Check(retreated && retreatCount>=2 && approachTravel>0.5f && travel>10,"original evasion and approach continue without extra attacks");
            Check(chargeFrames.Count==0 && surgeFrames.Count==0,"no secondary charge or blade column layers are spawned");
            recording?.Dispose();
            Pass("SW03 TIMING "+(enraged?"enraged":"normal")+" total="+(enemy.FightSeconds-castBegan).ToString("F4")+" warning="+warningSeconds.ToString("F4")+" active="+active.ToString("F4")+" gap="+Mathf.Clamp(game.Config.BossTuning.sword.scarRoundGap,0.3f,0.8f).ToString("F4"));
            await Seconds(0.05f);
            Check(phaseRoot==null && Root.GetComponentsInChildren<KarmaSwordScarRise>(true).Length==0,"SW03 completion cleans up independent effects without clearing a basic");
            if(enraged)
            {
                Check(thresholdCrossed,"health threshold crossed during active SW03");
                await Until(()=>(EnemyAttack)Get(enemy,"selected")==EnemyAttack.SW05 && (bool)Get(enemy,"running"));
                Check((int)Get(ultimate,"thresholdsUsed")==2,"pending ultimate starts after SW03 and consumes its gate once");
            }
            else
            {
                place(8);Call(behavior,"RequestBasic");Set(enemy,"timer",0f);
                Check((bool)Call(behavior,"SwordCandidateAvailable",EnemyAttack.SW01,enemy.Position,8f,true,true),"ordinary special eligibility resumes immediately after SW03");
                Check((bool)Call(behavior,"Eligible",EnemyAttack.SWBasic,8f,true,true),"basic eligibility resumes immediately after SW03");
                // Restore an ordinary ready-basic decision without the previous
                // slide's intentional skill-follow-up preference in this fixture.
                Set(behavior,"skillAfterRetreat",false);
                object[] resumed={8f,true,true,default(EnemyAttack)};
                Check((bool)Call(behavior,"TrySelect",resumed) && (EnemyAttack)resumed[3]==EnemyAttack.SWBasic,"ready basic can be selected immediately after the ground timeline");
            }
            Pass("ACTIVE SW03 "+(enraged?"enraged":"normal")+": no shield, real damage in every round, retreats="+retreatCount+", approach travel="+approachTravel.ToString("F3")+", zero basic melee/ranged or extra specials, five rounds/"+blades.Count+" blades, fixed warnings, one same-frame release per round, exact single-sprite presentation, special gating/cleanup and deferred SW05 passed");
        }
        await SW03Transitions();
    }
    static async Task SW03GroundEntry()
    {
        foreach(string mode in new[]{"interrupt","disable","death","warning-restart"})
        {
            Reset(); Force(EnemyAttack.SW03);
            var phase=GroundPhase;var scope=(Transform)Get(phase,"groundRoot");
            await Seconds(.20f);
            Check(GroundRunning && !(bool)Get(phase,"firstRoundReleased"),"initial command has not struck during anticipation");
            Check(scope.GetComponentsInChildren<KarmaBossAttack>().Length==0,"no damage before first warning expires");
            if(mode=="warning-restart")
            {
                Set(game.Player,"stunUntil",Time.time+.20f);await Seconds(.22f);
                Check(!(bool)Get(phase,"firstRoundReleased"),"player stun restarts first warning without an early ground strike");
                await Until(()=>(bool)Get(phase,"firstRoundReleased"));
                Check(scope.GetComponentsInChildren<KarmaSW05Energy>().Count(fx=>fx.name=="SW03 first ground strike / no damage")==1,
                    "first actual release owns exactly one crack effect after warning restart");
                Call(enemy,"InterruptPattern");
            }
            else if(mode=="interrupt")Call(enemy,"InterruptPattern");
            else if(mode=="disable")enemy.enabled=false;
            else enemy.TakeDamage(enemy.MaxHealth*2);
            await Until(()=>scope==null);
            Check(scope==null && !GroundRunning,"cancelling initial command releases its waiter and ground scope: "+mode);
            var animator=(KarmaCharacterAnimator)Get(enemy,"animator");
            Check(animator.GetType().GetProperty("SwordBodyAction",F).GetValue(animator)==null,"cancelled command cannot regain the body: "+mode);
        }
        Pass("SW03 initial command cancellation, death, disable and player-stun warning restart");
    }
    static async Task SW03Transitions()
    {
        foreach(string transition in new[] {"interrupt","returned-counter","disable","combat","death"})
        {
            Reset();Force(EnemyAttack.SW03);var phase=GroundPhase;var scope=(Transform)Get(phase,"groundRoot");
            await Until(()=>SW03PlannedCount>0 && scope.GetComponentsInChildren<KarmaBossAttack>().Length==SW03PlannedCount);
            Check(GroundRunning && !enemy.Invulnerable,"vulnerable active phase before "+transition);
            if(transition=="interrupt")Call(enemy,"InterruptPattern");
            else if(transition=="returned-counter")
            {
                typeof(KarmaGame).GetProperty("StageIndex").SetValue(game,2);
                Call(Get(enemy,"behavior"),"OnReturnedCounterHit");await Until(()=>enemy.Stunned);
            }
            else if(transition=="disable")enemy.enabled=false;
            else if(transition=="combat")enemy.CancelCombat();
            else enemy.TakeDamage(enemy.MaxHealth*2);
            await Until(()=>scope==null,timeout:3);
            Check(scope==null && !(bool)phase.GetType().GetProperty("Running",F).GetValue(phase),"independent timeline/root canceled on "+transition);
            if(enemy!=null)Check(!enemy.Invulnerable,"no SW03 shield left after "+transition);
            if(transition=="returned-counter")
            {
                await Until(()=>!enemy.Stunned);
                var p=new Vector2(enemy.Position.x+8,KarmaSpace.PlayerHalfHeight+0.02f);
                game.Player.transform.position=p;game.Player.GetComponent<Rigidbody2D>().position=p;Physics2D.SyncTransforms();
                Call(Get(enemy,"behavior"),"RequestBasic");
                Set(enemy,"timer",0f);
                await Until(()=>(bool)Get(enemy,"running"));
                Check(!GroundRunning,"stagger recovery does not restart old SW03");
            }
        }
        Pass("ACTIVE SW03 interruption, returned-counter/stagger/recovery, disable, combat cancellation and real death cleanup passed");
    }
    static async Task JumpExplosion()
    {
        foreach (bool attack in new[] { false, true })
        {
            Reset(); var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
            player.enabled = false;
            body.position = new Vector2(40, KarmaSpace.PlayerHalfHeight + 0.02f);
            player.transform.position = body.position; Set(player,"invulnerableUntil",0f); Physics2D.SyncTransforms();
            var spec = new KarmaAerialSwordPlanner.Shot { origin = new Vector2(35,5.05f), velocity = Vector2.down*20,
                size = new Vector2(6,1), life = 0.1f };
            var blades = new List<KarmaSW05RainBlade>();
            for (int i = 0; i < 2; i++) blades.Add(KarmaSW05RainBlade.Spawn(game,enemy,Root,spec,10,null));
            await Until(() => blades.All(b => b.Landed));
            var rain = blades[0]; Vector2 size = rain.ExplosionSize;
            // Normal jump only: FixedUpdate sets jumpSpeed once, then the
            // physics step applies constant gravity. No variable-height hold.
            float jumpY = 0, jumpV = game.Config.jumpSpeed, apex = 0;
            float step = Time.fixedDeltaTime;
            for (int n = 0; n < Mathf.CeilToInt(2*game.Config.jumpSpeed/Mathf.Abs(Physics2D.gravity.y*game.Config.gravityScale)/step); n++)
            {
                jumpV += Physics2D.gravity.y*game.Config.gravityScale*step;
                jumpY += jumpV*step; apex = Mathf.Max(apex,jumpY);
            }
            float torso = apex+player.DamageBounds.extents.y;
            Check(Mathf.Abs(size.y-torso) < player.DamageBounds.size.y*0.05f,
                "existing ceiling is already approximately the ordinary jump torso; no gameplay height change required");
            Check(Mathf.Abs(rain.ExplosionCenter.y-size.y/2) < 0.00001f
                && Mathf.Abs(size.x-Mathf.Max(1.6f,spec.size.y*1.8f)) < 0.00001f,
                "original floor anchoring and horizontal damage width retained");
            Check(Mathf.Abs(size.y-KarmaSW05RainBlade.CalculateExplosionHeight(game.Config)) < 0.001f,"derived height used by damage geometry");
            foreach (var b in blades) b.ArmExplosion(0.60f);
            var energy = KarmaSW05Energy.Prepare(Root,game,blades,0.60f);
            CheckSW05RestoredWarning(energy,blades.ToArray());
            var zones = (Vector3[])Get(energy,"zones");
            Check(zones.Length == 1 && Mathf.Abs(zones[0].z-size.y) < 0.001f
                && Mathf.Abs(zones[0].x-(35-size.x/2)) < 0.001f
                && Mathf.Abs(zones[0].y-(35+size.x/2)) < 0.001f,"detonation art bounds retain the original horizontal edges and ceiling");
            body.bodyType = RigidbodyType2D.Dynamic;
            body.position = new Vector2(35,KarmaSpace.PlayerHalfHeight+0.02f);
            player.transform.position = body.position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            float before = player.Health, began = enemy.FightSeconds;
            var drive = player.gameObject.AddComponent<KarmaSwordRefreshJumpDriver>();
            drive.player = player; drive.VerticalTrial = true;
            if (attack)
            {
                // Allow a real 180 ms reaction before the jump, then use the
                // unchanged upward air slash 200 ms into that jump.
                drive.UpAttackAt = 0.38f;await Seconds(0.18f);
                body.linearVelocity = Vector2.up*game.Config.jumpSpeed;
            }
            else
            {
                float apexTime = game.Config.jumpSpeed/Mathf.Abs(Physics2D.gravity.y*game.Config.gravityScale);
                await Seconds(0.60f-apexTime);
                body.linearVelocity = Vector2.up*game.Config.jumpSpeed;
            }
            await Until(() => enemy.FightSeconds-began >= 0.60f);
            var bounds = player.GetComponent<Collider2D>().bounds;
            Check(bounds.min.x < 35+size.x/2 && bounds.max.x > 35-size.x/2,"player remains horizontally inside hazard");
            Check(attack ? bounds.min.y > size.y : bounds.min.y < size.y,"real collider distinguishes apex jump from upward jump attack");
            Check((float)Get(player,"invulnerableUntil") <= Time.time && !player.Shielded && !player.TestInvincible,"avoidance trial has no immunity or guard");
            float beforeVisualHealth = player.Health; bool beforeVisualShock = player.Shocked;
            energy.Ignite(); CheckSW05ImmediateDischarge(energy,blades.Count);
            Check(player.Health == beforeVisualHealth && player.Shocked == beforeVisualShock,
                "the bright flash and lightning geometry alone cause no damage or Shock");
            var ledger = new KarmaAttackContact();
            Call(Get(enemy,"behavior"),"RegisterFinalDetonation",ledger);
            foreach (var b in blades) b.Explode(ledger);
            Capture(attack ? "sw05-jump-attack-clears" : "sw05-normal-jump-hit");
            float after = player.Health;
            Check(player.Shocked==!attack,"only the colliding final blast applies Shock");
            float shockUntil=(float)Get(player,"shockUntil");
            Check(attack || Mathf.Abs(shockUntil-Time.time-0.75f)<0.001f,"blast Shock lasts 0.75 seconds");
            Check(attack ? after == before : Mathf.Abs(before-after-10/game.Config.BossTuning.sword.aerialSwordDamageMultiplier) < 0.001f,
                "overlapping explosion deals one unchanged hit; upward movement alone avoids it");
            await Until(() => blades.All(b => b == null), () =>
            {
                // Remove post-hit immunity to expose any repeated-contact bug.
                Set(player,"invulnerableUntil",0f);
                Check(player.Health == after && (float)Get(player,"shockUntil")==shockUntil,"no repeated explosion damage or CC throughout active frames");
                if (attack) Check(player.GetComponent<Collider2D>().bounds.min.y > size.y,"air slash stays above hazard through full 0.12-second damage interval");
            });
            Check(enemy.FightSeconds-began >= 0.60f+KarmaSW05RainBlade.ExplosionActiveSeconds-0.02f,"damage lifetime retained");
            Action observeAfterDamage = () =>
            {
                Set(player,"invulnerableUntil",0f);
                Check(player.Health == after && (float)Get(player,"shockUntil") == shockUntil,
                    "residual fragments and dying electricity never deal another hit or refresh Shock");
            };
            if (!attack)
            {
                await Until(()=>energy == null || energy.Age >= 0.18f,observeAfterDamage);
                Check(energy != null,"shared lightning remains alive for harmless afterimages");
                float pausedAge = energy.Age; int pausedQuads = (int)Get(energy,"count");
                var vertices = (Vector3[])Get(Get(energy,"buffer"),"vertices");
                var pausedVertices = vertices.Take(pausedQuads*4).ToArray();
                game.TogglePause();
                try
                {
                    await Task.Delay(120);
                    Check(energy.Age == pausedAge && (int)Get(energy,"count") == pausedQuads
                        && pausedVertices.Select((vertex,index)=>vertex == vertices[index]).All(same=>same),
                        "pause freezes the shared residual clock and all branching geometry");
                }
                finally { if (game.Paused) game.TogglePause(); }
            }
            await Until(()=>energy == null,observeAfterDamage);
            Check(Root.GetComponentsInChildren<KarmaSW05Energy>(true).Length == 0,
                "fragments and afterglow naturally release every electricity mesh");
            UnityEngine.Object.Destroy(drive); player.enabled = true;
            Pass((attack ? "upward jump attack avoids" : "normal jump at apex is hit by") + " real SW05 explosion; height=" + size.y.ToString("F4") + ", collider bottom=" + bounds.min.y.ToString("F4"));
        }
    }
    static async Task DashExplosion()
    {
        ResetElectric();var player=game.Player;var body=player.GetComponent<Rigidbody2D>();
        body.position=new Vector2(40,KarmaSpace.PlayerHalfHeight+0.02f);player.transform.position=body.position;Physics2D.SyncTransforms();
        var spec=new KarmaAerialSwordPlanner.Shot {origin=new Vector2(35,5.05f),velocity=Vector2.down*20,size=new Vector2(6,1),life=0.1f};
        var blade=KarmaSW05RainBlade.Spawn(game,enemy,Root,spec,10,null);
        await Until(()=>blade.Landed);
        blade.ArmExplosion(0.60f);var energy=KarmaSW05Energy.Prepare(Root,game,new List<KarmaSW05RainBlade>{blade},0.60f);
        body.position=new Vector2(35,KarmaSpace.PlayerHalfHeight+0.02f);player.transform.position=body.position;Physics2D.SyncTransforms();
        float before=player.Health, right=blade.ExplosionCenter.x+blade.ExplosionSize.x/2;
        await Seconds(0.60f);
        Call(player,"BeginDirectionalSlash",Vector2.right,game.Config.Techniques.pierceDistance,Essence.SwordWave);
        Physics2D.SyncTransforms();
        Check(player.GetComponent<Collider2D>().bounds.min.x>right,"existing dash clears full final explosion geometry");
        Set(player,"invulnerableUntil",0f);
        var ledger=new KarmaAttackContact();Call(Get(enemy,"behavior"),"RegisterFinalDetonation",ledger);
        energy.Ignite();blade.Explode(ledger);
        await Until(()=>blade==null);
        Check(player.Health==before && !player.Shocked && !ledger.Consumed,"dash arrival stays safe throughout original active window");
        Pass("SW05 dash avoids unchanged blast geometry without added immunity");
    }
    static void RenderPalette()
    {
        var node = new GameObject("isolated actual skill render"); var camera = node.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 3; camera.aspect = 1; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.transform.position = new Vector3(0, 0, -10);
        node.AddComponent<UniversalAdditionalCameraData>();
        var picture = new GameObject("palette probe"); picture.layer = 31; var sr = picture.AddComponent<SpriteRenderer>();
        var rt = new RenderTexture(256, 256, 24); var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var old = RenderTexture.active;
        try
        {
            foreach (var pose in new[] { "Sword_Crescent_v6", "Sword_Release_v6", "Sword_Impact_v6", "Sword_GuardRing_v6", "SW04_CounterBlade_v6", "SW03_GroundScar_v7", "SW03_RisingBlade_v7", "SW05_JudgementSword_v2", "SW05_Impact_1" })
            {
                sr.sprite = KarmaArtwork.Sprite("SwordBossVFX", pose); Check(sr.sprite != null, "linked " + pose);
                sr.color = Color.white; sr.transform.position = -sr.sprite.bounds.center * (4.5f / Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y));
                sr.transform.localScale = Vector3.one * (4.5f / Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y));
                sr.sharedMaterial = Resources.Load<Material>(pose.StartsWith("SW05") ? "SwordWave/SW05/SW05CrimsonBlade" : pose == "SW03_RisingBlade_v7" ? "SwordWave/SW03/KarmaSwordRisePolish" : "SwordWave/Shared/KarmaSwordSkillPolish");
                Check(sr.sharedMaterial != null && !UnityEditor.ShaderUtil.ShaderHasError(sr.sharedMaterial.shader), "valid shader " + pose);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
                int blue = 0, red = 0, magenta = 0;
                foreach (var c in pixels.GetPixels32()) if (c.a > 40)
                { if (c.b > c.r * 1.2f && c.b > 70) blue++; if (c.r > c.b * 1.3f && c.r > 70) red++; if (c.r > 220 && c.b > 220 && c.g < 40) magenta++; }
                Check(blue > 30 && red < blue / 4 && magenta == 0, "blue/white real Unity render " + pose + " blue=" + blue + " red=" + red);
            }
        }
        finally { RenderTexture.active = old; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(picture); UnityEngine.Object.Destroy(node); }
        Pass("nine connected effect sprites rendered blue/white in Unity URP; no shader fallback");
    }
    static void RenderPacked()
    {
        Reset();
        var node = new GameObject("packed render camera"); var camera = node.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 4; camera.aspect = 1; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.transform.position = new Vector3(0,3,-10); node.AddComponent<UniversalAdditionalCameraData>();
        var fx = KarmaSwordPackedVfx.Spawn(Root,game,KarmaSwordPackedVfx.Clip.Detonation,Vector2.zero,new Vector2(6,6),1,30,1,true);
        fx.gameObject.layer = 31; fx.SetProgress(3f/8);
        var rt = new RenderTexture(512,512,24); var pixels = new Texture2D(512,512,TextureFormat.RGBA32,false);
        var active = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; pixels.ReadPixels(new Rect(0,0,512,512),0,0); pixels.Apply();
            int lit = pixels.GetPixels32().Count(p => p.b > 30);
            System.IO.Directory.CreateDirectory("output/requested-sw03-sw05");
            System.IO.File.WriteAllBytes("output/requested-sw03-sw05/packed-isolated.png",pixels.EncodeToPNG());
            Check(lit > 1000,"packed shader produces visible animated texture, lit pixels="+lit);
            Pass("isolated supplied detonation render, lit pixels="+lit);
        }
        finally { RenderTexture.active = active; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(node); UnityEngine.Object.Destroy(fx.gameObject); }
    }
    // Stream frames to the existing encoder: no large temporary PNG sequence.
    sealed class MotionRecording : IDisposable
    {
        readonly Camera camera;
        readonly RenderTexture target = new RenderTexture(768, 480, 24);
        readonly Texture2D pixels = new Texture2D(768, 480, TextureFormat.RGBA32, false);
        readonly System.Diagnostics.Process encoder;
        readonly KarmaSwordElectricRecorder recorder;
        readonly List<double> costs = new List<double>();
        readonly string name;
        float next;
        int frames;
        public MotionRecording(string name)
        {
            this.name = name;
            string folder = Environment.GetEnvironmentVariable("KARMA_MOTION_OUTPUT");
            string ffmpeg = Environment.GetEnvironmentVariable("KARMA_FFMPEG");
            Check(!string.IsNullOrEmpty(folder) && System.IO.File.Exists(ffmpeg), "motion encoder/output configured");
            System.IO.Directory.CreateDirectory(folder);
            encoder = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ffmpeg,
                "-y -loglevel error -f rawvideo -pix_fmt rgba -s 768x480 -r 24 -i pipe:0 -vf vflip -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p \"" + System.IO.Path.Combine(folder, name + ".mp4") + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true });
            camera = new GameObject("Sword motion review camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 6; camera.aspect = 768f / 480;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .045f, .09f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            recorder = game.gameObject.AddComponent<KarmaSwordElectricRecorder>(); recorder.Tick = Paint;
            next = Time.time;
        }
        void Paint()
        {
            if (Time.time + .001f < next) return;
            next += 1f / 24;
            camera.transform.position = new Vector3(enemy.Position.x, enemy.Position.y + 2.4f, -10);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            clock.Stop(); costs.Add(clock.Elapsed.TotalMilliseconds);
            var old = RenderTexture.active;
            try
            {
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 768, 480), 0, 0); pixels.Apply();
                var bytes = pixels.GetRawTextureData<byte>().ToArray();
                encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length); frames++;
            }
            finally { RenderTexture.active = old; }
        }
        public void Dispose()
        {
            recorder.Tick = null; UnityEngine.Object.Destroy(recorder); encoder.StandardInput.Close();
            Check(encoder.WaitForExit(10000) && encoder.ExitCode == 0, "motion video encoded"); encoder.Dispose();
            UnityEngine.Object.Destroy(camera.gameObject); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
            Check(frames > 8, "motion capture advances"); costs.Sort();
            Pass("MOTION VIDEO " + name + ": " + frames + " frames at 24 fps; render-request CPU median=" + costs[costs.Count/2].ToString("F3") + "ms (not GPU frame time)");
        }
    }
    static async Task AppearanceOnly(bool recordMotion = true)
    {
        string[] forms = { "Wave", "WaveRise", "WaveWard", "WaveFinal" };
        var patterns = new[] { EnemyAttack.SWBasic, EnemyAttack.SWBasic, EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05 };
        int samples = 0, casts = 0;
        int.TryParse(Environment.GetEnvironmentVariable("KARMA_MOTION_SKIP"), out int alreadyVerified);
        string onlyPattern = Environment.GetEnvironmentVariable("KARMA_MOTION_PATTERN");
        int ordinal = 0;
        // Eight appearance/facing cases, then the fastest full-form timing at
        // 30 Hz simulation. Encoding at 24 fps alone is not a low-FPS test.
        for (int scenario = 0; scenario < 9; scenario++)
        {
            int form = scenario < 8 ? scenario / 2 : 3;
            int side = scenario % 2 == 0 ? -1 : 1;
            bool stress = scenario == 8;
            Time.captureDeltaTime = stress ? 1f / 30 : 1f / 60;
            typeof(KarmaSW02AnimationChecks).GetField("game", F).SetValue(null, game);
            typeof(KarmaSW02AnimationChecks).GetMethod("Reset", F).Invoke(null, new object[] { form, side });
            enemy = (KarmaEnemy)typeof(KarmaSW02AnimationChecks).GetField("enemy", F).GetValue(null);
            typeof(KarmaSwordPlayChecks).GetField("game", F).SetValue(null, game);
            typeof(KarmaSwordPlayChecks).GetField("enemy", F).SetValue(null, enemy);
            if (stress) typeof(KarmaEnemy).GetProperty("Health", F).SetValue(enemy, enemy.MaxHealth * .3f);
            Set(game.Player, "invulnerableUntil", Time.time + 1000);
            var animator = (KarmaCharacterAnimator)Get(enemy, "animator");
            var picture = (SpriteRenderer)Get(animator, "picture");
            var art = (Transform)Get(enemy, "art");
            var poses = (Dictionary<string, Sprite>)Get(animator, "poses");
            var idle = poses["Idle"];
            Check(idle.texture.name == forms[form], "requested own-form Idle was loaded");
            var baseScale = (Vector3)Get(animator, "pictureScale");
            var baseColor = (Color)Get(animator, "pictureColor");
            var baseMaterial = (Material)Get(animator, "swordBaseMaterial");
            var combat = (Sprite[])Get(animator, "sw02Frames");
            var heights = (float[])Get(animator, "swordIdleHeights");
            var bodySamples = (Vector4[,])Get(animator, "swordBodySamples");
            Check(combat != null && combat.Length == 6, "all own-form combat sprites loaded");
            Exception observationError = null;
            bool sawCombat = false;
            bool checkedDamageIsolation = false;
            var displayed = new HashSet<string>();
            float firstRaised = -1, lastRaised = -1;
            var observer = new GameObject("Sword final-renderer appearance observer").AddComponent<KarmaSwordElectricRecorder>();
            observer.Tick = () =>
            {
                if (observationError != null) return;
                try
                {
                    Check(picture.sprite != null && picture.enabled, "body remains visible after LateUpdate");
                    displayed.Add(picture.sprite.name);
                    if (Get(enemy, "selected").ToString() == "SW03" && picture.sprite.name == "GroundRaise")
                    { if (firstRaised < 0) firstRaised = Time.time; lastRaised = Time.time; }
                    if (!(bool)Get(enemy, "running")) Set(enemy, "timer", float.MaxValue);
                    Check(picture.sprite.texture.name.StartsWith(forms[form], StringComparison.Ordinal), "no other form's sprite replaces body");
                    int index = Array.IndexOf(combat, picture.sprite);
                    var returns = (Sprite[])Get(animator, "basicReturnFrames");
                    int back = returns == null ? -1 : Array.IndexOf(returns, picture.sprite);
                    var skills = (Sprite[])Get(animator,"swordSkillFrames");
                    int skill = skills == null ? -1 : Array.IndexOf(skills,picture.sprite);
                    bool calibrated = index >= 0 || back >= 0 || skill >= 0;
                    float factor = calibrated ? heights[form] / idle.pixelsPerUnit
                        / (skill >= 0 ? ((float[])Get(animator,"swordSkillHeights"))[form] / skills[0].pixelsPerUnit
                        : bodySamples[form, index >= 0 ? 0 : 6].x / (index >= 0 ? combat[0] : returns[0]).pixelsPerUnit) : 1;
                    Check((picture.transform.localScale - baseScale * factor).sqrMagnitude < .000001f, "final body scale remains registered to Idle");
                    Check(Mathf.Abs(Mathf.Abs(art.localScale.x) - KarmaSpace.GuardianArtScale) < .0001f
                        && Mathf.Abs(art.localScale.y - KarmaSpace.GuardianArtScale) < .0001f, "parent has no second body scale");
                    Check(picture.sharedMaterial == (calibrated ? (Material)Get(animator, "swordBodyMaterial") : baseMaterial), "final body material restored for selected sprite");
                    if (!enemy.Stunned && Time.time >= (float)Get(animator, "hurtUntil") && Time.time >= (float)Get(animator, "damageFlashUntil"))
                        Check(picture.color == baseColor, "final body tint equals own Idle tint");
                    if (calibrated && !checkedDamageIsolation)
                    {
                        var bounds = enemy.DamageBounds; var p = picture.transform.localPosition;
                        var q = picture.transform.localRotation; var s = picture.transform.localScale;
                        try
                        {
                            picture.transform.localPosition += new Vector3(20, 30, 0);
                            picture.transform.localRotation = Quaternion.Euler(0, 0, 37); picture.transform.localScale *= 2;
                            var moved = enemy.DamageBounds;
                            Check((bounds.center - moved.center).sqrMagnitude < .000001f && (bounds.size - moved.size).sqrMagnitude < .000001f,
                                "body displacement/rotation/scale cannot change incoming hit geometry");
                            checkedDamageIsolation = true;
                        }
                        finally { picture.transform.localPosition = p; picture.transform.localRotation = q; picture.transform.localScale = s; }
                    }
                    sawCombat |= calibrated; samples++;
                }
                catch (Exception e) { observationError = e; }
            };
            try
            {
                for (int cast = 0; cast < patterns.Length; cast++)
                {
                    if (ordinal++ < alreadyVerified) continue;
                    var pattern = patterns[cast];
                    if (!string.IsNullOrEmpty(onlyPattern) && pattern.ToString() != onlyPattern) continue;
                    displayed.Clear(); checkedDamageIsolation = false;
                    firstRaised = lastRaised = -1;
                    string label = pattern == EnemyAttack.SWBasic ? (cast == 0 ? "BasicMelee" : "BasicRanged") : pattern.ToString();
                    enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
                    var at = new Vector2(20 + side * (cast == 0 ? 3 : 15), KarmaSpace.PlayerHalfHeight + .02f);
                    game.Player.transform.position = at; game.Player.GetComponent<Rigidbody2D>().position = at;
                    art.localScale = new Vector3(side * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    Physics2D.SyncTransforms();
                    using (var recording = recordMotion && (form == 0 && side == 1 || form == 3 && side == -1)
                        ? new MotionRecording(forms[form] + (stress ? "-Enraged30fps" : "") + "-" + label) : null)
                    {
                        Force(pattern);
                        if (pattern == EnemyAttack.SW03) Check(GroundRunning, "SW03 independent timeline actually started");
                        if (pattern == EnemyAttack.SW03 && onlyPattern == "SW03")
                        {
                            await Seconds(.3f);
                            Check(Mathf.Abs(enemy.Position.x - 20) < .05f, "safe SW03 command plants the feet instead of sliding");
                            await Until(()=>(bool)Get(GroundPhase,"firstRoundReleased"));
                            await Seconds(.25f);
                            Check(Mathf.Abs(enemy.Position.x - 20) > .1f, "ordinary spacing resumes after the first ground strike");
                        }
                        await Until(() => !(bool)Get(enemy, "running") || observationError != null, timeout: 45);
                        Set(enemy, "timer", float.MaxValue);
                        if (pattern == EnemyAttack.SW03) await Until(() => !GroundRunning || observationError != null, timeout: 45);
                        if (pattern == EnemyAttack.SW04)
                        {
                            // Exercise real absorption and all four responses,
                            // not just the initial dome-forming animation.
                            enemy.TakeDamage(1);
                            var counter = Get(Get(enemy, "behavior"), "counter");
                            await Until(() => (bool)Get(counter, "guardResponseRunning") || observationError != null);
                            await Until(() => !(bool)Get(counter, "guardResponseRunning") || observationError != null);
                        }
                        await Seconds(.2f);
                    }
                    if (observationError != null) throw observationError;
                    Check(displayed.Count >= 3 && checkedDamageIsolation, label + " displays distinct action poses with independent hit geometry");
                    if (pattern == EnemyAttack.SW03)
                        Check(displayed.Contains("GroundRaise") && displayed.Contains("GroundDrop") && displayed.Contains("GroundStrike") && lastRaised - firstRaised >= .075f,
                            "SW03 raises, drops and strikes exactly at first release before spacing resumes");
                    if (pattern != EnemyAttack.SW04)
                        Check(!(bool)animator.GetType().GetProperty("SwordBodyVisible", F).GetValue(animator), label + " naturally releases body ownership before explicit interruption");
                    casts++;
                    // Interrupt the separately owned ward, then verify the real
                    // Update/LateUpdate pipeline returns to the authored Idle.
                    Call(enemy, "InterruptPattern"); Set(enemy, "timer", float.MaxValue);
                    Call(Get(Get(enemy, "behavior"), "movement"), "Interrupt", false);
                    // Keep the real pose/render update alive but prevent the
                    // spacing AI from walking during the Idle-restoration check.
                    Set(enemy, "running", true); Set(enemy, "patternStarted", enemy.FightSeconds);
                    await Seconds(.4f);
                    if (observationError != null) throw observationError;
                    Check(picture.sharedMaterial == baseMaterial, "interruption restores Idle material");
                    Check((picture.transform.localScale - baseScale).sqrMagnitude < .000001f, "interruption restores Idle scale");
                    Set(enemy, "running", false);
                    Pass("MOTION " + forms[form] + " facing=" + side + (stress ? " enraged 30fps" : "") + " " + label + ": completed and restored Idle");
                }
                if (!string.IsNullOrEmpty(onlyPattern))
                {
                    if (onlyPattern == "SW03" && scenario < 2)
                    {
                        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
                        var close = new Vector2(20 + side * 3, KarmaSpace.PlayerHalfHeight + .02f);
                        var initial = scenario == 0 ? close : new Vector2(20 + side * 15, close.y);
                        game.Player.transform.position = initial; game.Player.GetComponent<Rigidbody2D>().position = initial;
                        Set(Get(Get(enemy, "behavior"), "movement"), "nextSwordRetreat", 0f);
                        Physics2D.SyncTransforms(); Force(EnemyAttack.SW03);
                        if (scenario == 1)
                        {
                            await Seconds(.1f);
                            Check(Mathf.Abs(enemy.Position.x - 20) < .05f, "mid-command pressure starts from planted feet");
                            game.Player.transform.position = close; game.Player.GetComponent<Rigidbody2D>().position = close;
                            Physics2D.SyncTransforms();
                        }
                        await Seconds(.2f);
                        Check(GroundRunning && Mathf.Abs(enemy.Position.x - 20) < .05f, "initial ground strike keeps its planted anticipation under pressure");
                        await Until(()=>(bool)Get(GroundPhase,"firstRoundReleased"));
                        await Seconds(.35f);
                        Check(GroundRunning && Mathf.Abs(enemy.Position.x - 20) > .5f,
                            "urgent evasion resumes after the single initial ground strike");
                        Call(enemy, "InterruptPattern");
                        Pass("SW03 urgent evasion remains live facing=" + side);
                    }
                    if (observationError != null) throw observationError;
                    continue;
                }
                animator.Hurt(); Call(animator, "StartDamageFlash", .3f);
                await Seconds(.08f);
                Check(picture.color == baseColor * new Color(1,.45f,.45f), "hit flash survives final body sample");
                await Seconds(.4f);
                Check(picture.color == baseColor, "hit flash returns to own Idle tint");
                Force(EnemyAttack.SW01); await Seconds(.12f);
                game.TogglePause(); var pausedSprite = picture.sprite; var pausedRotation = picture.transform.localRotation;
                await Task.Delay(120);
                Check(picture.sprite == pausedSprite && Quaternion.Angle(picture.transform.localRotation, pausedRotation) < .001f, "pause freezes displayed motion");
                game.TogglePause(); Set(enemy, "pendingParry", true);
                await Until(() => enemy.Stunned || observationError != null);
                await Seconds(.05f);
                Check(animator.GetType().GetProperty("SwordBodyAction", F).GetValue(animator) == null, "real parry stun cancels action pose");
                Check(Mathf.Abs((float)Get(art.GetComponent<KarmaWaveMotion>(), "motionAngle")) < .001f,
                    "stun clears attack lean while retaining its separately owned status sway");
                Check(sawCombat || ordinal <= alreadyVerified, "rendered authored combat frames, not a frozen Idle");
                if (observationError != null) throw observationError;
                Pass("APPEARANCE " + forms[form] + " facing=" + side + ": SWBasic-SW05, final sprite/scale/tint/material/parent, interruption and hit restore");
                observer.Tick = null;
                enemy.TakeDamage(enemy.MaxHealth * 2);
                await Task.Delay(40);
                Check(!enemy.Alive && animator.GetType().GetProperty("SwordBodyAction", F).GetValue(animator) == null, "death clears action ownership");
            }
            finally { observer.Tick = null; UnityEngine.Object.Destroy(observer.gameObject); }
        }
        Pass("APPEARANCE COMPLETE: " + casts + " real casts (" + alreadyVerified + " previously verified); " + samples + " final LateUpdate renderer samples; four forms x two directions plus enraged 30fps");
    }
    static async void Run()
    {
        int code = 0;
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Application.targetFrameRate = 120; Time.captureDeltaTime = 1f / 60;
            Application.logMessageReceived += (m, s, t) => { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) error = m+"\n"+s; };
            int stage = SessionState.GetInt(Key + ".Stage", 0);
            if(SessionState.GetBool(Key+".AppearanceOnly",false))
            {
                if(SessionState.GetBool(Key+".GroundEntryReview",false)) await SW03GroundEntry();
                await AppearanceOnly(Environment.GetEnvironmentVariable("KARMA_MOTION_NO_VIDEO")!="1"); Check(error==null,"runtime error: "+error);
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".VisualCleanup",false))
            {
                if(stage==0){await SW03Transitions();await SW04Sequence();}
                await SW04();
                Check(error==null,"runtime error: "+error);Pass("SW03 CLEANUP AND SW04 VISUAL CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".GroundedVfxReview",false))
            {
                Check(GraphicsSettings.currentRenderPipeline!=null,"validation copy must use the project's render pipeline");
                if(Environment.GetEnvironmentVariable("KARMA_REVIEW_FROM")!="SW05"){await ContinuousAimOnly();await SW04Sequence();}
                await BasicLinkOnly();await SW03Active();
                await AppearanceOnly(Environment.GetEnvironmentVariable("KARMA_MOTION_NO_VIDEO")!="1");
                Check(error==null,error??"no runtime errors");
                Pass("connected melee, skill commands, one initial SW03 ground strike, pale ward and path facets review complete");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".SkillVisualReview",false))
            {
                await BasicLinkOnly();await SW05();await SW03Active();await SW04Sequence();
                Check(error==null,"runtime error: "+error);Pass("SW03/SW04/SW05 VISUAL REVIEW CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".ShockAndDetonation",false))
            {
                await ShockRouting();await Electrocution();await SW05();await JumpExplosion();await DashExplosion();
                Check(error==null,"runtime error: "+error);Pass("SHOCK AND FINAL DETONATION CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".EvasionAndWard",false))
            {
                await SW03Active();await ContinuousAimOnly();await Electrocution();Check(error==null,"runtime error: "+error);Pass("EVASION ONLY, ORIGINAL AIM AND SW04 PRESERVATION CHECKS COMPLETE");
                await FrequencyOnly();Check(error==null,"runtime error: "+error);Pass("EVASION AND FREQUENCY CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".FrequencyOnly",false))
            {
                await FrequencyOnly();Check(error==null,"runtime error: "+error);Pass("CONTROLLED 120 SECOND FREQUENCY CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".ElectrocutionOnly",false))
            {
                await Electrocution();Check(error==null,"runtime error: "+error);Pass("SW04 ELECTROCUTION CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".ActiveSW03Only",false))
            {
                await SW03Active();if(!SessionState.GetBool(Key+".ScarPolish",false))await ContinuousAimOnly();Check(error==null,"runtime error: "+error);Pass("ACTIVE SW03 CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if(SessionState.GetBool(Key+".ContinuousAimOnly",false))
            {
                await ContinuousAimOnly();Check(error==null,"runtime error: "+error);Pass("SW01/SW02 CONTINUOUS AIM CHECKS COMPLETE");
                Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
            }
            if (SessionState.GetBool(Key + ".PackedRenderOnly",false))
            {
                RenderPacked(); Time.captureDeltaTime = 0; SessionState.SetBool(Key,false); EditorApplication.Exit(0); return;
            }
            if (SessionState.GetBool(Key + ".RequestedChanges", false))
            {
                bool rainOnly = SessionState.GetBool(Key + ".RainOnly",false);
                bool vfxOnly = SessionState.GetBool(Key + ".VfxOnly",false);
                bool wardFinal = SessionState.GetBool(Key + ".WardFinal",false);
                if (vfxOnly) RenderPacked();
                if (!rainOnly && !wardFinal) await SW03Active();
                if (SessionState.GetBool(Key+".Grand",false) || wardFinal) { await SW04Sequence(); await SW04(); }
                if (SessionState.GetBool(Key+".WardOnly",false))
                {
                    Check(error==null,"runtime error: "+error);Pass("SW04 FOCUSED CHECKS COMPLETE");
                    Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;
                }
                await SW05();
                if (!rainOnly && !vfxOnly) await JumpExplosion();
                foreach (string material in new[] { "SwordWave/SW05/SW05Celestial", "SwordWave/SW05/SW05CrimsonBlade", "SwordWave/SW05/SW05EmbeddedWood" })
                {
                    var asset = Resources.Load<Material>(material);
                    Check(asset != null && !UnityEditor.ShaderUtil.ShaderHasError(asset.shader),"SW05 shader compiles: " + material);
                }
                Check(error == null,"runtime error: " + error); Pass("REQUESTED SW03/SW05 CHECKS COMPLETE at normal speed");
                Time.captureDeltaTime = 0; SessionState.SetBool(Key,false); EditorApplication.Exit(0); return;
            }
            if (SessionState.GetBool(Key + ".SW03Only", false))
            {
                int step = SessionState.GetInt(Key + ".SW03Step", 0);
                if (step != 1) await SW03();
                if (step != 2) { await JumpAcrossBlade(); await DashAcrossBlade(); }
                Check(error == null, "runtime error: " + error); Pass("SW03 focused checks complete; selection=" + step);
                Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(0); return;
            }
            if (stage <= 0) await SW01();
            if (stage <= 1) await SW02();
            if (stage <= 2) await SW03();
            if (stage <= 3) { await JumpAcrossBlade(); await DashAcrossBlade(); await SW04(); }
            if (stage <= 4) await SW05();
            if (stage <= 5) await JumpExplosion();
            RenderPalette();
            Check(error == null, "runtime error: " + error); Pass("ALL REQUESTED FOCUSED CHECKS");
        }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(code);
    }
}
public sealed class KarmaSwordRefreshJumpDriver : MonoBehaviour
{
    public KarmaPlayer player; public float age; public bool slashed;
    public bool VerticalTrial; public float UpAttackAt = -1;
    public bool WalkOnly; public float TargetX, ReactAt, MoveSpeed;
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    void FixedUpdate()
    {
        age += Time.fixedDeltaTime;
        if (VerticalTrial)
        {
            typeof(KarmaPlayer).GetField("move",F).SetValue(player,0f);
            if (!slashed && UpAttackAt > 0 && age >= UpAttackAt)
            { slashed = true; typeof(KarmaPlayer).GetMethod("BeginAirSlash",F).Invoke(player,new object[] { Vector2.up }); }
            typeof(KarmaPlayer).GetMethod("FixedUpdate",F).Invoke(player,null); return;
        }
        if (WalkOnly)
        {
            float move = Time.time < ReactAt ? 0 : Mathf.Clamp((TargetX - player.Position.x) / (MoveSpeed * Time.fixedDeltaTime), -1, 1);
            typeof(KarmaPlayer).GetField("move", F).SetValue(player, move);
            typeof(KarmaPlayer).GetMethod("FixedUpdate", F).Invoke(player, null); return;
        }
        typeof(KarmaPlayer).GetField("move", F).SetValue(player, 1f);
        if (!slashed && age >= 0.42f)
        { slashed = true; typeof(KarmaPlayer).GetMethod("BeginAirSlash", F).Invoke(player, new object[] { Vector2.right }); }
        typeof(KarmaPlayer).GetMethod("FixedUpdate", F).Invoke(player, null);
    }
}
[DefaultExecutionOrder(650)]
public sealed class KarmaSwordElectricRecorder : MonoBehaviour
{
    public Action Tick;
    void LateUpdate() { Tick?.Invoke(); }
}
public sealed class KarmaSwordAimProbe : MonoBehaviour
{
    public Action Tick;
    void LateUpdate() { Tick?.Invoke(); }
}
#endif
