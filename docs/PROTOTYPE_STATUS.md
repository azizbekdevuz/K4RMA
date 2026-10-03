# Prototype status

Branch: `feat/real-asset-integration`. Unity project: `Game/`. Editor: `6000.6.2f1`.

The saved scene `Game/Assets/_Project/Scenes/PrototypeArena.unity` is the playable scene. Play does not rebuild it. `proto.dash`, `proto.projectile`, and `proto.guard` are still placeholder abilities. Deflect is the current prototype counter, not a final internalized technique.

## This pass

- The arena is dressed with RG Poly temple pieces and a few Japanese props. Primitive floor, walls, and pillars stay as hidden collision.
- Player and both guardians have `VisualAnchor` / `CurrentPlayerVisual` or `CurrentGuardianVisual`. Gameplay components stay on the roots. Tripo folders are still empty, so the temporary figures remain.
- `ArenaBounds` is shared by the player, the boss, and the camera. The playable range is -8.4 to 8.4, and the side walls sit on that range. The camera follows that whole range instead of stopping early.
- The altar guide is a small chevron above the player. It points from the player toward the altar and hides inside the altar, during the transfer, and outside the altar walk. There is no "Go to the altar" sentence.
- Gameplay banner sentences were removed. A successful Deflect is spark, sound, hit-stop, and the guardian stun. The E prompt is the imported key glyph.
- Stage 1 core stays pale. Stage 2 keeps the brighter inherited core.
- URP fog, warmer key light, cool fill, and a global volume with restrained bloom, vignette, and contrast were added.
- Quaternius `UAL2_Standard.fbx` is Humanoid, but the current figures are not humanoid rigs. Those clips were not applied. Tripo animation is still pending.
- `PresentationBootstrap` no longer rebuilds the old primitive scene on editor load. `K4RMA/Dress Temple Arena` can rebuild the dressing.

## What a person still needs to check

- Far left and far right: the player stays inside the frame and cannot walk out of the hall.
- The chevron stays above the player, turns toward the altar, and vanishes when E appears.
- Stage 1 reads as stone/neutral. Stage 2 reads as the same guardian with the transferred shot.
- The temple meshes are not pink, not huge, and not blocking the fight.
- The transfer reads without the old sentences.
- Deflect is readable from the flash, sound, and stun alone.

## Known slice limitation

Stage 2's projectile is preconfigured on `BossStage2`. `RunState` records the surrender. This slice does not compose an arbitrary sacrificed ability onto the next guardian at runtime.

## How to open

1. Install Unity `6000.6.2f1` with Windows Build Support (Mono).
2. Open the `Game` folder.
3. Open `Assets/_Project/Scenes/PrototypeArena.unity` and press Play.

Controls: A/D or arrows, Space, J, K, E at the altar, R after defeat or clear.

## Verification log

| Check | Result |
| :--- | :--- |
| Temple dressing compile and scene save | Passed. No `error CS` |
| EditMode sacrifice tests | Passed, 5/5 |
| Windows build | Passed. `Game/Builds/Windows/K4RMA-Prototype.exe` |
| Player launch smoke test | Process stayed up. No exception in `Player.log`. Not a playthrough |
| Player.log smoke test | Not a human playthrough |
| Human look at the temple, arrow, and camera edges | Still needed |
