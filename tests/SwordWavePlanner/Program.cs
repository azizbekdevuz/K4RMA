using System;
using System.Collections.Generic;
using System.Linq;
using KarmaPrototype;

static class Program
{
    const float Left=-15, Right=55, Width=70f/32, Half=0.7f, Speed=11.2f;
    const float Height=(26f*26/(2*9.81f*6)+3.1f+0.6f-0.05f)*0.92f;
    const float Visual=Height*704/1354*0.76f, Margin=0.25f;
    static int plans,fallbacks,attempts,unsafeCandidates,spacingCandidates,similarCandidates,transitions;
    static readonly List<int> counts=new();
    static readonly List<float> widths=new(),centerWidths=new(),internalSteps=new(),clusterSpans=new(),corridorShifts=new(),corridorCenterShifts=new(),layoutShifts=new();
    static readonly HashSet<string> combinations=new(),regions=new();
    static readonly Dictionary<int,int> clusterCounts=new(){{1,0},{3,0},{4,0}};
    static void Require(bool ok,string reason) { if(!ok)throw new Exception(reason); }
    static string Signature(KarmaSwordScarPlanner.Plan p)
        =>string.Join(',',p.Positions.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+"/"+string.Join(',',p.ClusterSizes);
    static float Reaction(float x,float velocity,Func<float,bool> clear=null)
    {
        float target=Math.Clamp(x+Math.Clamp(velocity*0.28f,-6,6),Left+Half,Right-Half);
        if(clear==null || clear(target))return target;
        float result=x;
        for(int i=1;i<=10000;i++) {float probe=x+(target-x)*i/10000;if(!clear(probe))break;result=probe;}
        return result;
    }
    static bool Hits(KarmaSwordScarPlanner.Plan p,float oldX,float x,float t,float warning)
    {
        for(int i=0;i<p.Positions.Length;i++)
        {
            float release=warning;
            // Full final contact throughout active time is conservative against
            // the unchanged smaller early-rise frames. No jump or invulnerability.
            if(t>=release && t<=release+0.65f && KarmaBossGeometry.SweptBox(
                p.Positions[i]-oldX,Height/2-1.55f,p.Positions[i]-x,Height/2-1.55f,
                0,0,Width/2+Half,Height/2+1.55f))return true;
        }
        return false;
    }
    static void Walk(KarmaSwordScarPlanner.Plan p,float x,float velocity,float warning,float speed,Func<float,bool> clear,
        KarmaSwordScarPlanner.Plan previous=null,float previousWarning=0.8f)
    {
        float reaction=Reaction(x,velocity,clear),old=x;
        Require(Math.Abs(p.SafeX-reaction)<=speed*(warning-0.28f)+0.003f,"reaction-delayed ordinary route arrives before first release");
        // SW03 starts the next warning only after the common active lifetime + rest,
        // and ClearRound deactivates the old scope. Still include old intervals in
        // this temporal collision oracle rather than checking only the endpoint.
        float offset=previousWarning+0.65f+0.32f;
        for(float t=0;t<=warning+0.65f+0.001f;t+=1f/240)
        {
            float at=t<0.28f?x+(reaction-x)*t/0.28f:
                reaction+Math.Sign(p.SafeX-reaction)*Math.Min(Math.Abs(p.SafeX-reaction),Math.Max(0,t-0.28f)*speed);
            Require(!Hits(p,old,at,t,warning),"entire route clears the simultaneous active window");
            if(previous!=null)Require(!Hits(previous,old,at,t+offset,previousWarning),"previous-round hazards do not block the next reaction/walk");
            old=at;
        }
    }
    static void Validate(KarmaSwordScarPlanner.Plan p,float x,float velocity,float warning=0.8f,float speed=Speed,Func<float,bool> clear=null,
        KarmaSwordScarPlanner.Plan previous=null)
    {
        int n=p.Positions.Length;
        Require(n>0 && n<=26 && p.ClusterSizes.Sum()==n,"dynamic count and warnings match");
        Require(p.Positions.SequenceEqual(p.Positions.OrderBy(a=>a)) && p.Positions.Distinct().Count()==n,"sorted distinct real sword positions");
        Require(p.ClusterSizes.All(c=>c==1||c==3||c==4),"only 1/3/4 spatial clusters");
        Require(p.ContactCount==p.ClusterSizes.Length && p.ContactIndices.Length==n && p.SwordClusters.Length==n,"explicit cluster-local contact mapping");
        int site=0;
        for(int c=0;c<p.ClusterSizes.Length;c++)
        {
            int first=site,size=p.ClusterSizes[c];clusterCounts[size]++;
            for(int i=0;i<size;i++,site++)
            {
                float at=p.Positions[site];
                Require(at-Width/2>=Left-0.0001f && at+Width/2<=Right+0.0001f,"unchanged individual hitbox stays in arena");
                Require(p.SwordClusters[site]==c && p.ContactIndices[site]==c,"sword -> cluster -> ledger mapping, independent of release time");
                if(i>0)
                {
                    float step=at-p.Positions[site-1];internalSteps.Add(step);
                    Require(step>=Visual*264/704+0.15f-0.0001f && step<=KarmaSwordScarPlanner.InternalSpacing(Visual)+0.3001f,"measured opaque blade body separation, not player gap");
                    Require(step<Width,"internal hitboxes can overlap coherently");
                }
                else if(site>0)Require(at-p.Positions[site-1]-Width>=2*Half+0.1f-0.0001f,"independent clusters cannot share one player-body encounter");
            }
            clusterSpans.Add(p.Positions[site-1]-p.Positions[first]+Width);
        }
        // Independent interval union from the full damage rectangles.
        var union=new List<(float l,float r)>();
        foreach(float at in p.Positions)
        {
            var interval=(at-Width/2,at+Width/2);
            if(union.Count>0 && interval.Item1<=union[^1].r)union[^1]=(union[^1].l,Math.Max(union[^1].r,interval.Item2));
            else union.Add(interval);
        }
        Require(union.Count==p.ClusterSizes.Length,"overlapping swords merge into their cluster's danger union");
        foreach(var corridor in p.Corridors)
        {
            Require(corridor.Left>=Left-0.0001f && corridor.Right<=Right+0.0001f && corridor.Width>=4*Half-0.0001f,"corridor wholly inside arena and >=2 player widths");
            Require(union.All(u=>u.r<=corridor.Left+0.0001f || u.l>=corridor.Right-0.0001f),"corridor is genuinely free between union boundaries");
            Require(Math.Abs(corridor.CenterMin-corridor.Left-Half-Margin)<0.0001f
                && Math.Abs(corridor.Right-corridor.CenterMax-Half-Margin)<0.0001f,"center interval subtracts collider half width AND tolerance");
            Require(corridor.CenterMax-corridor.CenterMin>=0.9f-0.0001f,"positive usable player-center interval");
            widths.Add(corridor.Width);centerWidths.Add(corridor.CenterMax-corridor.CenterMin);
        }
        Require(p.Corridors.Length>=(p.Fallback?1:2),"two physical corridors preferred; bounded fallback still has one");
        Require(p.Corridors.Any(c=>p.SafeX>=c.CenterMin-0.0001f && p.SafeX<=c.CenterMax+0.0001f),"selected safe center lies in a full corridor");
        float margin=Math.Min(p.SafeX-Left,Right-p.SafeX)-Half;
        foreach(float at in p.Positions)margin=Math.Min(margin,Math.Abs(at-p.SafeX)-Width/2-Half);
        Require(margin>=Margin-0.0001f && Math.Abs(margin-p.SafeClearance)<0.001f,"reported clearance matches union and arena boundaries");
        Require(clear==null || clear(p.SafeX),"actual route predicate accepts the entire horizontal sweep");
        Walk(p,x,velocity,warning,speed,clear,previous);Walk(p,x,0,warning,speed,clear,previous);
        var ledgers=Enumerable.Range(0,p.ContactCount).Select(_=>new KarmaAttackContact()).ToArray();int hits=0;
        for(int i=0;i<n;i++)for(int frame=0;frame<3;frame++)
            if(ledgers[p.ContactIndices[i]].TryConsume()){ledgers[p.ContactIndices[i]].RecordDamage();hits++;}
        Require(hits==p.ContactCount,"overlap never multiplies accepted cluster damage; unrelated clusters remain live");
        var blocked=new KarmaAttackContact();Require(blocked.TryConsume() && !blocked.TryConsume() && !blocked.DealtDamage,"blocked/immune encounter still consumes without recording damage");
        counts.Add(n);plans++;if(p.Fallback)fallbacks++;
        attempts+=p.CandidatesTried;unsafeCandidates+=p.UnsafeCandidates;spacingCandidates+=p.SpacingCandidates;similarCandidates+=p.SimilarCandidates;
        combinations.Add(string.Join(',',p.ClusterSizes));regions.Add(string.Join(',',Enumerable.Range(0,4).Select(q=>p.Positions.Count(at=>Math.Clamp((int)((at-Left)*4/(Right-Left)),0,3)==q))));
    }
    static KarmaSwordScarPlanner.Plan Draw(int seed,float x,float velocity,float warning=0.8f,float speed=Speed,Func<float,bool> clear=null,float[] previous=null,float safe=float.NaN)
    {
        Require(KarmaSwordScarPlanner.TryPlan(new Random(seed),Left,Right,Width,Visual,x,Half,velocity,speed,warning,safe,clear,out var p,previous),
            $"current-state route seed={seed} x={x} v={velocity}");return p;
    }
    static void SeedChecks()
    {
        var seeds=new HashSet<int>();for(int i=0;i<10000;i++)Require(seeds.Add(KarmaSwordScarPlanner.NextSeed()),"normal casts do not reset seed sequence");
        var field=typeof(KarmaSwordScarPlanner).GetField("seedSequence",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        int saved=(int)field.GetValue(null);
        try
        {
            foreach(int boundary in new[]{-2,int.MaxValue-2,0x3fffffff-2})
            {
                field.SetValue(null,boundary);string previous=null;
                for(int i=0;i<5;i++){var r=new Random(KarmaSwordScarPlanner.NextSeed());string stream=string.Join(',',Enumerable.Range(0,8).Select(_=>r.Next()));Require(stream!=previous,"unique seed streams at signed boundaries");previous=stream;}
            }
        }
        finally{field.SetValue(null,saved);}
    }
    static void ClockChecks()
    {
        var clock=new KarmaSwordScarClock(100);float expected=0;
        Require(clock.Sample(100,0.02f,false)==0,"every blade and aura starts at age zero on the creation frame");
        for(int frame=101;frame<201;frame++)
        {
            bool paused=frame>=110 && frame<120;
            float dt=frame%3==0?0.08f:frame%3==1?1f/60:1f/144;
            if(!paused)expected+=dt;
            // 26 blade Updates and 26 aura LateUpdates, sampled in any order.
            for(int sample=0;sample<52;sample++)
                Require(Math.Abs(clock.Sample(frame,dt,paused)-expected)<0.000001f,"one shared age through frame drops, pause and resume; never 52 time increments");
        }
        Console.WriteLine("CLOCK PASS: 26 blades + 26 auras share creation age, per-frame elapsed time and pause/resume; removing 0.25 x 5 shortens nominal cast by 1.25 seconds.");
    }
    // Selection-opportunity simulation, not a timed combat or Unity measurement.
    // Match the inspected reservoir sampler; eligibility masks are explicit so
    // cooldown/formed ward/range limits are never mistaken for larger weights.
    static int[] SelectionSample(float[] weights,bool[] eligible,bool dueBasics,bool repeatProtection)
    {
        var random=new Random(40304);var result=new int[5];int previous=-1,basics=0;bool due=false;
        for(int draw=0;draw<100000;draw++)
        {
            int selected=-1;float total=0;
            if(dueBasics && due && eligible[0])selected=0;
            else
                for(int candidate=0;candidate<5;candidate++)
                {
                    if(!eligible[candidate] || candidate==0 && repeatProtection && basics>=2
                        || candidate>0 && repeatProtection && candidate==previous)continue;
                    float weight=weights[candidate];if(weight<=0)continue;
                    total+=weight;
                    if(total<=weight || random.NextDouble()*total<weight)selected=candidate;
                }
            Require(selected>=0,"selection scenario has an eligible alternative");
            Require(!repeatProtection || selected==0 || selected!=previous,"skills cannot immediately repeat while alternatives exist");
            Require(!repeatProtection || selected!=0 || basics<2,"at most two consecutive basics while skills are available");
            basics=selected==0?basics+1:0;
            result[selected]++;previous=selected;due=selected!=0;
        }
        return result;
    }
    static void SelectionChecks()
    {
        var root=new System.IO.DirectoryInfo(Environment.CurrentDirectory);
        while(root!=null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName,"AGENTS.md")))root=root.Parent;
        Require(root!=null,"run inside the existing repository");
        string Read(string path)=>System.IO.File.ReadAllText(System.IO.Path.Combine(root.FullName,path));
        string asset=Read("Assets/Data/Resources/KarmaSwordPatternWeights.asset");
        string defaults=Read("Assets/Scripts/SwordWave/KarmaSwordPatternWeights.cs");
        string[] names={"swBasicWeight","sw01Weight","sw02Weight","sw03Weight","sw04Weight"};
        var current=names.Select(name=>float.Parse(System.Text.RegularExpressions.Regex.Match(asset,@"\b"+name+@":\s*([0-9.]+)").Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Require(current.SequenceEqual(new[]{3f,3f,3f,3f,3f}),"active asset changes only SW03/SW04 from 2 to 3");
        foreach(string name in names)Require(defaults.Contains(name+" = 3f"),"missing-asset defaults match active weights: "+name);
        string behavior=Read("Assets/Scripts/SwordWave/KarmaSwordBossBehavior.cs");
        string counter=Read("Assets/Scripts/SwordWave/Patterns/SW04.cs");
        string scar=Read("Assets/Scripts/SwordWave/Patterns/SW03.cs");
        Require(behavior.Contains("if (ground.Running) return false;")
            && behavior.Contains("ultimate.ThresholdPending && !counter.Formed")
            && behavior.IndexOf("next = EnemyAttack.SW05; return true;")<behavior.IndexOf("float total = !skillAfterRetreat"),"SW03 exclusion and SW05 priority precede weighted selection");
        Require(behavior.Contains("consecutiveBasics < 2 && (swordBasicDue || basicAfterRetreat)")
            && behavior.Contains("candidate == owner.previous") && behavior.Contains("Settings.Weights.allowRepeatFallback")
            && behavior.Contains("counter.Formed || owner.FightSeconds < counter.NextReadyAt")
            && behavior.Contains("counter.Formed && candidate != EnemyAttack.SW01 && candidate != EnemyAttack.SW02")
            && behavior.Contains("distance >= KarmaSpace.Units(4)"),"forced basic, repeat, cooldown, formed ward and range gates retained");
        Require(counter.Contains("Mathf.Max(8, Settings.swordGuardDuration + 4)")
            && counter.Contains("new KarmaAttackContact[4]") && counter.Contains("volley.Contacts[later].TryConsume();")
            && counter.Contains("slot.gameObject.SetActive(false);")
            && behavior.Contains("ParryStunSeconds { get { return 2; }")
            && behavior.Contains("kind = KarmaPlayer.CrowdControlKind.Shock; seconds = 0.75f;")
            && behavior.Contains("owner.game.StageIndex == 3 || owner.running && owner.selected == EnemyAttack.SW05"),"SW04 four counters/cancellation, parry stun exceptions and Shock unchanged");
        Require(scar.Contains("KarmaStage.ArenaWidth / 16f") && scar.Contains("new Vector2(ScarSize.x / 2, ScarSize.y)")
            && scar.Contains("KarmaPhysicsQueries.BoxCast") && scar.Contains("KarmaAudio.Play(KarmaSfxId.SwElectricDischarge)"),"fixed contact width and integrated physics/audio preserved");
        Require(scar.Contains("elapsed < releaseAt + active + rest") && scar.Contains("roundRoot.gameObject.SetActive(false)")
            && scar.IndexOf("ClearRound();",scar.IndexOf("for (int round"))<scar.IndexOf("KarmaSwordScarPlanner.TryPlan")
            && scar.Contains("float progress = Mathf.Clamp01(elapsed / warning)")
            && scar.Contains("contacts[CurrentPlan.ContactIndices[i]]"),"previous hazards finish before next warning; one common deadline and explicit contact mapping");
        Require(!scar.Contains("StaggerSeconds") && !scar.Contains("releaseAt[") && !scar.Contains("CurrentPlan.Groups")
            && scar.Contains("if (elapsed < warning) continue;") && scar.Contains("ReleaseRound(releaseRoot, size, active, contacts);"),"one deadline W, no second release or temporal assignment");
        string mark=scar.Substring(scar.IndexOf("KarmaSwordScarRise[] MarkRound"),scar.IndexOf("void ReleaseRound")-scar.IndexOf("KarmaSwordScarRise[] MarkRound"));
        string release=scar.Substring(scar.IndexOf("void ReleaseRound"),scar.IndexOf("IEnumerator ExecuteSequence")-scar.IndexOf("void ReleaseRound"));
        Require(!mark.Contains("yield") && !release.Contains("yield") && !release.Contains("StartCoroutine")
            && release.Contains("new KarmaSwordScarClock(Time.frameCount)") && release.Contains("i % 2 != 0, clock"),"all warnings and attacks created synchronously, with a common visual clock");
        Require(scar.Split("KarmaAudio.Play(KarmaSfxId.SwElectricDischarge)").Length==2,"one round release audio call, outside sword loop");
        string visual=Read("Assets/Scripts/SwordWave/Visuals/KarmaSwordScarRise.cs");
        Require(visual.Contains("RiseSeconds = 0.10f") && visual.Contains("AuraDelay = 0.045f")
            && visual.Split("roundClock.Sample(").Length==3,"blade and aura sample identical shared age with preserved rise/ignition durations");
        string profile=Read("Assets/Data/Resources/KarmaSwordBossProfile.asset");
        Require(profile.Contains("scarWarningSeconds: 0.95") && profile.Contains("scarActiveSeconds: 0.65")
            && profile.Contains("scarRoundGap: 0.32"),"existing warning tuning, active lifetime and gap preserved");
        string energy=Read("Assets/Scripts/SwordWave/Visuals/KarmaSW05Energy.cs");
        string pool=Read("Assets/Scripts/SwordWave/Visuals/KarmaSwordAttackPolish.cs");
        Require(energy.Contains("fx.sites=new Vector3[32]") && pool.Contains("Capacity = 96")
            && KarmaSwordScarPlanner.MaxSwords<=32 && 3*KarmaSwordScarPlanner.MaxSwords+2<96,
            "26 sites fit existing ground-impact buffer and even simultaneous 78 release sparks plus flashes fit pool; no runtime performance claim");
        var old=new[]{3f,3f,3f,2f,2f};
        var scenarios=new[]{
            ("far / all four skills ready",new[]{false,true,true,true,true},false,false),
            ("mid / basic and all skills ready",new[]{true,true,true,true,true},false,false),
            ("mid / forced basic after each skill + repeat protection",new[]{true,true,true,true,true},true,true),
            ("far / consecutive skills + repeat protection",new[]{false,true,true,true,true},false,true),
            ("SW04 cooling down / other skills ready",new[]{false,true,true,true,false},false,false),
            ("distance 6 / SW01 and SW03 out of range",new[]{true,false,true,false,true},true,true),
            ("ward formed / only basic SW01 SW02",new[]{true,true,true,false,false},true,true)};
        foreach(var scenario in scenarios)
        {
            var before=SelectionSample(old,scenario.Item2,scenario.Item3,scenario.Item4);
            var after=SelectionSample(current,scenario.Item2,scenario.Item3,scenario.Item4);
            for(int i=0;i<5;i++)
            {
                if(!scenario.Item2[i])Require(before[i]==0 && after[i]==0,"weight cannot bypass eligibility");
                if(i>=3 && scenario.Item2[i])Require(after[i]>before[i]+1000,"eligible SW03/SW04 usage increases materially");
                if(!scenario.Item3 && !scenario.Item4 && scenario.Item2[i])
                {
                    float total=current.Where((_,j)=>scenario.Item2[j]).Sum();
                    Require(Math.Abs(after[i]/100000f-current[i]/total)<0.008f,"sample agrees with eligible weighted probability");
                }
            }
            if(!scenario.Item2[3] && !scenario.Item2[4])Require(before.SequenceEqual(after),"ineligible changed weights cannot affect the pool");
            Console.WriteLine("SELECTION SIMULATION (100000 decisions each) "+scenario.Item1+": "+string.Join("; ",Enumerable.Range(0,5).Select(i=>$"{names[i]} {before[i]/1000f:F2}% -> {after[i]/1000f:F2}%")));
        }
        Console.WriteLine("PASS: serialized/default weights, eligible distributions and static preservation checks. No timed combat, cooldown occupancy or Unity gameplay measurement.");
    }
    static void Main(string[] args)
    {
        SelectionChecks();
        if(args.Contains("--selection-only"))return;
        ClockChecks();
        SeedChecks();
        int heldCases=0,heldHits=0,standingCases=0,standingHits=0;
        foreach(float warning in new[]{0.8f,0.855f})
        foreach(float velocity in new[]{-14f,0f,14f})
        for(int seed=0;seed<24;seed++)
        foreach(float x in new[]{-14.25f,-10f,3f,20f,37f,50f,54.25f})
        {
            var p=Draw(seed,x,velocity,warning);Validate(p,x,velocity,warning);
            Require(p.Positions.Length>=24,"normal geometry reaches prototype density target without shrinking swords");
            Require(Signature(p)==Signature(Draw(seed,x,velocity,warning)),"explicit seed reproduces positions/clusters/groups");
            float old=x;bool hit=false;
            for(float t=0;t<=warning+0.65f;t+=1f/240){float at=Math.Clamp(x+velocity*t,Left+Half,Right-Half);hit|=Hits(p,old,at,t,warning);old=at;}
            if(velocity==0){standingCases++;if(hit)standingHits++;}else{heldCases++;if(hit)heldHits++;}
        }
        int stationarySurvivors=0,casts=0,retainedRest=0,reversals=0;
        foreach(float start in new[]{-14.25f,3f,20f,37f,54.25f})
        foreach(float direction in new[]{-14f,0f,14f})
        for(int cast=0;cast<4;cast++)
        {
            var random=new Random(KarmaSwordScarPlanner.NextSeed());KarmaSwordScarPlanner.Plan previous=null;
            float x=start,safe=float.NaN;bool stationary=true;int total=0;
            for(int round=0;round<5;round++)
            {
                float v=round%2==0?direction:-direction;
                Require(KarmaSwordScarPlanner.TryPlan(random,Left,Right,Width,Visual,x,Half,v,Speed,0.8f,safe,null,out var p,previous?.Positions),"five-round normal completion");
                Validate(p,x,v,previous:previous);total+=p.Positions.Length;
                stationary &= p.Positions.All(at=>Math.Abs(at-start)>Width/2+Half);
                if(previous!=null)
                {
                    transitions++;corridorShifts.Add(Math.Abs(p.SafeX-safe));layoutShifts.Add(KarmaSwordScarPlanner.Difference(p.Positions,previous.Positions));
                    var nextCorridor=p.Corridors.First(c=>p.SafeX>=c.CenterMin-0.0001f && p.SafeX<=c.CenterMax+0.0001f);
                    var oldCorridor=previous.Corridors.First(c=>safe>=c.CenterMin-0.0001f && safe<=c.CenterMax+0.0001f);
                    corridorCenterShifts.Add(Math.Abs(nextCorridor.Center-oldCorridor.Center));
                    if(p.Positions.All(at=>Math.Abs(at-safe)>Width/2+Half+Margin))retainedRest++;
                    Require(!previous.Positions.SequenceEqual(p.Positions),"never replay a prior row");
                }
                if(v!=0 && Math.Sign(p.SafeX-Reaction(x,v))!=Math.Sign(v))reversals++;
                previous=p;safe=p.SafeX;x=safe;
            }
            Require(total>=120 && total<=130,"five complete rounds of real damaging swords and one warning each");
            casts++;if(stationary)stationarySurvivors++;
        }
        foreach(int seed in new[]{11,22,33})
        {
            var p=Draw(seed,20,0);
            Console.WriteLine($"SEED {seed}: count={p.Positions.Length}, clusters={string.Join('+',p.ClusterSizes)}, safe={p.SafeX:F3}; corridors="+string.Join(" / ",p.Corridors.Select(c=>$"[{c.Left:F2},{c.Right:F2}] width={c.Width:F2} center=[{c.CenterMin:F2},{c.CenterMax:F2}]")));
        }
        Validate(Draw(23,19,14,clear:at=>at<=20),19,14,clear:at=>at<=20);
        foreach(float v in new[]{-100f,100f})Validate(Draw(42,20,v),20,v);
        Validate(Draw(21,20,0,speed:0),20,0,speed:0);
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(1),Left,Right,Width,Visual,20,Half,14,Speed,0.8f,float.NaN,_=>false,out _),"fully blocked state rejected rather than claiming safety");
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(1),Left,Right,Width,Visual,20,40,0,Speed,0.8f,float.NaN,null,out _),"oversized body rejected");
        Require(!KarmaSwordScarPlanner.TryPlan(new Random(1),Left,Right,Width,Visual,20,Half,0,Speed,0.79f,float.NaN,null,out _),"minimum warning retained");
        // 9.5 units cannot contain two 2.8 corridors + two 2.1875 blades.
        Require(KarmaSwordScarPlanner.TryPlan(new Random(7),-4.75f,4.75f,Width,Visual,0,Half,0,Speed,0.8f,float.NaN,null,out var fallback),"bounded current-state fallback");
        Require(fallback.Fallback && fallback.CandidatesTried==49 && fallback.Corridors.Length>=1,"48 attempts then one revalidated single corridor");
        Require(fallback.Positions.All(at=>Math.Abs(at-fallback.SafeX)>=Width/2+Half+Margin),"fallback respects current player body");
        Require(KarmaSwordScarPlanner.TryPlan(new Random(7),-4.75f,4.75f,Width,Visual,2,Half,0,Speed,0.8f,fallback.SafeX,null,out var moved,fallback.Positions)
            && moved.Positions.All(at=>Math.Abs(at-moved.SafeX)>=Width/2+Half+Margin),"fallback recalculates after player moves");
        Walk(fallback,0,0,0.8f,Speed,null);Walk(moved,2,0,0.8f,Speed,null,fallback);
        Require(moved.Corridors.All(c=>c.Left>=-4.75f && c.Right<=4.75f),"fallback corridor never counts off-arena space");
        var probe=Draw(11,20,0);float blade=probe.Positions[0];
        Require(Hits(probe,blade-Width/2-Half-0.2f,blade+Width/2+Half+0.2f,0.85f,0.8f),
            "temporal swept oracle catches crossing a still-active old hazard even with empty endpoints");
        Require(clusterCounts.All(p=>p.Value>0) && combinations.Count>100 && regions.Count>10,"composition/regions vary materially");
        Require(layoutShifts.Average()>0.75 && corridorShifts.Average()>1,"meaningful measured spatial movement, not tiny jitter");
        Require(retainedRest>0 && retainedRest<transitions,"sometimes retain, sometimes pressure previous rest position");
        Console.WriteLine($"COUNTS normal+edge/wall/zero-speed: min={counts.Min()} avg={counts.Average():F3} max={counts.Max()}; {string.Join(", ",counts.GroupBy(n=>n).Select(g=>$"{g.Key}:{g.Count()}"))}");
        Console.WriteLine($"CLUSTERS: {string.Join(", ",clusterCounts.Select(p=>$"{p.Key}:{p.Value}"))}; ordered compositions={combinations.Count}; regional layouts={regions.Count}; single release per round");
        Console.WriteLine($"GEOMETRY: contact={Width:F4}x{Height:F4}; internal step={internalSteps.Min():F3}..{internalSteps.Max():F3}; cluster span={clusterSpans.Min():F3}..{clusterSpans.Max():F3}; corridor width={widths.Min():F3}/{widths.Average():F3}/{widths.Max():F3}; valid center interval={centerWidths.Min():F3}/{centerWidths.Average():F3}/{centerWidths.Max():F3}");
        Console.WriteLine($"VARIETY: mean layout shift={layoutShifts.Average():F3}; physical corridor-center shift min/avg/max={corridorCenterShifts.Min():F3}/{corridorCenterShifts.Average():F3}/{corridorCenterShifts.Max():F3}; safe destination shift={corridorShifts.Min():F3}/{corridorShifts.Average():F3}/{corridorShifts.Max():F3}; retained prior rest={retainedRest}/{transitions}; stationary same-location five-round survivors={stationarySurvivors}/{casts}; reversal choices={reversals}");
        Console.WriteLine($"BEHAVIOR (measurements, not target failure rates): held-direction hits={heldHits}/{heldCases}; standing hits={standingHits}/{standingCases}; ordinary warning-aware routes pass all {plans} plans plus {transitions} transitions.");
        Console.WriteLine($"CANDIDATES: {attempts}; spacing rejected={spacingCandidates}; unsafe={unsafeCandidates}; similar={similarCandidates}; fallback={fallbacks}/{plans}; forced narrow-arena fallback count={fallback.Positions.Length}, attempts={fallback.CandidatesTried}");
        Console.WriteLine("PASS: cluster-local consumed/damaged/blocked contacts; original geometry; seeded corridors; no ability/immunity assumed. Unity physics/rendering/performance NOT executed.");
    }
}
