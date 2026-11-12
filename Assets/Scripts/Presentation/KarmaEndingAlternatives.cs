using UnityEngine;

namespace KarmaPrototype
{
    // The three alternatives share existing actors/art, not a common recoloured timeline.
    public sealed partial class KarmaEndingPresentation
    {
        SpriteRenderer[] memoryGuardians, environment;
        Color[] environmentColors;
        SpriteRenderer demonstration;
        Material memoryMaterial;

        void PrepareAlternative()
        {
            dissolveBlock ??= new MaterialPropertyBlock();
            // Capture only scenery, never actors, floor physics, or a global rendering setting.
            var scenery = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var renderer in game.World.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.sortingOrder <= -20) scenery.Add(renderer);
            environment = scenery.ToArray(); environmentColors = new Color[environment.Length];
            for (int i = 0; i < environment.Length; i++) environmentColors[i] = environment[i].color;
            if (game.EndingVersion == KarmaEndingVersion.Legacy)
                demonstration = Layer("Harmless technique demonstration", KarmaArtwork.Sprite("PlayerVFX", "Wave"), 5, 20);
            if (game.EndingVersion != KarmaEndingVersion.Cycle) return;
            memoryMaterial = Resources.Load<Material>("Shared/Rendering/KarmaEndingDissolve");
            var references = Resources.LoadAll<Sprite>("Shared/ReferencePoses");
            var names = new[] { "Neutral", "Wave", "Rise", "Ward" };
            memoryGuardians = new SpriteRenderer[4];
            for (int i = 0; i < memoryGuardians.Length; i++)
            {
                var sprite = System.Array.Find(references, s => s.name == names[i]);
                memoryGuardians[i] = Layer("Memory of " + names[i], sprite, 5.5f, 0);
                if (memoryMaterial != null) memoryGuardians[i].sharedMaterial = memoryMaterial;
            }
        }

        void DrawAlternative(float t)
        {
            Vector2 center = game.Player.Position + Vector2.up * 1.2f;
            if (game.EndingVersion == KarmaEndingVersion.Legacy) DrawLegacy(t, center);
            else if (game.EndingVersion == KarmaEndingVersion.Cycle) DrawCycle(t, center);
            else DrawLesson(t);
        }
        static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
        void TintScenery(Color tint)
        {
            for (int i = 0; i < environment.Length; i++)
                if (environment[i] != null) environment[i].color = environmentColors[i] * tint;
        }
        void DrawLegacy(float t, Vector2 center)
        {
            TintScenery(Color.Lerp(Color.white, new Color(1, .88f, .72f), Ease(12, 17, t) * .5f));
            float gather = Ease(13, 15.5f, t);
            for (int i = 0; i < symbols.Length; i++)
            {
                float start = i == 0 ? 6 : i == 1 ? 8.7f : 11.2f;
                Vector2 point = center + new Vector2((i - 1) * 4, 3.4f - Mathf.Abs(i - 1) * .6f);
                symbols[i].transform.position = Vector2.Lerp(point, center, gather);
                symbols[i].color = WithAlpha(game.Config.ColorOf((Essence)i), Ease(start, start + .35f, t) * (1 - gather) * .85f);
            }
            float age; string key;
            if (t < 8.7f) { age = t - 6; key = "Wave"; }
            else if (t < 11.2f) { age = t - 8.7f; key = "Rising"; }
            else { age = t - 11.2f; key = "Barrier"; }
            demonstration.sprite = KarmaArtwork.Sprite("PlayerVFX", key);
            bool show = age >= 0 && age < .8f;
            float progress = Mathf.Clamp01(age / .8f);
            demonstration.transform.position = center + (key == "Wave" ? Vector2.right * (1.3f + 6 * progress)
                : key == "Rising" ? new Vector2(1, 2 * progress) : Vector2.zero);
            demonstration.color = new Color(1, .96f, .83f, show ? Mathf.Sin(progress * Mathf.PI) * .8f : 0);
            if (demonstration.sprite != null)
                demonstration.transform.localScale = Vector3.one * (5 / Mathf.Max(demonstration.sprite.bounds.size.x, demonstration.sprite.bounds.size.y));
            aura.transform.position = center;
            aura.color = new Color(1, .86f, .5f, Ease(15, 15.7f, t) * (1 - Ease(16.4f, 17.5f, t)) * .45f);
            master.transform.position = new Vector2(14, KarmaTutorialStage.MasterGroundCenter(4.8f));
            master.color = new Color(1, .94f, .85f, Ease(16, 17, t));
        }
        void DrawCycle(float t, Vector2 center)
        {
            float release = Ease(13.5f, 17.5f, t);
            TintScenery(Color.Lerp(new Color(.64f, .64f, .75f), new Color(.92f, 1, 1), Ease(13.5f, 22, t)));
            for (int i = 0; i < memoryGuardians.Length; i++)
            {
                var image = memoryGuardians[i];
                image.transform.position = new Vector2(7 + i * 8.5f, 4.4f);
                Color tint = i == 0 ? new Color(.8f, .83f, .9f) : game.Config.ColorOf((Essence)(i - 1));
                image.color = WithAlpha(tint, Ease(3.5f + i * .3f, 5 + i * .3f, t) * .32f * (1 - release));
                if (memoryMaterial != null)
                {
                    image.GetPropertyBlock(dissolveBlock);
                    dissolveBlock.SetFloat("_Progress", release); image.SetPropertyBlock(dissolveBlock);
                }
            }
            for (int i = 0; i < symbols.Length; i++)
            {
                symbols[i].transform.position = new Vector2(12 + i * 8, 1.8f + release * 6);
                symbols[i].color = WithAlpha(game.Config.ColorOf((Essence)i), Ease(5, 6.5f, t) * (1 - release) * .75f);
            }
            // Reuse authored light cores as the seals disperse; no new placeholder texture.
            if (t >= 13.5f)
                for (int i = 0; i < particles.Length; i++)
                {
                    particles[i].gameObject.SetActive(true);
                    float age = t - 13.5f - (i % 12) * .09f;
                    float a = Mathf.Clamp01(age / 3);
                    particles[i].transform.position = new Vector2(12 + (i % 3) * 8 + Mathf.Sin(i * 2.1f) * a * 4, 1.8f + a * (4 + i % 5));
                    particles[i].color = WithAlpha(game.Config.ColorOf((Essence)(i % 3)), age > 0 && age < 3 ? Mathf.Sin(a * Mathf.PI) * .5f : 0);
                }
        }
        void DrawLesson(float t)
        {
            master.transform.position = new Vector2(15, KarmaTutorialStage.MasterGroundCenter(4.8f));
            master.color = new Color(1, 1, 1, Ease(1.5f, 2.5f, t));
            // The joke is the student's serious guard, then a quiet return to the master.
            // No additional enemy, hitbox, or damage event is created.
            aura.transform.position = game.Player.Position + Vector2.up;
            aura.color = new Color(1, .93f, .65f, Ease(14.1f, 14.25f, t) * (1 - Ease(14.3f, 14.7f, t)) * .25f);
        }
        void OnDestroy()
        {
            if (environment == null) return;
            for (int i = 0; i < environment.Length; i++)
                if (environment[i] != null) environment[i].color = environmentColors[i];
        }
    }
}
