using UnityEngine;
namespace KarmaPrototype
{
    public sealed class KarmaCharacterAnimator : MonoBehaviour
    {
        Transform blade, leftBoot, rightBoot;
        SpriteRenderer picture;
        Sprite[] frames;
        Vector2 slashDirection = Vector2.right;
        Vector3 pictureHome;
        Color pictureColor;
        float actionUntil, hurtUntil, deathAt = -1;
        public void Initialize()
        {
            blade = transform.Find("Katana"); leftBoot = transform.Find("Boot L"); rightBoot = transform.Find("Boot R");
        }
        public void ConfigureSprites(SpriteRenderer renderer, Sprite[] poses)
        {
            picture = renderer; frames = poses; picture.sprite = frames[0];
            pictureHome = picture.transform.localPosition; pictureColor = picture.color;
        }
        public void Attack() { Attack(Vector2.right); }
        public void Attack(Vector2 direction) { slashDirection = direction; actionUntil = Time.time + 0.18f; }
        public void Hurt() { hurtUntil = Time.time + 0.16f; }
        public void Die() { deathAt = Time.time; }
        public void Pose(float movement, bool airborne)
        {
            if (deathAt >= 0)
            {
                if (picture != null && frames.Length >= 9) picture.sprite = frames[8];
                else transform.localRotation = Quaternion.Euler(0,0,Mathf.Min(85,(Time.time-deathAt)*200));
                return;
            }
            if (picture != null)
            {
                int frame = 0;
                if (frames.Length > 1)
                {
                    if (Time.time < hurtUntil && frames.Length >= 9) frame = 7;
                    else if (Time.time < actionUntil)
                        frame = slashDirection.y < -0.25f ? 6 : slashDirection.y > 0.25f ? 5 : actionUntil - Time.time > 0.09f ? 3 : 4;
                    else if (airborne) frame = 1;
                    else if (Mathf.Abs(movement) > 0.1f) frame = (int)(Time.time * 9) % 2 == 0 ? 1 : 2;
                }
                picture.sprite = frames[frame];
                float bob = airborne ? 0 : Mathf.Abs(movement) > 0.1f ? Mathf.Sin(Time.time * 18) * 0.025f : Mathf.Sin(Time.time * 2) * 0.008f;
                picture.transform.localPosition = pictureHome + Vector3.up * bob;
                picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
                transform.localRotation = Quaternion.Euler(0, 0, Time.time < hurtUntil ? -8 : 0);
                return;
            }
            float step = airborne ? 12 : Mathf.Abs(movement) > 0.1f ? Mathf.Sin(Time.time * 13) * 18 : Mathf.Sin(Time.time * 2) * 2;
            if (leftBoot != null) leftBoot.localRotation = Quaternion.Euler(0,0,step);
            if (rightBoot != null) rightBoot.localRotation = Quaternion.Euler(0,0,-step);
            if (blade != null) blade.localRotation = Quaternion.Euler(0,0,Time.time < actionUntil ? -65 + (actionUntil-Time.time)*650 : airborne ? 30 : 15);
            transform.localRotation = Quaternion.Euler(0,0,Time.time < hurtUntil ? -12 : 0);
        }
        void Update() { if (deathAt >= 0) Pose(0,false); }
    }
}
