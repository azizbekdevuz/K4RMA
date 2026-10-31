using UnityEngine;
using System.Collections.Generic;
namespace KarmaPrototype
{
    public sealed class KarmaCharacterAnimator : MonoBehaviour
    {
        Transform blade, leftBoot, rightBoot;
        SpriteRenderer picture;
        Sprite[] frames;
        Sprite[] runFrames;
        float runAt;
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
        bool hasInheritedPoses;
        Vector3 pictureScale;
        float appearanceScale = 1;
        KarmaHeavyStrikeMotion heavyStrike;
        Sprite guardStagger;
        Vector3 staggerScale;
        public Vector2 HeavyHandPosition { get { return heavyStrike.HandPosition; } }
        public void ConfigureHeavyStrike(IReadOnlyList<Essence> order)
        {
            heavyStrike = new KarmaHeavyStrikeMotion(picture, pictureHome, pictureScale, order);
            Sprite reference = null;
            foreach (var sprite in Resources.LoadAll<Sprite>("K4RMAArtwork/Sheets/Stagger/" + heavyStrike.FormKey))
            {
                if (sprite.name == "Stagger") guardStagger = sprite;
                else if (sprite.name == "Reference") reference = sprite;
            }
            // Calibrate with a standing figure drawn at the same anatomical scale.
            // Using crouched height here would inflate the boss during every parry.
            if (guardStagger == null || reference == null)
                throw new System.InvalidOperationException("경직 스프라이트가 없습니다: " + heavyStrike.FormKey);
            staggerScale = pictureScale * (picture.sprite.bounds.size.y / reference.bounds.size.y);
        }
        public void PrepareHeavyStrike() { heavyStrike.Prepare(); }
        public void SwingHeavyStrike() { heavyStrike.Swing(); }
        public void EndHeavyStrike() { if (heavyStrike != null) heavyStrike.Stop(); }
        float empowermentAt, empowermentUntil;
        Essence empowermentEssence;
        Sprite beforeInheritance;
        Sprite[] revivalFrames, beforeRevivalFrames;
        public string CurrentPose { get; private set; }
        public void ConfigureSheet(string name)
        {
            sheet = name; poses.Clear();
            revivalFrames = name == "Player" ? null : LoadRevivalFrames(name);
            runFrames = null;
            guardianPreviousPosition = transform.position;
            var library = KarmaArtwork.Settings.spriteLibrary;
            if (library == null) return;
            foreach (var sprite in library.All(name)) if (sprite != null) poses[sprite.name] = sprite;
            if (name == "Player" && poses.TryGetValue("Run", out var run))
            {
                // The second movement key pose is also used for jump takeoff in
                // the original sheet. Prefer a dedicated Run2 when supplied.
                if (!poses.TryGetValue("Run2", out var next)) next = KarmaArtwork.Settings.playerMove2;
                runFrames = next != null && next != run ? new[] { run, next } : new[] { run };
            }
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
        public void SetGuardianAppearance(Sprite sprite, string form = null)
        {
            if (sprite == null || picture == null) return;
            guardianAppearance = sprite;
            appearanceScale = picture.sprite.bounds.size.y / Mathf.Max(0.01f, sprite.bounds.size.y);
            if (string.IsNullOrEmpty(form)) return;
            var inherited = KarmaArtwork.InheritedPoses(form);
            if (inherited.Length != 16) return;
            hasInheritedPoses = true;
            foreach (var pose in inherited) poses[pose.name] = pose;
            guardianAppearance = poses["Idle"];
            appearanceScale = 1;
            revivalFrames = LoadRevivalFrames(form);
        }
        public void Attack() { Attack(Vector2.right); }
        public void Attack(Vector2 direction) { slashDirection = direction; actionUntil = Time.time + 0.18f; }
        public void Hurt() { hurtUntil = Time.time + 0.16f; }
        public void Die()
        {
            deathAt = Time.time;
            EndHeavyStrike();
            if (picture == null) return;
            picture.transform.localPosition = pictureHome;
            picture.transform.localScale = pictureScale;
            picture.color = pictureColor;
        }
        static Sprite[] LoadRevivalFrames(string form)
        {
            var inherited = KarmaArtwork.InheritedPoses(form);
            if (inherited.Length == 16)
            {
                var names = new[] { "Death", "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand", "Idle" };
                var result = new Sprite[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    string pose = names[i];
                    result[i] = System.Array.Find(inherited, s => s.name == pose);
                    if (result[i] == null) return System.Array.Empty<Sprite>();
                }
                return result;
            }
            var sprites = Resources.LoadAll<Sprite>("K4RMAArtwork/Characters/Guardians/Revival/" + form);
            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }
        public void Empower(Essence essence, float seconds, Sprite previousAppearance = null, bool fromNeutral = false,
            string previousForm = null)
        {
            deathAt = -1;
            empowermentAt = Time.time;
            empowermentUntil = Time.time + seconds;
            empowermentEssence = essence;
            beforeInheritance = previousAppearance;
            beforeRevivalFrames = !string.IsNullOrEmpty(previousForm) ? LoadRevivalFrames(previousForm)
                : fromNeutral ? LoadRevivalFrames("Neutral") : revivalFrames;
            EmpowermentPose();
        }
        void GuardianRevivalPose(float t)
        {
            ShowPose("Idle");
            var after = guardianAppearance != null ? guardianAppearance : picture.sprite;
            bool hasFrames = revivalFrames != null && revivalFrames.Length == 6
                && beforeRevivalFrames != null && beforeRevivalFrames.Length == 6;
            // Every new pose is authored at a shared body scale with a ground pivot.
            // Never stretch a lying sprite to the standing character's height.
            if (t < 0.12f && beforeInheritance != null)
            {
                picture.sprite = beforeInheritance;
                picture.transform.localScale = pictureScale;
                CurrentPose = "ReviveFallen";
            }
            else if (hasFrames && t < 0.9f)
            {
                int frame = t < 0.24f ? 1 : t < 0.36f ? 2 : t < 0.62f ? 3 : t < 0.76f ? 4 : 5;
                picture.sprite = t < 0.48f ? beforeRevivalFrames[frame] : revivalFrames[frame];
                picture.transform.localScale = pictureScale;
                CurrentPose = "Revive" + frame.ToString("00");
            }
            else
            {
                picture.sprite = after;
                picture.transform.localScale = pictureScale * (guardianAppearance != null ? appearanceScale : 1);
                CurrentPose = "ReviveReady";
            }
        }
        bool EmpowermentPose()
        {
            if (picture == null || Time.time >= empowermentUntil) return false;
            float t = Mathf.InverseLerp(empowermentAt, empowermentUntil, Time.time);
            if (sheet == "Player")
            {
                string pose = t < 0.42f ? "Guard" : t < 0.76f
                    ? empowermentEssence == Essence.Guard ? "Parry" : empowermentEssence == Essence.RisingSlash ? "Rising" : "FollowThrough"
                    : "Idle";
                ShowPose(pose);
            }
            else
            {
                GuardianRevivalPose(t);
            }
            Color color = empowermentEssence == Essence.Guard
                ? new Color(1, 0.9f, 0.4f) : empowermentEssence == Essence.RisingSlash
                ? new Color(0.4f, 1, 1) : new Color(1, 0.65f, 0.35f);
            picture.color = Color.Lerp(pictureColor, color, Mathf.Sin(t * Mathf.PI) * 0.55f);
            float lift = sheet == "Player" && empowermentEssence == Essence.RisingSlash ? Mathf.Sin(t * Mathf.PI) * 0.35f : 0;
            if (sheet != "Player")
            {
                picture.transform.localRotation = Quaternion.identity;
                picture.color = Color.Lerp(new Color(0.55f, 0.55f, 0.6f), picture.color,
                    Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.38f)));
                float flash = Mathf.Clamp01(1 - Mathf.Abs(t - 0.5f) / 0.07f);
                picture.color = Color.Lerp(picture.color, Color.white, flash);
            }
            picture.transform.localPosition = pictureHome + Vector3.up * lift;
            transform.localRotation = Quaternion.identity;
            return true;
        }
        public void Pose(float movement, bool airborne, float verticalSpeed = 0, bool crouching = false)
        {
            if (deathAt >= 0)
            {
                float sinceDeath = Time.time - deathAt;
                if (sheet == "Player" && poses.ContainsKey("Death"))
                {
                    ShowPose(sinceDeath < 0.16f ? "Hurt" : sinceDeath < 0.38f ? "Crouch" : "Death");
                    picture.transform.localPosition = pictureHome;
                    picture.transform.localScale = pictureScale;
                    picture.transform.localRotation = Quaternion.identity;
                    picture.color = pictureColor;
                    transform.localRotation = Quaternion.identity;
                }
                else if (poses.ContainsKey("Death")) ShowPose("Death");
                else if (picture != null && frames.Length >= 9) picture.sprite = frames[8];
                else transform.localRotation = Quaternion.Euler(0,0,Mathf.Min(85,(Time.time-deathAt)*200));
                if (sheet != "Player" && picture != null)
                {
                    if (revivalFrames != null && revivalFrames.Length == 6) picture.sprite = revivalFrames[0];
                    float fall = Mathf.SmoothStep(0, 1, (Time.time - deathAt) / 0.65f);
                    picture.transform.localScale = pictureScale;
                    picture.transform.localPosition = pictureHome;
                    picture.transform.localRotation = Quaternion.identity;
                    picture.color = Color.Lerp(pictureColor, new Color(0.55f, 0.55f, 0.6f), fall);
                }
                return;
            }
            if (EmpowermentPose()) return;
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
                        : crouching ? "Crouch"
                        : Time.time < landedUntil ? "Land"
                        : Mathf.Abs(movement) > 0.1f ? "Run" : "Idle";
                    if (pose == "Run" && runFrames != null)
                    {
                        if (CurrentPose != "Run") runAt = Time.time;
                        ShowPose("Run");
                        picture.sprite = runFrames[(int)((Time.time - runAt) * 9) % runFrames.Length];
                    }
                    else ShowPose(pose);
                }
                float bob = airborne || crouching ? 0 : Mathf.Abs(movement) > 0.1f ? Mathf.Sin(Time.time * 18) * 0.025f : Mathf.Sin(Time.time * 2) * 0.008f;
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
            if (EmpowermentPose()) return;
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
            if (action == "Stunned" && guardStagger != null)
            {
                picture.sprite = guardStagger;
                picture.transform.localPosition = pictureHome;
                picture.transform.localScale = staggerScale;
                picture.transform.localRotation = Quaternion.identity;
                picture.color = new Color(pictureColor.r * 0.82f, pictureColor.g * 0.82f, pictureColor.b * 0.82f, pictureColor.a);
                CurrentPose = "Stagger/" + heavyStrike.FormKey;
                guardianPreviousPosition = transform.position;
                return;
            }
            if (heavyStrike != null && heavyStrike.Show(attack))
            { CurrentPose = heavyStrike.CurrentPose; return; }
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
            // A complete inherited sheet already preserves the stage equipment in every pose.
            // Older sheets still need the composed body fallback; GD02/stagger return above.
            bool composed = !hasInheritedPoses && guardianAppearance != null && (sheet == "Ward" || pose == "Idle" || pose == "Guard");
            if (composed) picture.sprite = guardianAppearance;
            picture.transform.localScale = pictureScale * (composed ? appearanceScale : 1);
            guardianPreviousPosition = transform.position;
            picture.transform.localPosition = pictureHome;
            picture.transform.localRotation = Quaternion.identity;
            picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
            transform.localRotation = Quaternion.identity;
        }
        void Update() { if (deathAt >= 0) Pose(0,false); }
    }
}
