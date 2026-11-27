using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        bool copiedTechnique;
        bool presentingCopy;
        bool finalTechniqueReleased, finishingBlowPending;

        bool TryDemonstrateFinalTechnique()
        {
            if (game.StageIndex != 2 || finalTechniqueReleased || Health > MaxHealth * 0.5f
                || Position.y > KarmaSpace.GuardianGroundY + 0.1f)
                return false;

            PlayLearnedTechnique(game.Player.Essences.MasteryLeader, true);
            return true;
        }
        string learningLine;
        float learningLineUntil;
        internal string LearningLine => FightSeconds < learningLineUntil ? learningLine : null;

        void SayLearning(string line)
        {
            learningLine = line;
            learningLineUntil = FightSeconds + 2.4f;
        }

        bool TryImitate()
        {
            var observed = game.Player.Essences.ObservedTechnique;
            if (game.StageIndex > 1 || copiedTechnique || !observed.HasValue || FightSeconds < 2.5f)
                return false;
            if (!ClearShot(Position, game.Player.Position) || Mathf.Abs(Position.x - game.Player.Position.x) > KarmaSpace.Units(7))
                return false;

            copiedTechnique = true;
            PlayLearnedTechnique(observed.Value, false);
            return true;
        }

        void PlayLearnedTechnique(Essence technique, bool mastered)
        {
            presentingCopy = running = true;
            patternStarted = FightSeconds;
            direction = game.Player.Position.x < Position.x ? -1 : 1;
            art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            StartCoroutine(DemonstrateTechnique(technique, mastered));
        }

        IEnumerator DemonstrateTechnique(Essence technique, bool mastered)
        {
            var movement = KarmaGuardianGrowth.OriginalMovement(technique);
            bool ward = movement == KarmaGuardianGrowth.Motion.Surround;
            bool rise = movement == KarmaGuardianGrowth.Motion.Rise;
            string pose = KarmaGuardianGrowth.OriginalCopyMotion(technique);
            float windup = mastered ? 0.62f : 0.94f;
            var color = game.Config.ColorOf(technique);
            SayLearning(mastered ? "마지막 기술… 이번엔 제대로 해 보겠다." :
                game.StageIndex == 0 ? "방금 그 움직임… 이렇게였나?" : "이번엔 저 기술을 따라 해 보자.");
            state = ActionState.Telegraph;
            Vector2 forward = Vector2.right * Facing;
            Vector2 center = Position + forward * KarmaSpace.Units(0.75f);
            Vector2 footprint = KarmaSpace.Units(rise ? new Vector2(2.8f, 4f) : new Vector2(2.6f, 2.3f));
            if (!ward)
            {
                if (rise) Mark("상승베기 모방 예고", center + Vector2.up, footprint);
                else PathMark(center, center + forward * KarmaSpace.Units(mastered ? 10 : 4), footprint);
            }
            animator.PresentLearningMotion(pose, 0);
            yield return Wait(windup);
            if (!Active) yield break;

            ClearPatternObjects();
            state = ActionState.Attack;
            animator.PresentLearningMotion(pose, KarmaBootstrap.Step);
            if (mastered) SayLearning("됐다. 이제 세 기술을 하나로 이을 수 있겠어.");
            if (ward)
            {
                fullShield = mastered;
                var barrier = KarmaCombatVFX.SpawnSprite(effectsRoot, Position, "PlayerVFX", "Barrier",
                    KarmaSpace.Units(new Vector2(mastered ? 2.6f : 1.4f, mastered ? 3.2f : 2f)), mastered ? 0.62f : 0.24f);
                if (barrier != null) barrier.color = Color.Lerp(color, Color.white, 0.45f);
            }
            else
            {
                Vector2 velocity = rise ? Vector2.up * KarmaSpace.Units(mastered ? 7 : 3)
                    : forward * KarmaSpace.Units(mastered ? 12 : 6);
                KarmaBossAttack.Spawn(game, this, effectsRoot, center, velocity, footprint,
                    mastered ? 0.84f : 0.48f, Damage * (mastered ? 1 : 0.45f),
                    color, new KarmaAttackContact(), 0, rise ? "Pillar" : "Wave",
                    restrainedVisual: !mastered, plainTechnique: true);
            }

            float releaseAge = 0;
            float releaseDuration = mastered ? 0.87f : 0.62f;
            Vector2 origin = Position;
            while (releaseAge < releaseDuration && Active)
            {
                yield return null;
                if (game.Paused) continue;
                releaseAge += Time.deltaTime;
                if (rise)
                    transform.position = origin + Vector2.up * (Mathf.Sin(Mathf.PI * Mathf.Clamp01(releaseAge / releaseDuration))
                        * KarmaSpace.Units(mastered ? 1.6f : 0.48f));
                if (releaseAge >= 0.22f)
                    animator.PresentLearningMotion(pose, KarmaBootstrap.Step * 2.01f);
            }
            if (!Active) yield break;
            if (rise) transform.position = origin;
            if (mastered) finalTechniqueReleased = true;
            fullShield = false;
            ClearPatternObjects();
            presentingCopy = running = false;
            state = ActionState.Approach;
            timer = behavior.DecisionDelay;
            if (mastered && finishingBlowPending) ReceiveDamage(Health);
        }

        void FixedUpdate()
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
            float dt = Time.fixedDeltaTime; FightSeconds += dt;
            if (Enraged && !announcedEnrage)
            {
                announcedEnrage = true;
                KarmaCombatVFX.SpawnSprite(effectsRoot, Position, "PersonalizationVFX", "Fusion",
                    KarmaSpace.Units(new Vector2(4, 4)), 0.7f, emitSparks: PrimaryType.HasValue);
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
            if (!presentingCopy) animator.GuardianPose(ActionStateNames[(int)state], selected, Position.y > KarmaSpace.Units(1.5f), Armored);
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
            if (TryDemonstrateFinalTechnique() || TryImitate()) return;
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
            var inherited = game.Player.Essences.PersonalizationOrder;
            if (inherited.Count == 3 && behavior.IsUltimate(selected))
            {
                SayLearning("이제 세 기술을 하나로. 이게 내 검이다.");
                KarmaGuardianGrowth.RecallMastery(game, this, animator);
            }
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
            // Short stuns can remove their stars before the one-shot finishes.
            if (!stunCueSoundStarted && !behavior.PlaysOwnStunSound)
                KarmaAudio.Play(KarmaSfxId.CombatBossStagger, lifetimeOwner: this);
            ShowStunVisual();
        }
        void ShowStunVisual()
        {
            stunCueSoundStarted = true;
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
            KarmaAudio.StopOwned(stunCue, 0.045f);
            Destroy(stunCue.gameObject);
            stunCue = null;
        }
        void InterruptPattern() { InterruptPatternCore(false); }
        void InterruptPatternCore(bool preservePressure)
        {
            StopAllCoroutines(); behavior?.Interrupt(preservePressure); ClearPatternObjects(); HideStunCue();
            behavior?.OnInterrupted();
            fullShield = running = pendingParry = presentingCopy = false;
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
