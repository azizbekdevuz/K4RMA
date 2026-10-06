using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy : MonoBehaviour
    {
        enum ActionState { Approach, Telegraph, Recover, Ward, Rising }
        KarmaGame game;
        int activeMeteors;
        Transform art;
        SpriteRenderer indicator;
        List<EnemyAttack> attacks;
        ActionState state;
        float timer, direction, lockedX;
        int turn;
        bool engaged, defenseHit, risingHitPlayer;
        float risingElapsed, risingStartX;
        public Essence? PrimaryType { get; private set; }
        readonly KarmaBurnState burn = new KarmaBurnState();
        
        public float BurnRemaining { get { return burn.Remaining(Time.time); } }
        public bool Stunned { get { return false; } }
        public bool Invulnerable { get { return false; } }
        public float FightSeconds { get; private set; }
        public int CombatPhase { get { return Health > MaxHealth * 0.6f ? 1 : Health > MaxHealth * 0.25f ? 2 : 3; } }
        public bool FlameWardActive { get { return false; } }
        KarmaBossTuning Tuning { get { return game.Config.BossTuning; } }
        float Damage { get { return game.Config.enemyDamage * Tuning.damageMultiplier; } }
        EnemyAttack selected;
        bool finalGuardian;
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool Alive { get { return Health > 0; } }
        public Vector2 Position { get { return transform.position; } }
        public string DisplayName { get; private set; }
        public string Intent { get; private set; }
        public bool Armored
        {
            get { return state == ActionState.Ward; }
        }
        public void Initialize(KarmaGame director, int stage)
        {
            game = director; finalGuardian = stage == 3;
            MaxHealth = Tuning.HealthFor(stage);
            Health = MaxHealth;
            DisplayName = finalGuardian ? "마지막 수호자 / 최종 시련" : new[] { "첫 번째 수호자", "두 번째 수호자", "세 번째 수호자" }[stage];
            PrimaryType = KarmaPatternCatalog.Primary(game.Player.Essences.SacrificeOrder);
            DisplayName += " · " + KarmaPatternCatalog.TypeName(game.Player.Essences.SacrificeOrder);
            art = KarmaArtwork.Guardian(transform, game.Player.Essences.SacrificeOrder, finalGuardian);
            bool suppliedArtwork = art != null;
            if (art == null) art = KarmaVisuals.Character(transform, PrimaryType.HasValue ? game.Config.ColorOf(PrimaryType.Value) : new Color(0.67f, 0.34f, 0.36f), finalGuardian);
            if (PrimaryType.HasValue && !suppliedArtwork)
            {
                Vector2 size = PrimaryType == Essence.Flame ? new Vector2(1.9f, 0.14f)
                    : PrimaryType == Essence.Dash ? new Vector2(0.35f, 1.7f) : new Vector2(1.1f, 1.45f);
                KarmaVisuals.Box(art, "주 타입 장비", new Vector2(0.65f, 0), size, game.Config.ColorOf(PrimaryType.Value), 6);
            }
            art.localScale = Vector3.one * 1.4f;
            // Only recipes whose ingredients have returned are unlocked.
            attacks = KarmaPatternCatalog.BuildCycle(game.Player.Essences.SacrificeOrder);
            foreach (var e in game.Player.Essences.SacrificeOrder)
            {
                var orb = KarmaVisuals.Box(transform, e.ToString(), new Vector2(-0.55f + (int)e * 0.55f, 1.65f),
                    Vector2.one * 0.23f, game.Config.ColorOf(e), 8);
                orb.transform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            timer = 1.4f; state = ActionState.Approach; Intent = "접근 중";
        }

        bool Enraged { get { return CombatPhase >= 2; } }

        public void MeteorFinished() { activeMeteors = Mathf.Max(0, activeMeteors - 1); }

    }
}
