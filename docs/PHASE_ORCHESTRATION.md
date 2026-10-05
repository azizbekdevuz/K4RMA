# K4RMA Phase Orchestration Contract

Status: Persistent execution contract  
Applies to: Phase 5 through Phase 14 and corrective loops around them  
Primary design authority: `docs/REFINED_GAME_SPEC.md`  
Execution ledger: `docs/REFINEMENT_PLAN.md`

---

# 1. Purpose

This document defines **how** the K4RMA refinement must be executed.

It does not replace or redesign the game.

The game design is defined by `docs/REFINED_GAME_SPEC.md` and the team's current requirements/specification. `docs/REFINEMENT_PLAN.md` records execution state, evidence, gates, and completed work.

This file exists so the coordinator can continue from Phase 5 through the final validation/documentation phases without relying on chat memory or inventing phase behavior.

---

# 2. Authority Order

When deciding what to build, use this order:

1. Current team-approved requirements/specification
2. `docs/REFINED_GAME_SPEC.md`
3. `docs/PHASE_ORCHESTRATION.md`
4. `docs/REFINEMENT_PLAN.md`
5. Current implementation
6. Legacy prototype behavior

Rules:

- Higher authority overrides lower authority.
- Do not silently modify a higher-authority document to match convenient code.
- If two current higher-authority sources genuinely conflict, STOP with `DESIGN_DECISION_REQUIRED`.
- Legacy behavior may remain temporarily only when explicitly preserved for compatibility and must not redefine the final mechanic.
- Do not revive abandoned story/gameplay concepts.

---

# 3. Non-Negotiable Game Decisions

The following are fixed unless the user explicitly reports a new team decision.

## 3.1 Product direction

- Final production target: 2D side-scrolling single-player boss-rush action game.
- Current Unity project: 2.5D technical/gameplay prototype.
- Do not convert the current prototype into final 2D production during this refinement.
- Primary submission target remains Windows PC.

## 3.2 Story / setting

- Protagonist is the master's disciple.
- The game is the master's final trial.
- The disciple must understand the principles of the master's techniques and turn them into their own style.
- Four guardians are fought.
- Japanese-fantasy training-ground visual direction remains.
- Do not introduce the abandoned exorcist / cursed-mask / possession story.

## 3.3 Terminology

Player-facing concept:
- Personalization = 고유화
- Original technique = 원형 기술
- Personalized technique = 고유화 기술
- Inherited technique = 계승 기술

Do not restore player-facing:
- sacrifice
- sacrificed
- unconscious
- unconsciousization
- 희생
- 무의식 / 무의식화

Legacy internal identifiers may remain temporarily only where changing them would create unnecessary serialization/runtime risk.

## 3.4 Fixed technique mapping

- SwordWave / 검기 -> PiercingSlash / 관통베기
- RisingSlash / 상승베기 -> AirHover / 체공
- Guard / 방어 -> Counter / 반격

Do not reinterpret these mappings.

Legacy Deflect is **not** Counter and is **not** PiercingSlash.

## 3.5 Progression

Run start:
- SwordWave = Original
- RisingSlash = Original
- Guard = Original

After personalization:
- selected original becomes unavailable to the player
- corresponding personalized behavior becomes available
- guardian inheritance records the original technique
- personalization is irreversible during the run
- personalization order is preserved

Stage progression:
- Stage 1 guardian: neutral, no inherited originals
- after Stage 1: choose one of 3 originals to personalize
- Stage 2 guardian: inherited #1
- after Stage 2: choose one of 2 remaining originals
- Stage 3 guardian: inherited #1 + #2
- after Stage 3: final remaining original is personalized
- Stage 4 guardian: inherited all 3 originals
- Stage 4 player: all 3 personalized techniques
- Stage 4 victory completes the run

Archetype rule:
- first personalized technique determines guardian main archetype
- later inherited techniques add attacks/combinations/minor traits
- later techniques never replace the first archetype
- all six personalization orders must be structurally valid

Do not build six separate guardian implementations.

---

# 4. Agent Roles

For every non-trivial phase implementation or corrective iteration, use:

1. `implementer`
2. `tester`
3. `verifier`

The coordinator must not collapse these roles.

## 4.1 Implementer

Responsibilities:
- inspect the actual current implementation before editing
- implement only the current approved phase or corrective task
- preserve unrelated working behavior
- add/modify tests where appropriate
- update `docs/REFINEMENT_PLAN.md` with factual evidence only
- clearly distinguish automated evidence from untested runtime claims

Must not:
- self-approve the phase
- advance to a later phase
- redesign team-approved gameplay
- silently weaken requirements
- commit/push/merge/rebase/reset/stash/clean/switch branches

## 4.2 Tester

Responsibilities:
- independently inspect implementation and diff
- run the complete relevant Unity test suite
- verify actual compile result
- verify regressions
- check repository/Unity safety
- add tests only when they genuinely prove behavior
- state what automated tests cannot prove

Must not:
- trust implementer summary without inspection
- change feature semantics merely to make tests pass
- call fake pure/EditMode tests proof of scene physics, visuals, feel, readability, or audio
- approve phase advancement
- commit/push/merge

## 4.3 Verifier

Readonly final gate.

Responsibilities:
- independently compare:
  - requirements/spec
  - `REFINED_GAME_SPEC.md`
  - this orchestration contract
  - `REFINEMENT_PLAN.md`
  - source/diff
  - test/compile evidence
- detect scope creep
- detect unsupported completion claims
- determine exact next gate

Final verdict must be one of:

- `PASS_AUTOMATED`
- `HUMAN_GATE`
- `BLOCKED`
- `DESIGN_DECISION_REQUIRED`

---

# 5. Phase State Machine

For every phase:

1. Coordinator reconstructs current state.
2. Coordinator writes an internal phase contract.
3. Delegate implementation to `implementer`.
4. Delegate independent validation to `tester`.
5. Delegate independent final review to `verifier`.
6. Act on the verdict.

## 5.1 PASS_AUTOMATED

Use only when:
- required implementation is complete
- complete relevant test suite passes
- compile passes
- repository safety checks pass
- no material runtime/visual/audio/feel requirement remains unproven

If the current phase has no required human evidence:
- record evidence
- advance automatically to the next phase

If a human gate is inherently required:
- verifier must return `HUMAN_GATE`, not `PASS_AUTOMATED`

## 5.2 BLOCKED

For normal technical blockers:
- coordinator creates the smallest corrective task
- delegate to implementer
- rerun tester
- rerun verifier

Repeat without involving the user unless:
- the same conceptual blocker survives three corrective cycles
- destructive/user-owned environment action is required
- a design/requirements decision is required

Do not start another phase.

## 5.3 HUMAN_GATE

Stop.

Return the smallest exact manual checklist that proves only what automation cannot prove.

The coordinator must wait for explicit user observation.

If user reports PASS:
- append exact human evidence to `REFINEMENT_PLAN.md`
- close only supported checklist items
- proceed to next phase

If user reports FAIL:
- keep current phase open
- convert only failed observations into corrective tasks
- implementer -> tester -> verifier
- return to the same human gate

Do not infer a pass from silence, tests, or implementation intent.

## 5.4 DESIGN_DECISION_REQUIRED

Stop when:
- current team sources conflict
- required behavior is genuinely unspecified
- satisfying a requirement would require changing an approved game rule
- the proposed fix would change technique identity, stage structure, story, archetype rule, or other non-negotiable behavior

Return:
- exact ambiguity/conflict
- affected phase
- smallest decision needed

Do not invent the decision.

---

# 6. Allowed Mid-Phase Adaptation

The implementation may evolve when evidence requires it.

Allowed without user approval:
- refactoring internal class boundaries
- extracting focused interfaces/components
- changing implementation ownership/lifecycle
- replacing temporary preview/dev plumbing
- removing obsolete legacy runtime paths after their replacement is proven
- strengthening tests
- adding PlayMode tests when appropriate
- fixing serialization-safe runtime wiring
- fixing race/state/reset/collision/lifecycle bugs
- small tuning adjustments needed for the already-approved mechanic to function reliably
- introducing narrowly scoped debug/test hooks
- modifying an implementation detail that does not alter approved player-facing behavior

Not allowed without user/team decision:
- changing story
- changing technique mapping
- changing stage count
- changing first-choice archetype rule
- changing cumulative inheritance
- changing the meaning of 고유화
- replacing Counter with Guard/Deflect behavior
- replacing AirHover with another launcher
- replacing PiercingSlash with a projectile/free dash
- adding or removing core skills
- adding generic mobs as a new core loop
- converting the prototype to final 2D
- adding multiplayer/web/accounts/backend
- major new systems outside requirements
- changing requirements/spec to fit implementation

---

# 7. Git / Repository Safety

Agents must never:
- `git commit`
- `git push`
- `git merge`
- `git rebase`
- `git reset`
- `git stash`
- `git clean`
- switch branches
- discard unrelated user changes

The user owns commit/push checkpoints.

Before each phase:
- inspect branch
- inspect `git status`
- inspect relevant diff
- protect pre-existing dirty work

At phase boundary report exact changed paths.

---

# 8. Unity Safety

Unity project root:

`D:\Workplace\Gaming\K4RMA\Game`

Never open `PrototypeArena.unity` directly through Explorer or file association.

Never use `-openfile` on the scene.

Use Unity Hub or explicit `-projectPath` pointing to the `Game` project.

After every meaningful Unity batch operation, verify:

Must be absent:
- `Game/Assets/Packages`
- `Game/Assets/ProjectSettings`
- `Game/Assets/Library`

Must be present:
- `Game/Packages/manifest.json`
- `Game/ProjectSettings/ProjectSettings.asset`

Do not hide a nested-project recurrence using `.gitignore`.

Do not kill the user's running Unity editor merely to acquire a test lock.

If the editor lock prevents automated tests:
- stop the test attempt safely
- report the blocker
- continue only when the project is available

Scene/editor tools such as `TempleArenaPass`, `PresentationBootstrap`, `UxPolishPass`, or similar regenerators must not be run blindly.

---

# 9. Testing / Evidence Rules

At every implementation phase:

- discover the current actual test count
- do not assume an old count is authoritative
- run the complete relevant Unity test suite
- record total / passed / failed / skipped
- record log/result paths
- record compile evidence
- distinguish written tests from executed tests
- preserve historical failed attempts in the plan when useful
- do not weaken assertions just to obtain green results

Automated tests may prove deterministic state/routing/rules.

Automated tests must not be presented as proof of:
- combat feel
- visible telegraph readability
- actual user-perceived animation/VFX
- camera composition
- audio readability
- physical scene interactions unless a real runtime/physics test genuinely covers them
- presentation clarity

Those require a human gate when material.

---

# 10. Phase 4 Handoff

Phase 4 created isolated prototype implementations of:
- PiercingSlash
- AirHover
- Counter

The preview flags exist only to test those mechanics.

They are not final progression.

Legacy Deflect remains separate.

Before Phase 5 starts, the Phase 4 human gameplay checklist must be explicitly passed by the user.

Any Phase 4 checklist item that depends on real personalization state is intentionally carried into Phase 5 rather than being falsely marked complete.

---

# 11. Phase 5 — Four-Stage Runtime Progression

## Goal

Make `RunProgressionState` the authoritative live run state and connect the already-built original/personalized mechanics to the actual four-stage run.

## Required behavior

### Runtime authority

- one authoritative mutable `RunProgressionState` per run
- no independent player/UI/boss copies that can diverge
- gameplay reads technique state from that authoritative progression
- legacy `RunState`/sacrifice data must not remain a second source of truth

A compatibility shell may temporarily exist only if needed for serialized scene safety, but final refined runtime decisions must come from `RunProgressionState`.

### Player technique routing

If state is `Original`:
- SwordWave usable
- RisingSlash usable
- Guard usable
- corresponding personalized mechanic inactive

If state is `Personalized`:
- original player active unavailable
- corresponding personalized mechanic active:
  - SwordWave -> PiercingSlash
  - RisingSlash -> AirHover
  - Guard -> Counter

Preview flags must not be the authoritative run mechanism after integration.

They may remain as explicit dev diagnostics only if isolated and harmless.

### Stage flow

Stage 1:
- neutral guardian
- no inheritance
- player has 3 originals
- victory goes to personalization step

Stage 2:
- guardian inherits #1
- main archetype = #1
- player has 1 personalized + 2 originals

Stage 3:
- guardian inherits #1 + #2
- main archetype remains #1
- player has 2 personalized + 1 original

Stage 4:
- guardian inherits all 3 originals
- main archetype remains #1
- player has all 3 personalized
- victory completes the run

### Personalization

Phase 5 may use a minimal functional selector/transition.

The polished player-facing selection UX belongs to Phase 8.

Even the temporary Phase 5 path must not present the mechanic as sacrifice/unconscious.

After Stage 3:
- only the final original remains
- it becomes personalized before Stage 4

### Legacy migration

Audit:
- `RunDirector`
- `RunState`
- `SacrificeRules`
- `PrototypeIds`
- altar/transition flow
- `PlayerAbilityState`
- `PlayerCombat`
- HUD
- boss configuration
- restart

Do not:
- map legacy Deflect to Counter
- make old projectile transfer the final SwordWave personalization
- dual-write old/new run models indefinitely
- preserve legacy semantics merely because they already work

Prefer a cohesive migration to one runtime authority.

### Restart/reset

New run must reset:
- stage
- player HP
- guardian HP
- technique states
- personalization order
- guardian inheritance
- main archetype
- projectiles
- cooldowns
- selection state
- PiercingSlash transient state
- AirHover transient state/gravity
- Counter window/cooldown
- temporary VFX

## Automated evidence

At minimum:
- new run gives all three originals
- each of 3 first choices produces correct Stage 2 state
- all six orders
- exact state after first/second/third personalization
- first archetype never changes
- guardian inherited originals accumulate in order
- originals become unavailable exactly when personalized
- matching personalized mechanic becomes active
- Stage 4 has all three personalized + all three inherited originals
- completion occurs only after Stage 4 victory
- restart restores a clean Stage 1 state
- no stale transient combat state
- no dual authoritative state divergence
- complete regression suite

## Human gate

Required.

Manual checklist must prove:
- Stage 1 -> selection -> Stage 2 transition
- chosen original disappears from player behavior
- matching personalized mechanic is usable
- next guardian reflects inherited original
- Stage 2 -> Stage 3
- Stage 3 -> Stage 4
- Stage 4 player has all three personalized mechanics
- Stage 4 victory ends run
- restart returns to clean Stage 1
- no old sacrifice/Deflect semantic leak in normal refined flow

Do not begin Phase 6 until this passes.

---

# 12. Phase 6 — Guardian Pattern Architecture

## Goal

Make guardian inheritance modular and scalable rather than hardcoded per stage/order.

## Required architecture

Refactor only as much as necessary.

Preferred conceptual separation:
- guardian controller/lifecycle
- guardian brain/decision layer
- pattern selector
- modular guardian attack patterns

Possible abstraction:
- `IGuardianPattern`
- `GuardianPattern`

Core patterns:
- BasicMelee
- SwordWave
- RisingSlash
- Guard

Combination patterns where needed:
- Rising + SwordWave
- Guard -> SwordWave
- Guard -> RisingSlash

Do not create six separate guardian controllers.

Do not create a giant generic framework beyond the project's needs.

## Selection rules

Pattern selection may consider:
- distance
- cooldown
- stage
- inherited originals
- main archetype
- previous action
- recovery
- player position

Required behavior:
- one primary attack behavior at a time
- major attacks telegraphed
- meaningful recovery
- cooldowns respected
- avoid same-action spam
- avoid intentionally unavoidable overlaps
- non-inherited patterns unavailable
- Stage 3/4 accumulation works

## Automated evidence

- pattern eligibility by inherited set
- no unowned inherited technique used
- cooldown/action-lock behavior
- no simultaneous incompatible primary actions
- Stage 3 has #1 + #2 pattern capability
- Stage 4 has all 3
- archetype information reaches selector
- regression suite passes

## Human gate

Required.

Demonstrate representative Stage 2, 3, and 4 fights.

Verify:
- inherited patterns are perceptible
- major attacks are telegraphed
- player receives reaction time
- recovery/counterplay exists
- combinations do not create obviously unavoidable overlaps
- Stage 3/4 feel more complex without becoming random spam

---

# 13. Phase 7 — Guardian Archetypes

## Goal

Make the first personalization create a persistent recognizable main guardian identity.

## SwordWave archetype

Required:
- signature ranged pressure
- readable SwordWave effect
- distance-oriented behavior

Possible:
- retreat/reposition
- ranged slash
- punish unsafe approach

If later RisingSlash inherited:
- rise/jump -> SwordWave at altered height / vertical-to-ranged sequence

If later Guard inherited:
- brief Guard -> SwordWave

## RisingSlash archetype

Required:
- vertical pressure
- rising attack
- air/ground threat

Possible:
- aggressive rising slash
- punish jump timing
- rise -> SwordWave if SwordWave later inherited
- Guard -> RisingSlash if Guard later inherited

## Guard archetype

Required:
- readable finite defensive state
- punishes careless attack
- clear recovery/counterplay
- never permanent invulnerability

Possible:
- Guard -> SwordWave if SwordWave inherited
- Guard -> RisingSlash if RisingSlash inherited

## Visual differentiation

Minimum prototype distinction:
- SwordWave: blade/ranged energy accent
- RisingSlash: vertical/wind/upward accent
- Guard: defensive sigil/shield/guard accent

Later inheritance:
- minor VFX/material traits only
- does not replace base identity

Do not over-invest in final 3D art.

## Automated evidence

- archetype derived only from first personalization
- later personalization never changes archetype
- all six orders retain correct first archetype
- selector receives correct archetype
- each archetype has distinct weighting/signature capability
- Stage 4 still keeps first archetype

## Human gate

Required.

Provide a deterministic/dev-safe way to see all three archetypes.

Verify:
- SwordWave type reads primarily ranged
- RisingSlash type reads primarily vertical
- Guard type reads primarily defensive/counter-oriented
- later inherited skills add behavior but do not erase the primary identity
- visual differences are sufficient for prototype readability

---

# 14. Phase 8 — Personalization Selection Flow

## Goal

Replace temporary/legacy personalization presentation with the agreed player-facing 고유화 flow.

## Required

- no player-facing sacrifice wording
- no unconscious terminology
- reuse altar/transition only where useful
- show remaining original techniques
- show corresponding personalized result
- show original technique next guardian receives
- allow selection change before confirmation
- confirmation locks choice for current run
- duplicate confirmation cannot apply twice

Exact mappings:
- 검기 -> 관통베기
- 상승베기 -> 체공
- 방어 -> 반격

After Stage 3:
- only one original remains
- still show transformation/confirmation
- do not pretend there is a meaningful multi-choice decision

Keep transition concise.

Player should understand:
1. original active is removed
2. personalized effect is gained
3. original goes to next guardian

## Automated evidence

- only remaining originals selectable
- cancel/change before confirm works
- confirm mutates exactly once
- state cannot be undone after confirm
- Stage 3 single-remaining path works
- displayed mapping/inheritance data comes from real state
- restart resets selection state

## Human gate

Required.

Verify:
- wording is current
- choice/result/inheritance are understandable
- changing choice before confirmation works
- choice locks afterward
- no obsolete sacrifice/unconscious wording remains
- Stage 3 single-technique transition is clear
- screen is concise enough for the game

---

# 15. Phase 9 — HUD and UI

## Goal

Expose necessary combat/progression information without overloading the combat screen.

## Player HUD

Required:
- HP
- current stage
- three technique slots
- Original vs Personalized state visible

## Guardian HUD

Required:
- HP
- main guardian type/archetype
- inherited technique indicators

## Remove

- obsolete prototype labels
- debug-only player-facing text
- old sacrifice terminology
- dead input prompts

Use short labels/icons in combat.
Detailed explanation belongs in transition screens.

Avoid unnecessary new hardcoded strings when a simple centralized string/key approach is practical.
Do not turn this phase into full localization.

## Automated evidence

- HUD derives from actual runtime progression
- stage updates
- technique states update after each personalization
- guardian archetype/inherited list update correctly
- restart resets HUD
- no known obsolete player-facing labels in current source
- compile/regressions pass

## Human gate

Required.

Verify:
- readable at gameplay resolution
- no critical overlap/clipping
- state updates correctly after transitions
- not overloaded
- guardian/player information is distinguishable
- no obsolete terminology

---

# 16. Phase 10 — Story and Presentation

## Goal

Align title/start/gameplay presentation with the approved master/disciple story and Japanese-fantasy training-ground identity.

## Required

Preserve:
- master/disciple final trial
- four guardians
- technique personalization as developing one's own swordsmanship
- Japanese-fantasy training-ground environment
- concise gameplay explanation

Do not introduce:
- cursed-mask
- exorcist
- possession
- alternate village/festival story
- long cutscene system

Possible:
- title/start update
- concise opening text
- concise rule explanation
- optional "game story" access if easy

Do not spend major time producing final 3D art.
Keep current stable Japanese arena unless a requirement demands a change.

## Automated evidence

- search player-facing text for obsolete/forbidden story terms
- compile/tests pass
- no progression regression
- no accidental final-2D conversion

## Human gate

Required.

Verify:
- title/start clearly communicates the final-trial premise
- gameplay text is concise
- story is still master/disciple
- Japanese-fantasy direction is retained
- no abandoned story leaks into player-facing presentation

---

# 17. Phase 11 — Camera / Arena / Editor Safety

## Goal

Harden the prototype environment and editor behavior after the full gameplay loop exists.

## Runtime requirements

- preserve `ArenaBounds`
- player clamp stable
- guardian clamp stable
- camera follows correctly
- PiercingSlash does not break camera
- final guardian fits side-view composition
- no old limited-camera-range regression

Do not rebuild camera architecture unless evidence requires it.

## Editor tooling audit

Inspect:
- `TempleArenaPass`
- `PresentationBootstrap`
- `UxPolishPass`
- any other competing scene-generation/editor pass

Requirements:
- no competing scene regeneration
- retained generation pass is idempotent if retained
- entering Play does not regenerate gameplay state
- no accidental duplicate altar/arena structures
- serialized references remain stable

Nested project safety remains mandatory.

## Automated evidence

- compile/tests
- source/editor audit
- bounds/state tests where practical
- nested project path checks
- no unintended scene rewrite
- no duplicate-regeneration behavior evident from tooling

## Human gate

Required.

Verify:
- player at left/right bounds
- guardian at bounds
- normal camera follow
- camera during PiercingSlash
- Stage 4/final guardian composition
- entering Play does not unexpectedly rebuild the scene
- restarting does not produce duplicated environment objects

---

# 18. Phase 12 — Audio / Feedback

## Goal

Ensure important combat/progression events are readable.

Priority is functionality/readability, not audio perfection.

## Required useful feedback

Where applicable:
- sword swing
- hit
- player damage
- guardian damage
- original technique
- personalized major technique
- successful Counter
- personalization
- victory
- defeat

Optional:
- BGM refinement
- advanced hit polish

Optional polish must not block completion.

Avoid building a large generalized audio framework unless current code requires it.

## Automated evidence

- compile/tests
- cue references
- null/lifecycle safety
- no missing required enum/route references
- no regression from feedback code

## Human gate

Required if audible/visual feedback changed.

Verify:
- each required event is perceivable
- Counter success is clear
- personalization success is clear
- victory/defeat are clear
- audio does not obscure gameplay
- no obviously broken/repeating sounds

---

# 19. Phase 13 — Final Validation

## Goal

Validate the complete refined prototype. Do not redesign systems here unless a validation failure requires a corrective loop.

## Automated validation

Must run:
- full progression tests
- all six order tests
- state reset
- inheritance
- all three archetypes
- Stage 4 exact state
- complete gameplay regression suite
- clean compile
- nested-project safety checks
- Windows build

Record:
- total/passed/failed/skipped
- compile result/log
- build output/result
- changed files from any corrective cycle

## Human Run A

Order:
- SwordWave
- RisingSlash
- Guard

Verify:
- Stage 2 SwordWave archetype
- Stage 3 still SwordWave archetype
- Stage 4 all 3 inherited
- player Stage 4 has all 3 personalized

## Human Run B

Order:
- Guard
- SwordWave
- RisingSlash

Verify:
- Stage 2 Guard archetype
- later techniques do not replace Guard main identity

## Human Run C

First choice:
- RisingSlash

Verify:
- Stage 2 RisingSlash archetype

## Additional manual validation

- restart returns clean state
- Windows build launches
- no critical `Player.log` exceptions
- controls still work
- presentation remains usable
- no obsolete player-facing terminology

Any human failure:
- reopen Phase 13
- corrective implementer -> tester -> verifier loop
- rerun affected validation
- do not proceed until passed

---

# 20. Phase 14 — Documentation

## Goal

Document the actual finished prototype truthfully.

Update/create:
- `docs/PROTOTYPE_STATUS.md` or equivalent final status document

Must clearly distinguish:

FINAL GAME:
- 2D side-scrolling boss-rush

CURRENT PROTOTYPE:
- 2.5D technical/gameplay prototype

Document:
- controls
- three originals
- three personalized techniques
- four-stage progression
- personalization
- cumulative guardian inheritance
- first-choice archetypes
- all-six-order support
- current HUD/selection flow
- restart behavior
- test status
- Windows build status
- known limitations
- prototype-only 3D assets
- what remains for final 2D production

Documentation must not describe planned/unverified behavior as completed behavior.

Verifier must compare documentation claims against current code/tests/build evidence.

If only documentation changes remain and all claims are already supported:
- Phase 14 may end with `PASS_AUTOMATED`
- no extra gameplay human gate is required

---

# 21. Final Done Criteria

Do not declare refinement complete until all are supported by evidence:

- full Stage 1 -> Stage 4 run works
- all six orders structurally supported
- all three guardian main archetypes work
- all three personalized mechanics work
- originals become unavailable after personalization
- guardian inheritance accumulates originals
- first archetype persists
- Stage 4 guardian owns all three originals
- Stage 4 player owns all three personalized techniques
- restart is clean
- obsolete player-facing sacrifice/unconscious terminology is gone
- Japanese-fantasy/master-disciple presentation remains
- compile is clean
- tests pass
- Windows build succeeds and launches
- no critical runtime exception
- no nested Unity project exists
- documentation distinguishes current 2.5D prototype from final 2D production

The agents still do not commit/push/merge.

At final completion, coordinator returns:
- final implementation summary
- final automated evidence
- final human evidence
- build result
- known limitations
- exact Git status
- professional recommended commit grouping/messages

---

# 22. Required Phase-Boundary Report

At every phase boundary return:

## PHASE
Current phase number/name.

## VERDICT
`PASS_AUTOMATED`, `HUMAN_GATE`, `BLOCKED`, or `DESIGN_DECISION_REQUIRED`.

## IMPLEMENTED
What actually changed.

## AUTOMATED EVIDENCE
Compile/test/build evidence that actually ran.

## REGRESSION STATUS
What existing behavior was revalidated.

## SPEC / REQUIREMENTS COMPLIANCE
Why the implementation still matches the approved design.

## NOT PROVEN
Anything automation cannot establish.

## HUMAN TEST
Only when needed; smallest exact checklist.

## FILES CHANGED
Exact paths.

## GIT STATUS
Branch, dirty/clean status, and confirmation that no commit/push/merge was performed.

## UNITY PROJECT SAFETY
Nested project checks.

## REFINEMENT PLAN
What checklist/evidence was updated.

## NEXT ACTION
Exact next step.

Do not start a later phase while the current phase is `HUMAN_GATE`, `BLOCKED`, or `DESIGN_DECISION_REQUIRED`.
