using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Each inherited body has its own overhead poses, with one shared GD02 timing.
    sealed class KarmaHeavyStrikeMotion
    {
        readonly SpriteRenderer picture;
        readonly KarmaGuardHeavyGlow glow;
        readonly Vector3 home, scale, damageScale;
        readonly Sprite[] poses = new Sprite[3];
        readonly Sprite[] heatedPoses = new Sprite[3];
        readonly Sprite[] shieldlessPoses = new Sprite[3], shieldlessHeatedPoses = new Sprite[3];
        public bool ShieldAway { get; set; }
        public Sprite DamageSprite { get; private set; }
        public Vector3 DamageScale { get { return damageScale; } }
        bool active, swinging, heated;
        float prepareAt, swingAt;
        readonly Sprite[] waveDrawings;
        float wavePoseAt = -1f;
        bool waveReleased;
        int waveWarningTicks;
        public string FormKey { get; private set; }
        public string CurrentPose { get; private set; }
        // Bottom edge of the supporting boot, in pixels from the top of each 768 px GD02 cell.
        // The sword tip is below the boot in several drawings, so alpha bounds cannot anchor the feet.
        static readonly int[] wardSoles = { 728, 727, 728, 709, 703, 709 };
        static readonly int[] waveSoles = { 673, 673, 672, 664, 664, 662 };
        static readonly int[] riseSoles = { 685, 686, 686, 639, 638, 639 };
        static readonly int[] finalSoles = { 684, 694, 691, 654, 650, 657 };
        static readonly int[] trialHeavySoles = { 667, 663, 660, 640, 639, 640 };
        // 00–05: match the visor at eye level to the corresponding Idle. Keep the forward
        // boot at its authored X while resizing, including the lunge where the rear heel lifts.
        static readonly (float scale, int frontBootX)[] finalBody = {
            (0.97f, 576), (0.89f, 551), (1.01f, 576), (0.96f, 577), (0.85f, 524), (0.89f, 544)
        };
        static readonly (float scale, int frontBootX)[] trialBody = {
            (0.90f, 545), (0.82f, 600), (0.85f, 574), (0.82f, 541), (0.94f, 438), (0.79f, 452)
        };
        readonly int[] soles;
        public bool Active => active;
        public Sprite RegisteredBody { get; private set; }
        public Sprite OverlayBoundsDrawing(SpriteRenderer renderer) => renderer == glow.Overlay ? glow.BoundsDrawing : null;
        public int BodyFrame
        {
            get
            {
                // Shielded GD02 reaches its raised pose during the charge and holds it through the cue.
                int cueFrame = ShieldAway ? 3 : 2;
                if (swinging)
                {
                    int elapsed = Mathf.FloorToInt((Time.time - swingAt) * KarmaBootstrap.FramesPerSecond);
                    return Mathf.Min(4 + elapsed, 5);
                }
                if (heated) return cueFrame;
                int frame = Mathf.FloorToInt((Time.time - prepareAt) * KarmaBootstrap.FramesPerSecond);
                return Mathf.Clamp(frame, 0, ShieldAway ? cueFrame - 1 : cueFrame);
            }
        }

        public KarmaHeavyStrikeMotion(SpriteRenderer renderer, Vector3 position, Vector3 originalScale, Sprite standing,
            IReadOnlyList<Essence> order)
        {
            picture = renderer; home = position;
            glow = renderer.gameObject.AddComponent<KarmaGuardHeavyGlow>();
            string key = order.Count >= 3 ? "WardFinal" : order.Count == 2
                ? order[1] == Essence.SwordWave ? "WardWave" : "WardRise" : "Ward";
            FormKey = key;
            soles = key == "WardWave" ? waveSoles : key == "WardRise" ? riseSoles : key == "WardFinal" ? finalSoles : wardSoles;
            glow.Initialize(renderer, key);
            if (key == "WardWave" || key == "WardFinal")
                waveDrawings = KarmaArtResource.Frames(KarmaMotionArt.Root + key + "/GD02Followup");
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
            damageScale = originalScale * (KarmaBodyPoses.HitBounds(standing).size.y / (KarmaBodyPoses.HitBounds(poses[0]).size.y * bodyFraction));
            scale = originalScale;
        }
        public void Prepare(float chargeSeconds = 0.6f)
        {
            active = true; swinging = heated = false; prepareAt = Time.time;
            wavePoseAt = -1f;
            glow.Prepare(chargeSeconds); Apply(0);
        }
        public void PrepareWave(float warningSeconds)
        {
            if (waveDrawings?.Length != 12 || !active || !swinging) return;
            waveReleased = false;
            waveWarningTicks = Mathf.Max(1, Mathf.CeilToInt(warningSeconds * KarmaBootstrap.FramesPerSecond));
            wavePoseAt = Time.time;
        }
        public void ReleaseWave()
        {
            if (wavePoseAt < 0f) return;
            waveReleased = true;
            wavePoseAt = Time.time;
        }
        public Sprite WaveDrawing(int heavyFrame)
        {
            if (heavyFrame != 5 || wavePoseAt < 0f || ShieldAway) return null;
            int tick = Mathf.FloorToInt(Mathf.Max(0f, Time.time - wavePoseAt) * KarmaBootstrap.FramesPerSecond);
            int frame = waveReleased ? Mathf.Min(4 + tick, 11) : Mathf.Min(tick * 4 / waveWarningTicks, 3);
            return waveDrawings[frame];
        }
        public void Heat() { heated = true; glow.Cue(); if (active && !swinging) Apply(0); }
        public void Swing() { swinging = true; swingAt = Time.time; glow.Strike(); Apply(1); }
        public void AlignBody(int frame, Sprite sprite, Sprite boundsDrawing, bool phaseChanged)
        {
            RegisteredBody = picture.sprite;
            // Match the Wave helmet and shield to its standing form at the same camera scale.
            var bodyScale = FormKey == "WardWave" ? scale * 0.88f : scale;
            float offsetX = 0f;
            if (FormKey == "WardFinal")
            {
                var drawing = (ShieldAway ? trialBody : finalBody)[frame];
                bodyScale = scale * drawing.scale;
                offsetX = (drawing.frontBootX - sprite.pivot.x) * (scale.x - bodyScale.x) / sprite.pixelsPerUnit;
            }
            int bootEdge = ShieldAway ? trialHeavySoles[frame] : soles[frame];
            float soleY = sprite.rect.height - bootEdge - sprite.pivot.y;
            picture.transform.localScale = bodyScale;
            picture.transform.localPosition = home + new Vector3(offsetX, -soleY * bodyScale.y / sprite.pixelsPerUnit, 0);
            glow.RegisterDrawing(RegisteredBody, boundsDrawing);
            if (phaseChanged && (FormKey == "Ward" || glow.Visible))
                glow.Sample(updateBounds: FormKey == "Ward");
        }
        public void Stop() { active = swinging = heated = false; wavePoseAt = -1f; glow.Stop(); }
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
