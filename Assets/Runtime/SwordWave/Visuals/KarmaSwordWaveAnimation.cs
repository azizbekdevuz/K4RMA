using UnityEngine;

namespace KarmaPrototype
{
    // SW01/SW02 cosmetics only. Imported Sprite subassets supply every frame;
    // attack movement, contacts and lifetime remain owned by KarmaBossAttack.
    [DefaultExecutionOrder(360)]
    public sealed class KarmaSwordWaveAnimation : MonoBehaviour
    {
        enum Playback { Charge, Flight, Burst, ChargeRelease }
        const int FrameCount = 5;
        static readonly int[] FlightDrawings = { 0, 3 };
        static readonly string[] Clips = { "Charge", "SW01Flight", "SW02Flight", "Impact" };
        static readonly Sprite[,] frames = new Sprite[4, FrameCount];
        static readonly System.Collections.Generic.Dictionary<KarmaEnemy, KarmaSwordWaveAnimation> charges =
            new System.Collections.Generic.Dictionary<KarmaEnemy, KarmaSwordWaveAnimation>();
        KarmaGame game;
        KarmaEnemy owner;
        SpriteRenderer picture, swordPicture;
        KarmaCharacterAnimator swordAnimator;
        Transform chargeScope;
        Playback playback;
        Vector2 size, velocity;
        float age, duration, nextEcho, chargeBodyHeight;
        int pattern, clip, first, count;
        bool heavy, aiming, terminal, basicRanged;
        KarmaSwordWaveEnergyPool energyPool;
        KarmaSwordWaveEnergyPool.Rig energy;
        KarmaSwordVfxPool echoes;

        static Sprite Frame(int clip, int frame)
        {
            var sprite = frames[clip, frame];
            if (sprite == null) sprite = KarmaArtwork.Sprite("SwordBossVFX", "SwordEnergy_" + Clips[clip] + "_" + frame);
            if (sprite == null) throw new System.InvalidOperationException("Missing SwordEnergy frame: " + Clips[clip] + "/" + frame);
            frames[clip, frame] = sprite;
            return sprite;
        }
        internal static Sprite BasicFlightDrawing => Frame(1, FlightDrawings[0]);
        void Initialize(KarmaGame director, KarmaEnemy enemy, SpriteRenderer renderer)
        {
            game = director; owner = enemy; picture = renderer; pattern = enemy.CurrentPattern;
            // Preload before release, through the existing boss sprite library.
            for (int c = 0; c < Clips.Length; c++)
                for (int f = 0; f < FrameCount; f++) Frame(c, f);
            KarmaVisuals.ApplyMaterial(picture);
            if (pattern == (int)EnemyAttack.SW01 || pattern == (int)EnemyAttack.SW02)
            {
                picture.color = Color.white;
                KarmaSwordSkillSurface.Apply(picture, KarmaSwordSkillSurface.SteelPalette);
            }
            else picture.color = KarmaSwordAttackPolish.WaveTint;
        }
        public static void Charge(KarmaGame game, KarmaEnemy owner, Transform art, Transform scope, float seconds, bool heavy, bool aiming = false, bool basicRanged = false)
        {
            // Explicit ranged mode: sharing SW01 flight art never grants a charge.
            if (owner.CurrentPattern != (int)EnemyAttack.SW01 && owner.CurrentPattern != (int)EnemyAttack.SW02
                && !(owner.CurrentPattern == (int)EnemyAttack.SWBasic && basicRanged)) return;
            StopCharge(owner);
            var node = new GameObject("Sword energy charge frames / no damage");
            node.transform.SetParent(game.World, false);
            var animation = node.AddComponent<KarmaSwordWaveAnimation>();
            animation.Initialize(game, owner, node.AddComponent<SpriteRenderer>());
            animation.playback = Playback.Charge; animation.duration = Mathf.Max(0.01f, seconds);
            animation.heavy = heavy; animation.aiming = aiming;
            animation.basicRanged = owner.CurrentPattern == (int)EnemyAttack.SWBasic && basicRanged;
            animation.chargeScope = scope;
            animation.chargeBodyHeight = Mathf.Max(2, owner.DamageBounds.size.y);
            charges[owner] = animation;
            var sword = art.Find("Guardian sprite");
            animation.swordPicture = sword != null ? sword.GetComponent<SpriteRenderer>() : null;
            animation.swordAnimator = sword != null ? sword.GetComponentInParent<KarmaCharacterAnimator>() : null;
            if (animation.swordAnimator != null) animation.chargeBodyHeight = animation.swordAnimator.SwordChargeBodyHeight;
            animation.picture.sortingLayerID = animation.swordPicture != null ? animation.swordPicture.sortingLayerID : 0;
            animation.picture.sortingOrder = animation.swordPicture != null ? animation.swordPicture.sortingOrder + 1 : 27;
            animation.size = heavy ? new Vector2(2.6f, 3.5f) : new Vector2(1.7f, 2.5f);
            animation.ApplyCharge();
            animation.AttachEnergy(SwordEnergyPhase.Charge);
        }
        public static void Flight(KarmaGame game, KarmaEnemy owner, KarmaBossAttack shot, Vector2 velocity, Vector2 size)
        {
            var animation = shot.gameObject.AddComponent<KarmaSwordWaveAnimation>();
            animation.Initialize(game, owner, shot.Visual);
            animation.playback = Playback.Flight; animation.velocity = velocity; animation.size = size;
            animation.heavy = owner.CurrentPattern == (int)EnemyAttack.SW02;
            animation.clip = animation.heavy ? 2 : 1;
            animation.echoes = KarmaSwordVfxPool.For(game);
            animation.ApplyFlight();
            if (animation.pattern != (int)EnemyAttack.SWBasic) animation.AttachEnergy(SwordEnergyPhase.Flight);
        }
        static KarmaSwordWaveAnimation Burst(KarmaGame game, KarmaEnemy owner, Vector2 at, Vector2 size, int first, int count, float seconds, bool flip, float angle = 0, bool heavy = false)
        {
            var node = new GameObject("Sword energy slash fragments / no damage");
            node.transform.SetParent(game.World, false); node.transform.position = at;
            node.transform.rotation = Quaternion.Euler(0, 0, angle);
            var animation = node.AddComponent<KarmaSwordWaveAnimation>();
            animation.Initialize(game, owner, node.AddComponent<SpriteRenderer>());
            animation.playback = Playback.Burst; animation.size = size;
            animation.first = first; animation.count = count; animation.duration = seconds;
            animation.heavy = heavy;
            animation.picture.sortingOrder = 37; animation.picture.flipX = flip;
            animation.Paint(3, first, size, new Vector2(0.74f, 0.74f));
            animation.AttachEnergy(first == 2 ? SwordEnergyPhase.Dissipate : count == 5 ? SwordEnergyPhase.Impact : SwordEnergyPhase.Release);
            return animation;
        }
        public static void Release(KarmaGame game, KarmaEnemy owner, Vector2 at, Vector2 size, bool heavy, float facing)
        {
            if (owner.CurrentPattern == (int)EnemyAttack.SW01 || owner.CurrentPattern == (int)EnemyAttack.SW02
                || (owner.CurrentPattern == (int)EnemyAttack.SWBasic && charges.TryGetValue(owner, out var charge) && charge.basicRanged))
                ReleaseCharge(owner, facing);
            else StopCharge(owner);
            bool basicRelease = owner.CurrentPattern == (int)EnemyAttack.SWBasic;
            var sword = owner.transform.Find("Artwork/Guardian sprite");
            var renderer = sword != null ? sword.GetComponent<SpriteRenderer>() : null;
            Vector2 anchor = BladePoints(renderer, out _, out _, out var tip) ? tip : at;
            var burst = Burst(game, owner, anchor, new Vector2(Mathf.Min(size.x, heavy ? 4.2f : 2.8f), Mathf.Min(size.y, heavy ? 4.7f : 3.2f)),
                0, 3, heavy ? 0.23f : 0.18f, facing < 0, heavy: heavy);
            bool downcut = basicRelease || at.y >= 4 || sword != null && sword.GetComponentInParent<KarmaCharacterAnimator>()?.SwordCarriesShield == true;
            // The discharge starts on the contact drawing; the projectile keeps its advertised path.
            burst.energy?.ConfigureRelease(anchor, heavy ? 0 : downcut ? -1 : 1);
            burst.DrawEnergy(0);
        }
        public static void Impact(KarmaGame game, KarmaEnemy owner, Vector2 at, bool heavy = true)
        {
            Burst(game, owner, at, heavy ? new Vector2(3.2f, 3.8f) : new Vector2(2.1f, 2.7f),
                0, 5, heavy ? 0.22f : 0.18f, owner.Facing < 0, 18, heavy);
        }
        void AttachEnergy(SwordEnergyPhase phase)
        {
            energyPool = KarmaSwordWaveEnergyPool.For(game);
            energy = energyPool.Rent(owner, phase, heavy, aiming, size, picture.flipX ? -1 : 1,
                transform.position, picture.sortingLayerID, picture.sortingOrder + 1);
            DrawEnergy(0);
        }
        void DrawEnergy(float dt)
        {
            if (energy == null) return;
            var camera = game.GameCamera;
            float unitsPerPixel = camera != null && camera.orthographic && camera.pixelHeight > 0
                ? 2 * camera.orthographicSize / camera.pixelHeight : 0;
            if (playback == Playback.Charge || playback == Playback.ChargeRelease)
            {
                if (!BladePoints(swordPicture, out var heel, out var bend, out var tip))
                {
                    heel = bend = tip = (Vector2)transform.position;
                }
                energy.ConfigureBlade(heel - (Vector2)transform.position, bend - (Vector2)transform.position,
                    tip - (Vector2)transform.position, unitsPerPixel, chargeBodyHeight, basicRanged, Mathf.Max(0, duration - age));
            }
            else energy.ConfigureVisual(picture.bounds, transform.position, unitsPerPixel);
            energy.Draw(transform.position, age, duration, dt);
        }
        void ReturnEnergy()
        {
            if (energyPool != null && energy != null) energyPool.Return(energy);
            energy = null;
        }
        void Paint(int sequence, int frame, Vector2 footprint, Vector2 coreFraction)
        {
            picture.sprite = Frame(sequence, frame);
            var bounds = picture.sprite.bounds.size;
            picture.transform.localScale = new Vector3(footprint.x / (bounds.x * coreFraction.x), footprint.y / (bounds.y * coreFraction.y), 1);
        }
        bool basicVisualOrigin;
        Vector2 basicFlightOrigin, basicBladeOffset;
        float basicJoinDistance;
        internal static Vector2 BasicTorsoOrigin(Vector2 sole, float height, float facing)
            => sole + new Vector2(Mathf.Sign(facing) * height * 0.18f, height * 0.55f);
        internal static Vector2 BasicLaunchOrigin(KarmaEnemy owner, Vector2 from, float facing)
        {
            var art = owner.transform.Find("Artwork");
            var animator = art != null ? art.GetComponent<KarmaCharacterAnimator>() : null;
            if (animator == null) return from;
            Vector2 sole = art.TransformPoint(animator.SwordVisualHome + animator.SwordFoot);
            Vector2 origin = BasicTorsoOrigin(sole, animator.SwordChargeBodyHeight, facing) + from - owner.Position;
            origin.x = Mathf.Clamp(origin.x, KarmaStage.ArenaLeft, KarmaStage.ArenaRight);
            return origin;
        }
        internal static void AlignBasicVisual(KarmaBossAttack shot)
        {
            var animation = shot.GetComponent<KarmaSwordWaveAnimation>();
            if (animation == null || animation.pattern != (int)EnemyAttack.SWBasic || animation.basicVisualOrigin) return;
            animation.basicVisualOrigin = true;
            var sword = animation.owner.transform.Find("Artwork/Guardian sprite");
            var renderer = sword != null ? sword.GetComponent<SpriteRenderer>() : null;
            if (BladePoints(renderer, out _, out var bend, out var tip))
            {
                Vector2 blade = Vector2.Lerp(bend, tip, 0.45f);
                blade.x = Mathf.Clamp(blade.x, KarmaStage.ArenaLeft, KarmaStage.ArenaRight);
                animation.basicFlightOrigin = shot.transform.position;
                animation.basicBladeOffset = blade - animation.basicFlightOrigin;
                // The quadratic blend can remove at most twice its offset per unit progress.
                // Give the shot enough travel to keep the visible edge moving forward.
                animation.basicJoinDistance = Mathf.Max(animation.velocity.magnitude * KarmaBootstrap.Step,
                    2.1f * animation.basicBladeOffset.magnitude);
                Vector2 axis = animation.velocity.normalized;
                float wall = axis.x < 0 ? KarmaStage.ArenaLeft : KarmaStage.ArenaRight;
                if (Mathf.Abs(wall - animation.basicFlightOrigin.x) < Mathf.Abs(axis.x) * animation.basicJoinDistance
                    && axis.y * animation.basicBladeOffset.y > 0)
                {
                    // At a wall horizontal motion stops. The vertical part must still advance.
                    animation.basicJoinDistance = Mathf.Max(animation.basicJoinDistance, 2.1f * animation.basicBladeOffset.y / axis.y);
                }
            }
            animation.ApplyBasicVisualOrigin();
        }
        void ApplyBasicVisualOrigin()
        {
            if (!basicVisualOrigin || basicJoinDistance <= 0) return;
            float travel = Vector2.Distance(basicFlightOrigin, transform.position);
            float remaining = 1 - Mathf.Clamp01(travel / basicJoinDistance);
            Vector3 shown = transform.position + (Vector3)(basicBladeOffset * (remaining * remaining));
            shown.x = Mathf.Clamp(shown.x, KarmaStage.ArenaLeft, KarmaStage.ArenaRight);
            picture.transform.position = shown;
        }
        void ApplyFlight()
        {
            // A fixed footprint and pivot keep the cutting edge legible at the
            // existing fast speed; only the authored energy shapes change.
            Paint(clip, FlightDrawings[(int)(age * KarmaBootstrap.FramesPerSecond) % FlightDrawings.Length], heavy ? size : new Vector2(size.x * 1.12f, size.y),
                heavy ? new Vector2(0.83f, 0.86f) : new Vector2(0.65f, 0.81f));
            picture.flipX = velocity.x < 0; picture.flipY = false;
            if (pattern == (int)EnemyAttack.SWBasic && velocity.sqrMagnitude > 0.000001f)
                picture.transform.rotation = Quaternion.Euler(0, 0, BasicFlightAngle(velocity));
            else picture.transform.localRotation = Quaternion.identity;
            ApplyBasicVisualOrigin();
        }
        internal static float BasicFlightAngle(Vector2 direction)
            => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - (direction.x < 0 ? 180 : 0);
        void ApplyCharge()
        {
            // No floating charge sprite: only the mesh sheath on the actual blade.
            picture.enabled = false;
            if (BladePoints(swordPicture, out var heel, out _, out _)) transform.position = heel;
        }

        // Endpoints are measured on the displayed 96-unit cell, before sprite and parent mirroring.
        // Sample the steel at the heel, curve and tip of each articulated combat pose.
        // The restored volley drawings have different curved blades from the general combat sheets.
        // Coordinates follow the middle of the steel at the guard, midpoint and tip, in a 96-unit cell.
        static readonly System.Collections.Generic.Dictionary<string, (Vector2 heel, Vector2 bend, Vector2 tip)> volleyBlades = new()
        {
            ["Wave/SW01_00"] = (new(32.85f, 30.41f), new(19.93f, 26.86f), new(6.76f, 24.36f)),
            ["Wave/SW01_01"] = (new(57.40f, 41.17f), new(74.11f, 41.75f), new(90.83f, 40.92f)),
            ["Wave/SW01_02"] = (new(62.45f, 40.16f), new(77.49f, 41.94f), new(92.66f, 42.75f)),
            ["Wave/SW01_03"] = (new(57.53f, 43.43f), new(71.84f, 45.41f), new(86.29f, 46.26f)),
            ["Wave/Windup_00"] = (new(35.42f, 56.16f), new(23.03f, 72.35f), new(9.83f, 87.92f)),
            ["Wave/FollowThrough_00"] = (new(36.60f, 54.58f), new(21.28f, 67.23f), new(4.68f, 78.30f)),
            ["WaveRise/SW01_00"] = (new(55.25f, 20.65f), new(42.41f, 15.28f), new(28.68f, 12.22f)),
            ["WaveRise/SW01_01"] = (new(62.77f, 49.70f), new(76.69f, 47.49f), new(90.26f, 43.31f)),
            ["WaveRise/SW01_02"] = (new(71.31f, 34.46f), new(80.64f, 33.58f), new(89.79f, 31.27f)),
            ["WaveRise/SW01_03"] = (new(63.59f, 62.73f), new(74.47f, 70.56f), new(86.51f, 76.57f)),
            ["WaveRise/Windup_00"] = (new(41.16f, 57.15f), new(24.14f, 71.32f), new(5.63f, 83.63f)),
            ["WaveRise/FollowThrough_00"] = (new(68.31f, 58.29f), new(80.12f, 67.20f), new(92.73f, 75.09f)),
            ["WaveWard/SW01_00"] = (new(42.12f, 20.06f), new(28.29f, 26.64f), new(16.53f, 36.27f)),
            ["WaveWard/SW01_01"] = (new(57.20f, 47.20f), new(72.35f, 46.10f), new(87.13f, 41.68f)),
            ["WaveWard/SW01_02"] = (new(61.73f, 31.81f), new(75.47f, 30.83f), new(89.04f, 28.63f)),
            ["WaveWard/SW01_03"] = (new(54.23f, 51.37f), new(70.13f, 55.26f), new(86.54f, 56.88f)),
            ["WaveWard/Windup_00"] = (new(32.91f, 59.51f), new(21.76f, 73.34f), new(9.29f, 86.06f)),
            ["WaveWard/FollowThrough_00"] = (new(33.67f, 54.59f), new(21.37f, 63.26f), new(7.33f, 68.13f)),
            ["WaveFinal/SW01_00"] = (new(42.66f, 17.09f), new(26.17f, 21.85f), new(10.96f, 29.72f)),
            ["WaveFinal/SW01_01"] = (new(59.68f, 44.17f), new(74.29f, 43.42f), new(88.75f, 41.15f)),
            ["WaveFinal/SW01_02"] = (new(67.09f, 28.69f), new(78.74f, 28.71f), new(90.37f, 28.19f)),
            ["WaveFinal/SW01_03"] = (new(65.41f, 59.29f), new(77.63f, 64.31f), new(90.48f, 67.74f)),
            ["WaveFinal/Windup_00"] = (new(39.32f, 59.27f), new(24.49f, 71.94f), new(8.80f, 83.61f)),
            ["WaveFinal/FollowThrough_00"] = (new(68.01f, 64.55f), new(79.22f, 74.47f), new(90.85f, 83.89f)),
        };
        static readonly System.Collections.Generic.Dictionary<string, (Vector2 heel, Vector2 bend, Vector2 tip)> combatBlades = new()
        {
            ["Wave/FollowThrough_00"] = (new(63.875f, 60.375f), new(73.125f, 69.000f), new(81.500f, 78.000f)),
            ["Wave/FollowThrough_01"] = (new(56.750f, 63.375f), new(62.125f, 74.750f), new(66.500f, 86.250f)),
            ["Wave/FollowThrough_02"] = (new(42.375f, 68.250f), new(34.500f, 78.625f), new(26.125f, 88.125f)),
            ["Wave/Guard_00"] = (new(46.625f, 46.875f), new(56.375f, 37.875f), new(66.625f, 29.875f)),
            ["Wave/Guard_01"] = (new(43.125f, 46.375f), new(51.000f, 35.375f), new(59.500f, 25.250f)),
            ["Wave/Guard_02"] = (new(40.500f, 46.375f), new(44.750f, 34.250f), new(49.875f, 22.875f)),
            ["Wave/SW01_00"] = (new(26.625f, 37.750f), new(17.375f, 39.875f), new(8.250f, 41.375f)),
            ["Wave/SW01_01"] = (new(46.875f, 44.500f), new(56.750f, 43.500f), new(66.375f, 43.125f)),
            ["Wave/SW01_02"] = (new(48.125f, 48.125f), new(57.375f, 48.250f), new(66.500f, 49.125f)),
            ["Wave/SW01_03"] = (new(45.125f, 55.250f), new(53.500f, 59.625f), new(61.375f, 64.500f)),
            ["Wave/SW02_00"] = (new(46.125f, 51.214f), new(52.125f, 43.714f), new(58.406f, 37.071f)),
            ["Wave/SW02_01"] = (new(45.750f, 51.964f), new(53.719f, 48.107f), new(61.688f, 45.000f)),
            ["Wave/SW02_02"] = (new(47.531f, 52.714f), new(55.969f, 51.536f), new(64.312f, 51.000f)),
            ["Wave/SW02_03"] = (new(54.094f, 50.357f), new(62.625f, 49.500f), new(70.969f, 49.393f)),
            ["Wave/SW02_04"] = (new(52.406f, 55.071f), new(60.562f, 56.571f), new(68.344f, 58.607f)),
            ["Wave/SW02_05"] = (new(46.875f, 59.786f), new(53.062f, 66.536f), new(58.594f, 73.607f)),
            ["Wave/SWBasic_00"] = (new(38.906f, 26.424f), new(44.623f, 16.494f), new(48.663f, 6.023f)),
            ["Wave/SWBasic_01"] = (new(48.641f, 49.016f), new(59.711f, 48.005f), new(70.537f, 47.016f)),
            ["Wave/SWBasic_02"] = (new(46.382f, 59.195f), new(56.482f, 63.460f), new(66.359f, 67.631f)),
            ["Wave/SWBasicRanged_00"] = (new(41.156f, 37.625f), new(38.625f, 31.125f), new(36.469f, 24.375f)),
            ["Wave/SWBasicRanged_01"] = (new(48.656f, 48.375f), new(54.188f, 50.375f), new(59.438f, 53.000f)),
            ["Wave/SWBasicRanged_02"] = (new(45.938f, 55.250f), new(49.219f, 61.625f), new(52.031f, 68.250f)),
            ["Wave/Windup_00"] = (new(39.625f, 68.000f), new(32.250f, 77.750f), new(24.250f, 86.750f)),
            ["Wave/Windup_01"] = (new(54.125f, 48.875f), new(63.125f, 40.625f), new(72.625f, 33.250f)),
            ["Wave/Windup_02"] = (new(61.875f, 32.250f), new(69.500f, 22.375f), new(77.625f, 13.375f)),
            ["WaveFinal/FollowThrough_00"] = (new(51.360f, 61.250f), new(60.000f, 70.125f), new(67.920f, 79.375f)),
            ["WaveFinal/FollowThrough_01"] = (new(45.000f, 65.125f), new(49.560f, 76.250f), new(53.400f, 87.375f)),
            ["WaveFinal/FollowThrough_02"] = (new(29.160f, 69.375f), new(20.760f, 77.250f), new(12.120f, 84.500f)),
            ["WaveFinal/Guard_00"] = (new(50.250f, 50.125f), new(59.625f, 42.250f), new(69.250f, 35.000f)),
            ["WaveFinal/Guard_01"] = (new(47.250f, 51.000f), new(54.750f, 41.375f), new(62.500f, 32.500f)),
            ["WaveFinal/Guard_02"] = (new(44.750f, 49.750f), new(49.125f, 38.625f), new(54.125f, 28.125f)),
            ["WaveFinal/SW01_00"] = (new(26.875f, 40.000f), new(16.375f, 42.000f), new(6.000f, 43.500f)),
            ["WaveFinal/SW01_01"] = (new(40.875f, 38.250f), new(49.375f, 31.375f), new(58.250f, 25.375f)),
            ["WaveFinal/SW01_02"] = (new(47.375f, 49.125f), new(57.625f, 48.375f), new(67.750f, 48.250f)),
            ["WaveFinal/SW01_03"] = (new(46.750f, 57.375f), new(56.000f, 62.625f), new(64.750f, 68.375f)),
            ["WaveFinal/SW02_00"] = (new(44.719f, 58.714f), new(50.250f, 52.393f), new(55.969f, 46.500f)),
            ["WaveFinal/SW02_01"] = (new(44.531f, 58.179f), new(52.031f, 54.857f), new(59.531f, 52.071f)),
            ["WaveFinal/SW02_02"] = (new(46.125f, 58.714f), new(54.000f, 57.857f), new(61.781f, 57.643f)),
            ["WaveFinal/SW02_03"] = (new(51.656f, 57.857f), new(59.250f, 57.321f), new(66.656f, 57.429f)),
            ["WaveFinal/SW02_04"] = (new(50.719f, 60.000f), new(58.500f, 61.607f), new(65.906f, 63.857f)),
            ["WaveFinal/SW02_05"] = (new(45.844f, 64.821f), new(51.375f, 71.357f), new(56.438f, 78.107f)),
            ["WaveFinal/SW03_00"] = (new(55.375f, 52.375f), new(56.375f, 63.125f), new(56.750f, 73.875f)),
            ["WaveFinal/SW03_01"] = (new(53.500f, 47.375f), new(55.125f, 58.000f), new(56.000f, 68.375f)),
            ["WaveFinal/SW03_02"] = (new(56.125f, 54.125f), new(57.875f, 64.625f), new(58.875f, 75.000f)),
            ["WaveFinal/SW03_03"] = (new(57.875f, 61.000f), new(61.750f, 71.375f), new(64.875f, 81.875f)),
            ["WaveFinal/SW04_00"] = (new(45.125f, 49.625f), new(52.375f, 40.250f), new(60.000f, 31.625f)),
            ["WaveFinal/SW04_01"] = (new(42.625f, 48.375f), new(47.000f, 37.625f), new(51.875f, 27.250f)),
            ["WaveFinal/SW04_02"] = (new(55.875f, 49.125f), new(66.250f, 45.125f), new(76.750f, 41.750f)),
            ["WaveFinal/SW04_03"] = (new(59.625f, 48.500f), new(70.625f, 46.375f), new(81.375f, 45.000f)),
            ["WaveFinal/SW05_00"] = (new(51.750f, 42.000f), new(61.375f, 35.250f), new(71.250f, 29.125f)),
            ["WaveFinal/SW05_01"] = (new(41.000f, 35.250f), new(31.875f, 28.125f), new(23.250f, 20.625f)),
            ["WaveFinal/SW05_02"] = (new(55.250f, 45.125f), new(66.000f, 43.250f), new(76.625f, 42.250f)),
            ["WaveFinal/SW05_03"] = (new(55.500f, 65.625f), new(60.500f, 75.625f), new(64.750f, 85.750f)),
            ["WaveFinal/SWBasic_00"] = (new(46.270f, 36.800f), new(49.885f, 27.900f), new(52.348f, 18.545f)),
            ["WaveFinal/SWBasic_01"] = (new(53.170f, 55.919f), new(60.631f, 54.893f), new(67.928f, 53.891f)),
            ["WaveFinal/SWBasic_02"] = (new(51.914f, 63.723f), new(59.197f, 68.119f), new(66.319f, 72.419f)),
            ["WaveFinal/SWBasicRanged_00"] = (new(41.156f, 42.125f), new(38.156f, 33.750f), new(35.625f, 25.375f)),
            ["WaveFinal/SWBasicRanged_01"] = (new(50.344f, 53.250f), new(57.469f, 56.250f), new(64.125f, 59.750f)),
            ["WaveFinal/SWBasicRanged_02"] = (new(47.438f, 60.375f), new(51.750f, 69.375f), new(55.500f, 78.625f)),
            ["WaveFinal/Windup_00"] = (new(38.250f, 69.250f), new(28.875f, 77.625f), new(19.375f, 85.250f)),
            ["WaveFinal/Windup_01"] = (new(56.125f, 51.875f), new(65.750f, 43.750f), new(75.625f, 36.375f)),
            ["WaveFinal/Windup_02"] = (new(63.875f, 32.375f), new(72.625f, 21.750f), new(81.875f, 12.000f)),
            ["WaveRise/FollowThrough_00"] = (new(55.375f, 58.250f), new(65.125f, 67.125f), new(74.125f, 76.375f)),
            ["WaveRise/FollowThrough_01"] = (new(49.250f, 62.375f), new(54.625f, 73.375f), new(59.000f, 84.375f)),
            ["WaveRise/FollowThrough_02"] = (new(33.875f, 65.500f), new(24.000f, 73.000f), new(14.000f, 79.750f)),
            ["WaveRise/Guard_00"] = (new(47.000f, 51.250f), new(57.375f, 41.750f), new(68.125f, 33.000f)),
            ["WaveRise/Guard_01"] = (new(44.000f, 51.125f), new(52.250f, 39.250f), new(61.000f, 28.625f)),
            ["WaveRise/Guard_02"] = (new(41.500f, 51.125f), new(45.875f, 38.250f), new(51.125f, 26.125f)),
            ["WaveRise/SW01_00"] = (new(29.750f, 38.625f), new(17.250f, 41.750f), new(4.875f, 43.875f)),
            ["WaveRise/SW01_01"] = (new(49.750f, 52.000f), new(60.500f, 50.625f), new(71.125f, 50.125f)),
            ["WaveRise/SW01_02"] = (new(52.500f, 51.125f), new(64.375f, 51.125f), new(75.875f, 51.875f)),
            ["WaveRise/SW01_03"] = (new(49.625f, 58.625f), new(60.125f, 64.000f), new(70.125f, 70.000f)),
            ["WaveRise/SW02_00"] = (new(46.219f, 63.643f), new(51.188f, 57.214f), new(56.344f, 51.643f)),
            ["WaveRise/SW02_01"] = (new(46.219f, 62.143f), new(53.438f, 58.500f), new(60.656f, 55.500f)),
            ["WaveRise/SW02_02"] = (new(47.344f, 63.214f), new(54.844f, 61.929f), new(62.156f, 61.393f)),
            ["WaveRise/SW02_03"] = (new(52.500f, 60.643f), new(60.281f, 59.571f), new(67.969f, 59.464f)),
            ["WaveRise/SW02_04"] = (new(51.375f, 63.750f), new(58.969f, 64.929f), new(66.188f, 66.750f)),
            ["WaveRise/SW02_05"] = (new(47.156f, 67.929f), new(52.781f, 73.821f), new(57.938f, 80.250f)),
            ["WaveRise/SW03_00"] = (new(50.000f, 54.125f), new(51.625f, 65.750f), new(52.375f, 77.125f)),
            ["WaveRise/SW03_01"] = (new(48.500f, 48.625f), new(50.750f, 60.125f), new(52.125f, 71.500f)),
            ["WaveRise/SW03_02"] = (new(50.625f, 56.250f), new(52.875f, 67.250f), new(54.375f, 78.000f)),
            ["WaveRise/SW03_03"] = (new(52.125f, 60.000f), new(56.625f, 70.375f), new(60.125f, 80.875f)),
            ["WaveRise/SWBasic_00"] = (new(47.183f, 36.547f), new(51.142f, 27.240f), new(53.954f, 17.453f)),
            ["WaveRise/SWBasic_01"] = (new(54.478f, 55.240f), new(61.973f, 54.440f), new(69.302f, 53.659f)),
            ["WaveRise/SWBasic_02"] = (new(52.225f, 62.569f), new(58.992f, 66.580f), new(65.610f, 70.501f)),
            ["WaveRise/SWBasicRanged_00"] = (new(40.250f, 38.875f), new(35.000f, 28.750f), new(30.625f, 19.000f)),
            ["WaveRise/SWBasicRanged_01"] = (new(54.500f, 47.875f), new(67.000f, 51.250f), new(79.000f, 55.375f)),
            ["WaveRise/SWBasicRanged_02"] = (new(47.875f, 61.750f), new(53.875f, 70.250f), new(59.125f, 79.250f)),
            ["WaveRise/Windup_00"] = (new(42.125f, 68.500f), new(31.375f, 76.750f), new(20.500f, 84.000f)),
            ["WaveRise/Windup_01"] = (new(59.000f, 52.625f), new(68.750f, 43.500f), new(79.000f, 35.250f)),
            ["WaveRise/Windup_02"] = (new(65.500f, 40.625f), new(73.250f, 30.125f), new(81.750f, 20.625f)),
            ["WaveWard/FollowThrough_00"] = (new(62.250f, 59.625f), new(70.500f, 66.875f), new(77.750f, 74.750f)),
            ["WaveWard/FollowThrough_01"] = (new(55.250f, 62.000f), new(60.375f, 72.000f), new(64.125f, 82.125f)),
            ["WaveWard/FollowThrough_02"] = (new(40.250f, 67.500f), new(33.000f, 76.125f), new(25.125f, 83.500f)),
            ["WaveWard/Guard_00"] = (new(47.000f, 49.125f), new(55.500f, 40.625f), new(64.750f, 33.750f)),
            ["WaveWard/Guard_01"] = (new(44.000f, 50.750f), new(50.375f, 41.250f), new(57.625f, 32.875f)),
            ["WaveWard/Guard_02"] = (new(41.375f, 49.500f), new(44.875f, 38.750f), new(49.375f, 28.875f)),
            ["WaveWard/SW01_00"] = (new(25.375f, 38.375f), new(16.125f, 40.875f), new(6.875f, 42.250f)),
            ["WaveWard/SW01_01"] = (new(39.500f, 37.875f), new(46.625f, 31.250f), new(54.500f, 25.750f)),
            ["WaveWard/SW01_02"] = (new(46.625f, 47.875f), new(56.125f, 46.500f), new(65.375f, 46.250f)),
            ["WaveWard/SW01_03"] = (new(44.750f, 59.250f), new(52.875f, 63.250f), new(60.375f, 68.000f)),
            ["WaveWard/SW02_00"] = (new(45.094f, 54.429f), new(50.156f, 47.571f), new(55.781f, 41.893f)),
            ["WaveWard/SW02_01"] = (new(44.719f, 54.321f), new(51.750f, 50.571f), new(58.969f, 47.786f)),
            ["WaveWard/SW02_02"] = (new(46.406f, 54.964f), new(54.000f, 53.571f), new(61.406f, 53.250f)),
            ["WaveWard/SW02_03"] = (new(52.781f, 53.679f), new(60.094f, 52.393f), new(67.312f, 52.500f)),
            ["WaveWard/SW02_04"] = (new(51.281f, 57.429f), new(58.594f, 58.286f), new(65.438f, 60.321f)),
            ["WaveWard/SW02_05"] = (new(45.656f, 62.250f), new(51.375f, 67.821f), new(56.156f, 74.250f)),
            ["WaveWard/SW04_00"] = (new(38.250f, 42.125f), new(45.500f, 31.250f), new(53.750f, 21.750f)),
            ["WaveWard/SW04_01"] = (new(35.625f, 41.750f), new(39.375f, 29.875f), new(44.375f, 19.125f)),
            ["WaveWard/SW04_02"] = (new(51.875f, 42.250f), new(62.750f, 37.250f), new(73.750f, 33.500f)),
            ["WaveWard/SW04_03"] = (new(56.875f, 40.375f), new(68.625f, 36.875f), new(80.375f, 35.375f)),
            ["WaveWard/SWBasic_00"] = (new(44.903f, 37.158f), new(48.232f, 29.194f), new(50.416f, 20.751f)),
            ["WaveWard/SWBasic_01"] = (new(51.577f, 55.104f), new(58.267f, 54.199f), new(64.810f, 53.315f)),
            ["WaveWard/SWBasic_02"] = (new(48.779f, 63.488f), new(54.527f, 67.115f), new(60.149f, 70.662f)),
            ["WaveWard/SWBasicRanged_00"] = (new(39.500f, 37.250f), new(34.500f, 28.250f), new(30.750f, 19.250f)),
            ["WaveWard/SWBasicRanged_01"] = (new(53.750f, 53.875f), new(63.875f, 56.500f), new(73.500f, 60.000f)),
            ["WaveWard/SWBasicRanged_02"] = (new(47.875f, 65.875f), new(53.750f, 74.000f), new(58.500f, 82.375f)),
            ["WaveWard/Windup_00"] = (new(37.000f, 69.250f), new(29.500f, 78.125f), new(21.250f, 85.875f)),
            ["WaveWard/Windup_01"] = (new(53.500f, 48.750f), new(62.125f, 40.500f), new(71.375f, 33.375f)),
            ["WaveWard/Windup_02"] = (new(61.625f, 35.000f), new(68.250f, 25.875f), new(75.625f, 17.750f)),
        };
        static System.Collections.Generic.Dictionary<string, Vector4> chargeBladeSockets;
        static bool TryBladeEndpoints(string key, out Vector4 endpoints)
        {
            if (chargeBladeSockets == null)
            {
                chargeBladeSockets = new System.Collections.Generic.Dictionary<string, Vector4>();
                foreach (var line in Resources.Load<TextAsset>("Motions12/BladeSockets").text.Split(new[] {'\n'}, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = line.Split('\t');
                    chargeBladeSockets.Add(f[0], new Vector4(int.Parse(f[1]), int.Parse(f[2]), int.Parse(f[3]), int.Parse(f[4])));
                }
            }
            return chargeBladeSockets.TryGetValue(key, out endpoints);
        }
        static Vector2 ChargeBladePoint(SpriteRenderer renderer, Vector2 socket)
        {
            var sprite = renderer.sprite;
            var pixel = new Vector2(socket.x / 96 * sprite.rect.width, (1 - socket.y / 96) * sprite.rect.height);
            Vector2 local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
            if (renderer.flipX) local.x = -local.x;
            if (renderer.flipY) local.y = -local.y;
            return renderer.transform.TransformPoint(local);
        }

        internal static bool BladePoints(SpriteRenderer renderer, out Vector2 heel, out Vector2 bend, out Vector2 tip)
        {
            heel = bend = tip = default;
            if (renderer == null || renderer.sprite == null) return false;
            var sprite = renderer.sprite;
            var animator = renderer.GetComponentInParent<KarmaCharacterAnimator>();
            if (animator == null) return false;
            if (animator.SwordGroundBladePoints(out heel, out bend, out tip)) return true;
            if (animator.SwordBasicBladePoints(out heel, out bend, out tip)) return true;
            if (animator.SwordCastBladePoints(out heel, out bend, out tip)) return true;
            string key = animator.MotionForm + "/" + sprite.name;
            var drawings = animator.IsVolleyDrawing(sprite) ? volleyBlades : combatBlades;
            if (drawings.TryGetValue(key, out var cut))
            {
                heel = ChargeBladePoint(renderer, cut.heel);
                bend = ChargeBladePoint(renderer, cut.bend);
                tip = ChargeBladePoint(renderer, cut.tip);
                return true;
            }
            if (!TryBladeEndpoints(key, out var ends)) return false;
            heel = ChargeBladePoint(renderer, new Vector2(ends.x, ends.y));
            tip = ChargeBladePoint(renderer, new Vector2(ends.z, ends.w));
            bend = Vector2.Lerp(heel, tip, 0.5f);
            return true;
        }

        void LateUpdate()
        {
            if (game == null || owner == null || !owner.PresentationActive || owner.Stunned || game.Phase != RunPhase.Combat)
            { terminal = true; ReturnEnergy(); if (playback == Playback.Flight) enabled = false; else Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (playback == Playback.Charge)
            {
                if (owner.CurrentPattern != pattern || chargeScope == null || !chargeScope.gameObject.activeInHierarchy)
                { ReturnEnergy(); gameObject.SetActive(false); Destroy(gameObject); return; }
                ApplyCharge();
            }
            else if (playback == Playback.ChargeRelease)
            {
                if (owner.CurrentPattern != pattern || age >= duration
                    || !basicRanged && swordAnimator != null && swordAnimator.SwordBodyAction == null)
                { ReturnEnergy(); gameObject.SetActive(false); Destroy(gameObject); return; }
                ApplyCharge();
            }
            else if (playback == Playback.Flight)
            {
                ApplyFlight();
                if (age >= nextEcho)
                {
                    echoes.Echo(owner, picture, pattern == (int)EnemyAttack.SWBasic ? 0.22f : heavy ? 0.38f : 0.28f, heavy ? 0.11f : 0.075f, transform);
                    // Softer echoes leave the leading blade dominant.
                    nextEcho = age + 1f / 24;
                }
            }
            else
            {
                if (age >= duration) { Destroy(gameObject); return; }
                Paint(3, first + Mathf.Min(count - 1, (int)(age / duration * count)), size, new Vector2(0.74f, 0.74f));
            }
            DrawEnergy(Time.deltaTime);
        }
        void OnDisable()
        {
            ReturnEnergy();
            if (echoes != null) echoes.Cancel(transform);
            if (playback != Playback.Flight || terminal || game == null || owner == null || !owner.PresentationActive
                || owner.Stunned || game.Phase != RunPhase.Combat || !game.World.gameObject.activeInHierarchy) return;
            // Only an actual arena-boundary completion breaks into fragments.
            // Interrupting a projectile must not synthesize a hit or release.
            float edge = velocity.x > 0 ? KarmaStage.ArenaRight : KarmaStage.ArenaLeft;
            float endpoint = edge - (heavy ? Mathf.Sign(velocity.x) * size.x / 2 : 0);
            if (Mathf.Abs(transform.position.x - endpoint) > 0.05f) return;
            terminal = true;
            Vector2 at = transform.position;
            at.x = Mathf.Clamp(at.x, KarmaStage.ArenaLeft + size.x * 0.35f, KarmaStage.ArenaRight - size.x * 0.35f);
            Burst(game, owner, at, size * 0.7f, 2, 3, heavy ? 0.23f : 0.20f, velocity.x < 0, heavy: heavy);
        }
        static void ReleaseCharge(KarmaEnemy owner, float facing)
        {
            if (!charges.TryGetValue(owner, out var charge)) return;
            charges.Remove(owner);
            if (charge == null) return;
            // Reuse this charge's rig and buffers. Only cosmetic time continues;
            // the projectile is already released on the unchanged attack event.
            charge.playback = Playback.ChargeRelease;
            charge.age = 0; charge.duration = charge.heavy ? 0.20f : 0.16f;
            charge.chargeScope = null;
            charge.energy?.BeginChargeRelease(facing);
            charge.ApplyCharge(); charge.DrawEnergy(0);
        }
        static void StopCharge(KarmaEnemy owner)
        {
            if (!charges.TryGetValue(owner, out var charge)) return;
            charges.Remove(owner);
            if (charge == null) return;
            charge.gameObject.SetActive(false);
            Destroy(charge.gameObject);
        }
        void OnDestroy()
        {
            ReturnEnergy();
            if (owner != null && charges.TryGetValue(owner, out var charge) && charge == this) charges.Remove(owner);
        }
    }

    enum SwordEnergyPhase { Charge, Flight, Release, Impact, Dissipate, ChargeRelease }

    // Only SW01/SW02 rent these rigs. One additive draw per rig combines soft
    // blade-local lightning, directional cuts and a bounded spark simulation.
    // All buffers are allocated during the first charge, never during flight.
    [DefaultExecutionOrder(370)]
    sealed class KarmaSwordWaveEnergyPool : MonoBehaviour
    {
        const int Capacity = 8;
        readonly Rig[] rigs = new Rig[Capacity];
        KarmaGame game;
        uint serial;
        public static KarmaSwordWaveEnergyPool For(KarmaGame game)
        {
            var pool = game.World.GetComponent<KarmaSwordWaveEnergyPool>();
            if (pool != null) return pool;
            pool = game.World.gameObject.AddComponent<KarmaSwordWaveEnergyPool>();
            pool.game = game;
            for (int i = 0; i < Capacity; i++) pool.rigs[i] = new Rig(game.World, i);
            return pool;
        }
        public Rig Rent(KarmaEnemy owner, SwordEnergyPhase phase, bool heavy, bool aiming, Vector2 size,
            float facing, Vector3 at, int layer, int order)
        {
            foreach (var rig in rigs)
                if (!rig.Active)
                {
                    rig.Begin(owner, phase, heavy, aiming, size, facing, at, layer, order, ++serial);
                    return rig;
                }
            // Keep existing sprites readable if an unusually crowded frame
            // exhausts the cosmetic budget; never evict a live attack's cue.
            return null;
        }
        public void Return(Rig rig) { rig.Stop(); }
        void LateUpdate()
        {
            foreach (var rig in rigs)
                if (rig != null && rig.Active && (game == null || game.Phase != RunPhase.Combat
                    || rig.Owner == null || !rig.Owner.PresentationActive || rig.Owner.Stunned)) rig.Stop();
        }
        void OnDisable() { foreach (var rig in rigs) if (rig != null) rig.Stop(); }
        void OnDestroy() { foreach (var rig in rigs) if (rig != null) rig.Dispose(); }

        public sealed class Rig
        {
            const int Strokes = 96, Points = 13, Lanes = 4, MoteCount = 40;
            const int VerticesPerStroke = Points * Lanes;
            readonly Vector3[] vertices = new Vector3[Strokes * VerticesPerStroke];
            readonly Color[] colors = new Color[Strokes * VerticesPerStroke];
            readonly Vector2[] path = new Vector2[Points];
            readonly Mote[] motes = new Mote[MoteCount];
            readonly GameObject node;
            readonly Mesh mesh;
            readonly MeshRenderer renderer;
            readonly MaterialPropertyBlock chargeMaterialProperties;
            SwordEnergyPhase phase;
            Vector2 size;
            Vector2 electricSize, electricOffset;
            float electricWidth;
            uint arcSeed;
            Vector2 swordOffset;
            Vector3 center;
            float facing, age, power, nextSpark, slashSign;
            bool heavy, aiming;
            int stroke, cursor;
            uint seed;
            public bool Active { get; private set; }
            public KarmaEnemy Owner { get; private set; }
            struct Mote { public Vector2 at, velocity; public float age, life, length, width, spin; }
            Color edgeColor, coreColor;

            public Rig(Transform root, int index)
            {
                node = new GameObject("SW01 SW02 pooled electricity " + index);
                node.transform.SetParent(root, false);
                mesh = new Mesh { name = "Sword energy ribbon buffer" }; mesh.MarkDynamic();
                mesh.vertices = vertices; mesh.colors = colors;
                var indices = new int[Strokes * (Points - 1) * (Lanes - 1) * 6];
                int n = 0;
                for (int s = 0; s < Strokes; s++)
                    for (int p = 0; p < Points - 1; p++)
                        for (int lane = 0; lane < Lanes - 1; lane++)
                        {
                            int v = s * VerticesPerStroke + p * Lanes + lane;
                            indices[n++] = v; indices[n++] = v + Lanes; indices[n++] = v + 1;
                            indices[n++] = v + 1; indices[n++] = v + Lanes; indices[n++] = v + Lanes + 1;
                        }
                mesh.triangles = indices;
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(42, 34, 2));
                node.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // The charge points are already mirrored in world space by the live sprite transform.
                // This mesh shares a sprite shader, so make its own shader flip neutral.
                chargeMaterialProperties = new MaterialPropertyBlock();
                chargeMaterialProperties.SetVector("unity_SpriteProps", new Vector4(1, 1, 0, 0));
                chargeMaterialProperties.SetColor("unity_SpriteColor", Color.white);
                node.SetActive(false);
            }
            public void Begin(KarmaEnemy owner, SwordEnergyPhase mode, bool strong, bool tracking, Vector2 footprint,
                float direction, Vector3 at, int layer, int order, uint serial)
            {
                Owner = owner; phase = mode; heavy = strong; aiming = tracking; size = footprint; facing = direction;
                bool steel = owner.CurrentPattern == (int)EnemyAttack.SW01 || owner.CurrentPattern == (int)EnemyAttack.SW02;
                edgeColor = steel ? new Color(0.129f, 0.331f, 0.662f) : new Color(0.34f, 0.65f, 0.96f);
                coreColor = steel ? new Color(0.745f, 0.837f, 0.92f) : new Color(0.92f, 1.02f, 1.14f);
                seed = serial * 747796405u; arcSeed = seed; age = nextSpark = 0; cursor = 0; center = at;
                electricSize = size; electricOffset = Vector2.zero; electricWidth = size.y * 0.02f;
                swordOffset = Vector2.zero; slashSign = 1;
                System.Array.Clear(motes, 0, motes.Length);
                renderer.sortingLayerID = layer; renderer.sortingOrder = order;
                renderer.SetPropertyBlock(phase == SwordEnergyPhase.Charge ? chargeMaterialProperties : null);
                node.transform.position = at; node.SetActive(true); Active = true;
                if (phase == SwordEnergyPhase.Release || phase == SwordEnergyPhase.Impact || phase == SwordEnergyPhase.Dissipate)
                    Emit(phase == SwordEnergyPhase.Dissipate ? (heavy ? 16 : 9) : (heavy ? 18 : 11),
                        phase == SwordEnergyPhase.Dissipate ? 2.8f : heavy ? 4.2f : 3.2f);
            }
            public void ConfigureVisual(Bounds bounds, Vector3 origin, float unitsPerPixel)
            {
                // Measure the unchanged SpriteRenderer, including its parent scale.
                // Match Paint's authored core fractions; transparent margins are not blade width.
                if (phase == SwordEnergyPhase.Charge || phase == SwordEnergyPhase.ChargeRelease) return;
                Vector2 fraction = phase == SwordEnergyPhase.Flight
                    ? (heavy ? new Vector2(0.83f, 0.86f) : new Vector2(0.65f, 0.81f)) : new Vector2(0.74f, 0.74f);
                electricSize = Vector2.Scale((Vector2)bounds.size, fraction);
                electricOffset = (Vector2)(bounds.center - origin);
                // Screen-readable electrical edges, bounded by the existing blade.
                electricWidth = Mathf.Min(electricSize.y * 0.05f,
                    Mathf.Max(electricSize.y * (heavy ? 0.030f : 0.024f), unitsPerPixel * (heavy ? 3.5f : 2.8f)));
            }
            float CrackleFlicker(int line, int point)
            {
                uint tick = (uint)(age * 24 + line * 0.37f);
                return Noise(arcSeed + (uint)(line * 199 + point * 31) + tick * 977) * 2 - 1;
            }
            Vector2 ElectricEdge(float t)
            {
                float y = t * 2 - 1;
                if (heavy && phase == SwordEnergyPhase.Flight)
                    return electricOffset + new Vector2(facing * electricSize.x * (0.38f - 0.74f * y * y), electricSize.y * y * 0.42f);
                return electricOffset + new Vector2(facing * electricSize.x * (0.395f - 0.54f * y * y),
                    electricSize.y * y * 0.48f);
            }
            public void ConfigureRelease(Vector2 sword, float sweep)
            { swordOffset = sword - (Vector2)center; slashSign = sweep; }
            // A local hash supplies flicker and varied particles without consuming
            // UnityEngine.Random (which owns the independent SW01 attack order).
            static float Noise(uint value)
            {
                unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16; }
                return (value & 0xffff) / 65535f;
            }
            float Flicker(int line, int point)
            { return Noise(seed + (uint)(line * 199 + point * 31 + (int)(age * (heavy ? 42 : 35)) * 977)) * 2 - 1; }
            float Sample() { return Noise(++seed); }
            public void Draw(Vector3 at, float seconds, float duration, float dt)
            {
                if (!Active) return;
                if (phase == SwordEnergyPhase.Charge && Owner != null) facing = Owner.Facing;
                center = at; age = seconds; node.transform.position = center;
                stroke = 0; System.Array.Clear(colors, 0, colors.Length);
                power = heavy ? 1 : 0.72f;
                if (phase == SwordEnergyPhase.Charge) Charge(Mathf.Clamp01(age / duration));
                else if (phase == SwordEnergyPhase.ChargeRelease) ChargeRelease(Mathf.Clamp01(age / duration));
                else if (phase == SwordEnergyPhase.Flight) Flight();
                else Burst(Mathf.Clamp01(age / duration));
                DrawMotes(dt);
                if (phase == SwordEnergyPhase.Charge || phase == SwordEnergyPhase.ChargeRelease)
                    ClipChargeToArena();
                mesh.SetVertices(vertices, 0, vertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetColors(colors);
                // Charge bounds follow its hand anchor and include outer intake
                // bolts plus the directional release. Flight bounds stay unchanged.
                var bounds = phase == SwordEnergyPhase.Charge || phase == SwordEnergyPhase.ChargeRelease
                    ? (smallCharge ? new Bounds(ChargeCenter(), Vector3.one * (ChargeRadius(1) * 6.4f))
                        : new Bounds((Vector3)((bladeHeel+bladeTip)*0.5f), Vector3.one * (Vector2.Distance(bladeHeel,bladeTip)+chargeBodyHeight*0.6f)))
                    : new Bounds(Vector3.zero, new Vector3(42, 34, 2));
                // The fixed buffers are reused; only occupied ribbons are drawn.
                mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, stroke * (Points - 1) * (Lanes - 1) * 6)
                    { firstVertex = 0, vertexCount = vertices.Length, bounds = bounds },
                    UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
                // Explicit bounds avoid a scan over invisible buffer slots.
                mesh.bounds = bounds;
            }
            void ClipChargeToArena()
            {
                // Sword anchors stay attached to the pose. Trim only electricity
                // at the physical walls, including its intake and release wisps.
                float left = KarmaStage.ArenaLeft - center.x, right = KarmaStage.ArenaRight - center.x;
                for (int i = 0; i < stroke * VerticesPerStroke; i++)
                {
                    var p = vertices[i];
                    colors[i].a *= Mathf.Clamp01(Mathf.Min(p.x - left, right - p.x) / 0.18f);
                    p.x = Mathf.Clamp(p.x, left, right);
                    vertices[i] = p;
                }
            }
            void Ribbon(float width, Color color, bool angular = false, float core = 0.16f)
            {
                if (stroke >= Strokes) return;
                int start = stroke++ * VerticesPerStroke;
                for (int p = 0; p < Points; p++)
                {
                    Vector2 tangent = path[Mathf.Min(p + 1, Points - 1)] - path[Mathf.Max(0, p - 1)];
                    float t = p / (float)(Points - 1);
                    Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * width
                        * (angular ? 1 - Mathf.Abs(t * 2 - 1) : 0.18f + Mathf.Sin(t * Mathf.PI) * 0.82f);
                    float fade = Mathf.Sin(t * Mathf.PI);
                    for (int lane = 0; lane < Lanes; lane++)
                    {
                        float offset = lane == 0 ? -1 : lane == 1 ? -core : lane == 2 ? core : 1;
                        int v = start + p * Lanes + lane;
                        vertices[v] = path[p] + normal * offset;
                        colors[v] = new Color(color.r, color.g, color.b, lane == 0 || lane == 3 ? 0 : color.a * fade);
                    }
                }
            }
            void Segment(Vector2 from, Vector2 to, float width, Color color, float jitter = 0, int key = 0, bool angular = false)
            {
                Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized;
                for (int p = 0; p < Points; p++)
                {
                    float t = p / (float)(Points - 1);
                    path[p] = Vector2.Lerp(from, to, t) + normal * (Flicker(key, p) * jitter * Mathf.Sin(t * Mathf.PI));
                }
                Ribbon(width, color, angular);
            }
            static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
            void ElectricRibbon(float width, float alpha)
            {
                // A broad azure sheath and a separate crisp white cutting core.
                // Their silhouettes taper independently instead of flattening
                // the sprite into one uniformly overexposed glow.
                Ribbon(width * 2.4f, Alpha(edgeColor, alpha * 0.48f));
                Ribbon(width * 0.62f, Alpha(coreColor, alpha));
            }
            void BladeBolt(Vector2 from, Vector2 to, float width, float alpha, float kink, int key)
            {
                Vector2 delta = to - from;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
                // Four straight sections produce hard electrical corners. No
                // sinusoidal spine or delayed world-space tail follows the blade.
                for (int p = 0; p < Points; p++)
                {
                    float joint = p / (float)(Points - 1) * 4;
                    int a = Mathf.Min((int)joint, 3), b = a + 1;
                    Vector2 start = from + delta * (a * 0.25f)
                        + normal * (a == 0 ? 0 : CrackleFlicker(key, a) * kink);
                    Vector2 end = from + delta * (b * 0.25f)
                        + normal * (b == 4 ? 0 : CrackleFlicker(key, b) * kink);
                    path[p] = Vector2.Lerp(start, end, joint - a);
                }
                // Crisp white current with a clearly visible blue fringe.
                Ribbon(width * 2.2f, Alpha(edgeColor, alpha * 0.55f));
                Ribbon(width, Alpha(coreColor, alpha), core: 0.45f);
            }
            void Crescent(Vector2 offset, Vector2 footprint, float width, float alpha, bool white)
            {
                for (int p = 0; p < Points; p++)
                {
                    float y = p / (float)(Points - 1) * 2 - 1;
                    path[p] = offset + new Vector2(facing * footprint.x * (0.37f - 0.54f * y * y), footprint.y * y * 0.48f);
                }
                if (white) ElectricRibbon(width, alpha);
                else Ribbon(width, Alpha(edgeColor, alpha));
            }
            void Glint(Vector2 at, float reach, float alpha)
            {
                Segment(at - Vector2.right * reach, at + Vector2.right * reach, 0.24f, Alpha(edgeColor, alpha * 0.5f));
                Segment(at - Vector2.right * reach, at + Vector2.right * reach, 0.08f, Alpha(coreColor, alpha));
                Segment(at - Vector2.up * reach * 0.7f, at + Vector2.up * reach * 0.7f, 0.16f, Alpha(edgeColor, alpha * 0.5f));
                Segment(at - Vector2.up * reach * 0.7f, at + Vector2.up * reach * 0.7f, 0.06f, Alpha(coreColor, alpha));
            }
            Vector2 Edge(float t)
            {
                if (heavy && phase == SwordEnergyPhase.Flight) return ElectricEdge(t);
                float y = t * 2 - 1;
                return new Vector2(facing * size.x * (0.37f - 0.54f * y * y), size.y * y * 0.48f);
            }
            Vector2 bladeHeel, bladeBend, bladeTip;
            float bladePixelSize, chargeBodyHeight, chargeSecondsLeft;
            bool smallCharge;
            public void ConfigureBlade(Vector2 heel, Vector2 bend, Vector2 tip, float unitsPerPixel = 0, float bodyHeight = 4, bool small = false, float secondsLeft = float.MaxValue)
            {
                smallCharge = small; chargeSecondsLeft = secondsLeft;
                chargeBodyHeight = Mathf.Max(2, bodyHeight);
                bladeHeel = heel; bladeBend = bend; bladeTip = tip;
                bladePixelSize = Mathf.Max(0, unitsPerPixel);
            }
            Vector2 OnBlade(float t)
            {
                // Quadratic control passes through the measured midpoint.
                Vector2 control = bladeBend * 2 - (bladeHeel + bladeTip) * 0.5f;
                return bladeHeel * ((1 - t) * (1 - t)) + control * (2 * t * (1 - t)) + bladeTip * (t * t);
            }
            Vector2 ChargeCenter() { return OnBlade(0.78f); }
            float ChargeIntensity => smallCharge ? 0.50f : heavy ? 1.35f : 0.90f;
            float ChargeRadius(float focus)
            { return chargeBodyHeight * (heavy ? 0.39f : 0.30f) * 0.60f * (smallCharge ? 0.375f : 1) * Mathf.Lerp(0.85f, 1.25f, focus); }
            public void BeginChargeRelease(float direction)
            { phase = SwordEnergyPhase.ChargeRelease; facing = direction < 0 ? -1 : 1; }
            void ChargeDisc(Vector2 at, Vector2 axis, float radius, float flatten, Color color, int rows)
            {
                Vector2 normal = new Vector2(-axis.y, axis.x);
                // Overlapping filled strips create a soft area, independent of
                // the sword silhouette. No sprite, allocation or new material.
                for (int row = 0; row < rows; row++)
                {
                    float y = Mathf.Lerp(-0.9f, 0.9f, row / (float)(rows - 1));
                    float span = radius * Mathf.Sqrt(1 - y * y);
                    for (int p = 0; p < Points; p++)
                        path[p] = at + axis * Mathf.Lerp(-span, span, p / (float)(Points - 1))
                            + normal * (y * radius * flatten);
                    Ribbon(radius * 1.5f / rows, color, core: 0.78f);
                }
            }
            void ChargeCore(Vector2 at, Vector2 axis, float radius, float focus, float alpha)
            {
                // Translucent aura and a smaller white center leave the sword
                // and character readable while retaining sharp intake lightning.
                ChargeDisc(at, axis, radius * 1.35f, 0.90f,
                    Alpha(new Color(0.18f, 0.35f, 0.64f), alpha * 0.22f * ChargeIntensity), 9);
                ChargeDisc(at, axis, radius, 0.88f,
                    Alpha(edgeColor, alpha * (0.32f + focus * 0.20f) * ChargeIntensity), 9);
                ChargeDisc(at, axis, radius * 0.34f, 0.90f,
                    Alpha(coreColor, alpha * (0.85f + focus * 0.40f) * ChargeIntensity), 7);
            }
            float BladeFillProgress(float t)
            { return heavy ? (aiming ? Mathf.Lerp(0.06f,0.35f,t) : Mathf.Lerp(0.35f,1,t)) : Mathf.Lerp(0.06f,1,t); }
            void BladeFill(float t, float alpha = 1)
            {
                if (Vector2.Distance(bladeHeel,bladeTip)<0.01f || alpha<=0) return;
                float front = BladeFillProgress(t);
                float width = Mathf.Max(chargeBodyHeight*(heavy?0.034f:0.025f),bladePixelSize*2);
                float brightness = (0.35f+front*0.65f)*alpha;
                if (!heavy)
                {
                    // SW01 energizes both faces from the outset; the bright pulse still travels toward the tip.
                    for (int p=0; p<Points; p++) path[p]=OnBlade(p/(float)(Points-1));
                    Ribbon(width*1.55f, Alpha(edgeColor, (0.40f+0.36f*t)*alpha), core:0.72f);
                }
                // Blue steel lights from the guard to the tip. A bounded aura
                // extends a little beyond the blade, with no free-floating core.
                for (int p=0;p<Points;p++) path[p]=OnBlade(front*p/(Points-1));
                Ribbon(width*2.3f,Alpha(new Color(0.15f,0.33f,0.67f),brightness*0.38f));
                Ribbon(width,Alpha(edgeColor,brightness*(heavy?0.75f:0.60f)),core:0.65f);
                Ribbon(width*0.27f,Alpha(coreColor,brightness*0.65f));
                int arcs = heavy?6:4;
                for (int a=0;a<arcs;a++)
                {
                    float u = front*(a+0.35f)/arcs;
                    Vector2 from=OnBlade(u),to=OnBlade(Mathf.Min(front,u+0.14f));
                    Vector2 axis=(to-from).normalized,normal=new Vector2(-axis.y,axis.x);
                    float blink=0.75f+0.25f*CrackleFlicker(a+120,0);
                    BladeBolt(from,to,width*0.22f,brightness*blink,width*0.6f,a+120);
                    // Short sparks protrude from the illuminated portion only.
                    Vector2 end=to+normal*(width*(a%2==0?2.8f:-2.8f));
                    BladeBolt(to,end,width*0.15f,brightness*blink*0.6f,width*0.4f,a+140);
                }
                BladeChargeAccents(front,width,brightness);
            }
            void BladeChargeAccents(float front, float width, float brightness)
            {
                // Two counter-winding filaments make the sheath feel alive.
                // Their reach stays within a few blade widths of the steel.
                for (int side=-1;side<=1;side+=2)
                {
                    for (int p=0;p<Points;p++)
                    {
                        float u=front*p/(Points-1);
                        Vector2 axis=(OnBlade(Mathf.Min(1,u+0.02f))-OnBlade(Mathf.Max(0,u-0.02f))).normalized;
                        Vector2 normal=new Vector2(-axis.y,axis.x);
                        float envelope=Mathf.Sin(p/(float)(Points-1)*Mathf.PI);
                        float winding=Mathf.Sin(u*16-age*(heavy?9:12)+side*1.5f);
                        path[p]=OnBlade(u)+normal*(side*width*(1.1f+winding*0.4f)*envelope);
                    }
                    Ribbon(width*0.38f,Alpha(edgeColor,brightness*0.55f));
                    Ribbon(width*0.10f,Alpha(coreColor,brightness*0.45f));
                }
                // Distinct short packets climb from the guard toward the front.
                int packets=heavy?4:2;
                for (int a=0;a<packets;a++)
                {
                    float u=Mathf.Repeat(age*(heavy?1.7f:2.2f)+a/(float)packets,1);
                    float head=front*u,tail=Mathf.Max(0,head-0.16f);
                    float pulse=Mathf.Sin(u*Mathf.PI)*brightness;
                    BladeBolt(OnBlade(tail),OnBlade(head),width*0.24f,pulse,width*0.25f,a+180);
                }
                // A small blade-aligned star gathers at the lit front just
                // before release. No floating sphere, ring or whole-body flash.
                float ready=Mathf.Clamp01((front-0.72f)/0.28f);
                Vector2 tip=OnBlade(front),direction=(bladeTip-bladeBend).normalized;
                Vector2 cross=new Vector2(-direction.y,direction.x);
                float reach=width*(heavy?3.8f:3.0f);
                Segment(tip-direction*reach,tip+direction*reach,width*0.35f,Alpha(edgeColor,brightness*ready*0.55f));
                Segment(tip-direction*reach*0.6f,tip+direction*reach*0.6f,width*0.10f,Alpha(coreColor,brightness*ready));
                Segment(tip-cross*reach*0.5f,tip+cross*reach*0.5f,width*0.12f,Alpha(edgeColor,brightness*ready*0.70f));
            }
            void BladeChargeDischarge(float t)
            {
                float fade=(1-t)*(1-t);
                if (fade<=0) return;
                Vector2 tip=OnBlade(1),forward=Vector2.right*facing;
                Vector2 normal=Vector2.up;
                float width=chargeBodyHeight*(heavy?0.034f:0.025f);
                int rays=heavy?5:3;
                for (int a=0;a<rays;a++)
                {
                    float side=a/(float)(rays-1)*2-1;
                    Vector2 end=tip+forward*width*(2+t*5)+normal*(side*width*(1+t*2));
                    BladeBolt(tip,end,width*0.25f,fade*(heavy?0.95f:0.70f),width*0.35f,a+220);
                }
            }
            void Charge(float t)
            {
                if (!smallCharge) { BladeFill(t); return; }
                if (Vector2.Distance(bladeHeel, bladeTip) < 0.01f) return;
                float focus = heavy ? (aiming ? Mathf.Lerp(0.15f, 0.4f, t) : Mathf.Lerp(0.4f, 1, t))
                    : Mathf.Lerp(0.18f, 1, t);
                Vector2 at = ChargeCenter();
                Vector2 axis = (bladeBend - bladeHeel).normalized;
                Vector2 normal = new Vector2(-axis.y, axis.x) * facing;
                // Contract during the last two authored frames of preparation.
                // Tracking SW02 remains open until its existing locked warning.
                float contraction = aiming ? 0 : Mathf.Clamp01(1 - chargeSecondsLeft / (2f / 24));
                float radius = ChargeRadius(focus) * Mathf.Lerp(1, 0.28f, contraction);
                float width = Mathf.Max(radius * (heavy ? 0.10f : 0.085f), bladePixelSize * (smallCharge ? 1.5f : 3));
                ChargeCore(at, axis, radius, focus, 0.9f + focus * 0.35f);
                int arcs = smallCharge ? 2 : heavy ? 10 : 6;
                for (int a = 0; a < arcs; a++)
                {
                    uint key = arcSeed + (uint)a * 131;
                    float angle = a * 2.39996f + Noise(key) * 0.7f;
                    Vector2 outward = axis * Mathf.Cos(angle) + normal * Mathf.Sin(angle);
                    float u = Mathf.Repeat(age * (heavy ? 2.4f : 3.0f) + Noise(key + 7), 1);
                    Vector2 target = at + outward * radius * 0.25f;
                    Vector2 from = at + outward * radius * (2.1f - u * 1.25f);
                    float blink = 0.85f + Flicker(a + 20, 0) * 0.15f;
                    BladeBolt(from, target, width, (1.1f + focus * 0.8f) * blink * ChargeIntensity,
                        radius * (0.20f + focus * 0.10f) * facing, a + 1);
                }
                int streams = smallCharge ? 1 : heavy ? 6 : 4;
                for (int a = 0; a < streams; a++)
                {
                    float angle = a * 2.39996f - age * 0.7f;
                    Vector2 outward = axis * Mathf.Cos(angle) + normal * Mathf.Sin(angle);
                    float u = Mathf.Repeat(age * (2.0f + focus) + a * 0.31f, 1);
                    Vector2 head = at + outward * radius * (2.0f * (1 - u));
                    Vector2 tail = head + outward * radius * 0.40f;
                    BladeBolt(tail, head, width * 0.7f, Mathf.Sin(u * Mathf.PI) * (1 + focus) * ChargeIntensity,
                        radius * 0.05f * facing, a + 40);
                }
                if (heavy)
                    for (int a = 0; a < 3; a++)
                    {
                        float u = Mathf.Repeat(age * 3.2f + a / 3f, 1);
                        BladeBolt(OnBlade(Mathf.Lerp(0.30f, 0.72f, u)), OnBlade(Mathf.Lerp(0.55f, 1, u)),
                            width * 0.65f, (0.5f + focus) * ChargeIntensity, radius * 0.07f, a + 80);
                    }
                int particles = smallCharge ? 3 : heavy ? 16 : 10;
                for (int a = 0; a < particles; a++)
                {
                    float u = Mathf.Repeat(age * (heavy ? 2.8f : 3.4f) + Noise(arcSeed + (uint)a * 73), 1);
                    float angle = a * 2.39996f;
                    Vector2 outward = axis * Mathf.Cos(angle) + normal * Mathf.Sin(angle);
                    Vector2 head = at + outward * radius * (2.2f * (1 - u));
                    Segment(head + outward * radius * 0.10f, head, width * 0.45f,
                        Alpha(a % 3 == 0 ? coreColor : edgeColor, Mathf.Sin(u * Mathf.PI) * 1.3f * ChargeIntensity));
                }
            }
            void ChargeRelease(float t)
            {
                if (!smallCharge) { BladeFill(1, 1-t); BladeChargeDischarge(t); return; }
                Vector2 at = ChargeCenter();
                Vector2 axis = (bladeBend - bladeHeel).normalized;
                float radius = ChargeRadius(1);
                // The core already contracted before the actual launch event.
                // Discharge begins on that event, without delaying the projectile.
                float burst = Mathf.Clamp01(t);
                ChargeCore(at, axis, radius * 0.28f * (1 - burst), 1, 1 - burst);
                Vector2 forward = Vector2.right * facing;
                Vector2 normal = Vector2.up;
                int bolts = smallCharge ? 2 : heavy ? 12 : 6;
                for (int a = 0; a < bolts; a++)
                {
                    float spread = (a / (float)(bolts - 1) - 0.5f) * radius;
                    Vector2 end = at + forward * (radius * (0.35f + burst * 2.1f))
                        + normal * (spread * burst);
                    BladeBolt(at + normal * (spread * 0.12f), end,
                        radius * 0.11f * (1 - burst), (1 - burst) * 1.6f * ChargeIntensity,
                        radius * 0.08f * facing, a + 60);
                }
            }
            void Flight()
            {
                // Keep the authored crescent and white cutting edge; every extra
                // bolt, chip and speed cut stays close to that silhouette.
                if (!heavy) Crescent(Vector2.zero, size, 0.11f, power * 0.95f, true);
                if (!heavy) StaggeredMantle();
                for (int p = 0; p < Points; p++) path[p] = ElectricEdge(p / (float)(Points - 1));
                Ribbon(electricWidth * (heavy ? 6.0f : 4.0f), Alpha(edgeColor, heavy ? 1.25f : 0.9f));
                Ribbon(electricWidth * 2.4f, Alpha(edgeColor, heavy ? 1.1f : 0.85f));
                Ribbon(electricWidth * 1.2f, Alpha(coreColor, heavy ? 1.25f : 0.95f));
                // Staggered short branches keep a readable continuous outline.
                int arcs = heavy ? 10 : 7;
                float ignition = 1 + Mathf.Clamp01(1 - age / 0.18f) * 0.7f;
                for (int a = 0; a < arcs; a++)
                {
                    float span = heavy ? 0.14f : 0.105f;
                    float head = 0.08f + a * (0.84f - span) / (arcs - 1);
                    float pulse = 0.70f + 0.30f * Mathf.Sin(age * (15 + a * 1.7f) + a * 2.1f + Noise(arcSeed) * 6);
                    BladeBolt(ElectricEdge(head), ElectricEdge(head + span), electricWidth,
                        Mathf.Min(1.8f, (heavy ? 1.6f : 1.3f) * pulse * ignition), electricSize.y * (heavy ? 0.034f : 0.026f), a + 1);
                }
                float launchTail = Mathf.Clamp01(1 - age / (heavy ? 0.24f : 0.16f));
                for (int a = 0; a < (heavy ? 5 : 3) && launchTail > 0; a++)
                {
                    Vector2 tail = ElectricEdge(0.18f + a * 0.15f);
                    BladeBolt(tail, tail + new Vector2(-facing * electricSize.x * (heavy ? 0.36f : 0.24f), electricSize.y * 0.025f),
                        electricWidth * 1.2f, launchTail * 1.4f, electricSize.y * 0.03f, 17 + a);
                }
                // Short horizontal blades flash in place along the moving wave.
                // They have no history buffer, curve, sag or expanding ghost arc.
                int cuts = heavy ? 8 : 5;
                uint tick = (uint)(age * 22);
                for (int a = 0; a < cuts; a++)
                {
                    uint key = seed + (uint)a * 131 + tick * 977;
                    float height = 0.08f + Noise(key) * 0.84f;
                    Vector2 tip = Edge(height) - Vector2.right * facing * size.x * 0.06f;
                    float length = heavy ? 0.7f + Noise(key + 7) * 0.6f : 0.45f + Noise(key + 7) * 0.45f;
                    for (int p = 0; p < Points; p++)
                    {
                        float t = p / (float)(Points - 1);
                        path[p] = tip - Vector2.right * facing * length * (1 - t);
                    }
                    float pulse = 0.65f + 0.35f * Mathf.Sin(Mathf.Repeat(age * 18 + a * 0.17f, 1) * Mathf.PI);
                    ElectricRibbon(heavy ? 0.16f : 0.115f, power * pulse);
                }
                if (age >= nextSpark)
                {
                    Emit(heavy ? 5 : 3, heavy ? 3.2f : 2.6f);
                    nextSpark = age + (heavy ? 0.04f : 0.055f) + Sample() * 0.015f;
                }
            }
            void StaggeredMantle()
            {
                // SW01 only: a 25% larger, broken cyan mantle adds presence.
                // The opaque sprite and white cutting spine keep their original
                // hitbox-sized footprint. No white edge claims the outer reach.
                Vector2 outer = size * new Vector2(1.2f, 1.25f);
                for (int a = 0; a < 5; a++)
                {
                    for (int p = 0; p < Points; p++)
                    {
                        float t = (a + 0.08f + p / (float)(Points - 1) * 0.76f) / 5;
                        float y = t * 2 - 1;
                        path[p] = new Vector2(facing * outer.x * (0.37f - 0.54f * y * y), outer.y * y * 0.48f);
                    }
                    Ribbon(0.22f, Alpha(edgeColor, 0.64f + 0.10f * Flicker(a + 70, 0)));
                }
            }
            void Ring(float radius, float flatten, float rotation, float alpha, float width, int pieces, bool white = false)
            {
                for (int a = 0; a < pieces; a++)
                {
                    for (int p = 0; p < Points; p++)
                    {
                        float angle = rotation + a * Mathf.PI * 2 / pieces + p / (float)(Points - 1) * (pieces == 1 ? 5.4f : 2.4f);
                        float r = radius * (1 + Flicker(a + 21, p) * 0.055f);
                        path[p] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * flatten);
                    }
                    if (white) ElectricRibbon(width, alpha);
                    else Ribbon(width, Alpha(edgeColor, alpha));
                }
            }
            void Burst(float t)
            {
                bool hit = phase == SwordEnergyPhase.Impact, dissolve = phase == SwordEnergyPhase.Dissipate;
                // A two-frame white peak, then faster azure expansion and a short
                // fragment tail. Cosmetic time never feeds the attack coroutine.
                float fade = Mathf.Pow(1 - t, 1.5f);
                float flash = Mathf.Clamp01(1 - age / (heavy ? 0.065f : 0.05f));
                if (!dissolve)
                {
                    int rings = heavy ? 2 : 1;
                    for (int a = 0; a < rings; a++)
                    {
                        float u = Mathf.Clamp01((t - a * 0.14f) / (1 - a * 0.14f));
                        float radius = Mathf.Lerp(0.3f, heavy ? 4.3f : 2.8f, Mathf.Sqrt(u));
                        Ring(radius, a == 0 ? 0.66f : 0.95f, -age * 6 + a * 1.4f,
                            (1 - u) * fade * power, heavy ? 0.14f : 0.10f, 3, a == 0);
                    }
                    Glint(hit ? Vector2.zero : swordOffset, heavy ? 3.2f : 2.2f, flash * (heavy ? 2.1f : 1.85f));
                    // Staggered razor streaks fan outward at different speeds.
                    int slashes = heavy ? 5 : 3;
                    for (int a = 0; a < slashes; a++)
                    {
                        float u = Mathf.Clamp01((t - a * 0.055f) / (1 - a * 0.055f));
                        float angle = hit ? 0.92f + (a - slashes / 2) * 0.47f
                            : slashSign * (a - slashes / 2) * 0.17f;
                        Vector2 axis = new Vector2(facing * Mathf.Cos(angle), Mathf.Sin(angle));
                        Vector2 offset = new Vector2(facing * u * (heavy ? 1.5f : 0.8f), (a - slashes / 2) * (hit ? 0.16f : 0.28f));
                        float length = (heavy ? 5.5f : 4.4f) * (0.35f + Mathf.Sqrt(u) * 0.65f);
                        for (int p = 0; p < Points; p++)
                        {
                            float v = p / (float)(Points - 1);
                            path[p] = offset + axis * Mathf.Lerp(-length * 0.45f, length, v)
                                + Vector2.up * (Mathf.Sin(v * Mathf.PI) * (hit ? 0.2f : slashSign * 0.45f));
                        }
                        ElectricRibbon((heavy ? 0.27f : 0.19f) * (1 - u), fade * (1 - u * 0.5f) * 1.25f);
                    }
                    if (!hit)
                    {
                        // Broad forward crescents peel off the actual launch
                        // point; the sword flash remains anchored above the lane.
                        for (int a = 0; a < (heavy ? 2 : 1); a++)
                        {
                            float u = Mathf.Clamp01((t - a * 0.12f) / (1 - a * 0.12f));
                            Crescent(new Vector2(facing * u * (heavy ? 3.8f : 2.4f), 0),
                                new Vector2(heavy ? 3.0f : 2.1f, (heavy ? 6.8f : 4.0f) * (0.6f + u * 0.4f)),
                                heavy ? 0.23f : 0.16f, fade * (1 - u * 0.4f), true);
                        }
                        if (flash > 0)
                            BladeBolt(electricOffset, electricOffset + new Vector2(facing * electricSize.x * 0.22f, electricSize.y * 0.12f),
                                electricWidth, flash * (heavy ? 1.0f : 0.85f), electricSize.y * 0.025f, 51);
                    }
                }
                else
                {
                    Ring(Mathf.Lerp(0.45f, heavy ? 2.8f : 2.0f, Mathf.Sqrt(t)), 0.72f, age * 5,
                        fade * power, heavy ? 0.14f : 0.10f, 3);
                }
                // Expiry breaks into short electric fragments, without a hit flash.
                // Existing contact routing still owns the outcome.
                int arcs = dissolve ? (heavy ? 10 : 6) : (heavy ? 12 : 8);
                for (int a = 0; a < arcs; a++)
                {
                    float angle = a * 2.39996f + Noise(seed + (uint)a) * 0.8f;
                    Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float radius = Mathf.Min(electricSize.x, electricSize.y) * (0.12f + Mathf.Sqrt(t) * (heavy ? 0.25f : 0.20f))
                        * (dissolve ? 0.65f : 1);
                    BladeBolt(electricOffset + axis * radius * 0.25f, electricOffset + axis * radius,
                        electricWidth * (1 - t) * (dissolve ? 0.85f : 1.2f),
                        fade * (heavy ? 1.2f : 1.0f) * (dissolve ? 0.85f : 1), radius * 0.16f * (1 - t), a + 30);
                }
            }
            void Emit(int count, float speed)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = Sample() * Mathf.PI * 2;
                    Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    bool flying = phase == SwordEnergyPhase.Flight;
                    // Flight chips use blade-local coordinates and a very short
                    // life, so rapid projectile travel cannot stretch them into
                    // a detached stream. Impact fragments remain in world space.
                    Vector2 at = flying ? ElectricEdge(0.08f + Sample() * 0.84f) : (Vector2)center + axis * 0.18f;
                    Vector2 velocity = flying ? new Vector2(-facing * (0.3f + Sample() * speed * 0.35f), axis.y * speed * 0.18f)
                        : axis * speed * (0.35f + Sample() * 0.65f);
                    motes[cursor] = new Mote { at = at, velocity = velocity, life = flying ? 0.11f + Sample() * 0.045f : 0.10f + Sample() * 0.06f,
                        length = size.y * (0.035f + Sample() * (heavy ? 0.035f : 0.025f)),
                        width = heavy ? 0.8f : 0.65f, spin = (Sample() - 0.5f) * 14 };
                    cursor = (cursor + 1) % MoteCount;
                }
            }
            void DrawMotes(float dt)
            {
                for (int i = 0; i < motes.Length; i++)
                {
                    ref var mote = ref motes[i];
                    if (mote.life <= 0 || mote.age >= mote.life) continue;
                    mote.age += dt; mote.at += mote.velocity * dt; mote.velocity *= Mathf.Exp(-dt * 3);
                    float fade = Mathf.Clamp01(1 - mote.age / mote.life);
                    bool flying = phase == SwordEnergyPhase.Flight;
                    Vector2 at = flying ? mote.at : mote.at - (Vector2)center;
                    Vector2 axis = mote.velocity.normalized;
                    if (i % 3 == 0)
                    {
                        float angle = mote.age * mote.spin;
                        axis = new Vector2(axis.x * Mathf.Cos(angle) - axis.y * Mathf.Sin(angle),
                            axis.x * Mathf.Sin(angle) + axis.y * Mathf.Cos(angle));
                    }
                    BladeBolt(at, at - axis * mote.length * fade, electricWidth * mote.width * fade,
                        fade * (heavy ? 0.85f : 0.70f), mote.length * 0.12f, i + 80);
                }
            }
            public void Stop() { Active = false; Owner = null; if (node != null) node.SetActive(false); }
            public void Dispose() { if (mesh != null) Object.Destroy(mesh); if (node != null) Object.Destroy(node); }
        }
    }
}
