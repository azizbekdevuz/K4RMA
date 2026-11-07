using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Each inherited body has its own overhead poses, with one shared GD02 timing.
    sealed class KarmaHeavyStrikeMotion
    {
        readonly SpriteRenderer picture;
        readonly Vector3 home, scale;
        readonly Sprite[] poses = new Sprite[3];
        readonly Sprite[] heatedPoses = new Sprite[3];
        bool active, swinging, heated;
        float swingAt;
        public string FormKey { get; private set; }
        public string CurrentPose { get; private set; }

        public KarmaHeavyStrikeMotion(SpriteRenderer renderer, Vector3 position, Vector3 originalScale,
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
            {
                if (poses[i] == null)
                    throw new System.InvalidOperationException("GD02 공격 자세가 없습니다: " + key + "/" + names[i]);
                if (heatedPoses[i] == null) heatedPoses[i] = poses[i];
            }
            float bodyFraction = key == "Ward" ? 0.65f : key == "WardRise" ? 0.54f : 0.56f;
            scale = originalScale * (picture.sprite.bounds.size.y / (poses[0].bounds.size.y * bodyFraction));
        }
        public void Prepare() { active = true; swinging = heated = false; Apply(0); }
        public void Heat() { heated = true; if (active && !swinging) Apply(0); }
        public void Swing() { swinging = true; swingAt = Time.time; Apply(1); }
        public void Stop() { active = swinging = heated = false; }
        public bool Show(EnemyAttack attack)
        {
            if (!active || (attack != EnemyAttack.GD02 && attack != EnemyAttack.GD05)) return false;
            int frame = !swinging ? 0 : Time.time - swingAt < 0.07f ? 1 : 2;
            Apply(frame);
            return true;
        }
        void Apply(int index)
        {
            picture.sprite = heated ? heatedPoses[index] : poses[index];
            picture.transform.localPosition = home;
            picture.transform.localScale = scale;
            picture.transform.localRotation = Quaternion.identity;
            CurrentPose = "GD02/" + poses[index].name;
        }
    }
}
