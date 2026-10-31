using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class GD05Pattern : GuardBossPattern
        {
            public GD05Pattern(KarmaEnemy owner) : base(owner) { }
            const float TrialCenter = 20;
            const float TrialRadius = 8 * KarmaSpace.Scale;

            public override IEnumerator Execute()
            {
                owner.state = ActionState.Telegraph;

                yield return owner.Wait(1);
                owner.shieldUntil = owner.shieldWarningUntil = 0;
                owner.game.Player.ConfineArena(TrialCenter - KarmaSpace.Units(7.4f), TrialCenter + KarmaSpace.Units(7.4f));
                yield return owner.Motion(new Vector2(TrialCenter, KarmaSpace.GuardianGroundY), 0.5f, Vector2.zero);
                var dome = KarmaGuardDome.Create(owner.game, owner.effectsRoot, new Vector2(TrialCenter, 0), TrialRadius);
                GuardDecoration("GuardBossVFX", "Seal", new Vector2(TrialCenter, KarmaSpace.Units(0.25f)),
                KarmaSpace.Units(new Vector2(16, 2)), new Color(1, 1, 1, 0.5f), 3);
                owner.trial.Begin(Mathf.Clamp(Settings.trialAttackCount, 3, 8), Settings.trialAllowedHits);
                for (int i = 0; i < owner.trial.Total; i++)
                {
                    var contact = new KarmaAttackContact(); // Simultaneous hazards share one contact per beat.
                    owner.state = ActionState.Telegraph;
                    bool left = i % 2 == 0;
                    switch (i % 5)
                    {
                        case 0: yield return TrialHorizontal(left, contact); break;
                        case 1: yield return TrialRain(contact); break;
                        case 2: yield return TrialPincer(contact); break;
                        case 3: yield return TrialPillars(contact); break;
                        default: yield return TrialWeave(left, contact); break;
                    }
                    owner.trial.CompleteAttack();
                    yield return owner.Wait(Mathf.Max(0.4f, Settings.trialAttackGap * owner.Tempo));
                }
                bool passed = owner.trial.Passed;
                owner.trial.Clear();
                if (!passed)
                {
                    // Keep the room intact while cracks and light announce its own detonation.

                    dome.Charge(0.95f);
                    yield return owner.Wait(0.95f);
                    dome.Explode();

                    var contact = new KarmaAttackContact();
                    float active = 0;
                    while (active < 0.18f && owner.Active)
                    {
                        if (!owner.game.Paused)
                        {
                            var player = owner.game.Player;
                            if (dome.Overlaps(player.Position, new Vector2(KarmaSpace.PlayerHalfWidth, KarmaSpace.PlayerHalfHeight)))
                            owner.TryAttackHit(player.Position, player.Position, Vector2.zero, owner.Damage * Settings.trialExplosionMultiplier, contact, 0);
                            active += Time.deltaTime;
                        }
                        yield return null;
                    }
                    yield return owner.Wait(0.57f);
                    owner.ClearPatternObjects(); owner.game.Player.ReleaseArena();
                    yield return owner.Recovery(1.5f);
                }
                else
                {
                    owner.game.Player.ReleaseArena(); dome.Dissolve();
                    yield return owner.Wait(0.4f);
                    yield return owner.Recovery(Mathf.Max(1, Settings.trialStunSeconds), scaleDuration: false);
                }
            }
            Vector2 TrialSide(bool left, float y)
            { return new Vector2(TrialCenter + (left ? -1 : 1) * (TrialRadius - KarmaSpace.Units(0.8f)), y); }
            float TrialWaveLife { get { return (TrialRadius * 2 - KarmaSpace.Units(1.6f)) / KarmaSpace.Units(10); } }
            Vector2 TrialVelocity(bool left) { return Vector2.right * KarmaSpace.Units(left ? 10 : -10); }
            KarmaBossAttack TrialShot(Vector2 at, Vector2 velocity, Vector2 size, float life, KarmaAttackContact contact, string pose = "Wave")
            {
                string visualPose = pose == "Ward" ? "Shard" : pose == "Pillar" ? "Pillar" : "Wave";
                return KarmaBossAttack.Spawn(owner.game, owner, owner.effectsRoot, at, velocity, size, life, owner.Damage * Settings.trialDamageMultiplier,
                new Color(1, 0.75f, 0.15f), contact, 0, visualPose, visualSheet: "GuardBossVFX");
            }
            SpriteRenderer TrialWaveMark(bool left, float y)
            {
                var start = TrialSide(left, y);
                return owner.PathMark(start, start + TrialVelocity(left) * TrialWaveLife, WaveSize);
            }
            IEnumerator TrialHorizontal(bool left, KarmaAttackContact contact)
            {
                float y = KarmaSpace.Units(0.9f);
                var mark = TrialWaveMark(left, y);

                yield return owner.Wait(owner.Warning(0.75f)); Destroy(mark.gameObject); owner.state = ActionState.Attack;
                TrialShot(TrialSide(left, y), TrialVelocity(left), WaveSize, TrialWaveLife, contact);
                yield return owner.Wait(TrialWaveLife + 0.05f);
            }
            IEnumerator TrialRain(KarmaAttackContact contact)
            {
                var starts = new Vector2[3]; var lives = new float[3]; var marks = new SpriteRenderer[3];
                Vector2 size = KarmaSpace.Units(new Vector2(0.65f, 0.85f));
                float aim = Mathf.Clamp(owner.game.Player.Position.x, TrialCenter - KarmaSpace.Units(4.4f), TrialCenter + KarmaSpace.Units(4.4f));
                float longest = 0;
                for (int i = 0; i < 3; i++)
                {
                    float x = aim + KarmaSpace.Units((i - 1) * 2.4f);
                    float dx = x - TrialCenter;
                    float roof = Mathf.Sqrt(TrialRadius * TrialRadius - dx * dx);
                    starts[i] = new Vector2(x, roof - KarmaSpace.Units(0.6f));
                    lives[i] = (starts[i].y - KarmaSpace.Units(0.8f)) / KarmaSpace.Units(12);
                    longest = Mathf.Max(longest, lives[i]);
                    marks[i] = owner.PathMark(starts[i], starts[i] + Vector2.down * KarmaSpace.Units(12) * lives[i], size);
                }

                yield return owner.Wait(owner.Warning(0.9f)); owner.state = ActionState.Attack;
                for (int i = 0; i < 3; i++)
                {
                    Destroy(marks[i].gameObject);
                    TrialShot(starts[i], Vector2.down * KarmaSpace.Units(12), size, lives[i], contact, "Ward");
                }
                yield return owner.Wait(longest + 0.05f);
            }
            IEnumerator TrialPincer(KarmaAttackContact contact)
            {
                float y = KarmaSpace.Units(0.9f);
                var left = TrialWaveMark(true, y); var right = TrialWaveMark(false, y);

                yield return owner.Wait(owner.Warning(0.85f));
                Destroy(left.gameObject); Destroy(right.gameObject); owner.state = ActionState.Attack;
                TrialShot(TrialSide(true, y), TrialVelocity(true), WaveSize, TrialWaveLife, contact);
                TrialShot(TrialSide(false, y), TrialVelocity(false), WaveSize, TrialWaveLife, contact);
                yield return owner.Wait(TrialWaveLife + 0.05f);
            }
            IEnumerator TrialPillars(KarmaAttackContact contact)
            {
                Vector2 size = KarmaSpace.Units(new Vector2(0.9f, 3.8f));
                var centers = new Vector2[3]; var marks = new SpriteRenderer[3];
                for (int i = 0; i < 3; i++)
                {
                    centers[i] = new Vector2(TrialCenter + KarmaSpace.Units((i - 1) * 4.8f), size.y / 2);
                    marks[i] = owner.Mark("바닥 봉인 · 기둥 사이가 안전", centers[i], size);
                }

                yield return owner.Wait(owner.Warning(0.85f)); owner.state = ActionState.Attack;
                for (int i = 0; i < 3; i++)
                {
                    Destroy(marks[i].gameObject);
                    TrialShot(centers[i], Vector2.zero, size, 0.3f, contact, "Pillar");
                }
                yield return owner.Wait(0.35f);
            }
            IEnumerator TrialWeave(bool left, KarmaAttackContact contact)
            {
                float high = KarmaSpace.Units(2.7f), low = KarmaSpace.Units(0.9f);
                var highMark = TrialWaveMark(left, high); var lowMark = TrialWaveMark(!left, low);

                yield return owner.Wait(owner.Warning(0.85f)); Destroy(highMark.gameObject); owner.state = ActionState.Attack;
                TrialShot(TrialSide(left, high), TrialVelocity(left), WaveSize, TrialWaveLife, contact);
                yield return owner.Wait(0.45f); Destroy(lowMark.gameObject);
                TrialShot(TrialSide(!left, low), TrialVelocity(!left), WaveSize, TrialWaveLife, contact);
                yield return owner.Wait(TrialWaveLife + 0.05f);
            }

        }
    }
}
