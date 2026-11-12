using UnityEngine;

namespace KarmaPrototype
{
    // A bounded set of reused sprites; nothing here can hit an actor or advance progression.
    public sealed partial class KarmaEndingPresentation : MonoBehaviour
    {
        KarmaGame game;
        Transform fallen;
        KarmaCharacterAnimator fallenAnimator;
        SpriteRenderer[] fallenPictures;
        readonly SpriteRenderer[] symbols = new SpriteRenderer[3];
        readonly SpriteRenderer[] particles = new SpriteRenderer[48];
        readonly Vector3[] particleOrigins = new Vector3[48];
        SpriteRenderer master, aura;
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
            ending.master = KarmaTutorialStage.Picture(root.transform, "Master", Vector2.zero, new Vector2(4.39f, 4.8f), 10);
            ending.master.color = Color.clear;
            if (game.IsEndingReview) ending.PrepareAlternative();
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
            float t = Mathf.Floor(game.EndingTime * 24f) / 24f;
            if (game.IsEndingReview)
            {
                game.Player.PresentEndingAlternative(t);
                DrawGuardian(t); DrawAlternative(t); return;
            }
            game.Player.PresentEnding(t);
            DrawGuardian(t);
            var center = game.Player.Position + Vector2.up * 1.1f;
            float absorb = Ease(16, 19, t);
            for (int i = 0; i < symbols.Length; i++)
            {
                float appear = Ease(9.5f + i * 2, 10.4f + i * 2, t);
                float angle = (t - 9.5f) * 0.8f + i * Mathf.PI * 2 / 3;
                float radius = 3.3f * (1 - absorb);
                symbols[i].transform.position = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.72f);
                symbols[i].transform.localRotation = Quaternion.Euler(0, 0, i == 0 ? Mathf.Sin(angle) * 20 : 0);
                var color = i == 2 ? new Color(1, 0.85f, 0.2f) : game.Config.ColorOf((Essence)i);
                color.a = appear * (1 - absorb) * 0.85f; symbols[i].color = color;
            }
            float joined = Ease(16.2f, 17.7f, t) * (1 - Ease(19, 20.5f, t));
            foreach (var symbol in convergence)
            {
                symbol.transform.position = center;
                symbol.transform.localRotation = Quaternion.Euler(0, 0, t * 18);
                symbol.color = new Color(1, 0.92f, 0.64f, joined * 0.32f);
            }
            float halo = Ease(17.5f, 19.3f, t) * (1 - Ease(20, 21, t));
            float finalPulse = Ease(32.5f, 32.9f, t) * (1 - Ease(33.3f, 34.5f, t));
            aura.transform.position = center;
            aura.color = new Color(1, 0.91f, 0.57f, Mathf.Max(halo * 0.28f, finalPulse * 0.4f));
            float memory = Ease(20.5f, 21.5f, t) * (1 - Ease(28.5f, 30, t));
            master.transform.position = new Vector2(game.Player.Position.x - 5.2f, KarmaTutorialStage.MasterGroundCenter(4.8f));
            master.color = new Color(0.65f, 0.76f, 0.92f, memory * 0.38f);
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
