using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        Sprite[] swordSkillFrames;
        int swordLinkLowFrame = 10;
        float basicLinkAt = float.NegativeInfinity, basicLinkSeconds;
        internal float SwordGroundProgress { get; private set; }
        internal float BasicLinkAge => Mathf.Max(0, Time.time - basicLinkAt);
        internal float BasicLinkDuration => basicLinkSeconds;
        internal bool BasicLinkActive => BasicBodyActive && BasicStrikeIndex == 0 && !float.IsNegativeInfinity(basicLinkAt);

        void ConfigureSwordSkillFrames()
        {
            if (swordSkillFrames != null || !poses.TryGetValue("Idle", out var idle)) return;
            var loaded = Resources.LoadAll<Sprite>("SwordWave/Forms/" + idle.texture.name + "_Skills");
            string[] names = { "FollowLow", "ReturnLoad", "GroundRaise", "GroundDrop", "GroundStrike", "GroundRecoil", "ShieldBrace", "SwordCommand" };
            var frames = new Sprite[names.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                foreach (var sprite in loaded) if (sprite.name == names[i]) frames[i] = sprite;
                if (frames[i] == null) throw new System.InvalidOperationException("Missing sword skill pose: " + idle.texture.name + "/" + names[i]);
            }
            swordSkillFrames = frames;
            if (idle.texture.name == "WaveRise" || idle.texture.name == "WaveWard") swordLinkLowFrame = 11;
        }
        internal void BeginBasicLink(float seconds)
        {
            if (!BasicBodyActive || BasicStrikeIndex != 0) return;
            basicLinkAt = Time.time; basicLinkSeconds = Mathf.Max(1f / 24, seconds);
        }
        internal void BeginSwordGroundStrike(float warning)
        {
            PrepareSwordBody("Summon", warning);
            SwordGroundProgress = 0;
        }
        internal void SampleSwordGroundStrike(float progress)
        {
            if (SwordBodyAction == "Summon" && !SwordBodyReleased)
                SwordGroundProgress = Mathf.Clamp01(progress);
        }
        internal void ReleaseSwordGroundStrike()
        {
            if (sheet != "Wave" || deathAt >= 0 || Time.time < hurtUntil) return;
            SwordGroundProgress = 1;
            ReleaseSwordBody("Summon");
            ApplyBasicBodyFrame(14);
        }
        int GroundStrikeFrame()
        {
            if (SwordBodyReleased) return SwordReleaseAge < 2f / 24 ? 14 : SwordReleaseAge < 5f / 24 ? 15 : 0;
            float remaining = SwordFrameTime(SwordBodyDuration * (1 - SwordGroundProgress));
            if (remaining <= 2f / 24) return 13;
            return SwordGroundProgress < 0.20f ? 0 : 12;
        }
        void ApplySwordSkillFrame(int frame)
        {
            if (swordSkillFrames == null) { ApplySwordBodyFrame(0); return; }
            picture.sprite = swordSkillFrames[frame];
            RegisterSwordBodySprite();
        }
        // One body scale per sheet, measured crown to planted sole in its
        // upright key. Crouching is articulation, never per-frame rescaling.
        static readonly float[] swordSkillHeights = { 390, 381, 384, 348 };
        static readonly Vector3[,] swordSkillWood = {
            {new Vector3(188,145,116),new Vector3(183,140,112),new Vector3(184,141,112),new Vector3(186,143,115),new Vector3(189,145,116),new Vector3(188,143,115),new Vector3(190,147,117),new Vector3(189,145,116)},
            {new Vector3(191,148,115),new Vector3(187,143,112),new Vector3(189,145,114),new Vector3(190,146,114),new Vector3(194,150,117),new Vector3(191,146,115),new Vector3(195,150,119),new Vector3(192,148,117)},
            {new Vector3(192,144,108),new Vector3(184,138,104),new Vector3(186,139,105),new Vector3(189,142,107),new Vector3(182,136,102),new Vector3(187,140,106),new Vector3(189,142,106),new Vector3(189,141,108)},
            {new Vector3(190,143,108),new Vector3(188,140,106),new Vector3(190,143,108),new Vector3(186,139,105),new Vector3(197,149,112),new Vector3(192,144,109),new Vector3(189,142,108),new Vector3(189,142,108)}
        };
        bool RegisterSwordSkillAppearance(Sprite idle, int form)
        {
            int frame = swordSkillFrames == null ? -1 : System.Array.IndexOf(swordSkillFrames, picture.sprite);
            if (frame < 0 || form < 0 || swordBodyMaterial == null) return false;
            float scale = (swordIdleHeights[form] / idle.pixelsPerUnit)
                / (swordSkillHeights[form] / picture.sprite.pixelsPerUnit);
            picture.transform.localScale = pictureScale * scale;
            Vector3 sample = swordSkillWood[form, frame], reference = swordIdleWood[form];
            swordBodyProperties.SetVector(SwordWoodGain, new Vector4(reference.x / sample.x, reference.y / sample.y, reference.z / sample.z, 1));
            picture.sharedMaterial = swordBodyMaterial; picture.SetPropertyBlock(swordBodyProperties);
            return true;
        }
    }
}
