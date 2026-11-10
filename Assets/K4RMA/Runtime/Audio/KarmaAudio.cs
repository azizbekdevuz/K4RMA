using System;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using UnityEngine;

namespace KarmaPrototype
{
    public enum KarmaSfxId
    {
        RsDashStart,
        RsDashImpact,
        RsSlash01,
        RsSlash02,
        RsTelegraph,
        RsUp01Ascend,
        RsUp01Descent,
        RsUp01Landing,
        RsUp02Pursuit,
        RsUp03Wave,
        RsShardsRelease,
        RsUp04ShieldBurst,
        RsUp04Knockback,
        RsUp05Charge,
        RsUp05Crossing,
        RsUp05Final,
        SwEnergyCharge,
        SwWaveLaunch,
        SwWaveTravel,
        SwWaveImpact,
        SwCrossingBlades,
        SwScarRise,
        SwUltimateRain,
        GdShieldActivate,
        GdShieldHit,
        GdShieldBreak,
        GdHeavySlam,
        GdCounter,
        GdProtectionPulse,
        GdUltimate,
        PlSwordSwing,
        PlRisingSlash,
        PlDash,
        PlLand,
        PlDamage,
        CombatBladeHit,
        CombatPerfectParry,
        CombatGuardBlock,
        CombatBossStagger,
        CombatBossDefeat,
        UiMove,
        UiConfirm,
        UiCancel,
        GameBossIntro,
        GameAbilityInherit,
        AmbArenaLoop,
        SwPerSwordThunder,
        GdBasicSwing,
        GdBasicHit,
        GdHeavySwing,
        GdHeavyHit,
        SwElectricDischarge,
        Count
    }

    public enum KarmaSfxBus { Gameplay, Ui, Ambience, Stinger }

    // Source volume is the bus gain only. AudioListener.volume applies master and mute.
    public static class KarmaAudio
    {
        const int GameplayVoices = 8;
        const float WindowPitchLimit = 1.35f;

        struct Cue
        {
            public string File;
            public float Gain, Cooldown, Tail;
            public int Priority, MaxVoices;
            public KarmaSfxBus Bus;
            public string Slot;
        }

        sealed class Voice
        {
            public AudioSource Source;
            public Behaviour LifetimeOwner;
            public KarmaSfxId Id;
            public KarmaSfxBus Bus;
            public string Slot;
            public float Gain, Life;
            public int Priority, StartedFrame;
            public bool Live, Held;
        }

        static readonly Cue[] cues = Build();
        static readonly float[] readyAt = new float[(int)KarmaSfxId.Count];
        static readonly bool[] missing = new bool[(int)KarmaSfxId.Count];
        static readonly Voice[] voices = new Voice[12];
        static KarmaSfxLibrary library;
        static KarmaAudioHost host;
        static KarmaGame game;
        static KarmaPlayer player;
        static RunPhase phase;
        static bool loadedLibrary, inheriting, haveLevels;
        static float nextGameSearch, effects, music;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            host = null;
            ClearHostState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Ensure();
            SceneManager.activeSceneChanged += SceneChanged;
        }

        static void SceneChanged(Scene previous, Scene current)
        {
            StopBus(KarmaSfxBus.Ui);
            ResetRun();
            game = null;
            nextGameSearch = 0f;
            Ensure();
        }

        static void ClearHostState()
        {
            Array.Clear(voices, 0, voices.Length);
            Array.Clear(readyAt, 0, readyAt.Length);
            Array.Clear(missing, 0, missing.Length);
            game = null;
            player = null;
            library = null;
            loadedLibrary = inheriting = haveLevels = false;
            phase = RunPhase.Title;
            nextGameSearch = 0f;
            effects = KarmaPreferences.EffectsVolume;
            music = KarmaPreferences.MusicVolume;
        }

        public static void Play(KarmaSfxId id, float window = 0f, Behaviour lifetimeOwner = null)
        {
            if (!Application.isPlaying || id < 0 || id >= KarmaSfxId.Count) return;
            if (LifetimeEnded(lifetimeOwner)) return;
            var cue = cues[(int)id];
            float now = cue.Bus == KarmaSfxBus.Ui ? Time.unscaledTime : Time.time;
            if (now < readyAt[(int)id]) return;
            var clip = Clip(id);
            if (clip == null) return;

            Ensure();
            var voice = Claim(id, cue);
            if (voice == null) return;
            if (cue.Bus == KarmaSfxBus.Ui) StopSlot(cue.Slot);
            if (voice.Live) Release(voice);
            float pitch = 1f;
            if (window > 0f)
            {
                // The faded charge edit fits as a whole, even at the shortest warning.
                float audible = Mathf.Max(0.05f, clip.length - cue.Tail);
                float limit = id == KarmaSfxId.RsUp05Charge ? 3f : WindowPitchLimit;
                pitch = Mathf.Clamp(audible / window, 1f, limit);
            }

            readyAt[(int)id] = now + cue.Cooldown;
            voice.Id = id;
            voice.Bus = cue.Bus;
            voice.LifetimeOwner = lifetimeOwner;
            voice.Slot = cue.Slot;
            voice.Gain = cue.Gain;
            voice.Life = window > 0f ? window : -1f;
            voice.Priority = cue.Priority;
            voice.StartedFrame = Time.frameCount;
            voice.Live = true;
            voice.Held = ShouldPause(cue.Bus);
            var source = voice.Source;
            source.clip = clip;
            source.pitch = pitch;
            source.priority = 128 - cue.Priority;
            source.loop = cue.Bus == KarmaSfxBus.Ambience;
            source.volume = cue.Gain * BusLevel(cue.Bus);
            source.Play();
            if (voice.Held) source.Pause();
        }

        // A slot groups cues for explicit cancellation. Group members may overlap.
        public static void StopSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            foreach (var voice in voices)
                if (voice != null && voice.Live && voice.Slot == slot) Release(voice);
        }

        public static void StopBus(KarmaSfxBus bus)
        {
            foreach (var voice in voices)
                if (voice != null && voice.Live && voice.Bus == bus) Release(voice);
        }

        internal static void Tick(KarmaAudioHost owner)
        {
            if (host != owner) return;
            FindGame();
            float nextEffects = KarmaPreferences.EffectsVolume;
            bool audition = haveLevels && game != null && game.SettingsOpen && !Mathf.Approximately(nextEffects, effects);
            effects = nextEffects;
            music = KarmaPreferences.MusicVolume;
            haveLevels = true;

            RecycleFinished();
            FollowGame();
            if (audition) Play(KarmaSfxId.UiMove);
            float ambienceDuck = HasVoice(KarmaSfxBus.Stinger) ? 0.45f : 1f;
            foreach (var voice in voices)
            {
                if (voice == null || !voice.Live || voice.Source == null) continue;
                var source = voice.Source;
                source.volume = voice.Gain * BusLevel(voice.Bus) * (voice.Bus == KarmaSfxBus.Ambience ? ambienceDuck : 1f);
                bool hold = ShouldPause(voice.Bus);
                if (hold != voice.Held)
                {
                    voice.Held = hold;
                    if (hold) source.Pause();
                    else source.UnPause();
                    continue;
                }
                if (hold || voice.StartedFrame == Time.frameCount || voice.Life < 0f) continue;
                voice.Life -= voice.Bus == KarmaSfxBus.Ui ? Time.unscaledDeltaTime : Time.deltaTime;
                if (voice.Life <= 0f) Release(voice);
            }
        }

        internal static void Detach(KarmaAudioHost owner)
        {
            if (host != owner) return;
            host = null;
            ClearHostState();
        }

        static void FindGame()
        {
            if (game != null && game.isActiveAndEnabled) return;
            if (!ReferenceEquals(game, null))
            {
                ResetRun();
                game = null;
                nextGameSearch = 0f;
            }
            if (Time.unscaledTime < nextGameSearch) return;
            nextGameSearch = Time.unscaledTime + 1f;
            var found = Object.FindAnyObjectByType<KarmaGame>();
            if (found == null || !found.isActiveAndEnabled) return;
            game = found;

            // The empty-scene bootstrap creates a camera without a listener.
            if (game.GameCamera != null && Object.FindAnyObjectByType<AudioListener>() == null)
                game.GameCamera.gameObject.AddComponent<AudioListener>();
        }

        static void ResetRun()
        {
            StopBus(KarmaSfxBus.Gameplay);
            StopBus(KarmaSfxBus.Stinger);
            StopBus(KarmaSfxBus.Ambience);
            Array.Clear(readyAt, 0, readyAt.Length);
            player = null;
            phase = RunPhase.Title;
            inheriting = false;
        }

        static void FollowGame()
        {
            if (game == null) return;
            if (!ReferenceEquals(player, game.Player))
            {
                ResetRun();
                player = game.Player;
            }
            var nextPhase = game.Phase;
            bool nextInheriting = game.GuardianInheriting;
            if (nextPhase == RunPhase.GuardianFallen && phase != RunPhase.GuardianFallen)
                Play(KarmaSfxId.CombatBossDefeat);
            if (nextInheriting && !inheriting)
            {
                StopBus(KarmaSfxBus.Gameplay);
                Play(KarmaSfxId.GameAbilityInherit);
            }
            if (nextPhase == RunPhase.Combat && phase != RunPhase.Combat && !inheriting)
                Play(KarmaSfxId.GameBossIntro);
            phase = nextPhase;
            inheriting = nextInheriting;

            bool wantBed = game.SettingsOpen || phase != RunPhase.Title;
            bool bedLive = HasVoice(KarmaSfxBus.Ambience);
            if (wantBed && !bedLive) Play(KarmaSfxId.AmbArenaLoop);
            if (!wantBed && bedLive) StopBus(KarmaSfxBus.Ambience);
        }

        static bool ShouldPause(KarmaSfxBus bus)
        {
            return game != null && game.Paused && bus != KarmaSfxBus.Ui
                && (bus != KarmaSfxBus.Ambience || !game.SettingsOpen);
        }

        static float BusLevel(KarmaSfxBus bus) { return bus == KarmaSfxBus.Ambience ? music : effects; }

        static bool HasVoice(KarmaSfxBus bus)
        {
            foreach (var voice in voices)
                if (voice != null && voice.Live && voice.Bus == bus) return true;
            return false;
        }

        static void RecycleFinished()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                if (voice == null) continue;
                if (voice.Source == null) voices[i] = null;
                else if (voice.Live && (LifetimeEnded(voice.LifetimeOwner) ||
                    (!voice.Held && voice.StartedFrame != Time.frameCount && !voice.Source.isPlaying)))
                    Release(voice);
            }
        }

        static bool LifetimeEnded(Behaviour owner)
        { return !ReferenceEquals(owner, null) && (owner == null || !owner.isActiveAndEnabled); }

        static Voice Claim(KarmaSfxId id, Cue cue)
        {
            RecycleFinished();
            int cap = cue.Bus == KarmaSfxBus.Gameplay ? GameplayVoices : cue.Bus == KarmaSfxBus.Ui ? 2 : 1;
            int live = 0, matching = 0;
            Voice free = null, victim = null, sameCueVictim = null;
            foreach (var voice in voices)
            {
                if (voice == null) continue;
                if (!voice.Live)
                {
                    free ??= voice;
                    continue;
                }
                if (voice.Bus != cue.Bus) continue;
                if (cue.Bus == KarmaSfxBus.Ui && voice.Slot == cue.Slot) return voice;
                live++;
                if (cue.MaxVoices > 0 && voice.Id == id)
                {
                    matching++;
                    if (sameCueVictim == null || Remaining(voice) < Remaining(sameCueVictim))
                        sameCueVictim = voice;
                }
                if (victim == null || voice.Priority < victim.Priority ||
                    (voice.Priority == victim.Priority && Remaining(voice) < Remaining(victim)))
                    victim = voice;
            }
            if (cue.MaxVoices > 0 && matching >= cue.MaxVoices) return sameCueVictim;
            if (live < cap) return free ?? Create();
            if (victim == null || victim.Priority > cue.Priority) return null;
            return victim;
        }

        static float Remaining(Voice voice)
        {
            var source = voice.Source;
            if (source.loop) return float.PositiveInfinity;
            var clip = source.clip;
            float seconds = (clip.samples - source.timeSamples) / (clip.frequency * source.pitch);
            return voice.Life < 0f ? seconds : Mathf.Min(seconds, voice.Life);
        }

        static Voice Create()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null) continue;
                var source = host.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                voices[i] = new Voice { Source = source };
                return voices[i];
            }
            return null;
        }

        static void Release(Voice voice)
        {
            voice.Live = voice.Held = false;
            voice.LifetimeOwner = null;
            voice.Slot = null;
            if (voice.Source == null) return;
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Source.loop = false;
        }

        static void Ensure()
        {
            if (host != null) return;
            var go = new GameObject("K4RMA Audio");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<KarmaAudioHost>();
        }

        static AudioClip Clip(KarmaSfxId id)
        {
            int index = (int)id;
            if (!loadedLibrary)
            {
                library = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary");
                loadedLibrary = true;
            }
            var clips = library != null ? library.Clips : null;
            var clip = clips != null && index < clips.Length ? clips[index] : null;
            if (clip == null && !missing[index])
            {
                missing[index] = true;
                Debug.LogWarning("K4RMA audio clip is missing: " + cues[index].File);
            }
            return clip;
        }

        static Cue[] Build()
        {
            var table = new Cue[(int)KarmaSfxId.Count];
            void Add(KarmaSfxId id, string file, float gain, float cooldown, float tail, int priority, KarmaSfxBus bus, string slot = null)
            { table[(int)id] = new Cue { File = file, Gain = gain, Cooldown = cooldown, Tail = tail, Priority = priority, Bus = bus, Slot = slot }; }
            // Keep primary warnings about 1 dB above the general boss reduction.
            void AddBoss(KarmaSfxId id, string file, float gain, float cooldown, float tail, int priority,
                string slot = null, bool warning = false)
                => Add(id, file, gain * (warning ? 0.71f : 0.63f), cooldown, tail, priority, KarmaSfxBus.Gameplay, slot);

            AddBoss(KarmaSfxId.RsDashStart, "RisingSlash/RS_Dash_Start.wav", 0.72f, 0.08f, 0.49f, 40, "rs-action");
            AddBoss(KarmaSfxId.RsDashImpact, "RisingSlash/RS_Dash_Impact.wav", 0.58f, 0f, 0.41f, 84);
            AddBoss(KarmaSfxId.RsSlash01, "RisingSlash/RS_Slash_01.wav", 0.6f, 0.05f, 0.35f, 62, "rs-action");
            AddBoss(KarmaSfxId.RsSlash02, "RisingSlash/RS_Slash_02.wav", 0.58f, 0.05f, 0.19f, 64, "rs-action");
            AddBoss(KarmaSfxId.RsTelegraph, "RisingSlash/RS_Telegraph.wav", 0.48f, 0.05f, 0.57f, 36, "rs-warn", warning: true);
            AddBoss(KarmaSfxId.RsUp01Ascend, "RisingSlash/RS_UP01_Ascend.wav", 0.58f, 0.05f, 0.9f, 44, "rs-motion");
            AddBoss(KarmaSfxId.RsUp01Descent, "RisingSlash/RS_UP01_Descent.wav", 0.46f, 0.05f, 0.25f, 70, "rs-motion");
            AddBoss(KarmaSfxId.RsUp01Landing, "RisingSlash/RS_UP01_Landing.wav", 0.56f, 0.08f, 0.72f, 86);
            AddBoss(KarmaSfxId.RsUp02Pursuit, "RisingSlash/RS_UP02_Pursuit.wav", 0.6f, 0.05f, 0.59f, 48, "rs-action");
            AddBoss(KarmaSfxId.RsUp03Wave, "RisingSlash/RS_UP03_Wave.wav", 0.58f, 0.08f, 0.52f, 72, "rs-action");
            AddBoss(KarmaSfxId.RsShardsRelease, "RisingSlash/RS_Shards_Release.wav", 0.34f, 0.08f, 0.37f, 60);
            AddBoss(KarmaSfxId.RsUp04ShieldBurst, "RisingSlash/RS_UP04_ShieldBurst.wav", 0.46f, 0.08f, 0.52f, 82);
            AddBoss(KarmaSfxId.RsUp04Knockback, "RisingSlash/RS_UP04_Knockback.wav", 0.42f, 0f, 0.29f, 66);
            AddBoss(KarmaSfxId.RsUp05Charge, "RisingSlash/RS_UP05_Charge_Short.wav", 0.5f, 0.05f, 0f, 52, "rs-warn", warning: true);
            AddBoss(KarmaSfxId.RsUp05Crossing, "RisingSlash/RS_UP05_Crossing.wav", 0.4f, 0.03f, 0.21f, 68, "rs-action");
            AddBoss(KarmaSfxId.RsUp05Final, "RisingSlash/RS_UP05_Final.wav", 0.6f, 0f, 0.62f, 88);
            AddBoss(KarmaSfxId.SwEnergyCharge, "SwordWave/SW_Energy_Charge.wav", 0.55f, 0.08f, 0.18f, 50, "sw-bed", warning: true);
            AddBoss(KarmaSfxId.SwWaveLaunch, "SwordWave/SW_Wave_Launch.wav", 0.5f, 0.06f, 0.49f, 64, "sw-wave");
            AddBoss(KarmaSfxId.SwWaveTravel, "SwordWave/SW_Wave_Travel.wav", 0.42f, 0.08f, 0.6f, 46, "sw-wave");
            AddBoss(KarmaSfxId.SwWaveImpact, "SwordWave/SW_Wave_Impact.wav", 0.52f, 0.08f, 0.54f, 80);
            AddBoss(KarmaSfxId.SwCrossingBlades, "SwordWave/SW_Crossing_Blades.wav", 0.48f, 0.06f, 0.23f, 70, "sw-pattern");
            AddBoss(KarmaSfxId.SwScarRise, "SwordWave/SW_Scar_Rise.wav", 0.55f, 0.1f, 0.44f, 74, "sw-pattern");
            AddBoss(KarmaSfxId.SwUltimateRain, "SwordWave/SW_Ultimate_Rain.wav", 0.42f, 0.2f, 1.08f, 86, "sw-rain", warning: true);
            AddBoss(KarmaSfxId.GdShieldActivate, "Guard/GD_Shield_Activate.wav", 0.4f, 0.1f, 0.32f, 58);
            Add(KarmaSfxId.GdShieldHit, "Guard/GD_Shield_Hit.wav", 0.42f, 0.08f, 0.49f, 64, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.GdShieldBreak, "Guard/GD_Shield_Break.wav", 0.5f, 0.12f, 0.28f, 84, KarmaSfxBus.Gameplay);
            AddBoss(KarmaSfxId.GdHeavySlam, "Guard/GD_Heavy_Slam.wav", 0.5f, 0.1f, 0.6f, 78, "gd-pattern");
            AddBoss(KarmaSfxId.GdCounter, "Guard/GD_Counter.wav", 0.48f, 0.1f, 0.29f, 80, "gd-pattern");
            AddBoss(KarmaSfxId.GdProtectionPulse, "Guard/GD_Protection_Pulse.wav", 0.34f, 0.2f, 0.22f, 76, "gd-pattern", warning: true);
            AddBoss(KarmaSfxId.GdUltimate, "Guard/GD_Ultimate.wav", 0.36f, 0.25f, 0.38f, 90, "gd-pattern", warning: true);
            Add(KarmaSfxId.PlSwordSwing, "Player/PL_Sword_Swing.wav", 0.5f, 0.06f, 0.53f, 58, KarmaSfxBus.Gameplay, "pl-swing");
            Add(KarmaSfxId.PlRisingSlash, "Player/PL_Rising_Slash.wav", 0.52f, 0.08f, 0.53f, 66, KarmaSfxBus.Gameplay, "pl-swing");
            Add(KarmaSfxId.PlDash, "Player/PL_Dash.wav", 0.4f, 0.08f, 0.45f, 60, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.PlLand, "Player/PL_Land.wav", 0.55f, 0.08f, 0f, 40, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.PlDamage, "Player/PL_Damage.wav", 0.32f, 0f, 0.27f, 74, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.CombatBladeHit, "Combat/Combat_Blade_Hit.wav", 0.48f, 0f, 0.39f, 78, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.CombatPerfectParry, "Combat/Combat_Perfect_Parry.wav", 0.5f, 0f, 0.84f, 92, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.CombatGuardBlock, "Combat/Combat_Guard_Block.wav", 0.5f, 0.08f, 0.49f, 70, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.CombatBossStagger, "Combat/Combat_Boss_Stagger.wav", 0.4f, 0f, 0.36f, 86, KarmaSfxBus.Gameplay);
            Add(KarmaSfxId.CombatBossDefeat, "Combat/Combat_Boss_Defeat.wav", 0.66f, 0.4f, 1.12f, 96, KarmaSfxBus.Stinger);
            Add(KarmaSfxId.UiMove, "UI/UI_Move.wav", 0.7f, 0.05f, 0.33f, 20, KarmaSfxBus.Ui, "ui-input");
            Add(KarmaSfxId.UiConfirm, "UI/UI_Confirm.wav", 0.42f, 0.05f, 0.25f, 24, KarmaSfxBus.Ui, "ui-input");
            Add(KarmaSfxId.UiCancel, "UI/UI_Cancel.wav", 0.42f, 0.05f, 0.53f, 24, KarmaSfxBus.Ui, "ui-input");
            Add(KarmaSfxId.GameBossIntro, "Progression/Game_Boss_Intro.wav", 0.72f, 0.4f, 2.16f, 30, KarmaSfxBus.Stinger);
            Add(KarmaSfxId.GameAbilityInherit, "Progression/Game_Ability_Inherit.wav", 0.58f, 0.4f, 1.46f, 34, KarmaSfxBus.Stinger);
            Add(KarmaSfxId.AmbArenaLoop, "Ambience/Amb_Arena_Loop.wav", 0.28f, 0.2f, 0f, 1, KarmaSfxBus.Ambience);
            AddBoss(KarmaSfxId.SwPerSwordThunder, "SwordWave/SW05_PerSword_Thunder.wav", 0.18f, 0f, 0f, 48, "sw-rain-impact");
            table[(int)KarmaSfxId.SwPerSwordThunder].MaxVoices = 3;
            AddBoss(KarmaSfxId.GdBasicSwing, "Guard/GD_Basic_Swing.wav", 0.42f, 0f, 0f, 60, "gd-pattern");
            AddBoss(KarmaSfxId.GdBasicHit, "Guard/GD_Basic_Hit.wav", 0.44f, 0f, 0f, 80);
            AddBoss(KarmaSfxId.GdHeavySwing, "Guard/GD02_Heavy_Swing.wav", 0.45f, 0f, 0f, 68, "gd-pattern");
            AddBoss(KarmaSfxId.GdHeavyHit, "Guard/GD02_Heavy_Hit.wav", 0.48f, 0f, 0f, 84);
            AddBoss(KarmaSfxId.SwElectricDischarge, "SwordWave/SW_Electric_Discharge.wav", 0.42f, 0f, 0f, 64, "sw-wave");
            return table;
        }
    }
}
