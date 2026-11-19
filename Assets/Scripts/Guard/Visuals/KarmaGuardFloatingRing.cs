using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Presentation only: preserves the original center and overlays an independent ring texture. Never changes sprite bounds.
    public sealed class KarmaGuardFloatingRing : MonoBehaviour
    {
        readonly Dictionary<Sprite, Vector4> shields = new Dictionary<Sprite, Vector4>();
        readonly Dictionary<Sprite, float> tilts = new Dictionary<Sprite, float>();
        MaterialPropertyBlock properties;
        KarmaGame game;
        SpriteRenderer picture, flying;
        Sprite rearWindup;
        Material original, flightOriginal, material;
        Func<bool> preparing, moving;
        Func<SpriteRenderer> flightSource;
        float angle, charge;
        public float Angle => angle;
        public float Charge => charge;
        public int MappedPoseCount => shields.Count;

        public void Initialize(KarmaGame director, SpriteRenderer body, Func<bool> tell, Func<bool> active,
            Func<SpriteRenderer> thrown)
        {
            game = director; picture = body; preparing = tell; moving = active; flightSource = thrown;
            properties = new MaterialPropertyBlock();
            original = picture.sharedMaterial;
            var shader = Resources.Load<Shader>("Guard/Effects/KarmaGuardFloatingRing");
            if (shader == null) throw new InvalidOperationException("검기 계승 방패 셰이더 누락");
            material = new Material(shader) { name = "WardWave floating shield (runtime)" };
            var rim = Resources.Load<Texture2D>("Guard/Effects/GuardFloatingWoodRing");
            if (rim == null) throw new InvalidOperationException("검기 계승 독립 목재 고리 누락");
            material.SetTexture("_RingTex", rim);
            // Pixel coordinates in the original, unchanged sheets, measured from the top-left.
            // Rear/occluded shields keep their existing rear view instead of painting a front face over the body.
            Add("Forms/WardWave", "Idle", 282, 173, 48, 69, -12);
            Add("Forms/WardWave", "Guard", 1192, 183, 57, 73, -12);
            Add("Forms/WardWave", "ShieldBash", 635, 450, 32, 76, -12);
            Add("Forms/WardWave", "Hurt", 915, 519, 30, 73, -10);
            Add("Forms/WardWave", "Crouch", 251, 815, 45, 66, -22);
            Add("Forms/WardWave", "Windup", 550, 726, 43, 66, -27);
            foreach (var pose in Resources.LoadAll<Sprite>("Guard/Forms/WardWave"))
                if (pose.name == "Windup") rearWindup = pose;
            Add("Forms/WardWave", "Recover", 900, 821, 27, 66, -8);
            Add("Forms/WardWave", "ReviveKneel", 583, 1074, 27, 64, -19);
            Add("Forms/WardWave", "ReviveRise", 883, 1046, 26, 70, -17);
            Add("Forms/WardWave", "ReviveStand", 1210, 1075, 43, 65, -12);
            foreach (string folder in new[] { "HeavyStrike", "MoltenStrike" })
            {
                Add(folder + "/WardWave", "Windup", 533, 511, 66, 92, -12);
                Add(folder + "/WardWave", "Downstroke", 1235, 503, 65, 90, -12);
                Add(folder + "/WardWave", "FollowThrough", 1953, 515, 63, 90, -12);
            }
            Add("Stagger/WardWave", "Stagger", 2010, 445, 57, 151, -14);
        }

        void Add(string path, string pose, float x, float y, float rx, float ry, float tilt)
        {
            foreach (var sprite in Resources.LoadAll<Sprite>("Guard/" + path))
                if (sprite.name == pose)
                {
                    shields[sprite] = new Vector4(x / sprite.texture.width, 1 - y / sprite.texture.height,
                        rx / sprite.texture.width, ry / sprite.texture.height);
                    tilts[sprite] = -tilt * Mathf.Deg2Rad;
                    return;
                }
            throw new InvalidOperationException("검기 계승 방패 자세 누락: " + path + "/" + pose);
        }

        void LateUpdate()
        {
            if (picture == null || material == null) return;
            bool tell = preparing();
            if (!game.Paused)
            {
                charge = tell ? Mathf.MoveTowards(charge, 1, Time.deltaTime * 3) : 0;
                if (moving()) angle = Mathf.Repeat(angle + Mathf.Lerp(14, 420, charge * charge) * Time.deltaTime, 360);
            }
            Apply(picture, false);
            SpriteRenderer current = flightSource();
            if (current != flying)
            {
                RestoreFlight();
                flying = current;
                if (flying != null) flightOriginal = flying.sharedMaterial;
            }
            if (flying != null && flying.gameObject.activeInHierarchy) Apply(flying, true);
        }

        void Apply(SpriteRenderer target, bool projectile)
        {
            var sprite = target.sprite;
            if (sprite == null) return;
            Vector4 ellipse;
            float tilt;
            if (projectile)
            {
                // WholeHot keeps its original heat, gold rings, pivot and flight transform.
                ellipse = new Vector4(650f / 1254, 1 - 625f / 1254, 520f / 1254, 535f / 1254);
                tilt = -20 * Mathf.Deg2Rad;
            }
            else if (!shields.TryGetValue(sprite, out ellipse))
            {
                target.sharedMaterial = original;
                return;
            }
            else tilt = tilts[sprite];
            target.sharedMaterial = material;
            target.GetPropertyBlock(properties);
            properties.SetVector("_Shield", ellipse);
            properties.SetVector("_Ring", new Vector4(angle * Mathf.Deg2Rad, projectile ? 0 : charge, tilt, projectile ? 2 : 1));
            // Pixel aspect compensates for wide heavy/stagger texture sheets.
            properties.SetFloat("_TextureAspect", (float)sprite.texture.width / sprite.texture.height);
            properties.SetFloat("_Perspective", projectile || sprite == rearWindup ? 0 : 0.28f);
            properties.SetFloat("_RearArc", sprite == rearWindup ? 1 : 0);
            target.SetPropertyBlock(properties);
        }

        void RestoreFlight()
        {
            if (flying != null) flying.sharedMaterial = flightOriginal;
            flying = null; flightOriginal = null;
        }
        void OnDisable()
        {
            charge = 0;
            if (picture != null) picture.sharedMaterial = original;
            RestoreFlight();
        }
        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
