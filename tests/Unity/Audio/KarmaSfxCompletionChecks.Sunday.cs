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
            Call(enemy, "ShowStunCue"); Sample();
            Check(Count(KarmaSfxId.CombatBossStagger) == 1, kind + " repeated visual request stays silent");
        }
        await Prepare(Essence.SwordWave);
        PlacePlayer(new Vector2(17, KarmaSpace.PlayerHalfHeight));
        point = game.Player.GetComponent<Collider2D>().bounds.center;
        game.Player.BeginCounter();
        parries = game.Player.ParryCount;
        enemy.TryAttackHit(point, point, Vector2.one, 1, new KarmaAttackContact(breaksShieldOnParry: true), 0);
        await Wait(.15f);
        Check(game.Player.ParryCount == parries + 1 && !enemy.Stunned
            && Count(KarmaSfxId.CombatBossStagger) == 0, "Final SwordWave parry resistance has no false stun cue");
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
        Check(library != null && library.Clips.Length == (int)KarmaSfxId.Count, "64 registered IDs/clips");
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
        Check(Count(KarmaSfxId.GdShieldLaunch) == 1 && Source(KarmaSfxId.GdShieldFlight).loop, "One launch, controlled flight loop");
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
            var expected = pass ? KarmaSfxId.GdDomeSuccess : KarmaSfxId.GdDomeFailure;
            await Until(() => Count(expected) == 1, 35, "Actual dome outcome " + pass);
            await Wait(.6f);
            Check(Count(pass ? KarmaSfxId.GdDomeFailure : KarmaSfxId.GdDomeSuccess) == 0, "Mutually exclusive dome audio");
            Check(Count(expected) == 1, "Dome outcome once");
        }
        game.ReturnToTitle(); await Wait(.45f);
        Check(!Live(KarmaSfxId.SwSummoningEnergy) && !Live(KarmaSfxId.GdShieldFlight), "Title removes combat loops");
        await CheckLifecycle();
    }
}
#endif
