using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy : MonoBehaviour
    {
        BossBehavior behavior;
        enum ActionState { Approach, Telegraph, Attack, Recover, Stunned }
        static readonly string[] ActionStateNames = System.Enum.GetNames(typeof(ActionState));
        readonly List<RaycastHit2D> terrainHits = new List<RaycastHit2D>(8);
        static ContactFilter2D TerrainFilter => KarmaPhysicsQueries.DefaultFilter;
        KarmaGame game;
        Transform art, effectsRoot;
        KarmaCharacterAnimator animator;
        SpriteRenderer shieldHalo;
        List<EnemyAttack> attacks;
        ActionState state;
        internal int CurrentPattern { get { return (int)selected; } }
        EnemyAttack selected, previous = (EnemyAttack)(-1);
        float timer, direction, patternStarted, nextUltimate, stunnedUntil;
        bool engaged, running, stopped, fullShield, pendingParry;
        bool announcedEnrage;
        KarmaBossTuning Tuning { get { return game.Config.BossTuning; } }
        float Damage { get { return game.Config.enemyDamage * Tuning.damageMultiplier * behavior.Balance.damageScale; } }
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
        public float FightSeconds { get; private set; }
        public bool Stunned { get { return state == ActionState.Stunned; } }
        public bool Invulnerable { get { return fullShield; } }
        public bool Armored { get { return fullShield || behavior != null && behavior.Armored; } }
        public bool AutoShieldActive { get { return behavior != null && behavior.AutoShieldActive; } }
        public bool TryQueueParry() => TryQueueParry(Time.time);
        internal bool TryQueueParry(float pressedAt) { return behavior != null && behavior.TryQueueParry(pressedAt); }
        internal Vector3? CameraFocus { get { return behavior?.CameraFocus; } }
        internal float CameraSize(float aspect) { return behavior != null ? behavior.CameraSize(aspect) : 0; }
        public void Initialize(KarmaGame director, int stage)
        {
            game = director;
            MaxHealth = Tuning.HealthFor(stage); Health = MaxHealth;
            PrimaryType = KarmaPatternCatalog.Primary(game.Player.Essences.PersonalizationOrder);
            DisplayName = (stage == 3 ? "마지막 수호자" : "수호자 " + (stage + 1)) + " · " + KarmaPatternCatalog.TypeName(game.Player.Essences.PersonalizationOrder);
            art = KarmaArtwork.Guardian(transform, game.Player.Essences.PersonalizationOrder, stage == 3);
            art.localScale = Vector3.one * KarmaSpace.GuardianArtScale; animator = art.GetComponent<KarmaCharacterAnimator>();
            behavior = CreateBehavior();
            attacks = behavior.BuildCycle(game.Player.Essences.PersonalizationOrder);
            effectsRoot = new GameObject("Guardian pattern objects").transform; effectsRoot.SetParent(game.World, false);
            shieldHalo = KarmaRemnantVFX.CreateGuardHalo(transform, KarmaArtwork.BarrierColor);
            shieldHalo.transform.localScale = new Vector3(KarmaSpace.Units(2.2f) * KarmaSpace.GuardianSizeBoost, KarmaSpace.Units(3.1f) * KarmaSpace.GuardianSizeBoost, 1); shieldHalo.enabled = false;
            timer = 1.4f; nextUltimate = Tuning.ultimateCooldown;
            state = ActionState.Approach;
            behavior.Initialize();
            animator.ShowStandingArt();
        }
        public void PrepareRevival(Vector3 position, Vector3 artworkScale)
        {
            transform.position = position;
            art.localScale = artworkScale;
            engaged = true;
        }
        internal KarmaGuardianGrowth.Technique GrowthTechnique(Essence essence) => behavior.GrowthTechnique(essence);
        internal KarmaGuardianGrowth.Technique UltimateForeshadow => behavior.UltimateForeshadow;
        internal string VictoryLine => behavior.VictoryLine(game.StageIndex);
        Vector3 victoryArtStart;
        internal void BeginVictoryPresentation() { victoryArtStart = art.position; }
        internal void PresentVictory(float elapsed)
        {
            float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 1.05f, elapsed));
            art.position = Vector3.Lerp(victoryArtStart,
                new Vector3(victoryArtStart.x, KarmaSpace.GuardianGroundY, victoryArtStart.z), settle);
            art.localScale = new Vector3((game.Player.Position.x < Position.x ? -1 : 1) * KarmaSpace.GuardianArtScale,
                KarmaSpace.GuardianArtScale, 1);
            animator.PresentVictory(behavior.VictoryPose, elapsed);
        }
        internal Sprite GrowthReflection => behavior.GrowthReflection;
        internal Vector2 GrowthInspectionAnchor => behavior.GrowthInspectionAnchor;
        internal KarmaGuardianGrowth BeginGrowth(Sprite fallen, string previousForm)
            => KarmaGuardianGrowth.Play(game, this, animator, game.Player.Essences.PersonalizationOrder, fallen, previousForm, shieldHalo);
        bool Active { get { return !stopped && Alive && game != null && game.Phase == RunPhase.Combat; } }
    }
}
