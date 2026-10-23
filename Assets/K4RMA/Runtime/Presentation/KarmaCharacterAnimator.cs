using UnityEngine;
using System.Collections.Generic;
namespace KarmaPrototype
{
    public sealed class KarmaCharacterAnimator : MonoBehaviour
    {
        Transform blade, leftBoot, rightBoot;
        SpriteRenderer picture;
        Sprite[] frames;
        bool completePlayer, completeGuardian;
        int guardianState, specialPose;
        float stateAt, verticalSpeed, specialUntil;
        bool guardianGuard, playerGuard, playerCounter;
        public void ConfigureComplete(SpriteRenderer renderer, Sprite[] poses, bool guardian)
        { ConfigureSprites(renderer, poses); completeGuardian=guardian;completePlayer=!guardian; }
        public void GuardianState(int value,bool guard)
        { if(guardianState!=value)stateAt=Time.time;guardianState=value;guardianGuard=guard; }
        public void PlayerMotion(float velocity,bool guard,bool counter)
        { verticalSpeed=velocity;playerGuard=guard;playerCounter=counter; }
        public void SpecialPose(int index,float seconds){specialPose=index;specialUntil=Time.time+seconds;}
        Vector2 slashDirection = Vector2.right;
        Vector3 pictureHome;
        Color pictureColor;
        float actionUntil, hurtUntil, deathAt = -1;
        readonly Dictionary<string, Sprite> poses = new Dictionary<string, Sprite>();
        string sheet, explicitPose;
        float poseUntil, landedUntil;
        bool wasAirborne;
        Vector3 guardianPreviousPosition;
        Sprite guardianAppearance;
        Vector3 pictureScale;
        float appearanceScale = 1;
        public string CurrentPose { get; private set; }
        public void ConfigureSheet(string name)
        {
            sheet = name; poses.Clear();
            guardianPreviousPosition = transform.position;
            var library = KarmaArtwork.Settings.spriteLibrary;
            if (library == null) return;
            foreach (var sprite in library.All(name)) if (sprite != null) poses[sprite.name] = sprite;
        }
        public void PlayPose(string name, float seconds)
        {
            explicitPose = name; poseUntil = Time.time + seconds;
            ShowPose(name);
        }
        void ShowPose(string name)
        {
            if (picture == null || !poses.TryGetValue(name, out var sprite)) return;
            picture.sprite = sprite; CurrentPose = name;
            picture.transform.localScale = pictureScale;
        }
        public void Initialize()
        {
            blade = transform.Find("Katana"); leftBoot = transform.Find("Boot L"); rightBoot = transform.Find("Boot R");
        }
        public void ConfigureSprites(SpriteRenderer renderer, Sprite[] poses)
        {
            picture = renderer; frames = poses; picture.sprite = frames[0];
            pictureHome = picture.transform.localPosition; pictureColor = picture.color;
            pictureScale = picture.transform.localScale;
        }
        public void SetGuardianAppearance(Sprite sprite)
        {
            if (sprite == null || picture == null) return;
            guardianAppearance = sprite;
            appearanceScale = picture.sprite.bounds.size.y / Mathf.Max(0.01f, sprite.bounds.size.y);
        }
        public void Attack() { Attack(Vector2.right); }
        public void Attack(Vector2 direction) { slashDirection = direction; actionUntil = Time.time + 0.18f; }
        public void Hurt() { hurtUntil = Time.time + 0.16f; }
        public void Die() { deathAt = Time.time; }
        public void Pose(float movement, bool airborne, float verticalSpeed = 0)
        {
            if (deathAt >= 0)
            {
<<<<<<< HEAD
                if (poses.ContainsKey("Death")) ShowPose("Death");
=======
                if(picture!=null && (completePlayer||completeGuardian)) picture.sprite=frames[completeGuardian?frames.Length-1:15];
>>>>>>> e8d524ec86dec83deb348c14c41617b54d84da4c
                else if (picture != null && frames.Length >= 9) picture.sprite = frames[8];
                else transform.localRotation = Quaternion.Euler(0,0,Mathf.Min(85,(Time.time-deathAt)*200));
                return;
            }
            if (picture != null)
            {
                int frame = 0;
                if(completeGuardian)
                {
                    bool neutral=frames.Length==8;
                    if(Time.time<hurtUntil)frame=neutral?5:10;
                    else if(guardianState==4)frame=neutral?6:8;
                    else if(guardianState==1)frame=neutral?2:4;
                    else if(guardianState==2||Time.time<actionUntil)frame=neutral?3:airborne?9:5+(int)((Time.time-stateAt)*8)%3;
                    else if(guardianState==3)frame=neutral?4:8;
                    else if(guardianGuard)frame=4;
                    else if(Mathf.Abs(movement)>0.1f)frame=neutral?1:3;
                }
                else if(completePlayer)
                {
                    if(Time.time<hurtUntil)frame=14;
                    else if(Time.time<specialUntil)frame=specialPose;
                    else if(playerCounter)frame=13;
                    else if(playerGuard)frame=12;
                    else if(Time.time<actionUntil)
                    {
                        float t=Mathf.Clamp01(1-(actionUntil-Time.time)/0.18f);
                        frame=slashDirection.y< -0.25f?(t<0.5f?16:19):slashDirection.y>0.25f?(t<0.5f?11:12):(t<0.25f?8:t<0.5f?7:t<0.75f?9:10);
                    }
                    else if(airborne)frame=verticalSpeed< -0.1f?4:3;
                    else if(Mathf.Abs(movement)>0.1f)frame=1+(int)(Time.time*9)%2;
                }
                else if (frames.Length > 1)
                {
                    if (Time.time < hurtUntil && frames.Length >= 9) frame = 7;
                    else if (Time.time < actionUntil)
                        frame = slashDirection.y < -0.25f ? 6 : slashDirection.y > 0.25f ? 5 : actionUntil - Time.time > 0.09f ? 3 : 4;
                    else if (airborne) frame = 1;
                    else if (Mathf.Abs(movement) > 0.1f) frame = (int)(Time.time * 9) % 2 == 0 ? 1 : 2;
                }
                picture.sprite = frames[frame];
<<<<<<< HEAD
                if (sheet == "Player" && poses.Count > 0)
                {
                    if (wasAirborne && !airborne) landedUntil = Time.time + 0.1f;
                    wasAirborne = airborne;
                    string pose = Time.time < hurtUntil ? "Hurt"
                        : Time.time < poseUntil ? (explicitPose == "Pierce" && poseUntil - Time.time > 0.15f ? "Dash" : explicitPose)
                        : Time.time < actionUntil ? (slashDirection.y < -0.25f ? "AirSlash"
                            : slashDirection.y > 0.25f ? "Rising"
                            : actionUntil - Time.time > 0.13f ? "Windup"
                            : actionUntil - Time.time > 0.07f ? "Slash" : "FollowThrough")
                        : actionUntil > 0 && Time.time < actionUntil + 0.08f ? "Recovery"
                        : airborne ? (verticalSpeed > 2 ? "Jump" : verticalSpeed < -2 ? "Fall" : "Apex")
                        : Time.time < landedUntil ? "Land"
                        : Mathf.Abs(movement) > 0.1f ? "Run" : "Idle";
                    ShowPose(pose);
                }
=======
                if(completePlayer||completeGuardian)picture.transform.localScale=Vector3.one*KarmaSpace.Scale*(1+(frame==0?Mathf.Sin(Time.time*2)*0.006f:0));
>>>>>>> e8d524ec86dec83deb348c14c41617b54d84da4c
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
        public void GuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            // Poses follow the actual combat state, including long attacks and recovery.
            if (picture == null || deathAt >= 0) return;
            string pose;
            if (Time.time < hurtUntil) pose = "Hurt";
            else if (action == "Stunned") pose = sheet == "Neutral" ? "Kneel" : sheet == "Ward" ? "Crouch" : "Hurt";
            else if (action == "Recover") pose = sheet == "Ward" ? "Recover" : sheet == "Rise" ? "Landing" : sheet == "Wave" ? "FollowThrough" : "Recovery";
            else if (action == "Telegraph") pose = armored && poses.ContainsKey("Guard") ? "Guard" : "Windup";
            else if (action == "Attack")
            {
                if (sheet == "Rise") pose = airborne ? "AirSlash" : attack == EnemyAttack.UP01 || attack == EnemyAttack.UP03 ? "Rising" : "Slash";
                else if (sheet == "Ward") pose = attack == EnemyAttack.GD02 ? "ShieldBash" : attack == EnemyAttack.GD03 ? "Thrust" : "Windup";
                else if (sheet == "Wave") pose = attack == EnemyAttack.SW01 || attack == EnemyAttack.SW02 ? "Wave" : airborne ? "Rising" : "Slash";
                else pose = "Slash";
            }
            else pose = armored && poses.ContainsKey("Guard") ? "Guard"
                : (transform.position - guardianPreviousPosition).sqrMagnitude > 0.00001f
                    ? sheet == "Rise" ? "Dash" : "Move" : "Idle";
            if (!poses.ContainsKey(pose)) pose = "Idle";
            ShowPose(pose);
            bool composed = guardianAppearance != null && (pose == "Idle" || pose == "Guard");
            if (composed) picture.sprite = guardianAppearance;
            picture.transform.localScale = pictureScale * (composed ? appearanceScale : 1);
            guardianPreviousPosition = transform.position;
            picture.transform.localPosition = pictureHome;
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
        }
        void Update() { if (deathAt >= 0) Pose(0,false); }
    }
}
