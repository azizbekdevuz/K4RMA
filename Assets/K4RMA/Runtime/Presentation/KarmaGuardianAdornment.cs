using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    // Reference-inspired inherited energy. Cosmetic only; shield timing belongs to the enemy.
    public sealed class KarmaGuardianAdornment : MonoBehaviour
    {
        readonly List<SpriteRenderer> blades = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> rings = new List<SpriteRenderer>();
        public void Initialize(IReadOnlyList<Essence> order)
        {
            var owned = new HashSet<Essence>(order);
            if (owned.Contains(Essence.Flame))
            {
                for (int i = 0; i < 2; i++)
                {
                    var sr = KarmaRemnantVFX.CreateBladeAura(transform, KarmaArtwork.BossSwordColor);
                    sr.sortingOrder = 7;
                    sr.transform.localPosition = new Vector3(i == 0 ? -0.15f : 0.15f, i == 0 ? 0.3f : -0.2f, 0);
                    sr.transform.localScale = new Vector3(2.4f, 0.7f, 1); blades.Add(sr);
                }
            }
            if (owned.Contains(Essence.Ward))
            {
                for (int i = 0; i < 2; i++)
                {
                    var color = KarmaArtwork.BarrierColor; color.a = 0.32f;
                    var sr = KarmaRemnantVFX.CreateWardHalo(transform, color);
                    sr.sortingOrder = 4;
                    sr.transform.localPosition = new Vector3(i == 0 ? 0.55f : -0.35f, 0.05f, 0);
                    sr.transform.localScale = new Vector3(i == 0 ? 1.25f : 0.8f, i == 0 ? 2.25f : 1.5f, 1);
                    rings.Add(sr);
                }
            }
            if (owned.Contains(Essence.Dash) && KarmaPatternCatalog.Primary(order) != Essence.Dash)
            {
                var sr = KarmaRemnantVFX.CreateBladeAura(transform, new Color(0.74f, 0.91f, 0.85f, 0.45f));
                sr.sortingOrder = 4; sr.transform.localPosition = new Vector3(-0.25f, 0.35f, 0);
                sr.transform.localScale = new Vector3(1.2f, 2.6f, 1); blades.Add(sr);
            }
        }
        void Update()
        {
            for (int i = 0; i < blades.Count; i++)
            {
                var sr = blades[i];
                sr.transform.localRotation = Quaternion.Euler(0, 0, i == 2 ? 65 : Mathf.Sin(Time.time * 1.7f + i * 2) * 14);
                var c = sr.color; c.a = 0.38f + Mathf.Sin(Time.time * 2 + i) * 0.08f; sr.color = c;
            }
            foreach (var sr in rings)
            {
                var c = sr.color; c.a = 0.26f + Mathf.Sin(Time.time * 2.2f) * 0.06f; sr.color = c;
            }
        }
    }
}
