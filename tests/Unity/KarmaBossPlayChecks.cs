#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KarmaPrototype;

// Copy into an isolated project's Assets/Editor before -executeMethod KarmaBossPlayChecks.Run.
// Uses real Play Mode updates, coroutines, Sprite.Create and player damage resolution.
[InitializeOnLoad]
public static class KarmaBossPlayChecks
{
    const string Key = "K4RMA.BossPlayChecks";
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly EnemyAttack[] Cases = {
        EnemyAttack.GroundStrike, EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03, EnemyAttack.SW04, EnemyAttack.SW05,
        EnemyAttack.UP01, EnemyAttack.UP02, EnemyAttack.UP03, EnemyAttack.UP04, EnemyAttack.UP05,
        EnemyAttack.GD02, EnemyAttack.GD03, EnemyAttack.GD04, EnemyAttack.GD05, EnemyAttack.GD05,
        EnemyAttack.GroundStrike, EnemyAttack.SW02, EnemyAttack.UP01, EnemyAttack.UP02, EnemyAttack.GD02 };
    const int FailureCase = 15, EnrageStart = 16;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static int index, assertions, ticks, lifecycleStage, waitFrame;
    static float pauseFight;
    static bool sawShieldWarning;
    static bool started, sawRecovery, sawFullShield, responseTriggered, sawResponse, injectedFailure, sawTrialFailure;
    static double deadline;
    static float startedAt;
    static string warningError;
    static bool geometryChecked;
    static int personalizationCase;
    static bool sawInheritance, sawTransformation, sawPushUp, sawStandUp;
    static Sprite inheritedBefore;
    static float guardianFellAt;
    static Vector3 guardianFellPosition;
    static Vector3 guardianFallCameraPosition;
    static float guardianFallCameraSize;
    static bool sawAirborneLandingWait;
    static bool sawCeremonyWalk;
    static float ceremonyPreviousX;
    static int playerDeathCase;
    static float playerDiedAt, playerLandedAt, deathElapsed;
    static Vector3 deathCameraPosition;
    static float deathCameraSize;
    static bool sawPlayerCollapse, sawPlayerDeath, sawPlayerAirborne;
    static FieldInfo Field(object o, string name) => o.GetType().GetField(name, Flags);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o,value);
    static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static void Check(bool ok, string label) { assertions++; if (!ok) throw new Exception(label); }
    static KarmaBossPlayChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
        if(SessionState.GetBool(Key,false)) EditorApplication.update += AutoTick;
    }
    static void AutoTick()
    {
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.isPlaying && game==null && Time.frameCount>0) OnState(PlayModeStateChange.EnteredPlayMode);
    }
    public static void Run()
    {
        Application.logMessageReceived += CaptureWarningError;
        Debug.Log("K4RMA PLAY CHECK START");
        Application.runInBackground = true;
        PlayerSettings.runInBackground = true;
        SessionState.SetBool(Key,true);
        EditorApplication.update += AutoTick;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false) || game!=null) return;
        Debug.Log("K4RMA PLAY CHECK INITIALIZE frame="+Time.frameCount);
        try
        {
            Application.logMessageReceived -= CaptureWarningError;
            Application.logMessageReceived += CaptureWarningError;
            Time.captureDeltaTime = 0.05f;
            Application.runInBackground = true;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) game = new GameObject("Play check game").AddComponent<KarmaGame>();
            KarmaMigrationChecks.Run(game);
            game.Config.playerHealth = 1000000;
            game.StartRun();
            game.Player.Essences.Sacrifice(Essence.SwordWave);
            game.Player.Essences.Sacrifice(Essence.RisingSlash);
            game.Player.Essences.Sacrifice(Essence.Guard);
            Call(game,"LoadRoom",true);
            enemy = game.Enemies[0];
            game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            deadline = EditorApplication.timeSinceStartup + 180;
            // OverrideGeometry must run inside Unity's player loop.
            new GameObject("Play check driver").AddComponent<KarmaBossPlayCheckDriver>().Step = Tick;
        }
        catch(Exception e) { Finish(e); }
    }
    static void Tick()
    {
        try
        {
            if (warningError != null) throw new Exception(warningError);
            if (!geometryChecked) { PlayerUtilityChecks(); SpatialChecks(); CameraChecks(); BoundaryChecks(); ProgressionReloadChecks(); WarningGeometryChecks(); BossDamageBoundsChecks(); geometryChecked = true; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play checks exceeded wall-clock deadline");
            if (!EditorApplication.isPlaying) throw new Exception("Play mode ended unexpectedly");
            if (index >= Cases.Length) { if(LifecycleChecks()) Finish(null); return; }
            if (++ticks % 200 == 0) Debug.Log("CHECK TICK frame="+Time.frameCount+" time="+Time.time+" paused="+EditorApplication.isPaused+" state="+Get<object>(enemy,"state"));
            if (!started) StartCase();
            Set(game.Player,"invulnerableUntil",Time.time + 100);
            var attack = Cases[index];
            sawRecovery |= Get<object>(enemy,"state").ToString()=="Recover";
            sawFullShield |= enemy.Invulnerable;
            if (attack == EnemyAttack.SW04 && Get<bool>(enemy,"swordGuard") && !responseTriggered)
            { enemy.TakeDamage(1); responseTriggered = true; }
            if (attack == EnemyAttack.SW04 && responseTriggered && enemy.GetComponentInParent<KarmaGame>() != null)
                sawResponse |= game.World.GetComponentsInChildren<KarmaBossAttack>().Length > 0;
            if (index == FailureCase && enemy.TrialActive && !injectedFailure)
            {
                var trial = Get<KarmaTrialState>(enemy,"trial");
                for(int i=0;i<=trial.Allowed;i++) trial.Hit();
                injectedFailure = true;
            }
            sawTrialFailure |= enemy.Intent.Contains("시험 실패");
            if (Get<bool>(enemy,"running")) return;
            Check(sawRecovery,attack+" completed through recovery");
            Check(game.World.GetComponentsInChildren<KarmaBossAttack>().Length==0,attack+" no hostile residue after recovery");
            Check(!enemy.Invulnerable && !enemy.TrialActive,attack+" shield/trial released");
            Check(Mathf.Abs(enemy.Position.y-KarmaSpace.GuardianGroundY)<0.01f,attack+" landed");
            if(attack==EnemyAttack.SW05 || attack==EnemyAttack.UP04) Check(sawFullShield,attack+" protected airborne phase");
            if(attack==EnemyAttack.SW04) Check(sawResponse,"SW04 attack caused telegraphed retaliation");
            if(index==FailureCase) Check(sawTrialFailure,"GD05 failure resolved after full sequence");
            if(index>=EnrageStart) Check(enemy.Enraged,"enhanced sequence ran below half health");
            Check(Time.time-startedAt < 29,attack+" no watchdog recovery");
            Debug.Log("PLAY PASS "+index+" "+attack);
            index++; started=false;
        }
        catch(Exception e) { Finish(e); }
    }
    static void PlayerUtilityChecks()
    {
        var player = game.Player;
        var animator = player.GetComponentInChildren<KarmaCharacterAnimator>();
        var renderer = animator.GetComponentInChildren<SpriteRenderer>();
        animator.Pose(0, false, 0, true);
        var crouch = KarmaArtwork.Sprite("Player", "Crouch");
        Check(crouch != null && renderer.sprite == crouch && animator.CurrentPose == "Crouch", "integrated atlas crouch sprite is displayed");
        Check(crouch.texture == KarmaArtwork.Sprite("Player", "Idle").texture, "crouch shares the player action atlas");
        Check(crouch.bounds.size.y < KarmaArtwork.Sprite("Player", "Idle").bounds.size.y, "crouch art is shorter than standing art");
        animator.Pose(0, true, 3, true);
        Check(animator.CurrentPose == "Jump", "jump takes priority over crouch");
        animator.Pose(0, false);
        Check(animator.CurrentPose != "Crouch", "releasing crouch restores normal pose");
        Set(player, "invulnerableUntil", 0f);
        float health = player.Health, damage = game.DamageTaken;
        Check(!player.TestInvincible, "test invincibility starts disabled");
        player.ToggleTestInvincibility();
        player.TakeDamage(100);
        Check(player.Health == health && game.DamageTaken == damage, "test invincibility blocks health and damage statistics");
        player.ResetForRoom(player.Position, false);
        Check(player.TestInvincible, "test mode survives room reset");
        player.ToggleTestInvincibility();
        player.TakeDamage(10);
        Check(player.Health == health - 10 && game.DamageTaken == damage + 10, "disabling test mode restores damage");
        player.Heal(10);
        Set(player, "invulnerableUntil", 0f);
        game.DamageTaken = damage;
    }
    static void CaptureWarningError(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            && (message.Contains("Invalid vertex array") || stack.Contains("KarmaAttackWarning")))
            warningError = message;
    }
    static void SpatialChecks()
    {
        BossVisualChecks();
        var config = game.Config;
        var baseline = AssetDatabase.LoadAssetAtPath<KarmaConfig>("Assets/K4RMA/Settings/KarmaConfig.asset");
        Check(baseline != null, "serialized gameplay configuration exists");
        Check(baseline.playerHealth == 160 && baseline.swordDamage == 60,
            "serialized difficulty does not retain the 10000 HP / 1000 damage values");
        Check(baseline.swordWaveDamage == 72 && baseline.Techniques.risingDamage == 60
            && baseline.Techniques.counterDamage == 85 && baseline.healAfterSacrifice == 45,
            "skill damage and transition healing use the revised player balance");
        Check(KarmaCombatVFX.GlowMaterial != null, "additive combat material is included in Resources");
        foreach (Transform child in enemy.transform.Find("Artwork"))
            Check(child.name != "SwordPart" && child.name != "WardPart" && child.name != "RisePart",
                "guardian has no duplicate fixed-position equipment");
        Check(Mathf.Abs(config.jumpSpeed / config.gravityScale - 13f / 3) < 0.0001f,
            "spatial compensation preserves jump airtime");
        Check(Mathf.Abs(config.jumpSpeed * config.jumpSpeed / config.gravityScale - 169f / 3 * KarmaSpace.Scale) < 0.0001f,
            "spatial compensation scales jump height");
        Check(config.moveSpeed == baseline.moveSpeed && config.swordWaveSpeed == baseline.swordWaveSpeed,
            "serialized and default movement/projectile speeds agree");
        Check(config.Techniques.pierceDistance == baseline.Techniques.pierceDistance
            && config.BossTuning.swordSpeed == baseline.BossTuning.swordSpeed,
            "serialized and default skill/boss distances agree");
        Check(game.Player.GetComponent<BoxCollider2D>().size == KarmaSpace.PlayerSize,
            "physical body uses compensated dimensions");
        var body = game.Player.GetComponent<Rigidbody2D>();
        Set(game.Player, "move", 1f);
        Call(game.Player, "FixedUpdate");
        Check(Mathf.Abs(body.linearVelocity.x - config.moveSpeed) < 0.0001f,
            "movement applies compensated speed to the physics body");
        Set(game.Player, "jumpBufferedUntil", Time.time + 1f);
        Set(game.Player, "lastGrounded", Time.time);
        Call(game.Player, "FixedUpdate");
        Check(Mathf.Abs(body.linearVelocity.y - config.jumpSpeed) < 0.0001f,
            "buffered jump applies compensated vertical speed");
        Set(game.Player, "move", 0f);
        body.linearVelocity = Vector2.zero;
        var art = enemy.transform.Find("Artwork");
        var picture = art.Find("Guardian sprite");
        Check(Mathf.Abs(art.localScale.y - KarmaSpace.GuardianArtScale) < 0.0001f,
            "guardian keeps its larger silhouette");
        Check(Mathf.Abs(picture.position.y - (enemy.Position.y - KarmaSpace.GuardianGroundY)) < 0.0001f,
            "guardian foot pivot remains grounded after enlargement");

    }
    static void BossDamageBoundsChecks()
    {
        InheritedPoseChecks();
        foreach (string form in new[] { "Neutral", "Wave", "Rise", "Ward" })
        {
            var revival = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Guardians/Revival/" + form);
            Check(revival.Length == 6, form + " has six native get-up sprites");
            foreach (var frame in revival)
                Check(frame.pivot.y < frame.rect.height * 0.3f,
                    form + "/" + frame.name + " anchors to the ground");
        }
        Call(enemy, "InterruptPattern");
        var art = enemy.transform.Find("Artwork");
        var picture = art.Find("Guardian sprite").GetComponent<SpriteRenderer>();
        var originalSprite = picture.sprite;
        Vector3 originalScale = art.localScale;
        Vector2 originalPosition = enemy.Position, playerPosition = game.Player.Position;
        float health = enemy.Health;
        int facing = game.Player.Facing;
        foreach (string sheet in new[] { "Neutral", "Wave", "Rise", "Ward", "Inheritance" })
            foreach (var sprite in KarmaArtwork.Settings.spriteLibrary.All(sheet))
                foreach (int side in new[] { -1, 1 })
                {
                    picture.sprite = sprite;
                    art.localScale = new Vector3(side * Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
                    var bounds = picture.bounds;
                    Vector2 head = new Vector2(bounds.center.x, bounds.max.y - 0.01f);
                    Vector2 foot = new Vector2(bounds.center.x, bounds.min.y + 0.01f);
                    Check(enemy.IntersectsDamagePath(head + Vector2.left * 20, head + Vector2.right * 20, 0),
                        sheet + "/" + sprite.name + " head hit in facing " + side);
                    Check(enemy.OverlapsDamageArea(foot, Vector2.one * 0.02f), "foot/hem remains hittable");
                    Check(!enemy.IntersectsDamagePath(head + Vector2.up, head + Vector2.up + Vector2.right, 0.1f),
                        "attack entirely above artwork still misses");
                }
        picture.sprite = originalSprite; art.localScale = originalScale;
        // Real damage entry points at the upper edge, far above the old foot-centered circle.
        var body = game.Player.GetComponent<Rigidbody2D>();
        Action place = () =>
        {
            var bounds = enemy.DamageBounds;
            Vector2 at = new Vector2(bounds.min.x - 0.3f, bounds.max.y - 0.05f);
            game.Player.transform.position = at; body.position = at;
            typeof(KarmaPlayer).GetProperty("Facing").SetValue(game.Player, 1);
            Set(game.Player, "nextSword", 0f);
        };
        place(); float before = enemy.Health;
        Call(game.Player, "Attack");
        Check(enemy.Health < before, "normal sword damages the head");
        place(); before = enemy.Health;
        KarmaProjectile.Spawn(game, game.Player.Position, Vector2.right * 200, 1, Color.white, 0.1f);
        var shot = game.Projectiles[game.Projectiles.Count - 1];
        Call(shot, "Update");
        Check(enemy.Health < before && !shot.enabled, "swept projectile damages the head and stops");
        place(); before = enemy.Health;
        Call(game.Player, "BeginDirectionalSlash", Vector2.right, 1f, Essence.SwordWave);
        Check(enemy.Health < before, "directional slash damages upper artwork");
        place(); before = enemy.Health;
        Set(game.Player, "airSlashDirection", Vector2.right);
        Set(game.Player, "airSlashPrevious", game.Player.Position);
        Get<System.Collections.Generic.HashSet<KarmaEnemy>>(game.Player, "airSlashHits").Clear();
        Call(game.Player, "DamageAirSlash");
        Check(enemy.Health < before, "air slash damages upper artwork");
        before = enemy.Health; Call(game.Player, "DamageAirSlash");
        Check(enemy.Health == before, "air slash still damages each boss only once");
        place(); before = enemy.Health; game.Player.BeginRisingSlash();
        Check(enemy.Health < before, "rising slash damages upper artwork");
        place(); before = enemy.Health; Set(game.Player, "invulnerableUntil", 0f);
        game.Player.BeginCounter(); game.Player.TakeDamage(1);
        Check(enemy.Health < before, "counter measures range from the visible body");
        // World bounds must follow a boss moving through the air as well.
        var originalBounds = enemy.DamageBounds;
        enemy.transform.position += new Vector3(4, 5, 0);
        Check(Vector3.Distance(enemy.DamageBounds.center, originalBounds.center + new Vector3(4, 5, 0)) < 0.001f,
            "hurt bounds follow airborne motion");
        enemy.transform.position = originalPosition;
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, health);
        game.Player.transform.position = playerPosition; body.position = playerPosition; body.linearVelocity = Vector2.zero;
        typeof(KarmaPlayer).GetProperty("Facing").SetValue(game.Player, facing);
        Set(game.Player, "nextSword", 0f); Set(game.Player, "invulnerableUntil", 0f);
        Call(game.Player, "CancelAirSlash"); Call(enemy, "InterruptPattern");
    }
    static void InheritedPoseChecks()
    {
        foreach (Essence primary in Enum.GetValues(typeof(Essence)))
            foreach (Essence secondary in Enum.GetValues(typeof(Essence)))
            {
                if (primary == secondary) continue;
                var order = new System.Collections.Generic.List<Essence> { primary, secondary };
                for (int stage = 2; stage <= 3; stage++)
                {
                    if (stage == 3)
                        foreach (Essence third in Enum.GetValues(typeof(Essence))) if (!order.Contains(third)) { order.Add(third); break; }
                    string form = KarmaArtwork.GuardianFormName(order);
                    var sprites = KarmaArtwork.InheritedPoses(form);
                    Check(sprites.Length == 16, form + " has every combat and revival pose");
                    var host = new GameObject("Inherited pose check");
                    var art = KarmaArtwork.Guardian(host.transform, order, stage == 3);
                    var animator = art.GetComponent<KarmaCharacterAnimator>();
                    var picture = art.GetComponentInChildren<SpriteRenderer>();
                    string type = primary == Essence.SwordWave ? "Wave" : primary == Essence.RisingSlash ? "Rise" : "Ward";
                    foreach (var basePose in KarmaArtwork.Settings.spriteLibrary.All(type))
                    {
                        animator.PlayPose(basePose.name, 0);
                        Check(picture.sprite.name == basePose.name && picture.sprite.texture == sprites[0].texture,
                            form + "/" + basePose.name + " retains its inherited equipment");
                    }
                    animator.Hurt(); animator.GuardianPose("Idle", EnemyAttack.GroundStrike, false, false);
                    Check(picture.sprite.name == "Hurt" && picture.sprite.texture == sprites[0].texture,
                        form + " combat hit feedback keeps its inherited appearance");
                    var revival = Get<Sprite[]>(animator, "revivalFrames");
                    Check(revival.Length == 6 && Array.TrueForAll(revival, s => s != null && s.texture == sprites[0].texture),
                        form + " uses its own defeat and get-up frames");
                    animator.Die(); animator.Pose(0, false);
                    Check(picture.sprite.name == "Death" && picture.sprite.texture == sprites[0].texture,
                        form + " defeated pose keeps inherited equipment");
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
    }
    static void BossVisualChecks()
    {
        var root = new GameObject("Boss visual orientation probe");
        var renderer = root.AddComponent<SpriteRenderer>();
        var size = new Vector2(3, 2);
        foreach (var velocity in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            KarmaBossAttack.ConfigureVisual(renderer, velocity, size, -1, "Wave");
            Check(Vector2.Dot(renderer.transform.right, velocity.normalized) > 0.999f,
                "moving boss effect aligns with velocity " + velocity);
            Check(!renderer.flipX, "projectile orientation has no second facing flip");
            Check(Mathf.Abs(renderer.transform.localScale.x - renderer.transform.localScale.y) < 0.0001f,
                "boss artwork preserves sprite proportions");
            Check(renderer.bounds.size.x <= size.x * 1.151f && renderer.bounds.size.y <= size.y * 1.151f,
                "rotated boss effect stays within its reviewed world footprint");
        }
        foreach (string pose in new[] { "Cleave", "Pillar", "GroundRing" })
            foreach (int facing in new[] { -1, 1 })
            {
                KarmaBossAttack.ConfigureVisual(renderer, Vector2.zero, size, facing, pose);
                Check(renderer.flipX == (facing < 0), "static boss effect mirrors with facing " + pose);
                Check(Vector2.Dot(renderer.transform.up, Vector2.up) > 0.999f, "pillar/slash remains upright");
            }
        UnityEngine.Object.Destroy(root);
        var attack = KarmaBossAttack.Spawn(game, enemy, Get<Transform>(enemy, "effectsRoot"),
            enemy.Position, new Vector2(-3, 2), size, 1, 1, Color.yellow, null, 0);
        Check(attack.transform.localScale == Vector3.one && attack.transform.rotation == Quaternion.identity,
            "boss hit-box root is unaffected by art rotation or scale");
        Check(attack.Visual.transform.parent == attack.transform, "boss artwork has its own transform");
        UnityEngine.Object.Destroy(attack.gameObject);
    }
    static void CameraChecks()
    {
        var camera = game.GameCamera;
        float originalAspect = camera.aspect;
        Vector2 originalPosition = game.Player.Position;
        camera.aspect = 16f / 9;
        game.Player.ResetForRoom(new Vector2(KarmaStage.ArenaLeft + 2, KarmaSpace.Units(1)), false);
        for (int i = 0; i < 100; i++) Call(game, "LateUpdate");
        Vector3 left = camera.transform.position;
        float zoom = camera.orthographicSize;
        game.Player.ResetForRoom(new Vector2(KarmaStage.ArenaRight - 2, KarmaSpace.Units(1)), false);
        for (int i = 0; i < 100; i++) Call(game, "LateUpdate");
        Vector3 right = camera.transform.position;
        Check(right.x - left.x > 15, "camera follows horizontally through the expanded arena");
        Check(Mathf.Abs(right.y - left.y) < 0.001f && Mathf.Abs(camera.orthographicSize - zoom) < 0.001f,
            "horizontal scrolling preserves camera height and zoom");
        float halfWidth = zoom * camera.aspect;
        Check(left.x - halfWidth >= KarmaStage.ArenaLeft - 5.01f
            && right.x + halfWidth <= KarmaStage.ArenaRight + 5.01f,
            "camera stops at arena framing limits");
        var backdrop = game.World.Find("Mountain dojo artwork").GetComponent<SpriteRenderer>();
        Check(backdrop.bounds.min.x <= left.x - halfWidth + 0.001f
            && backdrop.bounds.max.x >= right.x + halfWidth - 0.001f,
            "one continuous panorama covers both camera extremes");
        Check(!backdrop.flipX && game.World.Find("Mountain dojo artwork left continuation") == null
            && game.World.Find("Mountain dojo artwork right continuation") == null,
            "scrolling uses no mirrored background copies");
        game.Player.ResetForRoom(originalPosition, false);
        camera.aspect = originalAspect;
    }
    static void ProgressionReloadChecks()
    {
        var essences = game.Player.Essences;
        var order = new System.Collections.Generic.List<Essence>(essences.SacrificeOrder);
        string serialized = JsonUtility.ToJson(essences);
        JsonUtility.FromJsonOverwrite(serialized, essences);
        Check(essences.Count == order.Count, "confirmed personalization count survives Unity serialization");
        for (int i = 0; i < order.Count; i++)
            Check(essences.SacrificeOrder[i] == order[i] && essences.HasRemnant(order[i])
                && !essences.HasActive(order[i]), "personalized skill ownership/order survives script reload");
        Call(game, "LoadRoom", false);
        enemy = game.Enemies[0];
        game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Check(essences.Count == order.Count, "room loading preserves owned skill HUD data");
        Set(essences, "progression", new KarmaProgressionState());
        Check(enemy.RestoreMissingPersonalizations(essences, order.Count, order[order.Count - 1]),
            "already-lost personalization state can migrate from the guardian cycle");
        for (int i = 0; i < order.Count; i++)
            Check(essences.SacrificeOrder[i] == order[i], "migration restores exact acquisition order");
        Check(!enemy.RestoreMissingPersonalizations(essences, order.Count, order[order.Count - 1]),
            "migration leaves existing ownership unchanged");
    }
    static void BoundaryChecks()
    {
        // Apply the update to already-existing geometry, as on an Editor script reload.
        game.World.Find("Right wall").localPosition = new Vector3(51, KarmaSpace.Units(4), 0);
        game.World.Find("Left wall").localPosition = new Vector3(-11, KarmaSpace.Units(4), 0);
        KarmaStage.RefreshBoundaries(game.World);
        // Simulate real collisions in an isolated physics scene, not just camera transforms.
        var scene = SceneManager.CreateScene("Boundary checks", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics = scene.GetPhysicsScene2D();
        foreach (var surface in game.World.GetComponentsInChildren<KarmaSurface>())
        {
            var source = surface.GetComponent<BoxCollider2D>();
            if (source == null) continue;
            var wall = new GameObject("Probe " + surface.name);
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.transform.position = surface.transform.position;
            wall.transform.localScale = surface.transform.lossyScale;
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = source.size; collider.offset = source.offset;
        }
        var probe = new GameObject("Movement collision probe");
        SceneManager.MoveGameObjectToScene(probe, scene);
        probe.AddComponent<BoxCollider2D>().size = KarmaSpace.PlayerSize;
        var body = probe.AddComponent<Rigidbody2D>();
        body.gravityScale = 0; body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.position = new Vector2(45, KarmaSpace.Units(1));
        for (int i = 0; i < 100; i++)
        { body.linearVelocity = Vector2.right * game.Config.moveSpeed; physics.Simulate(0.02f); }
        Check(body.position.x > 52, "player passes the former right invisible wall");
        Check(body.position.x <= KarmaStage.ArenaRight + 0.1f, "right boundary matches panorama edge");
        body.position = new Vector2(-5, KarmaSpace.Units(1));
        for (int i = 0; i < 100; i++)
        { body.linearVelocity = Vector2.left * game.Config.moveSpeed; physics.Simulate(0.02f); }
        Check(body.position.x < -12, "player passes the former left invisible wall");
        Check(body.position.x >= KarmaStage.ArenaLeft - 0.1f, "left boundary matches panorama edge");
        SceneManager.UnloadSceneAsync(scene);
    }
    static void WarningGeometryChecks()
    {
        var marker = KarmaVisuals.Box(null, "Warning geometry check", Vector2.zero, Vector2.one, Color.white);
        var original = marker.sprite;
        var warning = marker.gameObject.AddComponent<KarmaAttackWarning>();
        var from = new Vector2(20, 1.15f);
        var size = new Vector2(1.3f, 1.9f);
        var offsets = new[] { Vector2.zero, new Vector2(7, 0), new Vector2(0, 5),
            new Vector2(7, 5), new Vector2(-7, 5), new Vector2(7, -5), new Vector2(-7, -5) };
        foreach (var offset in offsets)
        {
            var to = from + offset;
            warning.SetPath(marker, from, to, size);
            Check(warningError == null, "warning geometry accepted by Unity");
            var vertices = marker.sprite.vertices;
            Check(vertices.Length == (offset.x != 0 && offset.y != 0 ? 6 : 4), "warning retains swept hull");
            var actual = marker.bounds;
            var min = Vector2.Min(from, to) - size / 2;
            var max = Vector2.Max(from, to) + size / 2;
            Check(Vector2.Distance(actual.min, min) < 0.0001f && Vector2.Distance(actual.max, max) < 0.0001f,
                "warning covers endpoint boxes without pivot shift");
        }
        var shape = marker.sprite;
        for (int i = 0; i < 100; i++)
            warning.SetPath(marker, from, new Vector2(13 + i * 0.017f, 7.31f - i * 0.021f), size);
        Check(warningError == null && marker.sprite == shape, "tracking warning reuses valid geometry");
        Check(original.vertices.Length == 4, "shared box sprite remains intact");
        UnityEngine.Object.Destroy(marker.gameObject);
    }
    static void StartCase()
    {
        Call(enemy,"InterruptPattern");
        typeof(KarmaEnemy).GetProperty("Health").SetValue(enemy, enemy.MaxHealth * (index >= EnrageStart ? 0.45f : 1));
        enemy.transform.position = new Vector3(20,KarmaSpace.GuardianGroundY,0);
        game.Player.transform.position = new Vector3(13,KarmaSpace.Units(1),0);
        game.Player.GetComponent<Rigidbody2D>().position = new Vector2(13,KarmaSpace.Units(1));
        Set(enemy,"engaged",true); Set(enemy,"selected",Cases[index]); Set(enemy,"direction",-1f);
        Set(enemy,"running",true); Set(enemy,"patternStarted",enemy.FightSeconds);
        Set(enemy,"nextShield",float.MaxValue);
        if(Cases[index]==EnemyAttack.GD02) Set(enemy,"shieldUntil",enemy.FightSeconds+5);
        enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern"));
        sawRecovery=sawFullShield=responseTriggered=sawResponse=injectedFailure=sawTrialFailure=false;
        startedAt=Time.time; started=true;
    }
    static bool LifecycleChecks()
    {
        if(lifecycleStage==0)
        {
            Call(enemy,"InterruptPattern");
            typeof(KarmaEnemy).GetProperty("PrimaryType").SetValue(enemy,(Essence?)Essence.Guard);
            Set(enemy,"engaged",true); Set(enemy,"running",true);
            Set(enemy,"patternStarted",enemy.FightSeconds);
            Set(enemy,"nextShield",enemy.FightSeconds+0.2f);
            lifecycleStage++; return false;
        }
        if(lifecycleStage==1)
        {
            sawShieldWarning |= Get<float>(enemy,"shieldWarningUntil")>0;
            if(!enemy.Armored) return false;
            Check(sawShieldWarning,"GD01 full generation warning before automatic shield");
            Check(Get<float>(enemy,"shieldUntil")>enemy.FightSeconds,"GD01 finite active duration");
            pauseFight=enemy.FightSeconds; waitFrame=Time.frameCount;
            game.TogglePause(); lifecycleStage++; return false;
        }
        if(lifecycleStage==2)
        {
            if(Time.frameCount<waitFrame+10) return false;
            Check(enemy.FightSeconds==pauseFight && enemy.Armored,"pause freezes shield and combat clock");
            game.TogglePause(); Call(enemy,"InterruptPattern");
            Set(enemy,"selected",EnemyAttack.GD02); Set(enemy,"engaged",true);
            Set(game.Player,"invulnerableUntil",0f); game.Player.BeginCounter();
            int parries=game.Player.ParryCount;
            enemy.TryAttackHit(game.Player.Position,game.Player.Position,new Vector2(2,2),12,new KarmaAttackContact(),0);
            Check(game.Player.ParryCount==parries+1,"GD02 real damage resolution parried");
            Check(Get<bool>(enemy,"pendingParry"),"GD02 queued interrupt");
            waitFrame=Time.frameCount; lifecycleStage++; return false;
        }
        if(lifecycleStage==3)
        {
            if(Time.frameCount<waitFrame+2) return false;
            Check(enemy.Stunned && !enemy.Armored,"GD02 parry cancels pattern and breaks shield");
            Set(game.Player,"invulnerableUntil",0f);
            var hit=new KarmaAttackContact();
            enemy.TryAttackHit(game.Player.Position,game.Player.Position,new Vector2(2,2),12,hit,1);
            Check(game.Player.Stunned,"GD03 real stun applied");
            float until=Get<float>(game.Player,"stunUntil");
            enemy.TryAttackHit(game.Player.Position,game.Player.Position,new Vector2(2,2),12,hit,1);
            Check(Get<float>(game.Player,"stunUntil")==until,"GD03 stun not refreshed");
            Call(enemy,"InterruptPattern"); Set(enemy,"selected",EnemyAttack.GD05);
            Set(enemy,"running",true); Set(enemy,"patternStarted",enemy.FightSeconds);
            enemy.StartCoroutine((IEnumerator)Call(enemy,"RunPattern"));
            lifecycleStage++; return false;
        }
        if(lifecycleStage==4)
        {
            if(!enemy.TrialActive) return false;
            Check(Get<bool>(game.Player,"confined"),"GD05 confines before attacks");
            var effects=Get<Transform>(enemy,"effectsRoot");
            game.PlayerDied();
            Check(!Get<bool>(game.Player,"confined"),"death releases confinement synchronously");
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length==0,"death disables live attacks synchronously");
            Check(!enemy.TrialActive && !enemy.Invulnerable,"death clears trial/shield");
            game.StartRun(); enemy=game.Enemies[0];
            Check(!game.Player.Stunned && !enemy.TrialActive && !enemy.Armored,"restart has no temporary state");
            effects=Get<Transform>(enemy,"effectsRoot");
            KarmaBossAttack.Spawn(game,enemy,effects,enemy.Position,Vector2.zero,Vector2.one,10,1,Color.white,null,0);
            game.Player.ResetForRoom(new Vector2(enemy.Position.x, 200), false);
            game.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.up * game.Config.jumpSpeed;
            sawAirborneLandingWait = false;
            sawCeremonyWalk = false; ceremonyPreviousX = game.Player.Position.x;
            enemy.TakeDamage(enemy.MaxHealth*2);
            Check(effects.GetComponentsInChildren<KarmaBossAttack>().Length==0,"boss death cancels owned attack");
            Check(game.Phase==RunPhase.GuardianFallen && !game.CanMove && !game.IsCombat,
                "boss death shows its fall before opening selection");
            guardianFellAt = Time.time;
            guardianFellPosition = enemy.transform.position;
            Call(game, "LateUpdate");
            guardianFallCameraPosition = game.GameCamera.transform.position;
            guardianFallCameraSize = game.GameCamera.orthographicSize;
            game.Choose(Essence.Guard); game.ConfirmChoice();
            Check(!game.PendingChoice.HasValue && game.Player.Essences.Count == 0,
                "selection input cannot skip the boss fall");
            lifecycleStage++; return false;
        }
        if(lifecycleStage==5)
        {
            if(game.Phase==RunPhase.GuardianFallen)
            {
                if (game.Player.SeparatingForCeremony)
                {
                    Check(game.Player.ReadyForPersonalization, "automatic separation begins after landing");
                    Check(Mathf.Abs(game.Player.Position.x-ceremonyPreviousX) <= game.Config.moveSpeed * Time.deltaTime + 0.2f,
                        "ceremony separation uses walking physics instead of teleporting");
                    if (game.Player.GetComponentInChildren<KarmaCharacterAnimator>().CurrentPose == "Run") sawCeremonyWalk = true;
                }
                ceremonyPreviousX = game.Player.Position.x;
                if(personalizationCase == 0 && Time.time-guardianFellAt > 0.1f)
                {
                    Check(game.Player.Position.y < 200, "airborne boss finisher descends instead of freezing");
                    if(Get<float>(game,"transitionLeft") <= 0 && !game.Player.ReadyForPersonalization)
                        sawAirborneLandingWait = true;
                }
                Check(game.GameCamera.transform.position == guardianFallCameraPosition
                    && game.GameCamera.orthographicSize == guardianFallCameraSize,
                    "boss fall preserves the combat camera framing");
                if(Time.time-guardianFellAt > 0.7f)
                {
                    var fallen = Get<Transform>(game,"fallenGuardian");
                    Check(fallen != null && fallen.gameObject.activeInHierarchy,
                        "fallen guardian remains visible after the old despawn time");
                    var fallenAnimator = fallen.GetComponent<KarmaCharacterAnimator>();
                    Check(fallenAnimator.CurrentPose == "Death", "guardian holds its defeated pose");
                }
                return false;
            }
            Check(game.Phase==RunPhase.Choosing && Time.time-guardianFellAt >= 1.7f,
                "selection opens only after the boss fall and hold");
            Check(game.Player.ReadyForPersonalization, "choice opens only after the player lands");
            Check(!game.Player.SeparatingForCeremony && sawCeremonyWalk,
                "selection waits for the automatic walking animation to finish");
            var corpseBounds = Get<Transform>(game,"fallenGuardian").GetComponentInChildren<SpriteRenderer>().bounds;
            Check(game.Player.Position.x + KarmaSpace.PlayerHalfWidth < corpseBounds.min.x
                || game.Player.Position.x - KarmaSpace.PlayerHalfWidth > corpseBounds.max.x,
                "player and defeated guardian are separated before the selection screen");
            Check(game.Player.Facing == (guardianFellPosition.x < game.Player.Position.x ? -1 : 1),
                "player faces the fallen guardian after stepping away");
            if (personalizationCase == 1) Check(game.Player.Position.x > guardianFellPosition.x,
                "guardian near left wall uses the available space on its right");
            if (personalizationCase == 2) Check(game.Player.Position.x < guardianFellPosition.x,
                "guardian near right wall uses the available space on its left");
            if(personalizationCase == 0)
                Check(sawAirborneLandingWait, "expired fall timer still waits for an airborne player to land");
            Check(game.GameCamera.transform.position == guardianFallCameraPosition
                && game.GameCamera.orthographicSize == guardianFallCameraSize,
                "selection retains the same combat framing");
            game.Choose((Essence)personalizationCase);
            game.ConfirmChoice();
            Check(game.Phase==RunPhase.Transition && !game.CanMove && !game.IsCombat,
                "confirmation closes selection and locks combat for actual player ceremony");
            Check(!game.GuardianInheriting && game.World.GetComponentInChildren<KarmaPersonalizationVFX>() != null,
                "player ceremony lives in the stage, not the selection UI");
            Check(game.Player.Essences.HasRemnant((Essence)personalizationCase), "confirmed skill already belongs to player");
            sawInheritance = sawTransformation = sawPushUp = sawStandUp = false;
            lifecycleStage++; return false;
        }
        if(lifecycleStage==6)
        {
            if(game.Phase==RunPhase.Transition)
            {
                if(Get<float>(game,"transitionLeft") < 0.4f)
                {
                    float combatSize = Mathf.Max(Get<Bounds>(game,"stageBackgroundBounds").extents.y,
                        25 / Mathf.Max(game.GameCamera.aspect, 0.01f));
                    float expectedSize = combatSize * (game.GuardianInheriting ? 0.8f : 0.75f);
                    Check(Mathf.Abs(game.GameCamera.orthographicSize - expectedSize) < expectedSize * 0.05f,
                        "ceremony uses moderate zoom relative to the combat view");
                }
                if(game.GuardianInheriting)
                {
                    enemy = game.Enemies[0];
                    Check(enemy.FightSeconds == 0 && !Get<bool>(enemy,"stopped"),
                        "guardian waits through inheritance without permanently stopping its AI");
                    var picture = enemy.GetComponentInChildren<KarmaCharacterAnimator>().GetComponentInChildren<SpriteRenderer>();
                    if(!sawInheritance)
                    {
                        Check(game.StageIndex==personalizationCase+1, "inheritance creates exactly the next stage");
                        inheritedBefore = picture.sprite;
                        Check(enemy.transform.position == guardianFellPosition,
                            "guardian revives at the place where it fell");
                        Check(enemy.GetComponentInChildren<KarmaCharacterAnimator>().CurrentPose == "ReviveFallen",
                            "inheritance begins with the actual fallen sprite");
                        sawInheritance = true;
                    }
                    string revivePose = enemy.GetComponentInChildren<KarmaCharacterAnimator>().CurrentPose;
                    if (revivePose == "Revive01" || revivePose == "Revive02") sawPushUp = true;
                    if (revivePose == "Revive04" || revivePose == "Revive05") sawStandUp = true;
                    Check(Mathf.Approximately(picture.transform.localScale.x, picture.transform.localScale.y),
                        "authored get-up poses keep body proportions instead of stretching");
                    if(Get<float>(game,"transitionLeft") < 0.8f && !sawTransformation)
                    {
                        Check(picture.sprite != inheritedBefore, "actual boss sprite transforms into its inherited form");
                        sawTransformation = true;
                    }
                }
                return false;
            }
            Check(game.Phase==RunPhase.Combat && sawInheritance && sawTransformation && sawPushUp && sawStandUp,
                "both actor ceremonies finish before normal combat resumes");
            Check(game.StageIndex==personalizationCase+1 && game.Player.Essences.Count==personalizationCase+1,
                "ceremony preserves progression and confirmed ownership");
            waitFrame=Time.frameCount; lifecycleStage++; return false;
        }
        if(lifecycleStage==7)
        {
            if(Time.frameCount<waitFrame+10) return false;
            Check(enemy.FightSeconds>0 && !Get<bool>(enemy,"stopped"), "inherited guardian AI resumes after ceremony");
            Check(enemy.transform.localScale==Vector3.one, "guardian ceremony restores collider root scale");
            float bossX = personalizationCase == 0 ? KarmaStage.BossLeftWall + 0.2f : KarmaStage.BossRightWall - 0.2f;
            enemy.transform.position = new Vector3(bossX, KarmaSpace.GuardianGroundY, 0);
            game.Player.ResetForRoom(new Vector2(bossX, KarmaSpace.PlayerHalfHeight), false);
            Physics2D.SyncTransforms();
            sawCeremonyWalk = false; ceremonyPreviousX = bossX;
            enemy.TakeDamage(enemy.MaxHealth*2);
            guardianFellAt = Time.time;
            guardianFellPosition = enemy.transform.position;
            Call(game, "LateUpdate");
            guardianFallCameraPosition = game.GameCamera.transform.position;
            guardianFallCameraSize = game.GameCamera.orthographicSize;
            if(++personalizationCase<3) { lifecycleStage=5; return false; }
            lifecycleStage=8; return false;
        }
        if(lifecycleStage==8)
        {
            if(game.Phase==RunPhase.GuardianFallen) return false;
            Check(game.Phase==RunPhase.Victory && !game.GuardianInheriting,
                "final boss victory does not start another inheritance");
            game.StartRun();
            game.Enemies[0].TakeDamage(game.Enemies[0].MaxHealth*2);
            lifecycleStage=9; return false;
        }
        if(lifecycleStage==9)
        {
            if(game.Phase==RunPhase.GuardianFallen) return false;
            game.Choose(Essence.Guard); game.ConfirmChoice();
            game.ReturnToTitle();
            Check(game.World==null && game.Player==null && game.Phase==RunPhase.Title,
                "leaving a ceremony clears the world and actor references");
            BeginPlayerDeathCheck(); lifecycleStage=10; return false;
        }
        if(lifecycleStage==10)
        {
            var animator=game.Player.GetComponentInChildren<KarmaCharacterAnimator>(true);
            sawPlayerCollapse |= animator.CurrentPose=="Hurt" || animator.CurrentPose=="Crouch";
            sawPlayerDeath |= animator.CurrentPose=="Death";
            if(!game.Player.ReadyForDefeat) sawPlayerAirborne=true;
            else if(playerLandedAt<0) playerLandedAt=Time.time;
            Check(!game.CanMove && !game.IsCombat && !game.Paused && Time.timeScale==1,
                "death stops combat and input while the scene keeps advancing");
            Call(game,"LateUpdate");
            Check(Vector3.Distance(game.GameCamera.transform.position,deathCameraPosition)<0.001f
                && Mathf.Abs(game.GameCamera.orthographicSize-deathCameraSize)<0.001f,
                "player death keeps the combat camera without zoom or tracking");
            Check(game.Elapsed==deathElapsed,"death presentation does not advance the result clock");
            Check(animator.gameObject.activeInHierarchy,"dying player does not flicker after the fatal hit");
            if(game.Phase==RunPhase.PlayerFallen)
            {
                float left=Get<float>(game,"transitionLeft");
                game.PlayerDied(); game.TogglePause();
                Check(Get<float>(game,"transitionLeft")==left && !game.Paused,
                    "repeated death callbacks and pause do not restart or freeze the fall");
                return false;
            }
            Check(game.Phase==RunPhase.Defeat && Time.time-playerDiedAt>=1.75f,
                "failure menu waits for the player death scene");
            Check(playerLandedAt>=0 && Time.time-playerLandedAt>=0.7f,
                "failure menu gives the settled fallen pose time to read");
            Check(sawPlayerCollapse && sawPlayerDeath,"player collapse leads into the held death pose");
            if(playerDeathCase==1) Check(sawPlayerAirborne && game.Player.ReadyForPersonalization,
                "airborne death lands before showing the failure menu");
            game.RetryRoom();
            Check(game.Phase==RunPhase.Combat && game.StageIndex==0 && game.Player.Health==game.Config.playerHealth
                && game.Player.Essences.Count==0,"failure retry resets the run and restores the player");
            if(++playerDeathCase<3) { BeginPlayerDeathCheck(); return false; }
            game.ReturnToTitle(); return true;
        }
        return false;
    }
    static void BeginPlayerDeathCheck()
    {
        game.StartRun();
        float y=playerDeathCase==1 ? 30 : playerDeathCase==2 ? KarmaSpace.Units(-9) : KarmaSpace.PlayerHalfHeight;
        game.Player.ResetForRoom(new Vector2(14,y),false);
        game.Player.GetComponent<Rigidbody2D>().linearVelocity=playerDeathCase==1 ? Vector2.up*game.Config.jumpSpeed : Vector2.zero;
        Physics2D.SyncTransforms();
        if(playerDeathCase==2) Call(game.Player,"FixedUpdate");
        else game.Player.TakeDamage(game.Player.Health*2);
        Check(game.Phase==RunPhase.PlayerFallen,"fatal damage or falling starts a visible death phase instead of the menu");
        Check(game.Projectiles.Count==0 && Get<bool>(game.Enemies[0],"stopped"),"fatal hit stops boss AI and projectiles");
        Call(game,"LateUpdate");
        deathCameraPosition=game.GameCamera.transform.position; deathCameraSize=game.GameCamera.orthographicSize;
        playerDiedAt=Time.time; playerLandedAt=-1; deathElapsed=game.Elapsed;
        sawPlayerCollapse=sawPlayerDeath=sawPlayerAirborne=false;
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; EditorApplication.update -= AutoTick; SessionState.SetBool(Key,false);
        string result = error==null ? "PASS: "+Cases.Length+" pattern runs, "+assertions+" Play Mode assertions" : "FAIL: "+error;
        File.WriteAllText("boss-play-validation.txt",result+"\n");
        if(error==null) Debug.Log(result); else Debug.LogException(error);
        EditorApplication.Exit(error==null ? 0 : 1);
    }
}
public sealed class KarmaBossPlayCheckDriver : MonoBehaviour
{
    public Action Step;
    void Update() { Step?.Invoke(); }
}
#endif
