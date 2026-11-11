using UnityEngine;

namespace KarmaPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed partial class KarmaPlayer : MonoBehaviour
    {
        KarmaGame game;
        Rigidbody2D body;
        PhysicsMaterial2D movementMaterial;
        Transform art;
        KarmaCharacterAnimator animator;
        SpriteRenderer aura;
        SpriteRenderer remnantHalo;
        readonly Collider2D[] groundHits = new Collider2D[12];
        float move, jumpBufferedUntil, lastGrounded = -10, nextSword, invulnerableUntil, guardUntil;
        readonly KarmaTechniqueState techniques = new KarmaTechniqueState();
        public bool Airborne { get { return body.linearVelocity.y > 0.1f || !Grounded(); } }
        public KarmaEssences Essences { get; private set; }
        public float Health { get; private set; }
        public int Facing { get; private set; } = 1;
        public Vector2 Position { get { return transform.position; } }
        public bool Shielded { get { return Time.time < guardUntil; } }
        public bool TestInvincible { get; private set; }
        public bool Crouching { get; private set; }
        public void ToggleTestInvincibility() { TestInvincible = !TestInvincible; }
        public void Initialize(KarmaGame director)
        {
            game = director;
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = game.Config.gravityScale;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var playerCollider = GetComponent<BoxCollider2D>();
            playerCollider.size = KarmaSpace.PlayerSize;
            movementMaterial = new PhysicsMaterial2D("Player No Friction") { friction = 0, bounciness = 0 };
            playerCollider.sharedMaterial = movementMaterial;
            Essences = gameObject.AddComponent<KarmaEssences>();
            Essences.Initialize(this, game);
            Health = game.Config.playerHealth;
            art = KarmaArtwork.Player(transform);
            animator = art.GetComponent<KarmaCharacterAnimator>();
            aura = KarmaVisuals.Box(transform, "Guard", Vector2.zero, KarmaSpace.Units(new Vector2(1.25f, 1.9f)),
                game.Config.guardColor, 2);
            aura.enabled = false;
            KarmaCombatVFX.ApplySprite(aura, "PlayerVFX", "Guard", KarmaSpace.Units(new Vector2(1.5f, 2.15f)));
            remnantHalo = KarmaRemnantVFX.CreateGuardHalo(transform, game.Config.guardColor);
            remnantHalo.enabled = false;
        }
        void FaceAction() { if (art != null) art.localScale = new Vector3(Facing, 1, 1); }
        void OnDestroy() { if (movementMaterial != null) Destroy(movementMaterial); }
        void Update()
        {
            if (game == null) return;
            if (game.Phase == RunPhase.Ending) return;
            if (!game.SettingsOpen && KarmaInput.Pressed(KarmaAction.ToggleInvincibility)) ToggleTestInvincibility();
            if (game.Paused) { airSlashBufferedUntil = 0; return; }
            if (!game.IsCombat) ClearStatus();
            aura.enabled = Shielded;
            remnantHalo.enabled = techniques.CounterReady(Time.time);
            art.gameObject.SetActive(game.Phase == RunPhase.Transition || game.Phase == RunPhase.GuardianFallen
                || game.Phase == RunPhase.PlayerFallen || game.Phase == RunPhase.Defeat
                || Time.time >= invulnerableUntil || ((int)(Time.time * 18) % 2 == 0));
            bool airborne = Airborne;
            Crouching = game.CanMove && !ControlLocked && !airborne && KarmaInput.Vertical < 0
                && !KarmaInput.Pressed(KarmaAction.Jump) && jumpBufferedUntil <= Time.time;
            if (!PresentControlState(!airborne)) animator.Pose(game.Phase == RunPhase.GuardianFallen ? ceremonyMove : move,
                airborne, body.linearVelocity.y, Crouching);
            if (!game.CanMove) { move = 0; airSlashBufferedUntil = 0; return; }
            if (ControlLocked) { move = 0; jumpBufferedUntil = 0; airSlashBufferedUntil = 0; return; }
            move = Crouching ? 0 : KarmaInput.Horizontal;
            int previousFacing = Facing;
            if (move != 0) Facing = move > 0 ? 1 : -1;
            if (Facing != previousFacing && !airborne && !ControlLocked) animator.BeginPlayerTurn();
            aura.flipX = remnantHalo.flipX = Facing < 0;
            art.localScale = new Vector3(animator.PlayerTurning ? -Facing : Facing, 1, 1);
            if (game.OpeningArrivalSpeech != null && KarmaInput.Pressed(KarmaAction.DialogueConfirm)) return;
            if (KarmaInput.Pressed(KarmaAction.Jump)) jumpBufferedUntil = Time.time + 0.12f;
            if (game.AllowsPlayerActions)
            {
                ReadUniqueSwordInput();
                if (KarmaInput.Pressed(KarmaAction.Attack)) airSlashBufferedUntil = Time.time + game.Config.Techniques.airSlashInputBuffer;
                if (KarmaInput.Held(KarmaAction.Attack) || airSlashBufferedUntil > Time.time) Attack();
                if (KarmaInput.Pressed(KarmaAction.SwordWave)) Essences.TryUse(Essence.SwordWave);
                if (KarmaInput.Pressed(KarmaAction.RisingSlash)) Essences.TryUse(Essence.RisingSlash);
                if (KarmaInput.Pressed(KarmaAction.Guard)) Essences.TryUse(Essence.Guard);
                if (KarmaInput.Pressed(KarmaAction.Counter)) Essences.TryCounter();
            }
        }

    }
}
