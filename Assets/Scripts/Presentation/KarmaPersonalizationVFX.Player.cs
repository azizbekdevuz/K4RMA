using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPersonalizationVFX
    {
        Essence comparisonEssence;
        KarmaGame comparisonGame;
        bool proofReleased;
        float comparisonFacing;
        Transform playerMemory;
        KarmaCharacterAnimator memoryAnimator, actualPlayerAnimator;
        SpriteRenderer memoryPicture, sourceEffect, proofEffect, rememberedStrike;

        void BeginPlayerComparison(KarmaGame game, Essence essence)
        {
            comparisonGame = game;
            comparisonEssence = essence; comparisonFacing = game.Player.Facing;
            actualPlayerAnimator = actor.GetComponentInChildren<KarmaCharacterAnimator>();
            playerMemory = new GameObject("Memory of sacrificed player technique").transform;
            playerMemory.SetParent(transform, false);
            var art = KarmaArtwork.Player(playerMemory);
            memoryAnimator = art.GetComponent<KarmaCharacterAnimator>();
            memoryPicture = art.GetComponentInChildren<SpriteRenderer>();
            memoryPicture.sortingOrder = 5;
            sourceEffect = Layer("Original skill memory", KarmaArtwork.Sprite("PlayerVFX",
                essence == Essence.SwordWave ? "Wave" : essence == Essence.RisingSlash ? "Rising" : "Barrier"), 4);
            proofEffect = Layer("Personalized technique demonstration", KarmaArtwork.Sprite(
                essence == Essence.Guard ? "PersonalizationVFX" : "PlayerVFX",
                essence == Essence.SwordWave ? "PierceTrail" : essence == Essence.RisingSlash ? "AirSlash" : "Counter"), 4);
            rememberedStrike = Layer("Remembered incoming strike", KarmaArtwork.Sprite("GuardianVFX", "Cleave"), 4);
        }

        void OnDisable() { if (actualPlayerAnimator != null) actualPlayerAnimator.EndPersonalizationProof(); }

        void DrawPlayerComparison()
        {
            if (memoryPicture == null) return;
            float t = Mathf.Floor(age * 24) / 24;
            float memoryAlpha = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.25f, t))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(MemoryDuration - 0.4f, MemoryDuration, t)));
            playerMemory.localPosition = new Vector3(comparisonFacing * 4.8f, -0.5f, 0);
            playerMemory.localScale = new Vector3(comparisonFacing, 1, 1);
            memoryAnimator.SampleGrowthSource(comparisonEssence, Mathf.Max(0, t - 0.1f));
            memoryPicture.color = new Color(0.75f, 0.88f, 1, memoryAlpha * MemoryOpacity);
            memoryPicture.enabled = memoryAlpha > 0;
            Vector2 source = new Vector2(comparisonFacing * 5.7f, 0.3f);
            if (comparisonEssence == Essence.SwordWave) source.x += comparisonFacing * Mathf.Clamp01(t) * 2;
            if (comparisonEssence == Essence.RisingSlash) source.y += Mathf.Clamp01(t) * 1.4f;
            Show(sourceEffect, source, 3, 0, Color.white, memoryAlpha * MemoryOpacity);
            sourceEffect.flipX = comparisonFacing < 0;
            proofEffect.color = rememberedStrike.color = Color.clear;
            float action = t - ProofStart;
            if (action < 0) return;
            float releaseAt = ProofRelease(comparisonEssence);
            if (!proofReleased && action >= releaseAt)
            {
                proofReleased = true;
                KarmaAudio.Play(comparisonEssence == Essence.Guard ? KarmaSfxId.CombatPerfectParry
                    : comparisonEssence == Essence.SwordWave ? KarmaSfxId.PlDash : KarmaSfxId.PlSwordSwing);
                comparisonGame.PresentationShake(0.14f, 0.45f);
            }
            float release = Mathf.InverseLerp(releaseAt, releaseAt + 0.47f, action);
            float opacity = ProofOpacity(release);
            Vector2 point = new Vector2(comparisonFacing * (1 + release * 3), 0.1f);
            float size = 3.8f, angle = 0;
            if (comparisonEssence == Essence.RisingSlash)
            {
                point = new Vector2(comparisonFacing * 2.4f, 1.8f);
                angle = -comparisonFacing * 35; size = 3.2f;
            }
            else if (comparisonEssence == Essence.Guard)
            {
                float approach = Mathf.InverseLerp(ProofAnticipation + 0.15f, releaseAt, action);
                Show(rememberedStrike, new Vector2(comparisonFacing * Mathf.Lerp(4.2f, 0.9f, approach), 0.4f),
                    2.2f, 0, new Color(0.8f, 0.88f, 1), action < releaseAt ? Mathf.Sin(approach * Mathf.PI * 0.5f) * 0.5f : 0);
                rememberedStrike.flipX = comparisonFacing > 0;
                release = Mathf.InverseLerp(releaseAt, releaseAt + 0.43f, action);
                opacity = ProofOpacity(release);
                point = new Vector2(comparisonFacing * (0.9f + release * 2.6f), 0.4f); size = 2.8f;
            }
            Show(proofEffect, point, size, angle, Color.white, opacity);
            proofEffect.flipX = comparisonFacing < 0;
        }

        static float ProofOpacity(float progress) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.12f, progress))
            * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 1, progress)));
    }
}
