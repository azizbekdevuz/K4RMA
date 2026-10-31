#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KarmaPrototype;

// Called by the existing Play Mode driver. The probe uses real Wave sprites
// under the live guardian, but never changes its animator or combat coroutine.
public static class KarmaWaveMotionPolishChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static FieldInfo Field(object value, string name) => value.GetType().GetField(name, Flags)
        ?? throw new MissingFieldException(value.GetType().Name, name);
    static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
    static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name, Flags).GetValue(value);
    static void Set(object value, string name, object field) => Field(value, name).SetValue(value, field);
    static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
    static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new Exception("Wave motion polish: " + label);
    }

    static bool SameBounds(Bounds a, Bounds b) => Vector3.Distance(a.center, b.center) < 0.0001f
        && Vector3.Distance(a.size, b.size) < 0.0001f;

    // Read the renderer at its authored transform as an independent reference,
    // rather than rebuilding the same AABB calculation as GetDamageBounds.
    static Bounds AuthoredBounds(KarmaCharacterAnimator animator, SpriteRenderer picture, Vector3 home)
    {
        var shownSprite = picture.sprite;
        Vector3 shownScale = picture.transform.localScale, shownPosition = picture.transform.localPosition;
        Quaternion shownRotation = picture.transform.localRotation;
        try
        {
            if (Property<bool>(animator, "GuardianPhaseOverridesPose"))
            {
                picture.sprite = Property<Sprite>(animator, "GuardianDamageSprite");
                picture.transform.localScale = Property<Vector3>(animator, "GuardianDamageScale");
            }
            picture.transform.localPosition = home;
            picture.transform.localRotation = Quaternion.identity;
            return picture.bounds;
        }
        finally
        {
            picture.sprite = shownSprite; picture.transform.localScale = shownScale;
            picture.transform.localPosition = shownPosition; picture.transform.localRotation = shownRotation;
        }
    }

    static void AdvanceChecked(KarmaEnemy enemy, GameObject root, KarmaCharacterAnimator animator,
        KarmaWaveMotion motion, SpriteRenderer picture, Vector3 home, float dt, string label)
    {
        Vector3 position = enemy.transform.position, scale = enemy.transform.localScale;
        Quaternion rotation = enemy.transform.rotation;
        Vector3 artworkPosition = root.transform.localPosition, artworkScale = root.transform.localScale;
        Quaternion artworkRotation = root.transform.localRotation;
        Bounds liveDamage = enemy.DamageBounds;
        Bounds authored = AuthoredBounds(animator, picture, home);
        Call(motion, "AdvanceMotion", dt);
        Check(enemy.transform.position == position && enemy.transform.localScale == scale && enemy.transform.rotation == rotation
            && root.transform.localPosition == artworkPosition && root.transform.localScale == artworkScale
            && root.transform.localRotation == artworkRotation, label + " changes only the display child transform");
        Check(SameBounds(enemy.DamageBounds, liveDamage)
            && SameBounds((Bounds)Call(motion, "GetDamageBounds", picture), authored),
            label + " preserves live and authored incoming damage bounds");
        if (!Property<bool>(motion, "HoverActive"))
        {
            float authoredFoot = root.transform.TransformPoint(home
                + Vector3.up * picture.sprite.bounds.min.y * picture.transform.localScale.y).y;
            float shownFoot = picture.transform.TransformPoint(Vector3.up * picture.sprite.bounds.min.y).y;
            Check(Get<Vector3>(motion, "motionOffset").y == 0 && Mathf.Abs(shownFoot - authoredFoot) < 0.00001f,
                label + " plants the authored feet without moving damage bounds");
        }
    }

    static void DisplayPose(KarmaCharacterAnimator animator, SpriteRenderer picture, Vector3 home, string pose)
    {
        Call(animator, "ShowPose", pose);
        // GuardianPose restores this position every Update; mimic that ordering
        // while keeping every motion sample on the explicit test delta clock.
        picture.transform.localPosition = home;
        picture.transform.localRotation = Quaternion.identity;
    }

    static void ResetClock(KarmaEnemy enemy, KarmaWaveMotion motion)
    {
        Call(motion, "ResetVisualTransform");
        Set(motion, "visualClock", 0f); Set(motion, "poseStartedAt", 0f);
        Set(motion, "gaitPhase", 0f); Set(motion, "previousPose", "");
        Set(motion, "previousRootPosition", enemy.Position);
    }

    static void LocomotionFrameRateChecks(KarmaGame game, KarmaEnemy enemy, GameObject root,
        KarmaCharacterAnimator animator, KarmaWaveMotion motion, SpriteRenderer picture, Vector3 home)
    {
        float speed = Mathf.Max(0.1f, game.Config.enemyMoveSpeed * game.Config.BossTuning.moveMultiplier);
        Vector3 referenceMove = Vector3.zero;
        float referenceAngle = 0, referenceGait = 0;
        foreach (int fps in new[] { 15, 30, 60, 120 })
        {
            float dt = 1f / fps;
            foreach (int facing in new[] { -1, 1 })
                foreach (int direction in new[] { -1, 1 })
                {
                    string label = fps + " FPS facing " + facing + " travel " + direction;
                    Set(enemy, "selected", EnemyAttack.SW01); Set(enemy, "fullShield", false);
                    enemy.transform.position = new Vector3(0, KarmaSpace.GuardianGroundY, 0);
                    root.transform.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    ResetClock(enemy, motion);
                    DisplayPose(animator, picture, home, "Move");
                    for (int frame = 0; frame < fps * 2 / 5; frame++)
                    {
                        enemy.transform.position += Vector3.right * (speed * direction * dt);
                        AdvanceChecked(enemy, root, animator, motion, picture, home, dt, label + " moving");
                    }
                    Check(Mathf.Sign(Get<float>(motion, "locomotionSpeed")) == direction * facing
                        && Mathf.Abs(Get<float>(motion, "locomotionSpeed")) > speed * 0.95f,
                        label + " uses measured travel speed and its direction relative to facing");
                    Check(Get<float>(motion, "gaitPhase") > 0 && Mathf.Abs(Get<Vector3>(motion, "motionOffset").x) > 0.005f,
                        label + " actual travel advances a visible grounded gait");
                    if (facing == 1 && direction == 1)
                    {
                        if (fps == 15)
                        {
                            referenceMove = Get<Vector3>(motion, "motionOffset");
                            referenceAngle = Get<float>(motion, "motionAngle");
                            referenceGait = Get<float>(motion, "gaitPhase");
                        }
                        else
                        {
                            Check(Mathf.Abs(Get<float>(motion, "gaitPhase") - referenceGait) < 0.0005f,
                                label + " equal root distance advances the same gait phase at every FPS");
                            Check(Vector3.Distance(Get<Vector3>(motion, "motionOffset"), referenceMove) < 0.0045f
                                && Mathf.Abs(Get<float>(motion, "motionAngle") - referenceAngle) < 0.5f,
                                label + " equal travel duration keeps sway and lean consistent across frame rates");
                        }
                    }

                    // Flipping art must carry its displayed world-space weight,
                    // including a backwards-moving sample, through the flip.
                    Vector3 worldOffset = root.transform.TransformVector(Get<Vector3>(motion, "motionOffset"));
                    float worldAngle = Get<float>(motion, "motionAngle") * facing;
                    root.transform.localScale = new Vector3(-facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    AdvanceChecked(enemy, root, animator, motion, picture, home, 0.00001f, label + " facing flip");
                    Check(Vector3.Distance(root.transform.TransformVector(Get<Vector3>(motion, "motionOffset")), worldOffset) < 0.0001f
                        && Mathf.Abs(Get<float>(motion, "motionAngle") * -facing - worldAngle) < 0.01f,
                        label + " a facing flip preserves displayed world displacement and lean");

                    // A clamped wall position still reports Move for a frame (or
                    // longer). No root travel must stop the gait and settle it.
                    float stoppedGait = Get<float>(motion, "gaitPhase");
                    AdvanceChecked(enemy, root, animator, motion, picture, home, dt, label + " first blocked frame");
                    Check(Mathf.Abs(Get<Vector3>(motion, "motionOffset").x) > 0.0001f,
                        label + " stopping carries its offset into a smooth settle");
                    for (int frame = 0; frame < fps; frame++)
                        AdvanceChecked(enemy, root, animator, motion, picture, home, dt, label + " blocked Move");
                    Check(Get<float>(motion, "gaitPhase") == stoppedGait
                        && Mathf.Abs(Get<float>(motion, "locomotionSpeed")) < speed * 0.00001f
                        && Mathf.Abs(Get<Vector3>(motion, "motionOffset").x) < 0.00005f
                        && Mathf.Abs(Get<float>(motion, "motionAngle")) < 0.002f,
                        label + " blocked Move stops walking and settles without root displacement");
                }

            string transition = fps + " FPS Idle/Move/Windup";
            enemy.transform.position = new Vector3(0, KarmaSpace.GuardianGroundY, 0);
            root.transform.localScale = new Vector3(KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            ResetClock(enemy, motion);
            DisplayPose(animator, picture, home, "Idle");
            for (int frame = 0; frame < fps / 5; frame++)
                AdvanceChecked(enemy, root, animator, motion, picture, home, dt, transition + " idle");
            Check(Get<float>(motion, "gaitPhase") == 0 && Get<Vector3>(motion, "motionOffset").x == 0,
                transition + " stationary Idle does not walk in place");
            enemy.transform.position += Vector3.right * (speed * dt);
            AdvanceChecked(enemy, root, animator, motion, picture, home, dt, transition + " delayed Move sprite");
            Check(Get<float>(motion, "locomotionSpeed") > 0 && Get<float>(motion, "gaitPhase") > 0,
                transition + " actual movement starts even while the previous Idle sprite is displayed");
            Vector3 carried = Get<Vector3>(motion, "motionOffset");
            DisplayPose(animator, picture, home, "Move");
            Check(Get<Vector3>(motion, "motionOffset") == carried, transition + " selecting Move carries displayed state");
            for (int frame = 0; frame < fps * 2 / 5; frame++)
            {
                enemy.transform.position += Vector3.right * (speed * dt);
                AdvanceChecked(enemy, root, animator, motion, picture, home, dt, transition + " approach");
            }
            carried = Get<Vector3>(motion, "motionOffset");
            float carriedAngle = Get<float>(motion, "motionAngle");
            DisplayPose(animator, picture, home, "Windup");
            Check(Get<Vector3>(motion, "motionOffset") == carried && picture.transform.localPosition == home,
                transition + " preparation retains motion across the animator position restore");
            AdvanceChecked(enemy, root, animator, motion, picture, home, dt, transition + " first preparation frame");
            Check(Vector3.Distance(Get<Vector3>(motion, "motionOffset"), carried) < 0.015f
                && Mathf.Abs(Mathf.DeltaAngle(Get<float>(motion, "motionAngle"), carriedAngle)) < 2,
                transition + " first preparation sample eases from the moving weight");
            for (int frame = 0; frame < fps / 3; frame++)
                AdvanceChecked(enemy, root, animator, motion, picture, home, dt, transition + " preparation");
            Check(Get<Vector3>(motion, "motionOffset").x < -0.023f && Get<float>(motion, "motionAngle") < -3.8f,
                transition + " settles into the existing attack anticipation");

            Set(enemy, "selected", EnemyAttack.SW05); Set(enemy, "fullShield", true);
            enemy.transform.position += Vector3.up * KarmaSpace.Units(6);
            ResetClock(enemy, motion);
            DisplayPose(animator, picture, home, "Guard");
            bool hovered = false;
            for (int frame = 0; frame < fps * 3 / 5; frame++)
            {
                AdvanceChecked(enemy, root, animator, motion, picture, home, dt, fps + " FPS SW05 hover");
                hovered |= Mathf.Abs(Get<Vector3>(motion, "motionOffset").y) > 0.0001f;
            }
            Check(Property<bool>(motion, "HoverActive") && hovered, fps + " FPS preserves the existing SW05 hover");
            DisplayPose(animator, picture, home, "Wave");
            bool recoiled = false;
            for (int frame = 0; frame < fps / 5; frame++)
            {
                AdvanceChecked(enemy, root, animator, motion, picture, home, dt, fps + " FPS SW05 release");
                recoiled |= Get<Vector3>(motion, "motionOffset").x > 0.01f;
            }
            Check(Property<bool>(motion, "HoverActive") && recoiled,
                fps + " FPS keeps release recoil together with airborne shield hover");
            DisplayPose(animator, picture, home, "Hurt");
            AdvanceChecked(enemy, root, animator, motion, picture, home, dt, fps + " FPS SW05 hurt");
            Check(!Property<bool>(motion, "HoverActive") && Get<Vector3>(motion, "motionOffset").y == 0,
                fps + " FPS hurt removes hover immediately");
            Call(motion, "OnDisable");
            Check(picture.transform.localPosition == home && picture.transform.localRotation == Quaternion.identity
                && Get<Vector3>(motion, "motionVelocity") == Vector3.zero && Get<float>(motion, "locomotionSpeed") == 0,
                fps + " FPS disabling clears display easing without moving the boss");
        }
        Set(enemy, "selected", EnemyAttack.SW01); Set(enemy, "fullShield", false);
        enemy.transform.position = new Vector3(0, KarmaSpace.GuardianGroundY, 0);
        root.transform.localScale = Vector3.one * KarmaSpace.GuardianArtScale;
        ResetClock(enemy, motion);
    }

    static void GuardSpacingChecks(KarmaEnemy enemy, GameObject root,
        KarmaCharacterAnimator animator, KarmaWaveMotion motion, SpriteRenderer picture, Vector3 home)
    {
        Sprite inherited = Get<Sprite>(animator, "guardianAppearance");
        float inheritedScale = Get<float>(animator, "appearanceScale");
        Vector3 originalPosition = enemy.transform.position, originalScale = root.transform.localScale;
        var originalSelected = Get<EnemyAttack>(enemy, "selected");
        var originalState = Get<object>(enemy, "state");
        try
        {
            Set(enemy, "selected", EnemyAttack.SW04);
            Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, "Approach"));
            Set(animator, "actionUntil", -1f); Set(animator, "hurtUntil", -1f);
            foreach (bool composed in new[] { false, true })
                foreach (int facing in new[] { -1, 1 })
                    foreach (int travel in new[] { -1, 1 })
                    {
                        string label = "guard spacing inherited " + composed + " facing " + facing + " travel " + travel;
                        Set(animator, "guardianAppearance", composed ? inherited : null);
                        enemy.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
                        root.transform.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                        Set(animator, "guardianPreviousPosition", root.transform.position);
                        ResetClock(enemy, motion);
                        animator.GuardianPose("Approach", EnemyAttack.SW04, false, true);
                        Check(animator.CurrentPose == "Guard", label + " stationary armor keeps its Guard pose");
                        Bounds originalGuard = AuthoredBounds(animator, picture, home);
                        Vector3 startedAt = enemy.transform.position;
                        Sprite damageSprite = picture.sprite;
                        for (int frame = 0; frame < 8; frame++)
                        {
                            enemy.transform.position += Vector3.right * (travel * 0.2f);
                            animator.GuardianPose("Approach", EnemyAttack.SW04, false, true);
                            Check(animator.CurrentPose == "Move" && picture.sprite == KarmaArtwork.Sprite("Wave", "Move"),
                                label + " actual armored approach uses the existing Move picture");
                            Bounds expected = originalGuard;
                            expected.center += enemy.transform.position - startedAt;
                            Check(Property<Sprite>(animator, "GuardianDamageSprite") == damageSprite
                                && SameBounds((Bounds)Call(motion, "GetDamageBounds", picture), expected),
                                label + " movement changes only world position of the original Guard damage silhouette");
                            AdvanceChecked(enemy, root, animator, motion, picture, home, 1f / 30, label);
                        }
                        Check(Get<float>(motion, "gaitPhase") > 0
                            && Mathf.Sign(Get<float>(motion, "locomotionSpeed")) == travel * facing,
                            label + " measured armored travel drives the existing signed walking motion");
                        animator.GuardianPose("Approach", EnemyAttack.SW04, false, true);
                        Check(animator.CurrentPose == "Guard", label + " stopping returns to the original Guard pose");
                        enemy.transform.position += Vector3.right * (travel * 0.2f);
                        animator.PlayWavePhase("Guard");
                        animator.GuardianPose("Telegraph", EnemyAttack.SW04, false, true);
                        Check(animator.CurrentPose == "Guard" && picture.sprite == KarmaArtwork.Sprite("Wave", "Guard"),
                            label + " a locked Guard warning cannot be replaced by Move");
                    }
        }
        finally
        {
            Set(animator, "guardianAppearance", inherited); Set(animator, "appearanceScale", inheritedScale);
            Set(enemy, "selected", originalSelected); Set(enemy, "state", originalState);
            enemy.transform.position = originalPosition; root.transform.localScale = originalScale;
            Set(animator, "guardianPreviousPosition", root.transform.position);
            animator.GuardianPose("Approach", originalSelected, false, false);
            ResetClock(enemy, motion);
        }
    }

    // Exercise the production Update ordering: GuardianPose first sees a
    // stationary root, then the spacing decision applies its first real step.
    // A separate initialized enemy keeps the live fight's fields untouched.
    static void FirstSpacingUpdateChecks(KarmaGame game)
    {
        Check(Time.deltaTime > 0, "first-spacing update runs on a positive Play Mode delta");
        var probeObject = new GameObject("Isolated first-spacing Update probe");
        var probe = probeObject.AddComponent<KarmaEnemy>();
        var player = game.Player;
        var playerBody = player.GetComponent<Rigidbody2D>();
        Vector3 playerPosition = player.transform.position;
        Vector2 playerBodyPosition = playerBody.position;
        Transform effects = null;
        try
        {
            probe.Initialize(game, 1);
            effects = Get<Transform>(probe, "effectsRoot");
            Check(probe.PrimaryType == Essence.SwordWave, "first-spacing probe uses the real Wave initializer");
            var artwork = Get<Transform>(probe, "art");
            var animator = Get<KarmaCharacterAnimator>(probe, "animator");
            var picture = artwork.Find("Guardian sprite").GetComponent<SpriteRenderer>();
            var motion = artwork.GetComponent<KarmaWaveMotion>();
            Check(picture != null && motion != null, "first-spacing probe uses the real Wave motion and damage-bounds path");
            Call(motion, "Start");
            foreach (bool approach in new[] { false, true })
                foreach (int travel in new[] { -1, 1 })
                    foreach (bool hurt in new[] { false, true })
                    {
                        string label = "first " + (approach ? "approach" : "retreat") + " Update travel " + travel + " hurt " + hurt;
                        probe.transform.position = new Vector3(20, KarmaSpace.GuardianGroundY, 0);
                        float facing = approach ? travel : -travel;
                        artwork.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                        player.transform.position = new Vector3(20 + facing * (approach ? 14 : 4), playerPosition.y, playerPosition.z);
                        playerBody.position = player.transform.position;
                        Set(probe, "engaged", true); Set(probe, "running", false); Set(probe, "pendingParry", false);
                        Set(probe, "state", Enum.Parse(Field(probe, "state").FieldType, "Approach"));
                        Set(probe, "selected", EnemyAttack.SW04); Set(probe, "timer", 100f); Set(probe, "direction", facing);
                        Set(probe, "swordGuard", true); Set(probe, "swordGuardUntil", probe.FightSeconds + 10);
                        Set(probe, "swordApproaching", approach); Set(probe, "swordApproachUntil", probe.FightSeconds + 10);
                        Set(probe, "nextSwordApproach", probe.FightSeconds + 10);
                        Set(probe, "nextSwordRetreat", probe.FightSeconds + 10);
                        Set(probe, "swordRetreatRemaining", approach ? 0f : 1f);
                        Set(probe, "swordRetreatDirection", (float)travel); Set(probe, "swordRetreatTarget", 100f);
                        Set(probe, "swordRetreatUntil", probe.FightSeconds + 10);
                        Set(animator, "guardianPreviousPosition", artwork.position);
                        Set(animator, "hurtUntil", hurt ? Time.time + 10 : -1f);
                        animator.GuardianPose("Approach", EnemyAttack.SW04, false, true);
                        Check(animator.CurrentPose == (hurt ? "Hurt" : "Guard"), label + " begins with the stationary priority pose");
                        Sprite damageSprite = Property<Sprite>(animator, "GuardianDamageSprite");
                        Vector3 damageScale = Property<Vector3>(animator, "GuardianDamageScale");
                        Bounds before = probe.DamageBounds;
                        Vector3 position = probe.transform.position;
                        ResetClock(probe, motion);
                        Call(probe, "Update");
                        Vector3 step = probe.transform.position - position;
                        Check(step.x * travel > 0.0001f && Mathf.Abs(step.y) < 0.00001f,
                            label + " applies its first real ground step through production Update");
                        Check(animator.CurrentPose == (hurt ? "Hurt" : "Move"),
                            label + " shows movement in its first frame while respecting hurt priority");
                        Bounds expected = before; expected.center += step;
                        Check(Property<Sprite>(animator, "GuardianDamageSprite") == damageSprite
                            && Property<Vector3>(animator, "GuardianDamageScale") == damageScale
                            && SameBounds(probe.DamageBounds, expected),
                            label + " preserves the original damage sprite, scale and translated live bounds");
                        Bounds beforeMotion = probe.DamageBounds;
                        Call(motion, "LateUpdate");
                        Check(SameBounds(probe.DamageBounds, beforeMotion), label + " LateUpdate cannot change incoming-hit bounds");
                        if (!hurt)
                            Check(Get<float>(motion, "gaitPhase") > 0, label + " advances walking motion on the first moved frame");
                        Check(Get<bool>(probe, "running") == false && Get<EnemyAttack>(probe, "selected") == EnemyAttack.SW04,
                            label + " does not begin or replace an attack");
                    }
        }
        finally
        {
            player.transform.position = playerPosition; playerBody.position = playerBodyPosition;
            // CancelCombat would release the live player's arena on disable.
            // This synchronous probe owns no coroutine or pattern objects.
            Set(probe, "stopped", true);
            if (effects != null) UnityEngine.Object.DestroyImmediate(effects.gameObject);
            UnityEngine.Object.DestroyImmediate(probeObject);
        }
    }

    public static void RunFocused() { KarmaWaveMotionFocusedChecks.Run(); }

    public static int Run(KarmaGame game, KarmaEnemy enemy)
    {
        assertions = 0;
        Check(game != null && enemy != null && enemy.Alive, "probe has an active live guardian");
        var oldSelected = Get<EnemyAttack>(enemy, "selected");
        var oldState = Get<object>(enemy, "state");
        bool oldShield = Get<bool>(enemy, "fullShield");
        Vector3 oldPosition = enemy.transform.position;
        Quaternion oldRotation = enemy.transform.rotation;
        var root = new GameObject("Isolated Wave motion polish probe");
        root.transform.SetParent(enemy.transform, false);
        root.transform.localScale = Vector3.one * KarmaSpace.GuardianArtScale;
        var image = new GameObject("Guardian sprite");
        image.transform.SetParent(root.transform, false);
        image.transform.localPosition = new Vector3(0, -KarmaSpace.GuardianGroundY / KarmaSpace.GuardianArtScale, 0);
        image.transform.localScale = Vector3.one * KarmaSpace.Scale;
        var picture = image.AddComponent<SpriteRenderer>();
        picture.enabled = false; // The test probe must not appear in the actual fight.
        var animator = root.AddComponent<KarmaCharacterAnimator>();
        var idle = KarmaArtwork.Sprite("Wave", "Idle");
        animator.ConfigureSprites(picture, new[] { idle });
        animator.ConfigureSheet("Wave");
        var inherited = KarmaArtwork.Sprite("Inheritance", "WaveFinal");
        Check(inherited != null, "probe uses the existing inherited Wave appearance");
        animator.SetGuardianAppearance(inherited);
        var motion = root.AddComponent<KarmaWaveMotion>();
        Vector3 home = image.transform.localPosition;
        try
        {
            Set(enemy, "selected", EnemyAttack.SW01);
            Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, "Attack"));
            Set(enemy, "fullShield", false);
            enemy.transform.rotation = Quaternion.identity;
            enemy.transform.position = new Vector3(oldPosition.x, KarmaSpace.GuardianGroundY, 0);
            Call(motion, "Start");

            // Guard must remain the Wave attack drawing, while incoming hits
            // retain the pre-polish inherited Guard sprite and authored scale.
            foreach (var attack in new[] { EnemyAttack.SW01, EnemyAttack.SW02 })
            {
                animator.PlayPose("Guard", 10);
                animator.GuardianPose("Attack", attack, false, true);
                Check(picture.sprite == KarmaArtwork.Sprite("Wave", "Guard")
                    && picture.transform.localScale == Vector3.one * KarmaSpace.Scale,
                    attack + " attack guard does not swap to inherited idle artwork");
                Check(Property<bool>(animator, "GuardianPhaseOverridesPose") && Property<Sprite>(animator, "GuardianDamageSprite") == inherited
                    && Property<Vector3>(animator, "GuardianDamageScale") == Vector3.one * KarmaSpace.Scale * Get<float>(animator, "appearanceScale"),
                    attack + " attack guard retains its original inherited incoming-hit silhouette");
                var visible = picture.sprite;
                Vector3 visibleScale = picture.transform.localScale;
                picture.sprite = inherited; picture.transform.localScale = Property<Vector3>(animator, "GuardianDamageScale");
                Bounds inheritedBounds = picture.bounds;
                picture.sprite = visible; picture.transform.localScale = visibleScale;
                Bounds actualBounds = (Bounds)Call(motion, "GetDamageBounds", picture);
                Check(Vector3.Distance(actualBounds.center, inheritedBounds.center) < 0.0001f
                    && Vector3.Distance(actualBounds.size, inheritedBounds.size) < 0.0001f,
                    attack + " damage-bounds entry point preserves the original inherited guard silhouette");
            }
            animator.GuardianPose("Approach", EnemyAttack.SW01, false, true);
            Check(picture.sprite == inherited, "nonattack inherited guard artwork is retained");

            // The next SW01 warning is queued in the same coroutine frame as
            // the previous release. It must not rewind that visible release.
            animator.Attack();
            animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
            Call(motion, "LateUpdate"); Call(motion, "LateUpdate");
            float releaseStarted = Get<float>(motion, "poseStartedAt");
            int releaseRevision = Property<int>(animator, "WaveMotionRevision");
            animator.PrepareWaveAttack(0.54f, false);
            animator.GuardianPose("Telegraph", EnemyAttack.SW01, false, false);
            Call(motion, "LateUpdate");
            Check(animator.CurrentPose == "Wave", "queuing preparation preserves the preceding release image");
            Check(Property<int>(animator, "WaveMotionRevision") == releaseRevision && Get<float>(motion, "poseStartedAt") == releaseStarted,
                "queuing preparation does not restart the displayed release clock");

            // GuardianPose restores the authored renderer transform in Update.
            // Motion must carry its own displayed state across that restoration.
            Set(animator, "actionUntil", -1f);
            animator.PlayPose("Windup", 10);
            animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
            for (int i = 0; i < 8; i++) Call(motion, "LateUpdate");
            Vector3 carried = Get<Vector3>(motion, "motionOffset");
            Check(carried.x < -0.01f, "windup visibly carries weight backwards");
            animator.PlayPose("Slash", 10);
            animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
            Check(Get<Vector3>(motion, "motionOffset") == carried && image.transform.localPosition == home,
                "animator position restoration leaves the saved displayed offset intact");
            Call(motion, "LateUpdate");
            Check(Vector3.Distance(Get<Vector3>(motion, "motionOffset"), carried) < 0.015f
                && Get<Vector3>(motion, "motionOffset").x < -0.005f,
                "the first swing sample continues from anticipation instead of snapping to neutral");
            for (int i = 0; i < 5; i++) Call(motion, "LateUpdate");
            carried = Get<Vector3>(motion, "motionOffset");
            Check(carried.x > 0.02f, "swing advances into the forward release");
            animator.Attack();
            animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
            Check(Get<Vector3>(motion, "motionOffset") == carried, "release keeps the completed swing offset");
            Call(motion, "LateUpdate");
            Check(Vector3.Distance(Get<Vector3>(motion, "motionOffset"), carried) < 0.025f,
                "release starts without a position jump");
            carried = Get<Vector3>(motion, "motionOffset");
            float carriedAngle = Get<float>(motion, "motionAngle");
            animator.GuardianPose("Recover", EnemyAttack.SW01, false, false);
            Call(motion, "LateUpdate");
            Check(animator.CurrentPose == "FollowThrough"
                && Vector3.Distance(Get<Vector3>(motion, "motionOffset"), carried) < 0.025f
                && Mathf.Abs(Mathf.DeltaAngle(Get<float>(motion, "motionAngle"), carriedAngle)) < 3,
                "recovery carries the release displacement and lean into its settle");

            foreach (int facing in new[] { -1, 1 })
                foreach (string pose in new[] { "Idle", "Move", "Windup", "Slash", "Wave", "Guard", "Rising", "FollowThrough", "Hurt" })
                {
                    root.transform.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    Set(animator, "actionUntil", -1f);
                    animator.PlayPose(pose, 10);
                    animator.GuardianPose("Attack", EnemyAttack.SW01, false, false);
                    var sprite = picture.sprite;
                    float authoredFootY = image.transform.parent.TransformPoint(home
                        + Vector3.up * sprite.bounds.min.y * image.transform.localScale.y).y;
                    for (int i = 0; i < 4; i++)
                    {
                        Call(motion, "LateUpdate");
                        float displayedFootY = image.transform.TransformPoint(Vector3.up * sprite.bounds.min.y).y;
                        Check(!Property<bool>(motion, "HoverActive") && Get<Vector3>(motion, "motionOffset").y == 0
                            && Mathf.Abs(displayedFootY - authoredFootY) < 0.00001f,
                            "grounded " + pose + " keeps its authored foot height when facing " + facing);
                    }
                    Check(picture.sprite == sprite, "secondary motion retains the authored " + pose + " image");
                }

            GuardSpacingChecks(enemy, root, animator, motion, picture, home);
            FirstSpacingUpdateChecks(game);
            LocomotionFrameRateChecks(game, enemy, root, animator, motion, picture, home);

            // A defense flag by itself is insufficient to float a grounded boss.
            Set(enemy, "selected", EnemyAttack.SW05);
            Set(enemy, "fullShield", true);
            animator.PlayWavePhase("Guard");
            animator.GuardianPose("Attack", EnemyAttack.SW05, false, true);
            Call(motion, "LateUpdate");
            Check(!Property<bool>(motion, "HoverActive") && Get<Vector3>(motion, "motionOffset").y == 0,
                "full shield does not make a grounded SW05 guardian hover");
            enemy.transform.position += Vector3.up * KarmaSpace.Units(6);
            Set(enemy, "fullShield", false);
            animator.GuardianPose("Attack", EnemyAttack.SW05, true, false);
            Call(motion, "LateUpdate");
            Check(!Property<bool>(motion, "HoverActive"), "airborne ascent/descent without shield receives no extra hover");
            Set(enemy, "fullShield", true);
            animator.GuardianPose("Attack", EnemyAttack.SW05, true, true);
            bool movedVertically = false;
            for (int i = 0; i < 8; i++)
            {
                Call(motion, "LateUpdate");
                movedVertically |= Mathf.Abs(Get<Vector3>(motion, "motionOffset").y) > 0.0001f;
            }
            Check(Property<bool>(motion, "HoverActive") && movedVertically, "only the airborne full-shield phase receives restrained hover motion");
            animator.Hurt();
            animator.GuardianPose("Attack", EnemyAttack.SW05, true, true);
            Call(motion, "LateUpdate");
            Check(animator.CurrentPose == "Hurt" && !Property<bool>(motion, "HoverActive") && Get<Vector3>(motion, "motionOffset").y == 0,
                "hurt immediately overrides airborne shield hover");
            Set(enemy, "state", Enum.Parse(Field(enemy, "state").FieldType, "Stunned"));
            animator.GuardianPose("Stunned", EnemyAttack.SW05, true, true);
            Call(motion, "LateUpdate");
            Check(!Property<bool>(motion, "HoverActive") && image.transform.localPosition == home
                && image.transform.localRotation == Quaternion.identity
                && Get<Vector3>(motion, "motionVelocity") == Vector3.zero,
                "stun clears secondary offsets, lean and easing velocity immediately");
        }
        finally
        {
            Set(enemy, "selected", oldSelected); Set(enemy, "state", oldState); Set(enemy, "fullShield", oldShield);
            enemy.transform.position = oldPosition; enemy.transform.rotation = oldRotation;
            UnityEngine.Object.DestroyImmediate(root);
        }
        return assertions;
    }
}

[InitializeOnLoad]
public static class KarmaWaveMotionFocusedChecks
{
    const string Key = "K4RMA.WaveMotionFocusedChecks";
    static bool finishing;
    [Serializable] sealed class Report
    {
        public string result, scope, error;
        public int assertions;
    }
    static KarmaWaveMotionFocusedChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
    }
    public static void Run()
    {
        SessionState.SetBool(Key, true);
        Application.runInBackground = true;
        EditorApplication.delayCall += DeferPlayEntry;
    }
    static void DeferPlayEntry() { EditorApplication.delayCall += BeginPlay; }
    static void BeginPlay()
    {
        if (!SessionState.GetBool(Key, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        { EditorApplication.delayCall += BeginPlay; return; }
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(0, error); }
    }
    static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        try
        {
            Time.captureDeltaTime = 0.05f;
            var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            if (game == null) game = new GameObject("Focused Wave motion game").AddComponent<KarmaGame>();
            game.StartRun();
            game.Player.Essences.Sacrifice(Essence.SwordWave);
            game.Player.Essences.Sacrifice(Essence.RisingSlash);
            game.Player.Essences.Sacrifice(Essence.Guard);
            typeof(KarmaGame).GetMethod("LoadRoom", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true });
            game.Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var driver = new GameObject("Focused Wave motion check driver").AddComponent<KarmaWaveMotionFocusedDriver>();
            driver.Step = () =>
            {
                driver.Step = null;
                try { Finish(KarmaWaveMotionPolishChecks.Run(game, game.Enemies[0]), null); }
                catch (Exception error) { Finish(0, error); }
            };
        }
        catch (Exception error) { Finish(0, error); }
    }
    static void Finish(int assertions, Exception error)
    {
        if (finishing) return;
        finishing = true;
        SessionState.SetBool(Key, false);
        var report = new Report { result = error == null ? "PASS" : "FAIL", assertions = assertions,
            scope = "Focused Wave motion polish, production Update first-spacing frames, incoming-hit bounds and pose priority; no full pattern cycle.",
            error = error == null ? null : error.ToString() };
        File.WriteAllText("wave-motion-validation.json", JsonUtility.ToJson(report, true));
        string verdict = error == null ? "PASS: " + assertions + " focused Wave motion assertions" : "FAIL: " + error;
        File.WriteAllText("wave-motion-validation.txt", verdict);
        if (error == null) Debug.Log(verdict); else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
[DefaultExecutionOrder(-10000)]
public sealed class KarmaWaveMotionFocusedDriver : MonoBehaviour
{
    public Action Step;
    void Update() { Step?.Invoke(); }
}
#endif
