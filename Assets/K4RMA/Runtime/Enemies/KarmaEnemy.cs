using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy : MonoBehaviour
    {
        enum ActionState { Approach, Telegraph, Recover, Charge, Ward, Volley, ChargeTurn, OverdriveWindup, OverdriveCharge, Stunned, MeteorRain }
        KarmaGame game;
        KarmaAttackOverlay overlay;
        int activeMeteors;
        Transform art;
        SpriteRenderer indicator;
        List<EnemyAttack> attacks;
        ActionState state;
        float timer, direction, lockedX, nextTrail;
        int turn, volleyLeft, volleyCount, remainingCharges, visiblePhase = 1;
        bool engaged, extraPressure;
        readonly KarmaBurnState burn = new KarmaBurnState();
        float reflectedDamage, reflectAt, nextAura, recoilRemaining;
        public float BurnRemaining { get { return burn.Remaining(Time.time); } }
        public bool Stunned { get { return state == ActionState.Stunned; } }
        public bool Invulnerable { get { return state == ActionState.OverdriveWindup || state == ActionState.OverdriveCharge; } }
        public float FightSeconds { get; private set; }
        public int CombatPhase { get { return Health > MaxHealth * 0.6f ? 1 : Health > MaxHealth * 0.25f ? 2 : 3; } }
        public bool FlameWardActive { get { return state == ActionState.Ward && selected == EnemyAttack.EmberAegis; } }
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
            get { return state == ActionState.Ward || (state == ActionState.Charge && selected == EnemyAttack.GuardedCharge); }
        }
        public void Initialize(KarmaGame director, int stage)
        {
            game = director; finalGuardian = stage == 3;
            overlay = gameObject.AddComponent<KarmaAttackOverlay>();
            overlay.Initialize(game, this);
            MaxHealth = Tuning.HealthFor(stage);
            Health = MaxHealth;
            DisplayName = finalGuardian ? "마지막 수호자 / 최종 시련" : new[] { "첫 번째 수호자", "두 번째 수호자", "세 번째 수호자" }[stage];
            art = KarmaVisuals.Character(transform, finalGuardian ? new Color(0.57f, 0.28f, 0.68f) : new Color(0.67f, 0.34f, 0.36f), finalGuardian);
            art.localScale = Vector3.one * 1.4f;
            // Only recipes whose ingredients have returned are unlocked.
            attacks = KarmaPatternCatalog.BuildCycle(game.Player.Essences.SacrificeOrder);
            if (!finalGuardian) attacks.Remove(EnemyAttack.Overdrive);
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
