using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class SW05Pattern : SwordBossPattern
        {
            public SW05Pattern(KarmaEnemy owner, SwordBossBehavior sword) : base(owner, sword) { }
            Vector2 AerialSwordCastPosition { get { return new Vector2(KarmaStage.ArenaCenter, KarmaSpace.Units(6)); } }
            float AerialSwordReachLife()
            {
                // Upper bound for rain planning; each blade ends when its tip lands.
                return new Vector2(KarmaStage.ArenaWidth, KarmaSpace.Units(12)).magnitude
                / Mathf.Max(0.01f, Settings.swordSpeed);
            }
            float AerialSwordInterval()
            {
                return Mathf.Max(1, Settings.aerialSwordInterval, owner.Warning(0.5f) + Time.fixedDeltaTime);
            }
            IEnumerator FormAerialSword(SpriteRenderer[] blades, float seconds)
            {
                // The supplied SW05 image, with no attack component, collider or glow.
                // Use the same yield/elapsed sequence as Wait so formation adds no delay.
                var readyScales = new Vector3[blades.Length];
                float elapsed = 0;
                for (int i = 0; i < blades.Length; i++)
                {
                    readyScales[i] = blades[i].transform.localScale;
                    blades[i].transform.localScale = readyScales[i] * 0.45f;
                    blades[i].color = new Color(0.65f, 0.7f, 0.72f, 0);
                }
                while (elapsed < seconds && owner.Active)
                {
                    yield return null;
                    if (owner.game.Paused) continue;
                    elapsed += Time.deltaTime;
                    for (int i = 0; i < blades.Length; i++)
                    {
                        // Stagger appearance, then hold every fully formed blade
                        // motionless for the last quarter of the advertised warning.
                        float delay = seconds * 0.3f * i / Mathf.Max(1, blades.Length - 1);
                        float formed = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - delay) / (seconds * 0.45f)));
                        blades[i].transform.localScale = readyScales[i] * Mathf.Lerp(0.45f, 1, formed);
                        blades[i].color = Color.Lerp(new Color(0.65f, 0.7f, 0.72f, 0), new Color(1, 1, 1, 0.95f), formed);
                    }
                }
            }
            public override IEnumerator Execute()
            {
                owner.state = ActionState.Telegraph;
                owner.animator.PlayWavePhase("Windup");
                owner.Mark("도약 준비 · 접촉 피해 없음", owner.Position, KarmaSpace.Units(new Vector2(2, 2)));
                yield return owner.Wait(0.8f); owner.ClearPatternObjects();
                Vector2 ground = new Vector2(KarmaStage.ArenaCenter, KarmaSpace.GuardianGroundY);
                owner.animator.PlayWavePhase("Rising");

                yield return owner.Motion(AerialSwordCastPosition, 0.5f, Vector2.zero);
                if (!owner.Active) yield break;
                owner.transform.position = AerialSwordCastPosition;
                owner.fullShield = true;
                owner.animator.PlayWavePhase("Guard");
                int seed = KarmaAerialSwordPlanner.ResolveSeed(Settings.aerialSwordSeed);
                var planner = KarmaAerialSwordPlanner.Create(owner.effectsRoot, owner.game, seed);
                int groups = planner.GroupSizes.Length;
                Vector2 size = sword.SwordSize(EnemyAttack.SW05);
                float warning = Mathf.Max(0.75f, owner.Warning(0.75f)), interval = Mathf.Max(AerialSwordInterval(), warning + 0.15f);
                float firstReleaseAt = owner.FightSeconds + warning;
                for (int group = 0; group < groups && owner.Active; group++)
                {
                    // Absolute release slots avoid accumulating warning/gap rounding.
                    // Old swords keep moving while the next full warning is formed.
                    float rhythm = group % 3 == 1 ? 0.06f : group % 3 == 2 ? -0.04f : 0;
                    float formationAt = firstReleaseAt + group * interval + rhythm - warning;
                    while (owner.Active && owner.FightSeconds < formationAt) yield return null;
                    if (!owner.Active) yield break;
                    KarmaAerialSwordPlanner.Group plan = null;
                    while (owner.Active && plan == null)
                    {
                        try
                        {
                            plan = planner.BuildGroup(owner.Position, group, warning, AerialSwordReachLife(),
                            size, Settings.swordSpeed * 1.75f, owner.FightSeconds);
                        }
                        catch (System.InvalidOperationException error) when
                        (error.Message == "SW05 could not reserve a physical sword-rain escape route.")
                        {
                            // Retry after the live swords advance; no unsafe group is released.
                        }
                        // Never launch an unsafe group. Live swords keep moving;
                        // re-plan positions/timing without omitting any promised blade.
                        if (plan == null) yield return owner.Wait(0.15f);
                    }
                    if (!owner.Active) yield break;
                    var marks = new SpriteRenderer[plan.shots.Length];
                    var blades = new SpriteRenderer[plan.shots.Length];
                    for (int member = 0; member < plan.shots.Length; member++)
                    {
                        var shot = plan.shots[member];
                        // The blade's transverse width is also its landing footprint.
                        // This fixed ground mark never tracks after the sky layout locks.
                        marks[member] = owner.PathMark(new Vector2(shot.origin.x, 0.18f), new Vector2(shot.origin.x, 0.18f),
                        new Vector2(shot.size.y, 0.26f));
                        marks[member].name = "SW05 낙하 지면 전조";
                        blades[member] = KarmaVisuals.Box(owner.effectsRoot, "Forming aerial sword / no damage",
                        shot.origin, Vector2.one, Color.white, 25);
                        KarmaWaveArtwork.Apply(blades[member], "SW05_AerialSword", shot.size, shot.velocity);
                    }

                    yield return FormAerialSword(blades, warning);
                    if (!owner.Active) yield break;
                    for (int member = 0; member < plan.shots.Length; member++)
                    {
                        marks[member].gameObject.SetActive(false); Destroy(marks[member].gameObject);
                        blades[member].gameObject.SetActive(false); Destroy(blades[member].gameObject);
                        var shot = plan.shots[member];
                        var attack = owner.Attack(shot.origin, shot.velocity, shot.size, shot.life);
                        planner.RegisterLaunch(shot, attack, shot.size, owner.FightSeconds);
                    }
                    owner.animator.ReleaseWavePhase();
                }
                // Each shot owns its boundary-limited lifetime. Normal completion
                // must not cancel a still-dangerous sword just to begin the landing.

                while (owner.Active && planner.HasLiveShots) yield return null;
                if (!owner.Active) yield break;
                owner.fullShield = false;
                owner.animator.PlayWavePhase("Rising");
                yield return owner.Motion(ground, 0.5f, Vector2.zero);
                // Let the last harmless impact finish during descent rather than
                // clearing it in the same frame as the last blade's ground contact.
                owner.ClearPatternObjects();
                yield return owner.Recovery(2);
            }
            public override bool CanReach(Vector2 from, Vector2 target)
            {
                float facing = target.x < from.x ? -1 : 1;
                Vector2 elevated = AerialSwordCastPosition;
                Vector2 aim = SwordBossBehavior.SwordAim(elevated, target + (Vector2)sword.SwordPlayerBounds.center - owner.game.Player.Position, facing);
                Vector2 origin = elevated + aim * KarmaAerialSwordPlanner.FormationRadius;
                float life = AerialSwordReachLife();
                float duration = 0.8f + 0.5f + owner.Warning(0.5f) + (KarmaAerialSwordPlanner.VolleyCount - 1) * AerialSwordInterval()
                    + life + 0.5f + Mathf.Max(0.38f, 2 * Mathf.Clamp(owner.Tuning.recoveryMultiplier, 0.4f, 2) * owner.Tempo);
                return duration < Mathf.Max(5, owner.Tuning.patternTimeout)
                    && sword.SwordShotCanReach(origin, aim * Settings.swordSpeed, sword.SwordSize(EnemyAttack.SW05), life, target);
            }

        }
    }
}
