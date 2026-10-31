#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;
using UnityEngine.SceneManagement;

// Independent continuous four-shot witness: real arena collisions, player
// acceleration/gravity and rotated swept damage, with fresh aim at every lock.
public static class KarmaSwordWardEscapeChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception("SW04 escape: " + message); }
    sealed class Shot { public Vector2 at, velocity; public float remaining; }
    public static int Run(KarmaGame game, KarmaEnemy enemy)
    {
        assertions=0;
        var scene=SceneManager.CreateScene("SW04 independent four-shot physics",new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics=scene.GetPhysicsScene2D();
        try
        {
            foreach (var surface in game.World.GetComponentsInChildren<KarmaSurface>())
            {
                var source=surface.GetComponent<BoxCollider2D>(); if (source==null) continue;
                var copy=new GameObject("SW04 escape terrain"); SceneManager.MoveGameObjectToScene(copy,scene);
                copy.transform.position=source.transform.position; copy.transform.localScale=source.transform.lossyScale;
                var collider=copy.AddComponent<BoxCollider2D>(); collider.size=source.size; collider.offset=source.offset;
            }
            var probe=new GameObject("SW04 real player motion"); SceneManager.MoveGameObjectToScene(probe,scene);
            var shape=probe.AddComponent<BoxCollider2D>();
            var realShape=game.Player.GetComponent<BoxCollider2D>(); shape.size=realShape.size; shape.offset=realShape.offset;
            var body=probe.AddComponent<Rigidbody2D>(); body.freezeRotation=true; body.gravityScale=game.Config.gravityScale;
            body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            Vector2 size=(Vector2)typeof(KarmaEnemy).GetMethod("SwordSize",Private).Invoke(enemy,new object[]{EnemyAttack.SW04});
            float tracking=(float)typeof(KarmaEnemy).GetProperty("GuardTrackingTime",Private).GetValue(enemy);
            float locked=(float)typeof(KarmaEnemy).GetProperty("GuardLockTime",Private).GetValue(enemy);
            float gap=(float)typeof(KarmaEnemy).GetProperty("GuardShotGap",Private).GetValue(enemy);
            Check(Mathf.Abs(tracking-.35f)<.0001f && locked>=.3f,"tracking and lock have separate readable stages");
            int walking=0,jumping=0;
            foreach (float start in new[]{-13f,0f,13f,27f,40f,53f})
                foreach (float phase in new[]{0f,1.3f,2.6f,4f})
                {
                    bool escaped=false;
                    int inward=start<KarmaStage.ArenaCenter?1:-1;
                    for (int jump=0;jump<5 && !escaped;jump++)
                        foreach (int turnMask in new[]{0,15,5,10,3,12,1,2,4,8,6,9,7,11,13,14})
                        {
                            if (!Survives(game,physics,body,shape,start,phase,inward,turnMask,jump,15,size,tracking,locked,gap)) continue;
                            escaped=true; if (jump==0) walking++; else jumping++;
                            Debug.Log("SW04 FULL ESCAPE x="+start+" phase="+phase+" inward="+inward+" directionMask="+turnMask+" jumpPolicy="+jump+" shots=4");
                            break;
                        }
                    for(int jump=1;jump<=4 && !escaped;jump++)
                        for(int jumpMask=1;jumpMask<15 && !escaped;jumpMask++)
                            for(int turnMask=0;turnMask<16 && !escaped;turnMask++)
                            {
                                if(!Survives(game,physics,body,shape,start,phase,inward,turnMask,jump,jumpMask,size,tracking,locked,gap)) continue;
                                escaped=true;jumping++;
                                Debug.Log("SW04 FULL ESCAPE x="+start+" phase="+phase+" inward="+inward+" directionMask="+turnMask+" jumpPolicy="+jump+" jumpMask="+jumpMask+" shots=4");
                            }
                    Check(escaped,"moving/jumping player has a full four-shot route at x="+start+" phase="+phase);
                }
            Check(walking+jumping==24,"all center/wall and orbit-phase scenarios finish beyond the final shot lifetime");
            Debug.Log("SW04 ESCAPE PASS cases=24 walking="+walking+" jumping="+jumping);
        }
        finally { SceneManager.UnloadSceneAsync(scene); }
        return assertions;
    }
    static bool Survives(KarmaGame game,PhysicsScene2D physics,Rigidbody2D body,BoxCollider2D shape,
        float start,float phase,int inward,int turnMask,int jump,int jumpMask,Vector2 size,float tracking,float locked,float gap)
    {
        const float step=.02f;
        body.simulated=false; body.transform.position=new Vector2(start,2); body.simulated=true;
        body.position=new Vector2(start,2); body.linearVelocity=Vector2.zero; Physics2D.SyncTransforms();
        for(int i=0;i<20;i++) physics.Simulate(step);
        var shots=new List<Shot>(); var contacts=new ContactPoint2D[8];
        float boss=Mathf.Clamp(start+(start<20?8:-8),KarmaStage.BossLeftWall,KarmaStage.BossRightWall);
        int number=0; float begins=0; bool aimed=false,launched=false;
        Vector2 origin=Vector2.zero,aim=Vector2.right;
        for(float t=0;t<8;t+=step)
        {
            if(number<4)
            {
                if(!aimed)
                {
                    float angle=phase+begins*3+number*Mathf.PI/2;
                    origin=new Vector2(boss,KarmaSpace.GuardianGroundY)+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*3.2f;
                    float margin=size.magnitude/2+.05f;
                    origin.x=Mathf.Clamp(origin.x,KarmaStage.ArenaLeft+margin,KarmaStage.ArenaRight-margin);
                    origin.y=Mathf.Clamp(origin.y,margin,24-margin); aimed=true;
                }
                if(t<begins+tracking) aim=(body.position+shape.offset-origin).normalized;
                if(!launched && t>=begins+tracking+locked)
                {
                    shots.Add(new Shot{at=origin,velocity=aim*game.Config.BossTuning.swordSpeed,
                        remaining=BoundaryLife(origin,aim*game.Config.BossTuning.swordSpeed,Mathf.Clamp(game.Config.BossTuning.swordLifetime,.5f,2))});
                    launched=true;
                }
                if(t>=begins+tracking+locked+gap) { begins+=tracking+locked+gap;number++;aimed=launched=false; }
            }
            bool grounded=false; int count=body.GetContacts(contacts);
            for(int i=0;i<count;i++) grounded|=contacts[i].normal.y>.5f;
            bool jumpNow=jump==1 && number<4 && t>=begins+tracking && t<begins+tracking+.08f
                || jump==2 && number<4 && t>=begins+tracking+locked && t<begins+tracking+locked+.08f
                || jump==3 && number<4
                || jump==4 && number<4 && t>=begins && t<begins+.08f;
            jumpNow &= ((jumpMask >> Mathf.Min(number,3)) & 1)!=0;
            Vector2 before=body.position+shape.offset;
            int input=((turnMask >> Mathf.Min(number,3)) & 1)==0?inward:-inward;
            body.linearVelocity=new Vector2(input*game.Config.moveSpeed,jumpNow && grounded?game.Config.jumpSpeed:body.linearVelocity.y);
            physics.Simulate(step);
            Vector2 after=body.position+shape.offset;
            bool live=false;
            foreach(var shot in shots)
            {
                if(shot.remaining<=0) continue; live=true;
                float dt=BoundaryLife(shot.at,shot.velocity,Mathf.Min(step,shot.remaining));
                Vector2 end=shot.at+shot.velocity*dt,axis=shot.velocity.normalized;
                Vector2 playerEnd=Vector2.Lerp(before,after,dt/step);
                if(KarmaBossGeometry.SweptOrientedBox(shot.at.x-before.x,shot.at.y-before.y,end.x-playerEnd.x,end.y-playerEnd.y,
                    0,0,axis.x,axis.y,size.x,size.y,shape.bounds.extents.x,shape.bounds.extents.y)) return false;
                shot.at=end;shot.remaining=dt<step-.00001f?0:shot.remaining-step;
            }
            if(number==4 && !live) return shots.Count==4;
        }
        return false;
    }
    static float BoundaryLife(Vector2 at,Vector2 velocity,float life)
    {
        if(velocity.x>0) life=Mathf.Min(life,(KarmaStage.ArenaRight-at.x)/velocity.x);
        else if(velocity.x<0) life=Mathf.Min(life,(KarmaStage.ArenaLeft-at.x)/velocity.x);
        if(velocity.y>0) life=Mathf.Min(life,(24-at.y)/velocity.y);
        else if(velocity.y<0) life=Mathf.Min(life,-at.y/velocity.y);
        return Mathf.Max(0,life);
    }
}
#endif
