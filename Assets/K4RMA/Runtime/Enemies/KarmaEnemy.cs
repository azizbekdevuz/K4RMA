using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy : MonoBehaviour
    {
        enum ActionState { Approach, Telegraph, Attack, Recover, Stunned }
        KarmaGame game;
        Transform art, effectsRoot;
        KarmaCharacterAnimator animator;
        SpriteRenderer shieldHalo;
        List<EnemyAttack> attacks;
        ActionState state;
        internal int CurrentPattern { get { return (int)selected; } }
        EnemyAttack selected, previous = (EnemyAttack)(-1);
        float timer, direction, patternStarted, nextUltimate, stunnedUntil;
        float shieldUntil, nextShield, shieldWarningUntil;
        float swordGuardUntil, nextGuardResponse;
        bool engaged, running, stopped, fullShield, frontalGuard, swordGuard, pendingParry;
        bool announcedEnrage;
        readonly KarmaTrialState trial = new KarmaTrialState();
        KarmaBossTuning Tuning { get { return game.Config.BossTuning; } }
        float Damage { get { return game.Config.enemyDamage * Tuning.damageMultiplier; } }
        public Essence? PrimaryType { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool Alive { get { return Health > 0; } }
        public bool Enraged { get { return Alive && Health <= MaxHealth * Tuning.enrageThreshold; } }
        float Tempo { get { return Enraged ? Mathf.Clamp(Tuning.enrageTempo, 0.65f, 1) : 1; } }
        float Warning(float seconds) { return Mathf.Max(0.28f, seconds * Mathf.Clamp(Tuning.warningMultiplier, 0.65f, 1.5f) * Tempo); }
        public int Facing { get { return direction < 0 ? -1 : 1; } }
        public Vector2 Position { get { return transform.position; } }
        public string DisplayName { get; private set; }
        public string Intent { get; private set; }
        public float FightSeconds { get; private set; }
        public bool Stunned { get { return state == ActionState.Stunned; } }
        public bool Invulnerable { get { return fullShield; } }
        public bool Armored { get { return fullShield || swordGuard || frontalGuard || FightSeconds < shieldUntil; } }
        public bool TrialActive { get { return trial.Active; } }
        public string DefenseStatus
        {
            get
            {
                if (fullShield) return "구형 방어막 · 무적";
                if (swordGuard) return "반격 검막 · 피해 감소/공격 시 검 사출";
                if (frontalGuard) return "정면 방어 · 뒤쪽 공격 가능";
                if (FightSeconds < shieldUntil) return "자동방어막 · 피해 감소 " + (shieldUntil - FightSeconds).ToString("0.0") + "초";
                if (shieldWarningUntil > 0) return "GD01 자동방어막 생성 예고";
                return Stunned ? "무력화 · 공격 기회" : "방어 해제";
            }
        }
        public string TrialStatus
        { get { return trial.Active ? "시험 피격 " + trial.Hits + "/" + trial.Allowed + " · 남은 공격 " + (trial.Total - trial.Completed) : ""; } }
        public void Initialize(KarmaGame director, int stage)
        {
            game = director;
            MaxHealth = Tuning.HealthFor(stage); Health = MaxHealth;
            PrimaryType = KarmaPatternCatalog.Primary(game.Player.Essences.SacrificeOrder);
            DisplayName = (stage == 3 ? "마지막 수호자" : "수호자 " + (stage + 1)) + " · " + KarmaPatternCatalog.TypeName(game.Player.Essences.SacrificeOrder);
            art = KarmaArtwork.Guardian(transform, game.Player.Essences.SacrificeOrder, stage == 3);
            if (art == null) art = KarmaVisuals.Character(transform, PrimaryType.HasValue ? game.Config.ColorOf(PrimaryType.Value) : Color.white, stage == 3);
            art.localScale = Vector3.one * KarmaSpace.GuardianArtScale; animator = art.GetComponent<KarmaCharacterAnimator>();
            attacks = KarmaPatternCatalog.BuildCycle(game.Player.Essences.SacrificeOrder);
            effectsRoot = new GameObject("Guardian pattern objects").transform; effectsRoot.SetParent(game.World, false);
            shieldHalo = KarmaRemnantVFX.CreateGuardHalo(transform, KarmaArtwork.BarrierColor);
            shieldHalo.transform.localScale = new Vector3(KarmaSpace.Units(2.2f) * KarmaSpace.GuardianSizeBoost, KarmaSpace.Units(3.1f) * KarmaSpace.GuardianSizeBoost, 1); shieldHalo.enabled = false;
            timer = 1.4f; nextShield = Tuning.autoShieldPeriod; nextUltimate = Tuning.ultimateCooldown;
            state = ActionState.Approach; Intent = "앞으로 나아가 시험을 시작하세요";
        }
        public void PrepareRevival(Vector3 position, Vector3 artworkScale)
        {
            transform.position = position;
            art.localScale = artworkScale;
            engaged = true;
        }
        public bool RestoreMissingPersonalizations(KarmaEssences essences, int stage, Essence last)
        {
            // Migrate an already-running session whose old readonly progression was lost on reload.
            // The guardian's inherited attack cycle and last confirmed choice identify the order.
            if (essences == null || essences.Count != 0 || stage < 1 || stage > 3 || attacks == null) return false;
            for (int code = 0; code < 27; code++)
            {
                var order = new List<Essence>();
                int digits = code;
                for (int i = 0; i < stage; i++)
                {
                    var essence = (Essence)(digits % 3); digits /= 3;
                    if (order.Contains(essence)) break;
                    order.Add(essence);
                }
                if (order.Count != stage || order[stage - 1] != last) continue;
                var cycle = KarmaPatternCatalog.BuildCycle(order);
                if (cycle.Count != attacks.Count) continue;
                bool matches = true;
                for (int i = 0; i < cycle.Count; i++) if (cycle[i] != attacks[i]) { matches = false; break; }
                if (!matches) continue;
                foreach (var essence in order) essences.Sacrifice(essence);
                return true;
            }
            return false;
        }
        bool Active { get { return !stopped && Alive && game != null && game.Phase == RunPhase.Combat; } }
    }
}
