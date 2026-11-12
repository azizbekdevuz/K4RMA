using UnityEngine;

namespace KarmaPrototype
{
    // A bounded set of reused sprites; nothing here can hit an actor or advance progression.
    public sealed class KarmaEndingPresentation : MonoBehaviour
    {
        KarmaGame game;
        Transform fallen;
        KarmaCharacterAnimator fallenAnimator;
        SpriteRenderer[] fallenPictures;
        readonly SpriteRenderer[] symbols = new SpriteRenderer[3];
        readonly SpriteRenderer[] particles = new SpriteRenderer[48];
        readonly Vector3[] particleOrigins = new Vector3[48];
        SpriteRenderer master, aura, finishedCut;
        readonly SpriteRenderer[] techniques = new SpriteRenderer[3];
        readonly int[] memoryOrder = { 0, 1, 2 };
        readonly SpriteRenderer[] convergence = new SpriteRenderer[3];
        MaterialPropertyBlock dissolveBlock;
        bool seeded, guardianFinished;

        public static KarmaEndingPresentation Create(KarmaGame game, Transform world, Transform fallen, Vector3 origin)
        {
            var root = new GameObject("Ending presentation"); root.transform.SetParent(world, false);
            var ending = root.AddComponent<KarmaEndingPresentation>();
            ending.game = game; ending.fallen = fallen;
            if (fallen != null)
            {
                fallen.position = origin;
                ending.fallenAnimator = fallen.GetComponent<KarmaCharacterAnimator>();
                ending.fallenPictures = fallen.GetComponentsInChildren<SpriteRenderer>();
                ending.fallenAnimator?.PresentEndingDefeat(0);
                var material = Resources.Load<Material>("Shared/Rendering/KarmaEndingDissolve");
                if (material != null) foreach (var picture in ending.fallenPictures) picture.sharedMaterial = material;
                ending.dissolveBlock = new MaterialPropertyBlock();
            }
            for (int i = 0; i < 3; i++)
            {
                ending.symbols[i] = ending.Layer("Completed technique " + (Essence)i,
                    KarmaArtwork.SkillIcon((Essence)i),
                    i == 2 ? 2.6f : i == 1 ? 2f : 1.8f, 18);
            }
            for (int i = 0; i < ending.particles.Length; i++)
                ending.particles[i] = ending.Layer("Guardian light " + i,
                    KarmaArtwork.Sprite("PersonalizationVFX", "Core"), 0.16f + i % 3 * 0.04f, 19);
            ending.aura = ending.Layer("Completed aura", KarmaArtwork.Sprite("PlayerVFX", "Barrier"), 5.5f, 4);
            var united = new GameObject("Three techniques united").transform; united.SetParent(root.transform, false);
            for (int i = 0; i < ending.convergence.Length; i++)
            {
                ending.convergence[i] = ending.Layer("United " + (Essence)i, KarmaArtwork.SkillIcon((Essence)i), 3.5f, 17);
                ending.convergence[i].transform.SetParent(united, false);
            }
            ending.techniques[0] = ending.Layer("Guard demonstration", KarmaArtwork.Sprite("PlayerVFX", "Barrier"), 4.4f, 12);
            ending.techniques[1] = ending.Layer("Rising demonstration", KarmaArtwork.Sprite("PlayerVFX", "Rising"), 5.4f, 13);
            ending.techniques[2] = ending.Layer("Wave demonstration", KarmaArtwork.Sprite("PlayerVFX", "Wave"), 5.8f, 14);
            ending.finishedCut = ending.Layer("Completed technique release", KarmaArtwork.Sprite("PlayerVFX", "Wave"), 8.2f, 15);
            var acquired = game.Player.Essences.PersonalizationOrder;
            for (int i = 0; i < acquired.Count; i++) ending.memoryOrder[(int)acquired[i]] = i;
            ending.master = KarmaTutorialStage.Picture(root.transform, "Master",
                new Vector2(KarmaGame.EndingMasterX, KarmaTutorialStage.MasterGroundCenter(4.8f)), new Vector2(4.39f, 4.8f), 10);
            ending.master.color = Color.clear;
            return ending;
        }
        SpriteRenderer Layer(string name, Sprite sprite, float size, int order)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = order; KarmaVisuals.ApplyMaterial(renderer);
            if (sprite != null) child.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            renderer.color = Color.clear;
            return renderer;
        }
        static float Ease(float start, float end, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, t));
        void LateUpdate()
        {
            if (game == null || game.Player == null || game.Phase != RunPhase.Ending) return;
            float t = Mathf.Floor(game.EndingTime * 24f + 0.001f) / 24f;
            game.Player.PresentEnding(t);
            DrawGuardian(t);

            var center = game.Player.Position + Vector2.up * 1.05f;
            float absorb = Ease(KarmaGame.EndingWaveAt + 0.32f, KarmaGame.EndingUnionAt, t);
            for (int i = 0; i < symbols.Length; i++)
            {
                int order = memoryOrder[i];
                float reveal = KarmaGame.EndingMasterAt + 0.65f + order * 1.6f;
                Vector2 memory = center + new Vector2((order - 1) * 3.1f, order == 1 ? 3.6f : 3.15f);
                symbols[i].transform.position = Vector2.Lerp(memory, center + Vector2.right * 0.5f, absorb);
                var color = game.Config.ColorOf((Essence)i);
                color.a = Ease(reveal, reveal + 0.6f, t) * (1 - absorb) * 0.9f;
                symbols[i].color = color;
            }

            float guardAge = t - (KarmaGame.EndingGuardAt + KarmaGame.EndingRelease);
            float liftAge = t - (KarmaGame.EndingLiftAt + KarmaGame.EndingRelease);
            float waveAge = t - (KarmaGame.EndingWaveAt + KarmaGame.EndingRelease);
            ShowTechnique(techniques[0], center + Vector2.right * 1.25f, game.Config.ColorOf(Essence.Guard), guardAge, 0.58f);
            ShowTechnique(techniques[1], center + new Vector2(1.5f, 0.2f + 2.4f * Mathf.Clamp01(liftAge / 0.5f)),
                game.Config.ColorOf(Essence.RisingSlash), liftAge, 0.56f);
            ShowTechnique(techniques[2], center + Vector2.right * (1.9f + 5.5f * Mathf.Clamp01(waveAge / 0.7f)),
                game.Config.ColorOf(Essence.SwordWave), waveAge, 0.72f);

            // Let each inherited color remain legible before the united release.
            float unionAge = t - KarmaGame.EndingUnionAt;
            float joined = Ease(-0.55f, 0.16f, unionAge) * (1 - Ease(0.25f, 1.28f, unionAge));
            for (int i = 0; i < convergence.Length; i++)
            {
                float spread = 1 - Ease(-0.55f, 0.12f, unionAge);
                convergence[i].transform.position = center + new Vector2(0.5f + (i - 1) * spread * 1.6f, 0.4f + spread * (2 - i * 0.45f));
                var color = Color.Lerp(game.Config.ColorOf((Essence)i), new Color(1, 0.92f, 0.64f), 1 - spread);
                color.a = joined * 0.37f;
                convergence[i].color = color;
            }
            ShowTechnique(finishedCut, center + Vector2.right * (1.4f + 8.2f * Mathf.Clamp01(unionAge / 1.25f)),
                new Color(1, 0.91f, 0.63f), unionAge, 1.25f);
            float halo = Ease(-0.3f, 0.55f, unionAge) * (1 - Ease(0.8f, 2.4f, unionAge));
            aura.transform.position = center;
            aura.color = new Color(1, 0.91f, 0.57f, halo * 0.42f);

            float memoryPresence = Ease(KarmaGame.EndingMasterAt, 11.8f, t) * (1 - Ease(34, 37.2f, t));
            master.color = new Color(0.83f, 0.89f, 1, memoryPresence * 0.84f);
        }

        static void ShowTechnique(SpriteRenderer renderer, Vector2 position, Color color, float age, float duration)
        {
            renderer.transform.position = position;
            color.a = Ease(0, 0.08f, age) * (1 - Ease(duration * 0.42f, duration, age)) * 0.9f;
            renderer.color = color;
        }
        void DrawGuardian(float t)
        {
            if (t >= 6)
            {
                if (!guardianFinished)
                {
                    guardianFinished = true;
                    foreach (var particle in particles) particle.gameObject.SetActive(false);
                    if (fallen != null) { fallen.gameObject.SetActive(false); Destroy(fallen.gameObject); fallen = null; }
                }
                return;
            }
            if (fallen != null && t < 6)
            {
                fallenAnimator?.PresentEndingDefeat(t);
                if (!seeded && t >= 1)
                {
                    var bounds = fallenPictures != null && fallenPictures.Length > 0 ? fallenPictures[0].bounds
                        : new Bounds(fallen.position, new Vector3(5, 4, 1));
                    for (int i = 0; i < particles.Length; i++)
                    {
                        float x = Mathf.Repeat(i * 0.618034f, 1);
                        float y = Mathf.Repeat(i * 0.414214f, 1);
                        particleOrigins[i] = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x),
                            Mathf.Lerp(bounds.min.y, bounds.max.y, y), 0);
                    }
                    seeded = true;
                }
                if (fallenPictures != null)
                    foreach (var picture in fallenPictures)
                    {
                        picture.GetPropertyBlock(dissolveBlock);
                        dissolveBlock.SetFloat("_Progress", Ease(1.1f, 5.2f, t));
                        picture.SetPropertyBlock(dissolveBlock);
                    }
            }
            if (fallen != null && t >= 5.5f) { fallen.gameObject.SetActive(false); Destroy(fallen.gameObject); fallen = null; }
            for (int i = 0; i < particles.Length; i++)
            {
                float age = t - 1.2f - i * 0.055f;
                float visible = age >= 0 && age < 1.65f ? Mathf.Sin(Mathf.Clamp01(age / 1.65f) * Mathf.PI) : 0;
                particles[i].transform.position = particleOrigins[i]
                    + new Vector3(Mathf.Sin(i * 2.4f) * age * 0.65f, age * (1.4f + i % 4 * 0.25f), 0);
                particles[i].color = new Color(1, 0.91f, 0.63f, visible * 0.8f);
            }
        }
    }
}
