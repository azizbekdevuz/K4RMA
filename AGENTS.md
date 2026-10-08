# K4RMA agent guide

K4RMA is a working title for a four-person university action game. The signature loop is a side-view boss fight, then surrendering one ability that the next guardian can use.

This file is persistent guidance for the `feat/2-5d-prototype` branch. Team decisions live in `docs/DECISIONS.md`. Do not invent decisions that are not recorded there.

## Scope that stays fixed

- Unity C#, PC first, single-player only.
- Side-view gameplay. This branch uses a locked 2.5D camera and primitive meshes to test that presentation.
- One arena and boss encounters. No normal mobs, procedural generation, or large maps.
- No multiplayer, networking, DOTS, save system, score, inventory, or web backend.
- No fighting-game frame data and no extra abstraction frameworks.

Final ability names, final stage count, final art, and the lasting 2D versus 2.5D choice are still open. See D-004 and D-007.

## Prototype versus final content

`proto.dash`, `proto.projectile`, and `proto.guard` are placeholder prototype data. They are not approved final skills. The only wired transfer in this slice is `proto.projectile`: the player loses it, the stage 2 guardian fires it, and the player gains `proto.projectile.deflect`. That counter is a prototype rule, not final design.

## Project

- Unity project: `Game/`
- Editor: **6000.6.2f1** (the editor installed on the prototype machine; pin `ProjectSettings/ProjectVersion.txt` to it)
- Playable scene: `Game/Assets/_Project/Scenes/PrototypeArena.unity`
- The saved scene is the playable scene. The editor setup menu creates it when it is missing. Do not rebuild the arena from scratch on every Play.

## Architecture

Keep responsibilities in small components:

- `PlayerInputReader` is the only keyboard reader.
- `PlayerController` / `PlayerCombat` move and attack.
- `Health` and hit feedback are shared.
- `AbilityDefinition`, `PlayerTuning`, and `BossEncounterConfig` hold replaceable data and tuning numbers.
- `RunState` and `SacrificeRules` are plain C# and own sacrifice results.
- `BossController` is a small explicit state machine.
- `Altar` and `RunDirector` apply the rules to the scene.
- `HudPresenter` displays state and does not decide it.

Avoid a god object, singletons, service locators, and dependency-injection frameworks. Tuning numbers live on those config assets, not as scattered literals inside combat code.

## Run and test

Open `Game/` in Unity `6000.6.2f1` and press Play on `PrototypeArena`.

Batch compile, EditMode tests, and the Windows player build are documented in `docs/PROTOTYPE_STATUS.md`. On this editor, run tests with `K4RMA.EditorTools.PrototypeTestRunner.RunEditMode`. Do not commit `Library/`, `Temp/`, `Logs/`, or `Builds/`.

## Documentation rule

Read `docs/PROJECT_CONTEXT.md` and `docs/DECISIONS.md` before changing design. When a prototype choice is easy to mistake for a final decision, label it as prototype data and record it as a new decision row instead of rewriting an older row.
