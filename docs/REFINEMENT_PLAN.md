# K4RMA Prototype Refinement Plan

Status: Execution checklist
Reference: docs/REFINED_GAME_SPEC.md

IMPORTANT:
Do not start a later phase until the current phase compiles and its relevant tests pass.

# Unity project opening

- Do not launch `PrototypeArena.unity` directly through Explorer or a file association.
- Open `D:\Workplace\Gaming\K4RMA\Game` as the Unity project through Unity Hub or an explicit `-projectPath`.
- Then open `Assets/_Project/Scenes/PrototypeArena.unity` from inside the Unity Editor.

Unity `6000.6.2f1` was started with `-openfile` on that scene and inferred `Game/Assets` as the project directory. That created the nested project removed in Phase 0. Do not add gitignore rules for `Game/Assets/Packages` or `Game/Assets/ProjectSettings`. If that nested project returns, it should stay visible in git status.

---

# Phase 0 — Safety and Baseline

Goal:
Establish known-good repository/project state before editing.

Tasks:

- [x] Confirm current Git branch.
- [x] Run `git status`.
- [x] Confirm Unity project root is `K4RMA/Game`.
- [x] Confirm `Game/Packages` exists.
- [x] Confirm `Game/ProjectSettings` exists.
- [x] Confirm `Game/Assets/Packages` does NOT exist. Removed 2026-10-04. Still absent after the Phase 2 Unity import.
- [x] Confirm `Game/Assets/ProjectSettings` does NOT exist. Removed 2026-10-04. Still absent after the Phase 2 Unity import.
- [ ] Open current PrototypeArena scene in the Editor. Not done. The saved scene YAML was audited instead.
- [x] Confirm current project compiles.
- [x] Run existing tests.
- [x] Record existing test count/result.
- [x] Record current known gameplay flow.
- [x] Do not commit.
- [x] Do not push.
- [x] Do not merge.

Evidence (2026-10-04, audit only, no gameplay edits):

- branch: `feat/real-asset-integration` at `9de4251`. Not `feat/2-5d-prototype`.
- project root: `Game/`. `Game/Packages/manifest.json` and `Game/ProjectSettings/ProjectVersion.txt` exist. Editor pin is `6000.6.2f1 (770e33f6875c)`.
- compile: Unity `6000.6.2f1` batchmode compiled scripts before the test run. Log `Game/Logs/audit-editmode.log` records script compilation time 17.481850s and contains no `error CS`. Batch test process exited with code 0.
- tests: EditMode assembly `K4RMA.Gameplay.Tests` only. 5 tests, 5 passed, 0 failed, 0 skipped. Result file `Game/Logs/audit-editmode-results.xml` (`testcasecount="5" result="Passed"`). Started 2026-10-04 10:44:11Z. Licensing warnings appeared in the log and did not fail the run.
- known baseline issues:
  - Accidental nested Unity data under `Game/Assets/` was present at audit time: untracked `Packages/` and `ProjectSettings/`, gitignored `Library/`, and untracked `Library.meta`, `Logs.meta`, `Packages.meta`, `ProjectSettings.meta`, `UserSettings.meta`.
  - Cleanup after Phase 1 acceptance removed those paths, plus the nested `Game/Assets/Logs/` and `Game/Assets/UserSettings/` directories created by the same Unity open. `Game/Packages`, `Game/ProjectSettings`, `Game/Assets/Assets`, `Game/Assets/Settings`, and `Game/Assets/Scenes` were not changed. Re-check after the Phase 2 batch import: `Game/Assets/Packages`, `Game/Assets/ProjectSettings`, and `Game/Assets/Library` are absent. `Game/Packages/manifest.json` and `Game/ProjectSettings/ProjectSettings.asset` are present. No gitignore rule was added.
  - `Game/Assets/Assets/` is tracked art (`Stylized Asia RG`, 578 tracked files). `Game/Assets/Settings/` is the real URP settings folder (`UniversalRenderPipelineGlobalSettings` guid is referenced by `ProjectSettings/GraphicsSettings.asset`). `Game/Assets/Scenes/SampleScene.unity` is tracked template content and is not in the build. Do not treat those three as the nested project.
  - Build list contains only `Assets/_Project/Scenes/PrototypeArena.unity`.
  - `TempleVolume.asset` profile `components` are three null `{fileID: 0}` entries. The scene Volume is global and points at that profile.
  - `SideViewCamera` fallback `minX`/`maxX` are still `-5.5`/`5.5`. The live path uses `ArenaBounds` `-8.4`/`8.4` because `arenaBounds` is assigned.
  - Batch run did not dirty tracked gameplay files. Untracked docs and the nested folders above were already untracked before the run.

---

# Phase 1 — Audit Existing Architecture

Goal:
Understand current implementation before modifying systems.

Inspect:

- [x] RunDirector
- [x] player skill/ability model
- [x] AbilityDefinition or equivalent
- [x] PlayerCombat
- [x] PlayerController
- [x] BossController
- [x] boss attacks/pattern logic
- [x] SacrificeSequence
- [x] AltarGuide
- [x] AltarPresence
- [x] HudPresenter
- [x] stage flow
- [x] scene dependencies
- [x] current editor passes
- [x] tests
- [x] docs/PROTOTYPE_STATUS.md

Identify all references to:

- [x] Flame — altar dressing primitive in `PresentationPass` / scene object `Flame`. Not a technique.
- [x] Dash — id `proto.dash` and `Dash.asset` only. No movement dash. Not read by combat.
- [x] Ward — no gameplay or scene references.
- [x] Projectile — wired player shot and boss shot. `Projectile.cs`, `Projectile.prefab`, `PlayerCombat`, `BossController`.
- [x] proto.projectile — the only id `SacrificeRules` accepts. Baked on `BossStage2.asset` and on `Altar.sacrificeAbilityId`.
- [x] Deflect — `proto.projectile.deflect` set by `SacrificeRules`. `PlayerCombat` opens a timed window. `Projectile` punishes only when the shot's `sourceBoss` is set.
- [x] Sacrifice — class and method names, altar field, tests, older docs (`README`, `DECISIONS` D-001/D-012, `PROJECT_CONTEXT`, `AGENTS.md`). Scene copy does not use the word.
- [x] Unconscious — no code, scene, or asset matches for unconscious / 무의식 / 무의식화 / 의식 / 희생.
- [x] Passive conversion — no implementation. `AbilityDefinition.combatWired` is written by the scene builder and never read at runtime.
- [x] Stage 2 hardcoding — `RunDirector.stageOneBoss` / `stageTwoBoss`, `RunPhase` has no stage 3/4, `SacrificeRules` always sets `StageIndex = 2`, `BossStage2.asset` pre-bakes `inheritedAbilityId: proto.projectile`. `BossController` does not read `RunState`.

Create notes:

- [x] reusable systems
- [x] obsolete systems
- [x] risky serialized fields
- [x] hardcoded paths
- [x] architecture changes required

Do NOT implement gameplay changes during the initial audit.

Deliverable:
Architecture assessment recorded below. Gameplay was not changed.

## Phase 1 audit record (2026-10-04)

Current run is a two-boss slice. `RunState` is a plain class created in `RunDirector.Awake` from `CreatePrototypeStart()`. It is not static and not `DontDestroyOnLoad`.

`RunPhase`: `Fight`, `Altar`, `Defeat`, `SliceComplete`. `StageIndex` is 1, then 2 after the only transfer. Stage 1 death sets `Altar` only when `StageIndex == 1`. Stage 2 death sets `SliceComplete`. Player death sets `Defeat` and disables both bosses.

Abilities are mixed, not an enum. `PrototypeIds` string constants. `AbilityDefinition` assets (`Dash`, `Projectile`, `Guard`) are catalog metadata on `PlayerAbilityState`. Combat behavior is hardcoded `if`s in `PlayerCombat` and `BossController`. Start owns `proto.dash`, `proto.projectile`, `proto.guard`, with `ActiveAbilityId = proto.projectile`. Only the projectile is combat-wired. K fires the shot, or Deflect after transfer. J is melee. There is no dash, no guard, no pause.

Transfer: altar trigger + E calls `BeginSacrifice("proto.projectile")`. `SacrificeSequence` plays, then `TrySacrifice`. Rules remove the projectile, set `SacrificedAbilityId` and `BossInheritedAbilityId` to that id, clear `ActiveAbilityId`, set `CounterAbilityId` to `proto.projectile.deflect`, set stage 2 and `Fight`. Director deactivates BossStage1, activates BossStage2, restores player HP, and moves the player to `PlayerSpawn`. The stage 2 shot exists because `BossStage2.asset` already contains `inheritedAbilityId: proto.projectile`. `RunState.BossInheritedAbilityId` is not consumed by the boss.

Boss attack choice is one `BossState` switch: approach, melee telegraph/strike/recover, and, only when the config id equals `proto.projectile` and cooldown is ready, projectile telegraph/shot. No pattern objects. `GuardianPose` special-cases `MeleeTelegraph`, `MeleeStrike`, and `ProjectileTelegraph`.

HUD reads `RunDirector.State`, health components, and `PlayerAbilityState`. It clears banner, ability label, and prompt every frame. Slot index 1 changes color when `SacrificedAbilityId == proto.projectile`. Slots 0 and 2 stay dim. Names are not shown. The E glyph shows only in `Altar` while inside the altar. `AltarGuide.Marker` ("Go to the altar") is inactive; `arrowRoot` is null and a chevron is created at runtime during stage-1 altar walk. Title text is "K4RMA" / "The disciple's final trial begins." Ending text is "Vertical Slice Complete".

Restart: R reloads the active scene only in `Defeat` or `SliceComplete`. That recreates `RunState`. R during fight or altar does nothing. `ImpactFeedback` restores `Time.timeScale` in `OnDestroy`. No run-state leak was found across that reload. Shared ScriptableObjects are read, not written, by current combat code.

Reusable: `Health`, `MeleeHitbox`, `IKnockback`, `BodyTint`, `ArenaBounds`, `SideViewCamera`, `PlayerController` movement, `PlayerInputReader`, `Projectile` travel/hit, `ImpactFeedback` / `AudioFeedback` / `SparkBurst`, altar trigger + short sequence timing, `PresentationShell` title/ending gate, pure C# rules + EditMode tests with no scene.

Replace for the new model: `RunState`, `SacrificeRules`, `PrototypeIds` as the skill list, single `ActiveAbilityId`, hardcoded altar id, two serialized boss slots, `BossController` projectile branch, `BossEncounterConfig.inheritedAbilityId`, HUD slot coloring, sacrifice naming in player-facing flow. Keep script file GUIDs if types move. Do not rename serialized fields without `FormerlySerializedAs`. High-risk fields: `RunDirector` boss/altar/sequence refs, `Altar.sacrificeAbilityId`, `BossController.config` and projectile refs, `PlayerCombat` projectile/weapon refs, `PlayerAbilityState.catalog`, `HudPresenter` health and slot refs, `SideViewCamera.arenaBounds`, `ArenaBounds.left/right`, `BossStage2.inheritedAbilityId`, projectile prefab guid `dee6efeb55c79e340beef8ffe1b48657`.

Editor: `PresentationBootstrap` is empty. `UxPolishPass` only logs. `PrototypeSceneBuilder` creates the arena only when the scene file is missing. `PresentationPass` (`K4RMA/Apply Presentation Pass`) rebuilds disciple/guardian visuals and the sacrifice sequence reference. `TempleArenaPass` (`K4RMA/Dress Temple Arena`) destroys `TempleSet`, moves walls, rewrites bounds, rebuilds HUD slots, and can stack another `AltarBase`. Neither runs on Play. A second Dress pass will not repair `TempleVolume`, because a Volume already exists. Do not run those menus during later phases unless the pass is made idempotent first.

Phase 2 should add a pure domain model and tests beside the current slice, and should not yet retarget combat, the scene, or the camera. Proposed types: `TechniqueId`, `PlayerTechniqueState`, `GuardianArchetype`, `TechniqueDefinition`, `PersonalizationOrder`, `GuardianInheritanceState`, `RunProgressionState`, `PersonalizationRules`. New tests in `Game/Assets/_Project/Tests/EditMode/` for initial state, one-at-a-time irreversible personalization, order preservation, first-choice archetype lock, all six permutations, cumulative inheritance, stage 4 holding all three originals, and reset. Existing `SacrificeRulesTests` document the old single path; replace them when `SacrificeRules` is replaced, and keep the pure-domain test style.

---

# Phase 2 — Progression Domain Model

Goal:
Replace single-path prototype progression with final reusable run-state logic.

Implement/refactor:

- [x] TechniqueId
- [x] PlayerTechniqueState
- [x] PersonalizationOrder — `RunProgressionState.PersonalizationOrder`, a read-only list. No separate class.
- [x] GuardianArchetype
- [x] Guardian inheritance state
- [x] RunProgressionState or equivalent

Required techniques:

- [x] SwordWave / 검기
- [x] RisingSlash / 상승베기
- [x] Guard / 방어

Personalized techniques:

- [x] PiercingSlash / 관통베기
- [x] AirHover / 체공
- [x] Counter / 반격

Rules:

- [x] Start with all 3 Original.
- [x] Personalization changes exactly one technique.
- [x] Personalization is irreversible during run.
- [x] Order is recorded.
- [x] Guardian inherits original technique.
- [x] First personalization sets guardian archetype.
- [x] Later techniques never overwrite archetype.
- [x] All 6 orders are valid.
- [x] Stage 4 contains all three inherited originals.
- [x] Restart creates clean initial state.

Tests:

- [x] initial state
- [x] each technique personalization
- [x] order preservation
- [x] first-choice archetype
- [x] later inheritance does not replace archetype
- [x] all six permutations
- [x] Stage 4 inheritance
- [x] restart/reset

The new model is additive. `RunState`, `SacrificeRules`, `PrototypeIds`, `SacrificeRulesTests`, `RunDirector`, combat, the altar, the HUD, and `PrototypeArena.unity` were not changed and are not wired to `RunProgressionState`.

Evidence (2026-10-04):

- Classes: `TechniqueId`, `PlayerTechniqueState`, `GuardianArchetype`, `PersonalizedTechniqueId`, `TechniqueDefinition`, `RunProgressionState`, `GuardianInheritanceState`, `PersonalizationRules`.
- Fixture: `K4RMA.Tests.RunProgressionStateTests`.
- Ordered record: `PersonalizationOrder` is the only stored sequence. `GuardianInheritanceState` is an immutable copy of that sequence plus the derived archetype.
- Compile: Unity `6000.6.2f1` batchmode, log `Game/Logs/phase2-editmode.log`. Script compilation time 24.524998s. No `error CS`. Test process exited with code 0.
- Tests: EditMode assembly `K4RMA.Gameplay.Tests`. `Game/Logs/phase2-editmode-results.xml`: total 30, passed 30, failed 0, skipped 0. Started 2026-10-04 11:38:39Z. 5 legacy `SacrificeRulesTests` passed. 25 new progression cases passed, including 6 `SixOrders_Stage4_PreservesOrderAndFirstArchetype` cases and `PersonalizedResults_MapFromOriginalTechniques`.

Hardening (2026-10-04):

- `TechniqueDefinition` stores localization keys (`OriginalNameKey`, `OriginalDescriptionKey`, `PersonalizedNameKey`, `PersonalizedDescriptionKey`). It does not store Korean display strings. `TechniqueId`, `PersonalizedTechniqueId`, and `GuardianArchetype` mapping is unchanged. Korean wording stays in `docs/REFINED_GAME_SPEC.md`.
- `RunProgressionState` constructor calls `Reset()`, so a new run assigns `Original` explicitly.
- `GuardianInheritanceState.InheritedTechniques` is exposed through `Array.AsReadOnly`. The public list cannot be cast back to `TechniqueId[]`.
- Compile: `Game/Logs/phase2-hardening.log`, script compilation time 62.760852s, no `error CS`, exit code 0.
- Tests: `Game/Logs/phase2-hardening-results.xml`, 2026-10-04 12:21:20Z. Total 31, passed 31, failed 0, skipped 0. 5 legacy `SacrificeRulesTests` and 26 `RunProgressionStateTests` passed, including `Inheritance_PublicListCannotBeMutated`.

Gate:

DO NOT proceed until:
- [x] project compiles
- [x] progression tests pass

---

# Phase 3 — Original Player Techniques

Goal:
Implement stable original forms before personalized transformations.

## 검기

- [x] manually activated
- [x] ranged sword-wave
- [x] readable VFX — implemented; human verification pending
- [x] collision/damage
- [x] cooldown/timing
- [ ] cannot activate after personalization

## 상승베기

- [x] manually activated
- [x] upward sword attack
- [x] player rises — implemented; human verification pending
- [x] correct vertical state handling — grounded and cooldown rules tested; human verification pending
- [ ] cannot activate after personalization

## 방어

- [x] manually activated
- [x] temporary protection
- [x] clear visual state — implemented; human verification pending
- [x] damage handling
- [ ] cannot activate after personalization

"Cannot activate after personalization" stays open. It needs runtime `RunProgressionState` integration, which is not part of Phase 3A.

Gate:

- [ ] all 3 work in Stage 1
- [x] compile clean
- [x] existing tests pass

Phase 3A evidence (2026-10-04):

- K fires the existing player projectile as SwordWave before the legacy transfer. After that transfer, K still opens the old Deflect. Deflect is not PiercingSlash.
- L is RisingSlash: grounded-only launch, upward `MeleeHitbox`, cooldown, gravity left on, cancelled on death or disable.
- I is Guard: `GuardTiming` window then recovery. `Health.ApplyDamage` asks `IIncomingDamageFilter` before HP changes, so melee and projectile hits are blocked and do not apply knockback. Boss health has no filter.
- `RunProgressionState` is not referenced by combat. RisingSlash and Guard stay available during the old stage 2 slice.
- Compile: `Game/Logs/phase3a-editmode.log`, script compilation time 28.644716s, no `error CS`, exit code 0.
- Tests: `Game/Logs/phase3a-editmode-results.xml`, 2026-10-04 13:00:58Z. Total 42, passed 42, failed 0, skipped 0. Previous 31 still passed. 11 new `OriginalTechniqueRulesTests` passed.
- Human play of Stage 1 was not done. The "all 3 work in Stage 1" gate stays open. Readable VFX, RisingSlash feel, and Guard visual readability are not treated as proven by EditMode tests.

Phase 3A hardening (2026-10-04):

- `PlayerController` resolves `PlayerGuard` in `Start` and again on use if the reference is still empty, so the movement multiplier sees the component `PlayerCombat` adds during `Awake`.
- `ApplyDamage(int amount, GameObject source = null)` passes the attacker. Melee passes the attacker root. A boss projectile passes the `BossController` object. A player projectile passes the owning health object. `PlayerGuard` accepts the source and does not use it.
- One new attack can start per frame. Guard input blocks that start before `PlayerGuard` ticks. RisingSlash beats melee and SwordWave. An active RisingSlash blocks the other two, and an active melee swing blocks RisingSlash.
- RisingSlash cooldown is clamped at zero. Gravity, grounded activation, and hitbox close on end, death, and disable were already in place.
- Compile: `Game/Logs/phase3a-hardening.log`, script compilation time 9.736802s, no `error CS`, exit code 0.
- Tests: `Game/Logs/phase3a-hardening-results.xml`, 2026-10-04 13:21:39Z. Total 48, passed 48, failed 0, skipped 0. Previous 42 still passed. 6 new attack-priority cases passed.

RisingSlash damage playtest (2026-10-05):

- Human play: L launches and lands, and guardian HP does not drop. J and K damage that same guardian.
- Cause: `MeleeHitbox.Begin` sampled `Collider.bounds` immediately after enabling a collider that had never been simulated, so the query box was empty. RisingSlash then sets upward velocity before the next physics step, so trigger contact does not land either. The runtime box was also above and short of the working melee box (`y=1.15`, forward `0.35` versus melee `y=0.15`, forward `1.15`).
- Fix: overlap uses the collider transform and size. The slash box sits in front of the player and covers the upward swing. `alreadyHit` still allows one hit per activation. Damage still goes through `Health.ApplyDamage`.
- "All 3 work in Stage 1" stays unchecked until the repaired slash is playtested.
- Compile: `Game/Logs/rising-slash-hit.log`, script compilation time 9.093811s, no `error CS`, exit code 0.
- Tests: `Game/Logs/rising-slash-hit-results.xml`, 2026-10-05 08:17:18Z. Total 48, passed 48, failed 0, skipped 0. No new tests. The contact fix is in Unity physics and was not given a fake EditMode stand-in.

---

# Phase 4 — Personalized Player Techniques

Goal:
Implement the three transformed combat mechanics.

## 검기 → 관통베기

- [ ] original 검기 disabled
- [ ] PiercingSlash integrated with melee flow
- [ ] player crosses guardian where safe
- [ ] damage occurs
- [ ] arena bounds respected
- [ ] walls respected
- [ ] no permanent collider overlap
- [ ] no infinite invulnerability
- [ ] visual trail/feedback

## 상승베기 → 체공

- [ ] original 상승베기 disabled
- [ ] aerial attack can temporarily delay falling
- [ ] hover duration limited
- [ ] reset on landing
- [ ] no infinite flight
- [ ] gravity safely restored
- [ ] knockback/death/reset handled
- [ ] visual feedback

## 방어 → 반격

- [ ] original 방어 disabled
- [ ] timed defensive response
- [ ] readable counter window
- [ ] successful counter staggers guardian
- [ ] clear VFX
- [ ] clear SFX if available
- [ ] no infinite stun loop
- [ ] usable reliably during demo

Tests where practical:

- [ ] original inputs inactive after personalization
- [ ] personalized states active
- [ ] reset clears transient state

Gate:

- [x] compile
- [x] tests
- [ ] manual Stage 1 behavior check

Phase 4 mechanics evidence (2026-10-05):

The three personalized mechanics are playable only through prototype preview flags on `PlayerTuning`. They do not read or write `RunProgressionState`, and the altar was not changed. With all three flags off, J/K/L/I and legacy Deflect stay on the previous path. Checklist rows above stay open: this slice does not disable originals through personalization, and Stage 1 feel was not played.

Dev activation, one mechanic at a time:

1. Open the Unity project `D:\Workplace\Gaming\K4RMA\Game`. Do not open the scene file from Explorer.
2. Select `Assets/_Project/Data/Prototype/PlayerTuning.asset`.
3. Under Prototype mechanic preview, enable only the mechanic to try. Leave the others off. The asset currently stores all three as off.
4. Open `PrototypeArena` inside the editor and press Play.
5. Turn the flag off again before ordinary play. Saving the asset with a flag on changes the next Play session.

- Piercing Slash: K, only while legacy Deflect is not active. After the old altar transfer, K remains Deflect.
- Air Hover: jump, then J while airborne. L does not launch.
- Counter: tap I as the hit arrives. Holding I is not Guard while this flag is on.

Implementation notes, not playtest proof:

- `PlayerPiercingSlash` is a short faced traversal. A miss moves at most `piercingSlashMissDistance` and stops inside `ArenaBounds`. A contacted body is damaged once through `Health.ApplyDamage`, then the player is moved to a non-overlapping exit on the far side when that exit fits, otherwise to the clear near side. Body collision with that target is ignored only during the cross and restored on finish, cancel, death, or disable.

Wall sweep correction (2026-10-05):

- `PiercingSlashClearance` capsule-casts the player body along the whole horizontal route, including each movement step and both candidate exits. Floor-only colliders are skipped. The contacted guardian is ignored for that route. Other solid colliders, including walls and pillars, are not.
- If the far exit or the path to it hits solid environment, the near exit is used when that path is clear. If both are blocked, the player stops at the last unblocked point on the preferred route and is not moved into the collider. Arena clamping remains. Guardian collision-ignore is still restored when the move ends. One `Health.ApplyDamage` call per activation is unchanged.
- `PiercingSlash_SolidWallStopsTheSweepBeforeContact`, `PiercingSlash_BlockedFarSideUsesAClearNearSide`, `PiercingSlash_WallsOnBothSidesStopOutsideTheGeometry`, `PiercingSlash_IgnoredGuardianDoesNotBlockAndAWallStillDoes`, and `PiercingSlash_OpenSweepReachesTheRequestedExit` place real colliders and call `Physics.CapsuleCast` / `Physics.OverlapCapsule`. They are not arena-number checks. Unity `6000.6.2f1` batchmode EditMode ran them on 2026-10-05 after `Game/Temp/UnityLockfile` was absent. No `-openfile`. All five passed (`Game/Logs/wall-sweep-editmode-results.xml`). That run was 82 passed, 1 failed, 0 skipped of 83. The failure is `Counter_BlocksPlayerDamageOnceAndRetaliatesOnce`: EditMode logs an error because `SparkBurst` calls `Destroy` during the counter test. That failure is outside this wall-sweep fix. The manual Stage 1 gate stays unchecked.
- `PlayerAirHover` starts only from an airborne J. It sets vertical speed to at most 0 for `airHoverDurationSeconds`, then restores gravity. One use per airborne period, plus `airHoverCooldownSeconds`. Landing rearms. Knockback cancels it. Death, disable, and turning the preview off clear it. `PlayerRisingSlash` does not launch while the preview is on.
- `PlayerCounter` opens a tap window. `Health.ApplyDamage` asks every `IIncomingDamageFilter` and stops before HP changes if one accepts the hit. One open window accepts one hit, retaliates once through `Health.ApplyDamage`, then closes. `BossController.ApplyCounterStagger` only enters Hurt. It does not call `ApplyDeflectPunish` and does not apply damage. Enemy health has no counter filter.

Checks:

- Unity batchmode EditMode did not run. The editor already had `D:\Workplace\Gaming\K4RMA\Game` open, so a second instance aborted. That editor was left open.
- Standalone Roslyn compile of `K4RMA.Gameplay` and `K4RMA.Gameplay.Tests` finished with no `error CS`. That is not the Unity test runner. The new cases were not executed. Serialized-field warnings from that standalone compile are the usual Unity inspector fields and were not treated as failures.
- `Game/Assets/Packages`, `Game/Assets/ProjectSettings`, and `Game/Assets/Library` were absent after the aborted batch attempt. `Game/Packages/manifest.json` and `Game/ProjectSettings/ProjectSettings.asset` were present.
- No commit, push, or merge. Branch remained `feat/real-asset-integration` at `6f2156d`.

Independent test attempt (2026-10-05, tester):

- The user's Unity `6000.6.2f1` processes were left running. `Game/Temp/UnityLockfile` was held by another process. The tester did not kill those processes.
- Command: `Unity.exe -batchmode -nographics -projectPath D:\Workplace\Gaming\K4RMA\Game -runTests -testPlatform EditMode -assemblyNames K4RMA.Gameplay.Tests`. No `-openfile`.
- The suite was NOT executed. No `phase4-editmode-results.xml` was written. Passed, failed, and skipped counts are unknown.
- Console abort: `Aborting batchmode due to fatal error: It looks like another Unity instance is running with this project open.` Project: `D:/Workplace/Gaming/K4RMA/Game`.
- Log `Game/Logs/phase4-editmode.log` (2026-10-05 10:56:37Z) records the same command and ends with `Application will exit with return code 1`. It contains no `error CS` and no test summary because compilation never started.
- After the abort, `Game/Assets/Packages`, `Game/Assets/ProjectSettings`, and `Game/Assets/Library` were absent. `Game/Packages/manifest.json` and `Game/ProjectSettings/ProjectSettings.asset` were present.
- Three unrun cases were added to `PersonalizedTechniqueRulesTests`: `SavedPlayerTuning_PreviewFlagsAreOff`, `PreviewRouting_LeavesRunProgressionUntouched`, and `CounterStagger_DoesNotDamageAndIsNotDeflectPunish`. They are not a pass.
- Last executed EditMode total remains the earlier run: 48 passed, 0 failed, 0 skipped (`Game/Logs/rising-slash-hit-results.xml`).
- Compile, tests, and manual Stage 1 gates stay open. Dress Temple Arena and Apply Presentation Pass were not run.

Independent retest (2026-10-05, tester):

- The earlier "not run" attempt above stays as history. This retest executed the suite. `Game/Temp/UnityLockfile` was absent. The only `unity.exe` process was Unity Hub `serve`, not the editor. No editor process was killed. No `-openfile`.
- Command: Unity `6000.6.2f1` `-batchmode -nographics -projectPath D:\Workplace\Gaming\K4RMA\Game -runTests -testPlatform EditMode -assemblyNames K4RMA.Gameplay.Tests`. Results `Game/Logs/phase4-retest-results.xml`. Log `Game/Logs/phase4-retest.log`. Started 2026-10-05 11:15:35Z.
- Compile: log line `AssetDatabase: script compilation time: 3.012433s`. No `error CS` in that log.
- Tests: total 83, passed 82, failed 1, skipped 0, inconclusive 0. XML result `Failed(Child)`. The editor log says `Exiting with code 2 (Failed). One or more tests failed.`
- The only failure is `K4RMA.Tests.PersonalizedTechniqueRulesTests.Counter_BlocksPlayerDamageOnceAndRetaliatesOnce`. Message: `Unhandled log message: '[Error] Cube: Destroy may not be called from edit mode! Use DestroyImmediate instead. Destroying an object in edit mode destroys it permanently'.` Stack: `SparkBurst.Build` line 40, `SparkBurst.Play`, `PlayerCounter.Retaliate` line 134, `Health.ApplyDamage` line 39, test line 542. The XML failure is that log, not an HP assertion. Six cube errors match the six shards. `Object.Destroy` is valid in Play Mode. EditMode rejects it and the test runner treats the error log as a failure. Not changed.
- Wall-sweep cases that passed in this same XML: `PiercingSlash_OpenSweepReachesTheRequestedExit`, `PiercingSlash_SolidWallStopsTheSweepBeforeContact`, `PiercingSlash_BlockedFarSideUsesAClearNearSide`, `PiercingSlash_WallsOnBothSidesStopOutsideTheGeometry`, `PiercingSlash_IgnoredGuardianDoesNotBlockAndAWallStillDoes`. Those five create `CapsuleCollider` / `BoxCollider` objects, call `Physics.SyncTransforms`, and go through `PiercingSlashClearance.TryMove` / `ChooseExit` (`Physics.CapsuleCastAll` and `Physics.OverlapCapsule`). They are not number-only checks. Older cases such as `PiercingSlash_WallBlocksTheFarSideAndKeepsAClearNearSide` still only call `PiercingSlashRules` with floats.
- No automated case places a floor or ceiling collider. No case drives `PlayerPiercingSlash` through scene walls. Manual Stage 1 stays unchecked.
- After the run, `Game/Assets/Packages`, `Game/Assets/ProjectSettings`, and `Game/Assets/Library` were absent. `Game/Packages/manifest.json` and `Game/ProjectSettings/ProjectSettings.asset` were present.
- Branch remained `feat/real-asset-integration` at `6f2156d`. No commit, push, or merge. Dress Temple Arena and Apply Presentation Pass were not run.

SparkBurst EditMode destroy fix (2026-10-05):

- `SparkBurst.Build` now removes each shard collider with `DestroyImmediate` only when `Application.isPlaying` is false. Play Mode still uses `Destroy`. Counter HP assertions were not changed.
- Unity `6000.6.2f1` batchmode EditMode reran `K4RMA.Gameplay.Tests` with the project lock absent. No `-openfile`. Results `Game/Logs/phase4-spark-results.xml`. Log `Game/Logs/phase4-spark.log`. The log says `Exiting with code 0 (Ok)`.
- Counts: total 83, passed 83, failed 0, skipped 0, inconclusive 0. `Counter_BlocksPlayerDamageOnceAndRetaliatesOnce` passed. The manual Stage 1 gate stays unchecked.

Independent final verification (2026-10-05, tester):

- Source check: `SparkBurst.Build` calls `Destroy` when `Application.isPlaying` is true and `DestroyImmediate` when it is false. `Counter_BlocksPlayerDamageOnceAndRetaliatesOnce` still asserts a blocked 12 leaves the player at 100, one retaliation drops the attacker from 40 to 31, and the next 12 drops the player to 88 while the attacker stays at 31.
- This tester ran Unity `6000.6.2f1` batchmode EditMode. `Game/Temp/UnityLockfile` was absent. No editor process was killed. No `-openfile`. Results `Game/Logs/phase4-final-results.xml`. Log `Game/Logs/phase4-final.log`. Log start 2026-10-05T11:24:06Z. Test run 2026-10-05 11:25:57Z.
- Compile: `AssetDatabase: script compilation time: 77.078091s`. No `error CS`. Log line: `Test run completed. Exiting with code 0 (Ok).`
- Tests: total 83, passed 83, failed 0, skipped 0, inconclusive 0. XML result `Passed`. `Counter_BlocksPlayerDamageOnceAndRetaliatesOnce` result `Passed`. No failure names.
- After the run, `Game/Assets/Packages`, `Game/Assets/ProjectSettings`, and `Game/Assets/Library` were absent. `Game/Packages/manifest.json` and `Game/ProjectSettings/ProjectSettings.asset` were present.
- Manual Stage 1 stays unchecked. EditMode does not prove Play Mode feel, gravity, scene collision, or readable counter feedback. HUMAN_GATE is still required.

---

# Phase 5 — Four-Stage Progression

Goal:
Implement full run.

Stage 1:

- [ ] neutral guardian
- [ ] player has 3 originals
- [ ] victory enters personalization selection

Stage 2:

- [ ] guardian has inherited #1
- [ ] main archetype = #1
- [ ] player has 1 personalized + 2 originals

Stage 3:

- [ ] guardian has #1 + #2
- [ ] main archetype remains #1
- [ ] #2 adds pattern
- [ ] player has 2 personalized + 1 original

Stage 4:

- [ ] guardian has all 3 originals
- [ ] main archetype remains #1
- [ ] player has all 3 personalized techniques
- [ ] victory ends run

Gate:

- [ ] complete one full run
- [ ] restart works
- [ ] no stale state

---

# Phase 6 — Guardian Pattern Architecture

Goal:
Make inheritance modular rather than hardcoded per stage.

Refactor if necessary:

- [ ] GuardianBrain
- [ ] GuardianPatternSelector
- [ ] pattern abstraction

Core patterns:

- [ ] BasicMelee
- [ ] SwordWave
- [ ] RisingSlash
- [ ] Guard

Combination patterns as needed:

- [ ] Rising + SwordWave
- [ ] Guard → SwordWave
- [ ] Guard → RisingSlash

Rules:

- [ ] one main action at a time
- [ ] telegraph before major attack
- [ ] recovery after major attack
- [ ] no unavoidable overlaps
- [ ] cooldowns respected
- [ ] avoid identical-action spam
- [ ] stage/archetype affects weighting

Do not create six separate guardian controllers.

Gate:

- [ ] all three archetypes demonstrable
- [ ] Stage 3 accumulation works
- [ ] Stage 4 uses all three

---

# Phase 7 — Guardian Archetypes

## 검기형

- [ ] signature ranged pressure
- [ ] readable sword-wave VFX
- [ ] distance-oriented behavior

## 상승베기형

- [ ] vertical pressure
- [ ] rising attack
- [ ] air/ground threat

## 방어형

- [ ] readable guard state
- [ ] punish careless attack
- [ ] finite guard duration
- [ ] recovery/counterplay

Visual differentiation:

- [ ] 검기 VFX/accent
- [ ] 상승베기 VFX/accent
- [ ] 방어 VFX/accent

Later inheritance:

- [ ] adds minor visual traits
- [ ] does not replace base identity

Do not over-invest in 3D art.

---

# Phase 8 — Personalization Selection Flow

Goal:
Replace sacrifice presentation.

- [ ] remove player-facing Sacrifice wording
- [ ] remove unconscious terminology
- [ ] reuse altar/transition system where useful
- [ ] show remaining original techniques
- [ ] show personalized result
- [ ] show next guardian inheritance
- [ ] allow selection change before confirmation
- [ ] lock choice after confirmation
- [ ] Stage 3 handles single remaining technique cleanly

Required examples:

검기
→ 관통베기

상승베기
→ 체공

방어
→ 반격

---

# Phase 9 — HUD and UI

Player HUD:

- [ ] HP
- [ ] stage
- [ ] three technique slots
- [ ] Original vs Personalized visible

Guardian HUD:

- [ ] HP
- [ ] guardian type
- [ ] inherited techniques

Remove:

- [ ] obsolete prototype labels
- [ ] debug-only user-facing text
- [ ] old sacrifice terminology
- [ ] dead input prompts

Do not overload combat screen.

---

# Phase 10 — Story and Presentation

- [ ] title / start presentation updated
- [ ] master/disciple final trial retained
- [ ] Japanese-fantasy environment retained
- [ ] no cursed-mask/exorcist story
- [ ] concise rules shown in run
- [ ] optional longer story accessible only if easy

Prototype environment:

- [ ] keep current stable Japanese arena
- [ ] do not spend major time on new 3D art

---

# Phase 11 — Camera / Arena / Editor Safety

- [ ] preserve ArenaBounds
- [ ] player clamp stable
- [ ] guardian clamp stable
- [ ] camera follows correctly
- [ ] final guardian fits
- [ ] no old camera-range regression

Editor scripts:

- [ ] inspect TempleArenaPass
- [ ] inspect PresentationBootstrap
- [ ] inspect UxPolishPass
- [ ] prevent competing scene regeneration
- [ ] make retained pass idempotent
- [ ] do not auto-regenerate gameplay state on Play

Check:

- [ ] no `Game/Assets/Packages`
- [ ] no `Game/Assets/ProjectSettings`

---

# Phase 12 — Audio / Feedback

Mandatory combat readability first.

- [ ] sword swing
- [ ] hit
- [ ] damage
- [ ] major technique
- [ ] successful counter
- [ ] personalization
- [ ] victory/defeat

Optional:

- [ ] BGM refinement
- [ ] advanced impact polish

Do not block completion on sound polish.

---

# Phase 13 — Validation

Automated:

- [ ] progression tests
- [ ] six order tests
- [ ] state reset
- [ ] inheritance
- [ ] archetypes
- [ ] Stage 4 state

Manual Run A:

검기
→ 상승베기
→ 방어

Verify:

- [ ] Stage 2 검기형
- [ ] Stage 3 still 검기형
- [ ] Stage 4 all 3 inherited
- [ ] player has all 3 personalized

Manual Run B:

방어
→ 검기
→ 상승베기

Verify:

- [ ] Stage 2 방어형
- [ ] later skills do not replace type

Manual Run C:

상승베기 first

Verify:

- [ ] Stage 2 상승베기형

Restart:

- [ ] clean state

Build:

- [ ] Windows build succeeds
- [ ] launch succeeds
- [ ] no critical Player.log exceptions

---

# Phase 14 — Documentation

Update:

docs/PROTOTYPE_STATUS.md

Must distinguish:

FINAL GAME:
2D side-scrolling boss-rush

CURRENT PROTOTYPE:
2.5D technical/gameplay prototype

Document:

- [ ] controls
- [ ] three originals
- [ ] three personalized skills
- [ ] stage flow
- [ ] inheritance
- [ ] guardian archetypes
- [ ] known limitations
- [ ] prototype-only 3D assets
- [ ] what final 2D production still requires

---

# Final Done Criteria

- [ ] full run works
- [ ] all 6 orders structurally supported
- [ ] all 3 main guardian archetypes work
- [ ] all 3 personalized techniques work
- [ ] Stage 4 combines all originals
- [ ] restart clean
- [ ] no obsolete player-facing terminology
- [ ] compile clean
- [ ] tests pass
- [ ] Windows build works
- [ ] no Git commit/push/merge performed
