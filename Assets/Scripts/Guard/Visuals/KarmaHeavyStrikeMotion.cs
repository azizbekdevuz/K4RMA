using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Each inherited body has its own overhead poses, with one shared GD02 timing.
    sealed class KarmaHeavyStrikeMotion
    {
        readonly SpriteRenderer picture;
        readonly Vector3 home, scale, damageScale;
        readonly Sprite[] poses = new Sprite[3];
        readonly Sprite[] heatedPoses = new Sprite[3];
        readonly Sprite[] shieldlessPoses = new Sprite[3], shieldlessHeatedPoses = new Sprite[3];
        public bool ShieldAway { get; set; }
        public Sprite DamageSprite { get; private set; }
        public Vector3 DamageScale { get { return damageScale; } }
        bool active, swinging, heated;
        float swingAt;
        public string FormKey { get; private set; }
        public string CurrentPose { get; private set; }

        public KarmaHeavyStrikeMotion(SpriteRenderer renderer, Vector3 position, Vector3 originalScale, Sprite standing,
            IReadOnlyList<Essence> order)
        {
            picture = renderer; home = position;
            string key = order.Count >= 3 ? "WardFinal" : order.Count == 2
                ? order[1] == Essence.SwordWave ? "WardWave" : "WardRise" : "Ward";
            FormKey = key;
            string[] names = { "Windup", "Downstroke", "FollowThrough" };
            foreach (var sprite in KarmaArtwork.Sprites("GuardHeavy" + key))
                for (int i = 0; i < names.Length; i++) if (sprite.name == names[i]) poses[i] = sprite;
            foreach (var sprite in KarmaArtwork.Sprites("GuardHeavyMolten" + key))
                for (int i = 0; i < names.Length; i++) if (sprite.name == names[i]) heatedPoses[i] = sprite;
            for (int i = 0; i < names.Length; i++)
                if (poses[i] == null || heatedPoses[i] == null)
                    throw new System.InvalidOperationException("GD02 공격 자세가 없습니다: " + key + "/" + names[i]);
            if (key == "WardFinal")
                for (int i = 0; i < names.Length; i++)
                {
                    shieldlessPoses[i] = KarmaArtwork.Sprite("GuardTrialHeavyWardFinal", names[i]);
                    shieldlessHeatedPoses[i] = KarmaArtwork.Sprite("GuardTrialHeavyMoltenWardFinal", names[i]);
                    if (shieldlessPoses[i] == null || shieldlessHeatedPoses[i] == null)
                        throw new System.InvalidOperationException("GD05 빈손 강공격 자세 누락: " + names[i]);
                }
            // Preserve the original damage bounds independently of visual artwork calibration.
            float bodyFraction = key == "Ward" ? 0.76f : key == "WardRise" ? 0.64f : key == "WardWave" ? 0.60f : 0.65f;
            damageScale = originalScale * (standing.bounds.size.y / (poses[0].bounds.size.y * bodyFraction));
            // Heavy sheets use 1.35 source pixels per idle pixel, including molten and shield-away poses.
            // Weapon reach and transparent padding must not change the body's pixel density.
            scale = originalScale * (poses[0].pixelsPerUnit / (standing.pixelsPerUnit * 1.35f));
        }
        public void Prepare() { active = true; swinging = heated = false; Apply(0); }
        public void Heat() { heated = true; if (active && !swinging) Apply(0); }
        public void Swing() { swinging = true; swingAt = Time.time; Apply(1); }
        public void Stop() { active = swinging = heated = false; }
        public bool Show(EnemyAttack attack)
        {
            if (!active || (attack != EnemyAttack.GD02 && attack != EnemyAttack.GD05)) return false;
            int frame = !swinging ? 0 : Time.time - swingAt < 2f / 24f ? 1 : 2;
            Apply(frame);
            return true;
        }
        void Apply(int index)
        {
            DamageSprite = heated ? heatedPoses[index] : poses[index];
            picture.sprite = ShieldAway ? (heated ? shieldlessHeatedPoses[index] : shieldlessPoses[index]) : DamageSprite;
            picture.transform.localPosition = home;
            picture.transform.localScale = scale;
            picture.transform.localRotation = Quaternion.identity;
            CurrentPose = index == 0 ? "GD02/Windup" : index == 1 ? "GD02/Downstroke" : "GD02/FollowThrough";
        }
    }
}
