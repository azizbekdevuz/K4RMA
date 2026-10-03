# Prototype status

Branch: `feat/2-5d-prototype`. Unity project: `Game/`. Editor: `6000.6.2f1`.

This slice is a locked side-view 2.5D graybox. `proto.dash`, `proto.projectile`, and `proto.guard` are placeholder abilities. Only `proto.projectile` is a complete transfer. The saved scene `Game/Assets/_Project/Scenes/PrototypeArena.unity` is the playable scene. Play does not rebuild it.

## What works

Verified in Unity batchmode on this machine:

- The project compiles with editor `6000.6.2f1`.
- `PrototypeArena` contains the arena, player, stage 1 guardian, inactive stage 2 guardian, altar, side-view camera, and HUD.
- Stage 1 boss config has no inherited ability. Stage 2 config inherits `proto.projectile`.
- EditMode tests for the sacrifice rules: 5 passed, 0 failed.
- Windows Mono player build succeeded.
- That player stayed running for about 18 seconds, created a Direct3D 11 device, and initialized input and physics. `Player.log` had no exception. Unused URP post-process shaders were stripped; the graybox does not use them.

The scene implements this loop: move, jump, melee, stage 1 telegraphed melee, altar surrender of prototype Projectile, stage 2 guardian shot, and K switching from shot to deflect. A person has not yet played that loop in the editor, so the feel checklist below is still open.

## What remains

- The manual playtest checklist.
- Final 2D versus 2.5D decision (D-004).
- Approved ability list and stage count (D-007).
- Dash and Guard combat, and any transfer besides `proto.projectile`.
- Stages 3 and 4, story scenes, scoring, and final art.

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

Menu **K4RMA → Create Prototype Arena If Missing** creates the scene only when that file is absent. It does not run on Play and does not overwrite an existing scene.

## How to open

1. Install Unity `6000.6.2f1` with Windows Build Support (Mono).
2. Open the `Game` folder as the project.
3. If the editor opens a template sample scene, switch to `Assets/_Project/Scenes/PrototypeArena.unity`.
4. Press Play.

## Batch commands

From PowerShell. The first two were run successfully here. On this editor, `-runTests` together with `-quit` exited before the test runner started, so tests were run with the execute method instead.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe"
$project = "D:\Workplace\Gaming\K4RMA\Game"
& $unity -batchmode -nographics -quit -projectPath $project -executeMethod K4RMA.EditorTools.PrototypeSceneBuilder.CreateArenaBatch -logFile "$env:TEMP\k4rma-scene.log"
& $unity -batchmode -nographics -projectPath $project -executeMethod K4RMA.EditorTools.PrototypeTestRunner.RunEditMode -logFile "$env:TEMP\k4rma-tests.log"
& $unity -batchmode -nographics -quit -projectPath $project -executeMethod K4RMA.EditorTools.PrototypeBuild.BuildWindows -logFile "$env:TEMP\k4rma-build.log"
```

Windows player output (not committed): `Game/Builds/Windows/K4RMA-Prototype.exe`.

## Manual playtest checklist

Play `PrototypeArena` and note each item before treating the slice as demo-ready:

- Movement responsiveness: left/right and jump start and stop without a noticeable delay.
- Melee readability: the swing, the hit, and the target reaction are obvious.
- Boss telegraph readability: the windup is visible early enough to react.
- Sacrifice cause and effect: a new player can tell what was given up and what the next guardian gained without a verbal explanation.
- Deflect versus projectile: K after the sacrifice feels like a different action, not a weaker version of the shot.
- Stage 2 pressure: the second fight is harder, and the player still has a meaningful response.
- 2.5D presentation: the side view and depth read clearly enough to keep exploring this direction.

## Verification log

| Check | Result |
| :--- | :--- |
| Project created with the installed URP blank template | Passed, editor `6000.6.2f1` |
| Script compile | Passed in the scene, test, and player batch runs. No `error CS` from project scripts |
| Saved scene | `PrototypeArena.unity` created by the editor builder |
| EditMode tests | Passed, 5/5, via `PrototypeTestRunner.RunEditMode` |
| Windows standalone build | Passed. Output `Game/Builds/Windows/K4RMA-Prototype.exe` |
| Player launch smoke test | Process stayed up for 18 seconds. No exception in `Player.log`. Not a gameplay playthrough |
| Presentation pass | Saved into `PrototypeArena` (disciple, guardian, altar sequence, HUD, title/ending) |
| EditMode tests after presentation | Passed, 5/5 |
| Windows build after presentation | Passed. `Game/Builds/Windows/K4RMA-Prototype.exe` |
| Manual playtest of the presentation | Not run by a person in this session |
