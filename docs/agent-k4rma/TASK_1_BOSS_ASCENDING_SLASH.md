# Task 1 — 상승베기 Guardian Combat: Production Implementation

## Mission

Transform the existing basic 상승베기 boss implementation into responsive, satisfying, challenging and visually expressive action-game combat.

This is a real Unity implementation task. Do not stop at auditing, planning, scaffolding or suggestions.

The team explicitly requested refinement and corrections to requirement mismatches, not arbitrary new gameplay systems.

Use `SOURCE_OF_TRUTH.md`, the official requirements and verified latest team decisions.

## Scope

Primary: 상승베기-type Guardian behavior and its inherited patterns.

Official patterns:

| ID | Name | Intended behavior |
|---|---|---|
| UP01 | 상승 내려찍기 | Rise, lock landing target, slam, horizontal shockwaves, recover |
| UP02 | 공중 추격 | Early pursuit targeting, telegraphed locked trajectory, rising attack, landing/recovery |
| UP03 | 지면 검기 상승 | Sword-energy inheritance; fixed telegraphed ground blade |
| UP04 | 방어막 내려찍기 | Guard inheritance; shielded ascent and slam with ground fragments |
| UP05 | 연속 횡단 공격 | Ground traversal, aerial pursuit, second traversal, final slam, exhaustion |

Use the complete official BPR-006–010 and SFR requirements for precise mechanics. The summary above is not a substitute.

Verify the actual assigned ownership and existing implementation before modifying any pattern.

## Core design principles

### Readable and fair

Every major attack must have:

- Distinct anticipation.
- Telegraph reflecting the actual threat.
- A clearly defined target/direction lock.
- Controlled active damage window.
- A genuine achievable response.
- An attack-ending transition.
- Meaningful recovery or vulnerability.

Once a direction or target is locked, do not secretly retarget unless the approved pattern explicitly allows it.

The player must not suffer unavoidable damage because the boss combines hazardous actions without a reachable safe route.

### Responsive and exciting

Combat should convey intentional sword technique and strong physical motion.

Improve:

- Anticipation and attack rhythm.
- Ascent, descent and traversal curves.
- Animation timing.
- Movement acceleration and deceleration.
- Landing weight and impact.
- Attack effects and hit reactions.
- Brief, appropriate hit-stop.
- Restrained camera impulse.
- Distinguishable attack and recovery states.
- Meaningful aggression variation.
- Attack selection using distance, height and safe-path constraints.

Prefer gameplay decisions over random spectacle.

Use tunable parameters for movement, damage, cooldowns and timing.

Do not invent new mechanics merely to make combat harder.

## Pattern details

### UP01 — 상승 내려찍기

The boss follows a fixed ascent trajectory, indicates its landing position, locks that position, descends and generates two ground-traveling shockwaves.

The player's aerial control or 체공 must provide a meaningful avoidance option.

Shockwave contact must not deal repeated damage across frames.

Recovery must remain vulnerable and non-cancelable except for legitimate interruption or death.

### UP02 — 공중 추격

The boss initially tracks the player's airborne position or another approved target condition.

The attack route must be shown before commitment.

After lock, the boss follows the committed route rather than repeatedly retargeting.

Prevent unfair consecutive repetition.

Ensure reliable landing, state exit and recovery even when the player changes altitude or reaches arena boundaries.

### UP03 — 지면 검기 상승

This pattern is available only under the corresponding inheritance conditions.

Lock forward direction, telegraph the ground danger zone and create the ascending sword-energy attack.

The hitbox must agree with the visible attack.

Residual particles are non-damaging unless explicitly specified.

Leave a real escape route behind the Guardian or outside the threatened zone.

### UP04 — 방어막 내려찍기

Under guard inheritance, the boss ascends protected, telegraphs the slam, descends, removes protection and releases ground-level fragments.

Do not accidentally duplicate the default UP01 shockwave on top of the fragment attack.

Provide viable aerial counterplay and vulnerable recovery.

Follow the verified approved defensive effect; do not add unlimited invulnerability.

### UP05 — 연속 횡단 공격

Implement the complete approved multi-phase attack:

1. Center preparation.
2. Ground traversal.
3. Aerial pursuit.
4. Second ground traversal.
5. Final descending strike.
6. Exhausted punish window.

Each hazardous phase must be recognizable, telegraphed and bounded.

The timing must allow alternating grounded and airborne responses where required.

Verify latest approved triggering, pursuit-duration and balance decisions before changing exact sequence parameters.

Do not substitute a short generic repeated dash sequence.

## Architecture

Inspect before refactoring.

Favor appropriate separation between:

- Pattern availability and selection.
- Attack lifecycle/state.
- Movement execution.
- Telegraph presentation.
- Hit detection and damage accounting.
- Animation/VFX/SFX.
- Interruption, death and reset cleanup.

Use the existing system where sound.

Avoid a second competing boss framework, unnecessary generalized abstractions and giant god classes.

Each attack must terminate correctly after completion, interruption, boss death or restart.

Handle arena edges and failed movement safely.

## Character and effect assets

Before implementation requiring new animation frames:

1. Inventory all canonical source images and metadata.
2. Search sheets and source archives for exact matching frames.
3. Verify slicing, pivots, PPU and animation references.
4. Reuse original frames or exact source-derived transformations.
5. Preserve original character appearance.
6. Produce compatible separate VFX where allowed.
7. Mark truly unavailable exact canonical frames as unresolved rather than replacing them with approximate character images.

Missing optional art should not block independent gameplay logic.

## Implementation approach

Work pattern by pattern, using real commits after each coherent completed feature or fix.

Suggested order:

1. Resolve existing combat defects and lifecycle issues.
2. Finish UP01.
3. Finish UP02.
4. Finish scoped UP03.
5. Finish scoped UP04.
6. Finish UP05.
7. Polish combat feedback and pattern selection.
8. Run combined regressions.

Adjust order based on actual dependencies and existing completion state.

Do not rewrite already correct patterns without a demonstrated reason.

## Validation

Verify as available:

- Unity imports and compiles.
- Required inheritance gates.
- All relevant personalization orders.
- Animation transitions.
- Telegraph/hitbox consistency.
- Target locking.
- Per-attack damage limits.
- Attack recovery.
- Safe arena-boundary behavior.
- Player counterplay.
- Boss interruption and death.
- Stage change and full restart.
- No lingering dangerous objects or timers.
- No new regressions in other boss types.

Run Unity play mode when available.

For subjective game feel, prefer actual gameplay inspection and repeated tuning. Automated code checks cannot establish whether combat is genuinely fun.

Record unverified items explicitly.

## Acceptance criteria

Task 1 is technically implemented when the approved assigned patterns work correctly, combat feel has been meaningfully improved, serious new errors are absent, and available validation passes.

Do not declare production-ready polish solely because scripts compile.

Commit each completed feature/bug fix using `k4rma-git.mdc`.

**Final Task 1 deliverable:** functioning improved boss combat, committed locally, with an evidence-based completion report and known remaining playtest needs.