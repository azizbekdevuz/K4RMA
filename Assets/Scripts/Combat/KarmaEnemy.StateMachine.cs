using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void Update()
        {
            if (game == null || stopped) return;
            if (game.Paused) return;
            if (game.Phase == RunPhase.Transition)
            {
                animator.GuardianPose("Idle", selected, false, false);
                return;
            }
            if (!Active) { CancelCombat(); return; }
            if (!engaged)
            {
                if (!behavior.CanEngage) { return; }
                engaged = true;
            }
            float dt = Time.deltaTime; FightSeconds += dt;
            if (Enraged && !announcedEnrage)
            {
                announcedEnrage = true;
                KarmaCombatVFX.SpawnSprite(effectsRoot, Position, "PersonalizationVFX", "Fusion", KarmaSpace.Units(new Vector2(4, 4)), 0.7f);
            }
            if (pendingParry)
            {
                pendingParry = false; InterruptPatternCore(true);
                behavior.OnParried();

                state = ActionState.Stunned; stunnedUntil = FightSeconds + Mathf.Max(0.1f, behavior.ParryStunSeconds);
                ShowStunCue();
            }
            behavior.Tick();
            shieldHalo.enabled = Armored || behavior.BlocksSelection;
            shieldHalo.color = behavior.ShieldColor;
            animator.GuardianPose(ActionStateNames[(int)state], selected, Position.y > KarmaSpace.Units(1.5f), Armored);
            if (Stunned)
            {
                if (FightSeconds < stunnedUntil) return;
                HideStunCue(); state = ActionState.Approach; timer = 0.8f;
            }
            if (running)
            {
                if (FightSeconds - patternStarted > Mathf.Max(behavior.PatternTimeout, Tuning.patternTimeout) || Position.y < KarmaSpace.Units(-2) || Position.y > KarmaSpace.Units(12))
                { InterruptPatternCore(true); state = ActionState.Approach; timer = 1.2f; }
                return;
            }
            float dx = game.Player.Position.x - Position.x;
            if (behavior.TickMovement(dt, dx)) return;
            timer -= dt;
            if (timer <= 0 && !behavior.BlocksSelection) BeginAttack();
        }
        void BeginAttack()
        {
            float distance = Mathf.Abs(game.Player.Position.x - Position.x);
            bool escape = Position.x > KarmaStage.ArenaLeft + 5 && Position.x < KarmaStage.ArenaRight - 5;
            bool sight = ClearShot(Position, game.Player.Position);
            if (!behavior.TrySelect(distance, sight, escape, out selected))
            { timer = behavior.RetryDelay; return; }
            behavior.OnSelected();
            previous = selected;
            if (behavior.IsUltimate(selected)) nextUltimate = FightSeconds + Mathf.Max(10, Tuning.ultimateCooldown);
            direction = game.Player.Position.x < Position.x ? -1 : 1;
            art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            running = true; patternStarted = FightSeconds;

            StartCoroutine(RunPattern());
        }
        IEnumerator RunPattern()
        {
            yield return behavior.Execute(selected);
            if (!Active) yield break;
            ClearPatternObjects();
            fullShield = false;
            behavior.EndAction(false);
            behavior.OnCompleted();
            state = ActionState.Approach; running = false;
            timer = Mathf.Max(0.22f, behavior.DecisionDelay);
        }
        IEnumerator Wait(float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds && Active)
            { yield return null; if (!game.Paused) elapsed += Time.deltaTime; }
        }
        IEnumerator Recovery(float seconds, bool keepAutoShield = false, bool scaleDuration = true)
        {
            ClearPatternObjects(); fullShield = false;
            behavior.EndAction(keepAutoShield);
            animator.EndGuardianAction();
            state = ActionState.Recover;

            yield return Wait(scaleDuration ? Mathf.Max(0.38f, seconds * Mathf.Clamp(Tuning.recoveryMultiplier, 0.4f, 2) * Tempo * behavior.Balance.recoveryScale) : seconds);
        }
        void Move(float distance)
        { transform.position = new Vector3(Mathf.Clamp(Position.x + distance, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), Position.y, 0); }
        bool ClearShot(Vector2 from, Vector2 to)
        {
            Physics2D.Linecast(from, to, TerrainFilter, terrainHits);
            foreach (var hit in terrainHits)
                if (hit.collider.GetComponent<KarmaSurface>() != null && hit.collider.GetComponent<PlatformEffector2D>() == null) return false;
            return true;
        }
        Transform stunCue;
        bool stunCueSoundStarted;
        void ShowStunCue()
        {
            // The electrical stun uses its own visual, and short stuns can remove
            // their stars before this one-shot finishes.
            if (!stunCueSoundStarted)
            {
                stunCueSoundStarted = true;
                KarmaAudio.Play(KarmaSfxId.CombatBossStagger, lifetimeOwner: this);
            }
            if (stunCue != null) return;
            var sprite = behavior.StunSprite;
            if (sprite == null) return;
            var go = new GameObject("기절 표시");
            stunCue = go.transform;
            stunCue.SetParent(transform, false);
            stunCue.localPosition = Vector3.up * KarmaSpace.Units(2.8f);
            var mark = go.AddComponent<SpriteRenderer>();
            mark.sprite = sprite;
            mark.sortingOrder = 40;
            KarmaVisuals.ApplyMaterial(mark);
            float fit = KarmaSpace.Units(2.1f) / Mathf.Max(0.01f, sprite.bounds.size.y);
            stunCue.localScale = Vector3.one * fit;
        }
        void HideStunCue()
        {
            stunCueSoundStarted = false;
            if (stunCue == null) return;
            Destroy(stunCue.gameObject);
            stunCue = null;
        }
        void InterruptPattern() { InterruptPatternCore(false); }
        void InterruptPatternCore(bool preservePressure)
        {
            StopAllCoroutines(); behavior?.Interrupt(preservePressure); ClearPatternObjects(); HideStunCue();
            behavior?.OnInterrupted();
            fullShield = running = pendingParry = false;
            transform.position = new Vector3(Mathf.Clamp(Position.x, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY, 0);
        }
        public void CancelCombat()
        {
            if (stopped) return;
            stopped = true; InterruptPattern(); behavior.StopCombat();
            if (shieldHalo != null) shieldHalo.enabled = false;
        }
        void OnDisable() { if (game != null) CancelCombat(); }
        void OnDestroy() { if (effectsRoot != null) Destroy(effectsRoot.gameObject); }
    }
}
