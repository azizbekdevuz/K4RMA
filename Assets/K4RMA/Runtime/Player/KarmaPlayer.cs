using UnityEngine;

namespace KarmaPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed partial class KarmaPlayer : MonoBehaviour
    {
        KarmaGame game;
        Rigidbody2D body;
        Transform art;
        KarmaCharacterAnimator animator;
        SpriteRenderer aura;
        SpriteRenderer remnantHalo;
        readonly Collider2D[] groundHits = new Collider2D[12];
        float move, jumpBufferedUntil, lastGrounded = -10, nextSword, invulnerableUntil, wardUntil;
        readonly KarmaTechniqueState techniques = new KarmaTechniqueState();
        public bool Airborne { get { return body.linearVelocity.y > 0.1f || !Grounded(); } }
        float knockbackUntil, knockbackDirection, knockbackSpeed;
        public bool KnockedBack { get { return Time.time < knockbackUntil; } }
        public KarmaEssences Essences { get; private set; }
        public float Health { get; private set; }
        public int Facing { get; private set; } = 1;
        public Vector2 Position { get { return transform.position; } }
        public bool Shielded { get { return Time.time < wardUntil; } }
        public void Initialize(KarmaGame director)
        {
            game = director;
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = game.Config.gravityScale;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            GetComponent<BoxCollider2D>().size = KarmaSpace.PlayerSize;
            Essences = gameObject.AddComponent<KarmaEssences>();
            Essences.Initialize(this, game);
            Health = game.Config.playerHealth;
            art = KarmaArtwork.Player(transform);
            if (art == null) art = KarmaVisuals.Character(transform, new Color(0.2f, 0.65f, 0.72f));
            animator = art.GetComponent<KarmaCharacterAnimator>();
            aura = KarmaVisuals.Box(transform, "Ward", Vector2.zero, KarmaSpace.Units(new Vector2(1.25f, 1.9f)),
                game.Config.wardColor, 2);
            aura.enabled = false;
            KarmaSheetEffects.Apply(aura, "PlayerVFX", "Guard", KarmaSpace.Units(new Vector2(1.5f, 2.15f)));
            remnantHalo = KarmaRemnantVFX.CreateWardHalo(transform, game.Config.wardColor);
            remnantHalo.enabled = false;
        }
        void Update()
        {
            if (game == null) return;
            if (game.Paused) { airSlashBufferedUntil = 0; techniques.ClearDirectionTap(); return; }
            if (!game.IsCombat) ClearStatus();
            aura.enabled = Shielded;
            remnantHalo.enabled = techniques.CounterReady(Time.time);
            art.gameObject.SetActive(Time.time >= invulnerableUntil || ((int)(Time.time * 18) % 2 == 0));
            animator.Pose(move, Airborne, body.linearVelocity.y);
            if (!game.CanMove) { move = 0; airSlashBufferedUntil = 0; techniques.ClearDirectionTap(); return; }
            if (Stunned) { move = 0; jumpBufferedUntil = 0; airSlashBufferedUntil = 0; techniques.ClearDirectionTap(); return; }
            move = KarmaInput.Horizontal;
            if (move != 0 && !KnockedBack) Facing = move > 0 ? 1 : -1;
            art.localScale = new Vector3(Facing, 1, 1);
            if (KarmaInput.Pressed(KarmaAction.Jump)) jumpBufferedUntil = Time.time + 0.12f;
            if (game.IsCombat)
            {
                ReadUniqueDirectionTap();
                if (KarmaInput.Pressed(KarmaAction.Attack)) airSlashBufferedUntil = Time.time + game.Config.Techniques.airSlashInputBuffer;
                if (KarmaInput.Held(KarmaAction.Attack) || airSlashBufferedUntil > Time.time) Attack();
                if (KarmaInput.Pressed(KarmaAction.SwordWave)) Essences.TryUse(Essence.Flame);
                if (KarmaInput.Pressed(KarmaAction.RisingSlash)) Essences.TryUse(Essence.Dash);
                if (KarmaInput.Pressed(KarmaAction.Ward)) Essences.TryUse(Essence.Ward);
                if (KarmaInput.Pressed(KarmaAction.Counter)) Essences.TryCounter();
            }
            else techniques.ClearDirectionTap();
        }

    }
}
