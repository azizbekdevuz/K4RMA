#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Install under Assets/Editor only while running this focused Play Mode check.
[InitializeOnLoad]
public static class KarmaSfxCompletionChecks
{
    const string Key = "K4RMA.SfxCompletion";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static KarmaGame game;
    static KarmaEnemy enemy;
    static bool started;
    static int assertions;
    static string runtimeError;
    static readonly HashSet<string> observed = new HashSet<string>();

    static KarmaSfxCompletionChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        Sample();
        if (!started) EditorApplication.isPaused = false;
        if (!started && Time.frameCount > 2) { started = true; Run(); }
    }
    public static void RunBatch()
    {
        Debug.Log("Starting focused SFX Play Mode checks");
        SessionState.SetBool(Key, true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok, string message)
    {
        assertions++;
        if (!ok) throw new Exception(message);
    }
    static object Get(object value, string field) { return value.GetType().GetField(field, Fields).GetValue(value); }
    static void Set(object value, string field, object next) { value.GetType().GetField(field, Fields).SetValue(value, next); }
    static object Call(object value, string method, params object[] args)
    { return value.GetType().GetMethod(method, Fields).Invoke(value, args); }
    static AudioSource[] Sources()
    {
        var host = GameObject.Find("K4RMA Audio");
        return host != null ? host.GetComponents<AudioSource>() : Array.Empty<AudioSource>();
    }
    static bool Playing(string clip)
    {
        foreach (var source in Sources())
            if (source.isPlaying && source.clip != null && source.clip.name == clip) return true;
        return false;
    }
    static void Sample()
    {
        foreach (var source in Sources())
            if (source.isPlaying && source.clip != null) observed.Add(source.clip.name);
    }
    static async Task Wait(float seconds)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < until)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            Sample();
            await Task.Delay(20);
        }
        Sample();
        Check(runtimeError == null, "No runtime exception: " + runtimeError);
    }
    static void Clear()
    {
        KarmaAudio.StopBus(KarmaSfxBus.Gameplay);
        observed.Clear();
    }
    static async Task Prepare(Essence kind)
    {
        game.StartRun();
        game.Player.Essences.Preview(kind);
        game.Player.Essences.Confirm(out _);
        foreach (Essence extra in Enum.GetValues(typeof(Essence)))
            if (extra != kind) { game.Player.Essences.Preview(extra); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        Call(game, "LoadRoom", true);
        enemy = game.Enemies[0];
        typeof(KarmaPlayer).GetProperty("Health").SetValue(game.Player, 10000f);
        Set(enemy, "engaged", true);
        Set(enemy, "timer", float.MaxValue);
        Set(enemy, "nextUltimate", float.MaxValue);
        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
        typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, true);
        await Wait(0.25f);
        Check(enemy.PrimaryType == kind, "Requested boss selected");
        Clear();
    }
    static void Pattern(EnemyAttack attack)
    {
        Call(enemy, "InterruptPattern");
        Set(enemy, "selected", attack);
        Set(enemy, "running", true);
        Set(enemy, "patternStarted", enemy.FightSeconds);
        Set(enemy, "direction", -1f);
        Call(Get(enemy, "behavior"), "OnSelected");
        enemy.StartCoroutine((IEnumerator)Call(enemy, "RunPattern"));
    }
    static void CaptureError(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error) runtimeError = message; }

    public static void RunFollowupBatch()
    {
        SessionState.SetBool(Key + ".Followup", true);
        RunBatch();
    }
    static async Task CheckReflection()
    {
        await Prepare(Essence.Guard);
        typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
        Set(enemy, "shieldUntil", enemy.FightSeconds + 100);
        Clear();
        int parries = game.Player.ParryCount;
        Pattern(EnemyAttack.GD03);
        double parryDeadline = EditorApplication.timeSinceStartup + 5;
        while (game.Player.ParryCount == parries && EditorApplication.timeSinceStartup < parryDeadline)
        {
            foreach (var sprite in ((Transform)Get(enemy, "effectsRoot")).GetComponentsInChildren<SpriteRenderer>())
                if (sprite.name == "GD03 큰 방패 투척")
                {
                    var body = game.Player.GetComponent<Rigidbody2D>();
                    body.position = sprite.transform.position;
                    game.Player.transform.position = body.position;
                    Physics2D.SyncTransforms();
                    game.Player.BeginCounter();
                }
            await Wait(0.02f);
        }
        Check(game.Player.ParryCount > parries, "GD03 actual reflection; parries=" + game.Player.ParryCount + " observed=" + string.Join(",", observed));
        Check(observed.Contains("GD_Shield_Break"), "GD03 breaks active shield; active=" + enemy.AutoShieldActive + " observed=" + string.Join(",", observed));
        await Wait(2);
        Check(observed.Contains("Combat_Boss_Stagger"), "Reflected shield actually stuns Guard");
    }
    static async Task CheckLifecycle()
    {
        game.ReturnToTitle();
        var privateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(KarmaAudio).GetMethod("Reset", privateStatic).Invoke(null, null);
        typeof(KarmaAudio).GetMethod("Boot", privateStatic).Invoke(null, null);
        await Wait(0.15f);
        int hosts = 0;
        foreach (var node in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (node.name == "K4RMA Audio") hosts++;
        Check(hosts == 1, "Subsystem reset leaves one audio host");
        Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "One audio listener after subsystem reset");
        KarmaAudio.Play(KarmaSfxId.UiConfirm);
        Check(Playing("UI_Confirm"), "Playback recreated after subsystem reset");
    }
    static async void Run()
    {
        Application.logMessageReceived += CaptureError;
        string result;
        int exit = 0;
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Check(game != null, "Game exists in Play Mode");
            if (SessionState.GetBool(Key + ".Followup", false))
            {
                await CheckReflection();
                await CheckLifecycle();
                Finish("PASS: " + assertions + " reflection and subsystem lifecycle assertions", 0);
                return;
            }
            var library = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary");
            Check(library != null && library.Clips.Length == (int)KarmaSfxId.Count, "Catalog length");
            foreach (var clip in library.Clips) Check(clip != null, "Serialized clip resolves");
            await Prepare(Essence.SwordWave);
            foreach (var attack in new[] { EnemyAttack.SW01, EnemyAttack.SW02, EnemyAttack.SW03 })
            {
                Clear();
                Pattern(attack);
                await Wait(attack == EnemyAttack.SW03 ? 3 : 4);
                Check(observed.Contains("SW_Electric_Discharge"), attack + " electrical release playback");
                Check(!observed.Contains("SW_Wave_Launch"), attack + " has no redundant generic launch");
            }
            Clear();
            Pattern(EnemyAttack.SW04);
            await Wait(1.5f);
            enemy.TakeDamage(1);
            await Wait(6);
            Check(observed.Contains("SW_Wave_Launch"), "SW04 counter projectile launch");
            Clear();
            var playerPoint = (Vector2)game.Player.GetComponent<Collider2D>().bounds.center;
            typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
            Set(game.Player, "invulnerableUntil", 0f);
            float health = game.Player.Health;
            var contact = new KarmaAttackContact();
            enemy.TryAttackHit(playerPoint, playerPoint, Vector2.one, 1, contact, 0, sourcePattern: EnemyAttack.SW04);
            Check(game.Player.Health < health && Playing("SW_Wave_Impact"), "SW04 actual damaging impact");
            KarmaAudio.StopBus(KarmaSfxBus.Gameplay);
            enemy.TryAttackHit(playerPoint, playerPoint, Vector2.one, 1, contact, 0, sourcePattern: EnemyAttack.SW04);
            Check(!Playing("SW_Wave_Impact"), "Consumed SW04 contact stays silent");
            typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, true);

            await Prepare(Essence.SwordWave);
            Clear();
            var rain = new List<KarmaSW05RainBlade>();
            for (int i = 0; i < 6; i++)
            {
                var shot = new KarmaAerialSwordPlanner.Shot {
                    origin = new Vector2(8 + i, 3), velocity = new Vector2(0, -10),
                    size = new Vector2(1, 0.4f), life = 0.25f, delay = i * 0.04f
                };
                rain.Add(KarmaSW05RainBlade.Spawn(game, enemy, (Transform)Get(enemy, "effectsRoot"), shot, 1, null));
            }
            Check(!Playing("SW05_PerSword_Thunder"), "No thunder at rain spawn");
            await Wait(0.7f);
            foreach (var blade in rain) Check(blade.Landed, "Sword actually landed");
            Check(observed.Contains("SW05_PerSword_Thunder"), "Landing thunder playback");
            int voices = 0;
            for (int i = 0; i < 12; i++)
            {
                KarmaAudio.Play(KarmaSfxId.SwPerSwordThunder);
                int count = 0;
                foreach (var source in Sources())
                    if (source.isPlaying && source.clip != null && source.clip.name == "SW05_PerSword_Thunder") count++;
                voices = Mathf.Max(voices, count);
            }
            Check(voices == 3, "Dense rain capped at three thunder voices");
            Call(enemy, "InterruptPattern");
            Check(!Playing("SW05_PerSword_Thunder"), "Interruption clears thunder tails");

            await Prepare(Essence.Guard);
            foreach (var attack in new[] { EnemyAttack.GroundStrike, EnemyAttack.GD02 })
            {
                Clear();
                Pattern(attack);
                await Wait(3);
                Check(observed.Contains(attack == EnemyAttack.GD02 ? "GD02_Heavy_Swing" : "GD_Basic_Swing"), "Guard release playback " + attack);
                Call(enemy, "InterruptPattern");
            }
            typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
            foreach (var attack in new[] { EnemyAttack.GroundStrike, EnemyAttack.GD02 })
            {
                string hit = attack == EnemyAttack.GD02 ? "GD02_Heavy_Hit" : "GD_Basic_Hit";
                Clear();
                Set(game.Player, "invulnerableUntil", 0f);
                Set(game.Player, "guardUntil", 0f);
                playerPoint = game.Player.GetComponent<Collider2D>().bounds.center;
                contact = new KarmaAttackContact();
                enemy.TryAttackHit(playerPoint + Vector2.up * 20, playerPoint + Vector2.up * 20,
                    Vector2.one, 1, contact, 0, sourcePattern: attack);
                Check(!Playing(hit), "Miss stays silent " + attack);
                game.Player.BeginGuard(1);
                enemy.TryAttackHit(playerPoint, playerPoint, Vector2.one, 1, new KarmaAttackContact(), 0, sourcePattern: attack);
                Check(!Playing(hit), "Block stays silent " + attack);
                Set(game.Player, "guardUntil", 0f);
                game.Player.BeginCounter();
                enemy.TryAttackHit(playerPoint, playerPoint, Vector2.one, 1, new KarmaAttackContact(), 0, sourcePattern: attack);
                Check(!Playing(hit), "Parry stays silent " + attack);
                Set(game.Player, "invulnerableUntil", 0f);
                health = game.Player.Health;
                enemy.TryAttackHit(playerPoint, playerPoint, Vector2.one, 1, new KarmaAttackContact(), 0, sourcePattern: attack);
                Check(game.Player.Health < health && Playing(hit), "Confirmed damaging contact playback " + attack);
            }
            await Prepare(Essence.SwordWave);
            KarmaAudio.StopBus(KarmaSfxBus.Gameplay);
            var effectNode = new GameObject("Audio lifetime check");
            effectNode.AddComponent<SpriteRenderer>();
            var effectOwner = effectNode.AddComponent<KarmaEffect>();
            effectOwner.Initialize(100);
            KarmaAudio.Play(KarmaSfxId.SwWaveTravel, lifetimeOwner: effectOwner);
            effectOwner.enabled = false;
            await Wait(0.1f);
            Check(!Playing("SW_Wave_Travel"), "Disabled owner cleanup");
            UnityEngine.Object.Destroy(effectNode);
            game.TogglePause();
            KarmaAudio.Play(KarmaSfxId.UiMove);
            Check(Playing("UI_Move"), "UI works during pause");
            game.TogglePause();
            game.ReturnToTitle();
            await Wait(0.2f);
            Check(!Playing("Amb_Arena_Loop"), "Title stops ambience");
            game.StartRun();
            enemy = game.Enemies[0];
            Set(enemy, "timer", float.MaxValue);
            Set(enemy, "engaged", true);
            typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, true);
            enemy.transform.position = new Vector3(game.Player.Position.x + 5, KarmaSpace.GuardianGroundY, 0);
            await Wait(0.3f);
            Clear();
            health = enemy.Health;
            game.Player.Essences.TryUse(Essence.SwordWave);
            Check(Playing("SW_Wave_Launch"), "Accepted player wave launch");
            await Wait(0.8f);
            Check(enemy.Health < health && observed.Contains("SW_Wave_Impact"), "Player wave confirmed impact");
            Clear();
            game.Player.Essences.TryUse(Essence.RisingSlash);
            Check(Playing("PL_Rising_Slash"), "Accepted active rising slash");
            game.Player.Essences.TryUse(Essence.Guard);
            Check(Playing("GD_Shield_Activate"), "Accepted player guard activation");
            game.Player.Essences.Preview(Essence.RisingSlash);
            game.Player.Essences.Confirm(out _);
            Clear();
            Call(game.Player, "BeginAirSlash", Vector2.right);
            Check(Playing("PL_Sword_Swing"), "Committed inherited air slash");
            Call(game.Player, "CancelAirSlash");
            Clear();
            Pattern(EnemyAttack.GroundStrike);
            await Wait(2);
            Check(observed.Contains("GD_Basic_Swing"), "Neutral guardian actual release");
            Call(enemy, "InterruptPattern");
            typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
            Set(game.Player, "invulnerableUntil", 0f);
            Set(game.Player, "guardUntil", 0f);
            playerPoint = game.Player.GetComponent<Collider2D>().bounds.center;
            health = game.Player.Health;
            enemy.TryAttackHit(playerPoint, playerPoint, Vector2.one, 1, new KarmaAttackContact(), 0);
            Check(game.Player.Health < health && Playing("Combat_Blade_Hit"), "Neutral guardian confirmed contact");
            await CheckReflection();
            result = "PASS: " + assertions + " catalog, actual-pattern release, contact outcome, rain landing, concurrency, cleanup and pause assertions. No audible quality verdict.";
        }
        catch (Exception error) { result = "FAIL: " + error; exit = 1; }
        Finish(result, exit);
    }
    static void Finish(string result, int exit)
    {
        Application.logMessageReceived -= CaptureError;
        SessionState.EraseBool(Key + ".Followup");
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/sfx-completion.txt", result);
        Debug.Log(result);
        EditorApplication.Exit(exit);
    }
}
#endif
