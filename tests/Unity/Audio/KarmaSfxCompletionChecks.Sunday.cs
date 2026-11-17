#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Reuses the existing focused runner. No test hooks are installed in runtime code.
public static partial class KarmaSfxCompletionChecks
{
    public static void RunSundayBatch()
    { SessionState.SetBool(Key + ".Sunday", true); RunBatch(); }
    public static void RunSundayContactsBatch()
    { SessionState.SetBool(Key + ".SundayContacts", true); RunBatch(); }

    [MenuItem("K4RMA/Validation/Stun and menu audio")]
    public static void RunStunAudioInEditor()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
        SessionState.SetBool(Key + ".Editor", true);
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        RunSundayContactsBatch();
    }

    static async Task CheckMixerSignal(AudioSource isolated, string label)
    {
        Check(isolated != null && isolated.isPlaying && !isolated.mute && isolated.volume > 0
            && AudioListener.volume > 0, label + " has a live unmuted source");
        var other = Array.FindAll(Sources(), source => source != isolated && !source.mute);
        var samples = new float[1024];
        float peak = 0;
        try
        {
            foreach (var source in other) source.mute = true;
            await Wait(0.08f);
            for (int i = 0; i < 6; i++)
            {
                AudioListener.GetOutputData(samples, 0);
                foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                await Wait(0.027f);
            }
        }
        finally
        {
            foreach (var source in other)
                if (source != null) source.mute = false;
        }
        Check(peak > 0.001f, label + " reached Unity's mixed audio output");
        string report = label + " listener output peak " + (20 * Mathf.Log10(peak)).ToString("F1") + " dBFS";
        Debug.Log(report);
        System.IO.Directory.CreateDirectory("output/validation");
        System.IO.File.AppendAllText("output/validation/stun-mixer.txt", report + Environment.NewLine);
    }
    public static void RunPlaytestGuardBatch()
    { SessionState.SetBool(Key + ".PlaytestGuard", true); RunBatch(); }

    static async Task CheckPlaytestGuard()
    {
        float savedEffects = KarmaPreferences.EffectsVolume;
        float savedMaster = KarmaPreferences.MasterVolume;
        bool savedMute = KarmaPreferences.Muted;
        try
        {
            KarmaPreferences.EffectsVolume = 0.8f;
            KarmaPreferences.MasterVolume = 0.75f;
            KarmaPreferences.Muted = false;
            await Prepare(Essence.Guard);
            PlacePlayer(new Vector2(18.7f, KarmaSpace.PlayerHalfHeight));
            Set(enemy, "shieldUntil", enemy.FightSeconds + 10f);
            float health = enemy.Health;
            Call(game.Player, "Attack");
            Sample();
            Check(enemy.Health < health && Count(KarmaSfxId.GdShieldHit) == 1
                && Count(KarmaSfxId.CombatBladeHit) == 0, "GD01 normal sword contact plays only shield impact");
            CheckGain(KarmaSfxId.GdShieldHit, 0.42f * 0.58f);
            CheckGain(KarmaSfxId.PlSwordSwing, 0.43f);

            await Wait(.1f);
            Clear();
            health = enemy.Health;
            game.Player.BeginRisingSlash();
            Sample();
            Check(enemy.Health < health && Count(KarmaSfxId.GdShieldHit) == 1
                && Count(KarmaSfxId.CombatBladeHit) == 0, "GD01 original rising slash uses the quieter shield impact");
            CheckGain(KarmaSfxId.GdShieldHit, 0.42f * 0.58f);
            CheckGain(KarmaSfxId.PlRisingSlash, 0.52f);

            await Wait(.1f);
            Clear();
            enemy.TakeDamage(1f);
            CheckGain(KarmaSfxId.GdShieldHit, 0.42f);
            await Wait(.1f);
            Clear();
            Set(enemy, "shieldUntil", 0f);
            PlacePlayer(new Vector2(18.7f, KarmaSpace.PlayerHalfHeight));
            game.Player.BeginRisingSlash();
            Sample();
            Check(Count(KarmaSfxId.CombatBladeHit) == 1 && Count(KarmaSfxId.GdShieldHit) == 0,
                "Unshielded rising slash retains ordinary confirmed contact audio");

            await Prepare(Essence.Guard);
            typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
            PlacePlayer(new Vector2(17, KarmaSpace.PlayerHalfHeight));
            Pattern(EnemyAttack.GD02);
            await Until(() => Live(KarmaSfxId.GdHeavyHit), 5, "GD02 damage cue");
            CheckGain(KarmaSfxId.GdHeavySwing, 0.69f * 0.63f);
            CheckGain(KarmaSfxId.GdHeavyHit, 0.66f * 0.63f);
            await Until(() => !(bool)Get(enemy, "running"), 10, "GD02 completion");
            Check(Count(KarmaSfxId.GdHeavySwing) == 1 && Count(KarmaSfxId.GdHeavyHit) == 1
                && Count(KarmaSfxId.PlDamage) == 1, "GD02 keeps one swing and one confirmed hit");

            await CheckDomeStunAudio(false);
            await CheckDomeStunAudio(true);
            await CheckReflection();
            Check(runtimeError == null, "No runtime error: " + runtimeError);
        }
        finally
        {
            KarmaPreferences.EffectsVolume = savedEffects;
            KarmaPreferences.MasterVolume = savedMaster;
            KarmaPreferences.Muted = savedMute;
        }
    }

    static async Task CheckDomeStunAudio(bool finalParry)
    {
        await Prepare(Essence.Guard);
        // Test invincibility keeps the trial on its success path. Counters still resolve normally.
        Pattern(EnemyAttack.GD05);
        await Until(() => enemy.TrialActive, 5, "GD05 trial starts");
        Transform parryStars = null;
        AudioSource parrySound = null;
        if (finalParry)
        {
            // Active starts 0.4 seconds before the countdown. Land a parry inside its final stun window.
            float late = enemy.FightSeconds + Mathf.Clamp(game.Config.BossTuning.guard.trialDuration, 2, 25) + 0.05f;
            await Until(() => enemy.FightSeconds >= late, 30, "GD05 final parry window");
            Check(enemy.TrialActive, "Late parry happens during the live trial");
            var player = game.Player;
            int before = player.ParryCount;
            player.BeginCounter();
            Vector2 center = player.DamageBounds.center;
            enemy.TryAttackHit(center, center, Vector2.one, 1f,
                new KarmaAttackContact(parryWithoutRiposte: true), 0f, sourcePattern: EnemyAttack.GD02);
            Check(player.ParryCount == before + 1 && enemy.Stunned, "Final GD05 contact resolves as a real parry");
            await Until(() => Live(KarmaSfxId.CombatBossSpinningStars), 1, "GD05 parry stars audio");
            parryStars = (Transform)Get(enemy, "stunCue");
            parrySound = Source(KarmaSfxId.CombatBossSpinningStars);
        }
        await Until(() => !enemy.TrialActive && enemy.Stunned && Get(enemy, "stunCue") != null,
            35, "GD05 reward stun with visible stars");
        float rewardStarted = enemy.FightSeconds;
        await Until(() => Live(KarmaSfxId.CombatBossSpinningStars), 1, "GD05 success stars audio");
        var stars = Source(KarmaSfxId.CombatBossSpinningStars);
        Check(!stars.loop, "GD05 success cannot wrap to the start of the stars clip");
        if (finalParry)
            Check(parryStars == (Transform)Get(enemy, "stunCue") && parrySound == stars && stars.isPlaying,
                "A late parry keeps the same stars and audio through trial success");
        await CheckMixerSignal(stars, finalParry ? "GD05 late parry stars" : "GD05 success stars");
        Call(enemy, "ShowStunCue");
        var mark = ((Transform)Get(enemy, "stunCue")).GetComponent<SpriteRenderer>();
        Check(mark.enabled, "GD05 displays stars while the one-shot plays");
        float stunEnds = (float)Get(enemy, "stunnedUntil");
        Check(Mathf.Abs(stunEnds - rewardStarted
            - game.Config.BossTuning.guard.trialSuccessStunSeconds) < .06f,
            "GD05 keeps its authored success stun duration");
        await Until(() => !mark.enabled && !Live(KarmaSfxId.CombatBossSpinningStars), 3,
            "GD05 finishes one star animation and one sound together");
        Check(enemy.Stunned && !enemy.Invulnerable && mark != null,
            "GD05 punish window remains open after the dizzy cue finishes");
        await Until(() => !(bool)Get(enemy, "running"), 15, "GD05 recovery");
        Check(Count(KarmaSfxId.CombatBossSpinningStars) == 1
            && Count(KarmaSfxId.GdDomeSuccess) == 0
            && Count(KarmaSfxId.CombatBossStagger) == 0
            && Count(KarmaSfxId.GdDomeFailure) == 0, "GD05 plays the spinning-stars clip once across the full success");
        await Until(() => !Live(KarmaSfxId.CombatBossSpinningStars), 1, "GD05 stars audio stops");
        Check(Get(enemy, "stunCue") == null, "GD05 clears the stars visual on recovery");
    }

    static void CheckGain(KarmaSfxId id, float expected)
    {
        var source = Source(id);
        Check(source != null && source.isPlaying
            && Mathf.Abs(source.volume - expected * KarmaPreferences.EffectsVolume) < 0.001f, id + " has the expected mixed gain");
    }

    static async Task CheckSundayContacts()
    {
        await Prepare(Essence.Guard);
        typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
        PlacePlayer(new Vector2(17, KarmaSpace.PlayerHalfHeight));
        Pattern(EnemyAttack.GD02);
        await Until(() => !(bool)Get(enemy, "running"), 12, "GD02 full attack");
        Check(Count(KarmaSfxId.GdHeavySwing) == 1 && Count(KarmaSfxId.GdHeavyHit) == 1
            && Count(KarmaSfxId.PlDamage) == 1, "GD02 release and real damaging contact once");
        Check(observed.Contains("GD02_Heavy_Hit_v2"), "Actual v2 AudioSource playback");

        await Prepare(Essence.Guard);
        typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
        PlacePlayer(new Vector2(17, KarmaSpace.PlayerHalfHeight));
        var point = (Vector2)game.Player.GetComponent<Collider2D>().bounds.center;
        Set(game.Player, "invulnerableUntil", 0f);
        float health = game.Player.Health;
        enemy.TryAttackHit(point + Vector2.up * 20, point + Vector2.up * 20,
            Vector2.one, 1, new KarmaAttackContact(), 0, sourcePattern: EnemyAttack.GD02);
        Check(game.Player.Health == health && !Live(KarmaSfxId.GdHeavyHit), "Miss has no heavy damage cue");
        game.Player.BeginGuard(1);
        enemy.TryAttackHit(point, point, Vector2.one, 1, new KarmaAttackContact(), 0, sourcePattern: EnemyAttack.GD02);
        Check(game.Player.Health == health && Live(KarmaSfxId.CombatGuardBlock) && !Live(KarmaSfxId.GdHeavyHit), "Block has block feedback only");
        Set(game.Player, "guardUntil", 0f);
        game.Player.BeginCounter();
        int parries = game.Player.ParryCount;
        enemy.TryAttackHit(point, point, Vector2.one, 1, new KarmaAttackContact(), 0, sourcePattern: EnemyAttack.GD02);
        Check(game.Player.ParryCount == parries + 1 && !Live(KarmaSfxId.GdHeavyHit), "Parry has no heavy damage cue");
        Set(game.Player, "invulnerableUntil", 0f);
        var contact = new KarmaAttackContact();
        enemy.TryAttackHit(point, point, Vector2.one, 1, contact, 0, sourcePattern: EnemyAttack.GD02);
        Check(game.Player.Health < health && Live(KarmaSfxId.GdHeavyHit), "Accepted damage plays v2");
        KarmaAudio.StopBus(KarmaSfxBus.Gameplay);
        enemy.TryAttackHit(point, point, Vector2.one, 1, contact, 0, sourcePattern: EnemyAttack.GD02);
        Check(!Live(KarmaSfxId.GdHeavyHit), "Consumed contact cannot play twice");

        foreach (Essence kind in Enum.GetValues(typeof(Essence)))
        {
            // Final-stage SwordWave deliberately resists stun; use its earlier-stage contract.
            await Prepare(kind, kind == Essence.SwordWave ? 2 : 3);
            typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
            PlacePlayer(new Vector2(17, KarmaSpace.PlayerHalfHeight));
            point = game.Player.GetComponent<Collider2D>().bounds.center;
            game.Player.BeginCounter();
            enemy.TryAttackHit(point, point, Vector2.one, 1,
                new KarmaAttackContact(breaksShieldOnParry: true), 0);
            await Until(() => enemy.Stunned, 2, kind + " real parry stun");
            Check(Get(enemy, "stunCue") != null && Count(KarmaSfxId.CombatBossStagger) == 1,
                kind + " stars and one stun sound");
            await Until(() => Live(KarmaSfxId.CombatBossSpinningStars), 1, kind + " animated stars audio");
            var stars = Source(KarmaSfxId.CombatBossSpinningStars);
            Check(stars.loop == (kind != Essence.SwordWave) && Count(KarmaSfxId.CombatBossSpinningStars) == 1,
                kind + " spins with one audio voice");
            await CheckMixerSignal(stars, kind + " spinning stars");
            Call(enemy, "ShowStunCue"); Sample();
            Check(Count(KarmaSfxId.CombatBossStagger) == 1, kind + " repeated visual request stays silent");
            Check(Count(KarmaSfxId.CombatBossSpinningStars) <= 1, kind + " repeated visual request does not restart stars audio");
            if (kind == Essence.Guard)
            {
                System.IO.Directory.CreateDirectory("output/validation");
                ScreenCapture.CaptureScreenshot("output/validation/stun-stars.png");
                await Wait(0.07f);
                var stun = Source(KarmaSfxId.CombatBossStagger);
                Call(enemy, "HideStunCue");
                Check(stun != null && stun.isPlaying, "Initial stun impact is still live after stars are removed");
                await Wait(0.12f);
                Check(!Live(KarmaSfxId.CombatBossSpinningStars), "Star removal stops its loop without cutting off the initial stun cue");
            }
        }
        await Prepare(Essence.SwordWave, 2);
        typeof(KarmaPlayer).GetProperty("TestInvincible").SetValue(game.Player, false);
        PlacePlayer(new Vector2(17, KarmaSpace.PlayerHalfHeight));
        point = game.Player.GetComponent<Collider2D>().bounds.center;
        game.Player.BeginCounter();
        enemy.TryAttackHit(point, point, Vector2.one, 1, new KarmaAttackContact(), 0, sourcePattern: EnemyAttack.SW04);
        await Until(() => enemy.Stunned, 2, "SW04 electrical parry stun");
        await Until(() => Live(KarmaSfxId.Sw04BossShock), 1, "SW04 electrical parry plays the boss shock");
        Check(Get(enemy, "stunCue") == null && Count(KarmaSfxId.Sw04BossShock) == 1,
            "Electrical feedback uses one boss shock without stars");
        Call(enemy, "ShowStunCue"); Sample();
        Check(Count(KarmaSfxId.Sw04BossShock) == 1 && Count(KarmaSfxId.CombatBossStagger) == 0
            && Count(KarmaSfxId.CombatBossSpinningStars) == 0 && Count(KarmaSfxId.Sw04PlayerShock) == 0,
            "SW04 boss electrocution keeps generic stun and player shock feedback silent");
        await CheckMixerSignal(Source(KarmaSfxId.Sw04BossShock), "SwordWave electrical stun");
        await Prepare(Essence.SwordWave);
        PlacePlayer(new Vector2(17, KarmaSpace.PlayerHalfHeight));
        point = game.Player.GetComponent<Collider2D>().bounds.center;
        game.Player.BeginCounter();
        parries = game.Player.ParryCount;
        enemy.TryAttackHit(point, point, Vector2.one, 1, new KarmaAttackContact(breaksShieldOnParry: true), 0,
            sourcePattern: EnemyAttack.SW04);
        await Wait(.15f);
        Check(game.Player.ParryCount == parries + 1 && !enemy.Stunned
            && Count(KarmaSfxId.CombatBossStagger) == 0 && Count(KarmaSfxId.Sw04BossShock) == 0,
            "Final SwordWave SW04 parry resistance has no false stun cue");
        game.ReturnToTitle();
        var library = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary");
        var menu = (KarmaSfxId)((int)KarmaSfxId.MenuMainTheme + library.DefaultMenuTrack);
        float previousMusicVolume = KarmaPreferences.MusicVolume;
        try
        {
            // The user's saved setting may intentionally mute music.
            KarmaPreferences.MusicVolume = 0.8f;
            await Until(() => Live(menu) && Source(menu).volume > 0, 2, "default title track fade-in");
            Check(Source(menu).loop, "Title track remains a loop");
            await CheckMixerSignal(Source(menu), "Default title music");
        }
        finally { KarmaPreferences.MusicVolume = previousMusicVolume; }
    }

    static AudioSource Source(KarmaSfxId id)
    {
        var clip = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary").Clips[(int)id];
        return Array.Find(Sources(), s => s.clip == clip);
    }
    static bool Live(KarmaSfxId id) => Source(id) != null && Source(id).isPlaying;

    static async Task CheckSunday()
    {
        var library = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary");
        Check(library != null && library.Clips.Length == (int)KarmaSfxId.Count, "Catalog matches registered IDs");
        Check(library.Clips[(int)KarmaSfxId.CombatBossSpinningStars].name == "Boss_Spinning_Stars", "Animated stars loop registered");
        Check(library.Clips.All(c => c != null), "All clips loaded in Unity");
        Check(library.Clips[(int)KarmaSfxId.GdHeavyHit].name == "GD02_Heavy_Hit_v2", "Heavy impact v2 registered");
        Check(library.Clips[(int)KarmaSfxId.CombatBossStagger].name == "Boss_Stun", "New stun registered at stable ID");

        game.ReturnToTitle();
        for (int i = 0; i < 4; i++)
        {
            KarmaAudio.ReviewMenuTrack(i); await Wait(.45f);
            var id = (KarmaSfxId)((int)KarmaSfxId.MenuMainTheme + i);
            Check(Live(id) && Source(id).loop, "Title candidate loops " + i);
            Check(Sources().Count(s => s.isPlaying && s.loop) == 1, "One music source " + i);
            Check(Mathf.Abs(Source(id).volume - .22f * KarmaPreferences.MusicVolume) < .002f, "Music preference " + i);
            Source(id).time = Source(id).clip.length - .12f;
            await Wait(.3f);
            Check(Source(id).time < 1 && Source(id).isPlaying, "Candidate crosses loop boundary " + i);
        }
        KarmaAudio.EndMenuReview();
        await Prepare(Essence.SwordWave); await Wait(.35f);
        Check(Live(KarmaSfxId.AmbArenaLoop), "Gameplay retains existing bed");
        Pattern(EnemyAttack.SW05); watchRain = true;
        await Until(() => Count(KarmaSfxId.SwAscensionIgnition) == 1, 5, "SW05 ascent ignition");
        Check(enemy.Position.y > KarmaSpace.GuardianGroundY, "Ignition accompanies actual ascent");
        await Until(() => Live(KarmaSfxId.SwSummoningEnergy), 5, "SW05 channel loop");
        game.TogglePause(); await Wait(.12f);
        int sample = Source(KarmaSfxId.SwSummoningEnergy).timeSamples;
        await Wait(.2f);
        Check(Source(KarmaSfxId.SwSummoningEnergy).timeSamples == sample, "Channel playhead held while paused");
        game.TogglePause(); await Wait(.15f);
        Check(Source(KarmaSfxId.SwSummoningEnergy).timeSamples > sample, "Channel resumes without restart");
        await Until(() => Count(KarmaSfxId.SwFinalExplosion) > 0, 35, "SW05 actual detonation");
        Check(rainSeen.Count > 0 && rainSeen.All(b => b == null || b.Landed), "Detonation after all rain lands");
        Check(Source(KarmaSfxId.SwFinalExplosion).volume >= 0.55f * KarmaPreferences.EffectsVolume,
            "Final explosion has the raised gain");
        await Until(() => !(bool)Get(enemy, "running"), 15, "SW05 completion");
        watchRain = false;
        Check(Count(KarmaSfxId.SwFinalExplosion) == 1 && Count(KarmaSfxId.SwAscensionIgnition) == 1, "One ignition and final explosion");
        Check(Count(KarmaSfxId.SwSummoningEnergy) == 1, "No restart per volley");
        Check(Count(KarmaSfxId.SwPerSwordThunder) > 0 && peakThunderVoices <= 3, "Individual rain feedback retains cap");
        Check(!Live(KarmaSfxId.SwSummoningEnergy), "No channel after descent");
        Clear(); Pattern(EnemyAttack.SW05);
        await Until(() => Live(KarmaSfxId.SwSummoningEnergy), 6, "Second cast channel");
        Call(enemy, "InterruptPattern"); await Wait(.1f);
        Check(!Live(KarmaSfxId.SwSummoningEnergy), "Interruption cancels channel");

        await Prepare(Essence.Guard);
        Pattern(EnemyAttack.GD03);
        await Until(() => Live(KarmaSfxId.GdShieldFlight), 5, "Shield flight");
        var flight = Source(KarmaSfxId.GdShieldFlight);
        Check(Count(KarmaSfxId.GdShieldLaunch) == 1 && flight.loop, "One launch, controlled flight loop");
        await Until(() => flight.isPlaying && flight.volume >= 0.14f * KarmaPreferences.EffectsVolume,
            1, "shield flight reaches raised gain after fade-in");
        await CheckMixerSignal(flight, "Guard shield flight");
        await Until(() => !(bool)Get(enemy, "running"), 15, "Shield normal return");
        await Wait(.2f);
        Check(Count(KarmaSfxId.GdShieldFlight) == 1 && !Live(KarmaSfxId.GdShieldFlight), "Return does not restart; catch stops");
        Pattern(EnemyAttack.GD03);
        await Until(() => Live(KarmaSfxId.GdShieldFlight), 5, "Second shield flight");
        Call(enemy, "InterruptPattern"); await Wait(.1f);
        Check(!Live(KarmaSfxId.GdShieldFlight), "Shield interruption cleanup");
        await CheckReflection();
        Check(!Live(KarmaSfxId.GdShieldFlight), "Reflected shield removed its loop");

        await Prepare(Essence.Guard); Pattern(EnemyAttack.GD04);
        await Until(() => !(bool)Get(enemy, "running"), 10, "Three ground eruptions");
        Check(Count(KarmaSfxId.GdGroundRise) == 3 && Count(KarmaSfxId.GdHeavySlam) == 1, "Three individual rises and one original slam");
        foreach (bool pass in new[] { true, false })
        {
            await Prepare(Essence.Guard); Pattern(EnemyAttack.GD05);
            var dome = Get(Get(enemy, "behavior"), "Dome");
            await Until(() => (bool)dome.GetType().GetProperty("Active").GetValue(dome), 8, "Dome trial active");
            if (!pass)
                for (int i = 0; i < 20; i++) Call(dome, "RecordHit");
            var expected = pass ? KarmaSfxId.CombatBossSpinningStars : KarmaSfxId.GdDomeFailure;
            await Until(() => Count(expected) == 1, 35, "Actual dome outcome " + pass);
            await Wait(.6f);
            Check(Count(pass ? KarmaSfxId.GdDomeFailure : KarmaSfxId.CombatBossSpinningStars) == 0, "Mutually exclusive dome audio");
            Check(Count(KarmaSfxId.GdDomeSuccess) == 0, "GD05 success uses the stars cue alone");
            Check(Count(expected) == 1, "Dome outcome once");
        }
        game.ReturnToTitle(); await Wait(.45f);
        Check(!Live(KarmaSfxId.SwSummoningEnergy) && !Live(KarmaSfxId.GdShieldFlight), "Title removes combat loops");
        await CheckLifecycle();
    }
}
#endif
