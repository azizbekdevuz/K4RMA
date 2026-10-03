# Prototype status

Branch: `feat/prototype-ux-polish`. Unity project: `Game/`. Editor: `6000.6.2f1`.

This slice is a locked side-view 2.5D prototype. `proto.dash`, `proto.projectile`, and `proto.guard` are placeholder abilities. Only `proto.projectile` is a complete transfer. The saved scene `Game/Assets/_Project/Scenes/PrototypeArena.unity` is the playable scene. Play does not rebuild it.

## UX polish on this branch

- After Stage 1, an on-screen marker says "Go to the altar". It sits above the altar when the altar is in view, and moves to the left or right edge with `<` or `>` when the altar is outside the camera. It bobs. It hides at the altar, when the surrender sequence starts, and in every phase except the altar walk.
- At the altar, the marker is gone and the prompt is `E  Surrender the shot`.
- The surrender lines are "The shot leaves you." then "It enters the next guardian."
- Stage 1's core and melee windup are pale and neutral. Stage 2 keeps the bright inherited core.
- A successful Deflect shows the word "Deflected." for a short time.

## What still needs a person to check

- The arrow is obvious within a second or two after Stage 1.
- The arrow points the right way from both sides of the arena.
- The marker disappears as soon as the player can press E.
- Stage 1 no longer reads as fire, and Stage 2 still reads as the changed guardian.
- The surrender orb still reads as player, then altar, then the next guardian.
- Deflect success is readable, and the melee windup is still readable in pale light.

## What works

Verified in Unity batchmode on this machine. A person has playtested the earlier slice; this polish pass has not been playtested by a person in this session.

- The project compiles with editor `6000.6.2f1`.
- `PrototypeArena` contains the arena, player, stage 1 guardian, inactive stage 2 guardian, altar, side-view camera, HUD, and altar guide.
- Stage 1 boss config has no inherited ability. Stage 2 config inherits `proto.projectile`.
- EditMode tests for the sacrifice rules: 5 passed, 0 failed, including after this polish.

## What remains

- Final 2D versus 2.5D decision (D-004).
- Approved ability list and stage count (D-007).
- Dash and Guard combat, and any transfer besides `proto.projectile`.
- Stages 3 and 4, story scenes, scoring, and final art.
- The exact gameplay meaning of an internalized technique is not decided. Deflect here is only the current prototype counter.

## Known slice limitation

Stage 2's projectile is preconfigured on `BossStage2`. `RunState` still records which ability was surrendered, and the altar sequence plays that transfer. This slice does not build a general system that composes an arbitrary sacrificed ability onto the next guardian at runtime.

## Temporary prototype assumptions

- Presentation under test: primitive meshes and a locked perspective camera with a slight yaw and downward pitch.
- Playable loadout: melee plus `proto.projectile`. Dash and Guard are owned catalog entries with no combat behavior. The altar can surrender only `proto.projectile`.
- After that surrender, K no longer fires a shot. K opens a short deflect window. A deflected guardian shot is destroyed and the guardian takes a brief punish.
- Stage 2 is the same arena and a second guardian object already placed in the scene. The player is moved back to the spawn point and healed to full for that fight. That heal is an encounter reset, not a stat upgrade.
- Restart reloads `PrototypeArena`.
- Controls: A/D or arrows, Space jump, J melee, K ability, E altar, R restart after defeat or clear.

## Tuning

Edit these assets. The numbers the fight uses are on the assets, and the scene references them.

- `Game/Assets/_Project/Data/Prototype/PlayerTuning.asset` — move speed, jump speed, melee timing and range, player HP, projectile speed and damage, deflect duration.
- `Game/Assets/_Project/Data/Prototype/BossStage1.asset` and `BossStage2.asset` — HP, approach, telegraph durations, melee damage, and the stage 2 projectile.

Menu **K4RMA → Create Prototype Arena If Missing** creates the scene only when that file is absent. It does not run on Play and does not overwrite an existing scene. **K4RMA → Apply Altar Guide** adds the guide if it is missing.

## How to open

1. Install Unity `6000.6.2f1` with Windows Build Support (Mono).
2. Open the `Game` folder as the project.
3. If the editor opens a template sample scene, switch to `Assets/_Project/Scenes/PrototypeArena.unity`.
4. Press Play.

## Batch commands

From PowerShell. On this editor, `-runTests` together with `-quit` exited before the test runner started, so tests are run with the execute method instead.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe"
$project = "D:\Workplace\Gaming\K4RMA\Game"
& $unity -batchmode -nographics -quit -projectPath $project -executeMethod K4RMA.EditorTools.UxPolishPass.ApplyBatch -logFile "$env:TEMP\k4rma-ux.log"
& $unity -batchmode -nographics -projectPath $project -executeMethod K4RMA.EditorTools.PrototypeTestRunner.RunEditMode -logFile "$env:TEMP\k4rma-tests.log"
& $unity -batchmode -nographics -quit -projectPath $project -executeMethod K4RMA.EditorTools.PrototypeBuild.BuildWindows -logFile "$env:TEMP\k4rma-build.log"
```

Windows player output (not committed): `Game/Builds/Windows/K4RMA-Prototype.exe`.

## Verification log

| Check | Result |
| :--- | :--- |
| UX polish compile and scene save | Passed. Altar guide saved. No `error CS` |
| EditMode tests after UX polish | Passed, 5/5 |
| Windows build after UX polish | Passed. `Game/Builds/Windows/K4RMA-Prototype.exe` |
| Player launch after UX polish | Process stayed up. No exception in `Player.log`. Not a playthrough |
| Human check of the altar arrow | Still needed |
