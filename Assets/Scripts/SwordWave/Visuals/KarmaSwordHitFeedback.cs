using UnityEngine;

namespace KarmaPrototype
{
    // Cosmetic, confirmed-damage-only feedback. World ownership lets the short
    // flash finish if the attack enters recovery in the same frame.
    public static class KarmaSwordHitFeedback
    {
        public static void ShowPlayerImpact(Transform root, Vector2 at)
        {
            var sprite = KarmaWaveArtwork.Sprite("Sword_HitBurst_v2");
            if (sprite == null) return;
            var flash = KarmaCombatVFX.Burst(root, at, sprite,
                "Sword player confirmed hit", Vector2.one * 2.7f, 0.17f,
                0, KarmaSwordAttackPolish.Blue, true);
            foreach (var renderer in flash.transform.parent.GetComponentsInChildren<SpriteRenderer>())
                { renderer.sortingOrder += 18; KarmaSwordSkillSurface.Apply(renderer, 4); }
        }
    }

    // Persistent status feedback follows the real stun state and combat clock.
    // Keep its geometry outside Artwork so it never enlarges damage bounds.
    [DefaultExecutionOrder(475)]
    public sealed class KarmaSwordStunFeedback : MonoBehaviour
    {
        KarmaEnemy owner;
        KarmaGame game;
        SpriteRenderer picture;
        Transform orbit;
        readonly LineRenderer[] stars = new LineRenderer[3];
        bool electrical;
        KarmaSwordElectrocution electrocution;
        public bool Electrocuted => electrical;
        public void ArmElectrocution() { electrical = true; }
        public void Initialize(KarmaEnemy enemy, KarmaGame director, Transform artwork)
        {
            owner = enemy; game = director;
            picture = artwork.Find("Guardian sprite").GetComponent<SpriteRenderer>();
            orbit = new GameObject("Sword stunned orbit").transform;
            orbit.SetParent(transform, false);
            var ring = Line("Stun orbit", 48, 0.045f);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 1.45f, Mathf.Sin(a) * 0.38f, 0));
            }
            ring.startColor = ring.endColor = new Color(0.35f, 0.8f, 1, 0.75f);
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = Line("Stun star " + i, 8, 0.065f);
                for (int j = 0; j < 8; j++)
                {
                    float a = j * Mathf.PI / 4;
                    float radius = j % 2 == 0 ? 0.28f : 0.09f;
                    stars[i].SetPosition(j, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius);
                }
                stars[i].startColor = stars[i].endColor = new Color(0.82f, 0.96f, 1);
            }
            orbit.gameObject.SetActive(false);
        }
        LineRenderer Line(string name, int points, float width)
        {
            var node = new GameObject(name); node.transform.SetParent(orbit, false);
            var line = node.AddComponent<LineRenderer>();
            line.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
            line.useWorldSpace = false; line.loop = true; line.positionCount = points;
            line.widthMultiplier = width; line.numCornerVertices = 2; line.sortingOrder = 45;
            return line;
        }
        void LateUpdate()
        {
            if (orbit == null) return;
            bool visible = owner != null && owner.PresentationActive && owner.Stunned;
            SetOrbitVisible(visible && !electrical);
            if (electrical && visible)
            {
                if (electrocution == null) electrocution = KarmaSwordElectrocution.ShowBoss(game, owner, picture);
                return;
            }
            if (!visible && electrocution != null)
            { electrocution.Stop(); electrocution = null; electrical = false; }
            if (owner == null || !owner.PresentationActive) electrical = false;
            if (!visible || picture == null || game.Paused) return;
            // WaveMotion resets the authored transform first. Lean at the feet;
            // the boss root and its original incoming-damage bounds stay fixed.
            float angle = 9 + Mathf.Sin(owner.FightSeconds * 5) * 1.5f;
            var rotation = Quaternion.Euler(0, 0, angle);
            var foot = picture.GetComponentInParent<KarmaCharacterAnimator>().SwordFoot;
            picture.transform.localPosition += foot - rotation * foot;
            picture.transform.localRotation *= rotation;
            orbit.position = new Vector3(picture.bounds.center.x, picture.bounds.max.y + 0.8f, 0);
            for (int i = 0; i < stars.Length; i++)
            {
                float a = owner.FightSeconds * 4 + i * Mathf.PI * 2 / stars.Length;
                stars[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 1.45f, Mathf.Sin(a) * 0.38f, 0);
                stars[i].transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            }
        }
        void SetOrbitVisible(bool visible)
        {
            if (orbit.gameObject.activeSelf == visible) return;
            orbit.gameObject.SetActive(visible);
            if (visible)
                KarmaAudio.Play(KarmaSfxId.CombatBossSpinningStars, lifetimeOwner: orbit, playOnce: true);
            else
                KarmaAudio.StopOwned(orbit, 0.045f);
        }

        void OnDisable()
        {
            electrical = false;
            if (electrocution != null) electrocution.Stop();
            if (orbit != null) SetOrbitVisible(false);
        }
    }

    // Presentation only: never writes status, immunity, physics or input.
    // Boss lifetime follows host stun; SW04/SW05 player lifetime follows real Shock.
    [DefaultExecutionOrder(500)]
    public sealed class KarmaSwordElectrocution : MonoBehaviour
    {
        KarmaGame game;
        KarmaPlayer player;
        KarmaEnemy boss;
        SpriteRenderer picture;
        readonly LineRenderer[] arcs = new LineRenderer[6], cores = new LineRenderer[6], sparks = new LineRenderer[3];
        readonly Vector3[] path = new Vector3[11];
        double began;
        Vector3 home, shifted;
        Quaternion rotation, trembled;
        Color color, tinted;
        Sprite originalSprite, shownSprite;
        bool painted, stopped, playerShockPlayed;
        public int FrameIndex { get; private set; }
        public double StartedAt => began;
        public double EndedAt { get; private set; }
        public bool IsBoss => boss != null;
        public static bool ShowPlayer(KarmaGame game, bool playShockSound = false)
        {
            var player = game.Player;
            if (player == null || player.Health <= 0 || !player.Shocked || !player.isActiveAndEnabled || !game.IsCombat) return false;
            // A refresh extends the real state; keep the same effect and its one-shot.
            foreach (var existing in game.World.GetComponentsInChildren<KarmaSwordElectrocution>())
            {
                if (existing.player != player || existing.stopped) continue;
                if (playShockSound) existing.PlayPlayerShock();
                return true;
            }
            var picture = player.transform.Find("Artwork/Player sprite")?.GetComponent<SpriteRenderer>();
            if (picture == null) return false;
            var fx = Create(game, picture);
            fx.player = player;
            if (playShockSound) fx.PlayPlayerShock();
            return true;
        }
        void PlayPlayerShock()
        {
            if (playerShockPlayed) return;
            playerShockPlayed = true;
            KarmaAudio.Play(KarmaSfxId.Sw04PlayerShock, lifetimeOwner: this);
        }
        public static KarmaSwordElectrocution ShowBoss(KarmaGame game, KarmaEnemy owner, SpriteRenderer picture)
        {
            var fx = Create(game, picture);
            fx.boss = owner;
            KarmaAudio.Play(KarmaSfxId.Sw04BossShock, lifetimeOwner: fx);
            return fx;
        }
        static KarmaSwordElectrocution Create(KarmaGame game, SpriteRenderer picture)
        {
            var node = new GameObject("SwordWave electrocution / presentation only");
            node.transform.SetParent(game.World, false);
            var fx = node.AddComponent<KarmaSwordElectrocution>();
            fx.game = game; fx.picture = picture; fx.began = Time.timeAsDouble;
            for (int i=0;i<6;i++)
            {
                fx.arcs[i] = fx.Line("SW04 cyan electrical arc "+i,11,0.055f,new Color(0.08f,0.65f,1,0.7f));
                fx.cores[i] = fx.Line("SW04 white discharge core "+i,11,0.017f,new Color(0.8f,0.98f,1,0.95f));
            }
            for(int i=0;i<3;i++) fx.sparks[i]=fx.Line("SW04 short electric spark "+i,3,0.025f,new Color(0.55f,0.95f,1,0.9f));
            return fx;
        }
        LineRenderer Line(string name,int points,float width,Color color)
        {
            var node = new GameObject(name); node.transform.SetParent(transform,false);
            var line = node.AddComponent<LineRenderer>();
            line.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
            line.useWorldSpace = true; line.positionCount = points; line.widthMultiplier = width;
            line.numCapVertices = 2; line.numCornerVertices = 1;
            line.sortingLayerID = picture.sortingLayerID; line.sortingOrder = picture.sortingOrder+6;
            line.startColor = line.endColor = color;
            return line;
        }
        void Restore()
        {
            if (!painted || picture == null) return;
            // Animator/death may already have authored a newer transform this frame.
            if (picture.transform.localPosition == shifted) picture.transform.localPosition = home;
            if (picture.transform.localRotation == trembled) picture.transform.localRotation = rotation;
            if (picture.color == tinted) picture.color = color;
            if (boss != null && picture.sprite == shownSprite) picture.sprite = originalSprite;
            painted = false;
        }
        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            EndedAt = Time.timeAsDouble;
            KarmaAudio.StopOwned(this, 0.035f);
            Restore();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        void LateUpdate()
        {
            if (stopped) return;
            bool active = game != null && game.Phase == RunPhase.Combat && picture != null
                && (boss != null ? boss.PresentationActive && boss.Stunned
                    : player != null && player.isActiveAndEnabled && player.Health > 0 && game.Player == player && player.Shocked);
            if (!active) { Stop(); return; }
            Restore();
            for (int i=0;i<arcs.Length;i++) { arcs[i].enabled = !game.Paused; cores[i].enabled = !game.Paused; }
            if (game.Paused)
            {
                foreach (var spark in sparks) spark.enabled = false;
                return;
            }
            float age = (float)(Time.timeAsDouble-began);
            FrameIndex = Mathf.FloorToInt(age * KarmaBootstrap.FramesPerSecond);
            // Enter over one frame. The status owns the end, including refresh;
            // a separate visual timer must not expire before the control lock.
            float envelope = Mathf.Min(1,(age+1f/60)*24);
            originalSprite=picture.sprite;
            if (boss != null) picture.GetComponentInParent<KarmaCharacterAnimator>().ShowSwordElectrocutionPose();
            home=picture.transform.localPosition;rotation=picture.transform.localRotation;color=picture.color;
            shownSprite=picture.sprite;
            float sign = FrameIndex%2==0 ? -1 : 1;
            float scale = Mathf.Max(0.01f,Mathf.Abs(picture.transform.parent.lossyScale.x));
            shifted = home+new Vector3(sign*(boss!=null?0.035f:0.022f)*envelope/scale,0,0);
            trembled = rotation*Quaternion.Euler(0,0,sign*0.7f*envelope);
            tinted = Color.Lerp(color,new Color(0.68f,0.93f,1,color.a),0.25f*envelope);
            picture.transform.localPosition=shifted;picture.transform.localRotation=trembled;picture.color=tinted;painted=true;
            Bounds b=picture.bounds;
            if (player != null) b=player.GetComponent<Collider2D>().bounds;
            else
            {
                // The existing guardian cells have transparent head/foot margins.
                // Keep short discharges around the body, inside that padded cell.
                b=new Bounds(new Vector3(b.center.x,b.min.y+b.size.y*0.43f,b.center.z),
                    new Vector3(b.size.x*0.65f,b.size.y*0.68f,b.size.z));
            }
            float h=Mathf.Max(0.4f,b.size.y),w=Mathf.Min(b.size.x,h*0.65f);
            float facing = picture.transform.lossyScale.x<0 ? -1 : 1;
            for(int i=0;i<6;i++)
            {
                float lane=(i%2==0?-1:1)*facing;
                Vector2 from=new Vector2(b.center.x+lane*w*(0.22f+0.045f*(i/2)),b.min.y+h*(0.12f+0.22f*(i/2)));
                Vector2 to=from+new Vector2(-lane*w*0.32f,h*0.31f);
                Vector2 normal=new Vector2(-(to-from).y,(to-from).x).normalized;
                for(int j=0;j<path.Length;j++)
                {
                    float t=j/(float)(path.Length-1);
                    float jag=j==0||j==path.Length-1?0:(Mathf.Repeat(Mathf.Sin(j*127.1f+i*311.7f+FrameIndex*74.7f)*43758.5453f,1)-0.5f)*h*0.11f;
                    path[j]=Vector2.Lerp(from,to,t)+normal*jag;
                }
                arcs[i].SetPositions(path);cores[i].SetPositions(path);
                arcs[i].widthMultiplier=(boss!=null?0.055f:0.035f)*envelope;
                cores[i].widthMultiplier=(boss!=null?0.017f:0.012f)*envelope;
            }
            for(int i=0;i<3;i++)
            {
                sparks[i].enabled=(FrameIndex+i)%3!=0;
                float a=FrameIndex*1.7f+i*2.094f;
                Vector2 axis=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Vector2 at=(Vector2)b.center+Vector2.Scale(axis,new Vector2(w*0.4f,h*0.35f));
                sparks[i].SetPosition(0,at);
                sparks[i].SetPosition(1,at+axis*h*0.055f+new Vector2(-axis.y,axis.x)*h*0.025f);
                sparks[i].SetPosition(2,at+axis*h*0.12f);
                sparks[i].widthMultiplier=0.025f*envelope;
            }
        }
        void OnDisable()
        {
            Restore();
            if (!stopped) { stopped = true; EndedAt = Time.timeAsDouble; Destroy(gameObject); }
        }
        void OnDestroy() { Restore(); }
    }

}
