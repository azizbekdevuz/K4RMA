# K4RMA Development Progress

## Project state

- Active branch: `feat/real-asset-integration`
- Baseline before Task 1: `ec0436215c540f42c20e3114039b1fe7fd9be49e`
- Unity: `6000.6.2f1` (`770e33f6875c`)
- Current task: Rising Slash rework from the October 29 review
- Task 1 status: Local commits now include the aggressive identity, distinct UP01–UP05, illustrated effects, and an editor practice menu. Human feel playtest not run.
- Task 2 status: Not started. The upper-left 체공 status line was removed from the combat HUD. The 체공 ability itself is still in the player code because the locked story text still defines it and the meeting note did not replace that requirement.
- Play Mode this session: Not run. The open editor compiled the scripts with no `error CS`. That does not validate feel, hit timing, or visuals.

## October 29 rising-slash rework

- UP01 is a locked ground area. UP04 is an eight-way shield burst whose parry stuns the guardian. They no longer share `RiseSlam`.
- UP03 is a large facing-direction sword wave. The small rising blade is gone.
- UP05 is eight warned crossings at mixed heights, then exhaustion.
- Rising Slash primary guardians close faster and can use a short melee only inside range.
- New effects live in `Assets/K4RMA/Resources/K4RMAArtwork/RisingSlash`. Fragments are cut from `Shield.png`. No character art was regenerated.
- Practice menu: `K4RMA/상승베기 연습`.
- Core checks: 402 passed with Unity's bundled dotnet. Isolated Play Mode regression was not rerun.
- Local commits: `d003b64` effects, `f79d208` rushdown melee, `9133fc7` importer settings, `277ab16` distinct attacks, `03ae373` practice menu, `9e7ef07` HUD text. Nothing pushed.

## Verified source decisions

Details: `docs/agent-k4rma/SOURCE_OF_TRUTH.md`.

- In-repo authority remains `요구사항_명세서_2조.pdf`. It was not edited.
- Personalized 상승베기 is 체공: one airborne hang per jump, ended by releasing J or by the 0.72s limit.
- Original 상승베기 stays on Left Shift until that technique is personalized.
- UP05 step timing is still the existing tunable 0.65s warning/reset. No later locked duration was found.
- UP04 no longer adds the enraged delayed eruption on top of fragments.

## Task 1 — Boss 상승베기 and player 체공

| Component | Status | Evidence / blocker |
|---|---|---|
| Source and implementation audit | Done | Prior setup commit |
| Canonical asset inventory | Partial | Existing PlayerVFX Rising and AirSlash poses reused. No new character art |
| Original 상승베기 | Implemented | Left Shift launches an upward strike with a 0.22s single-hit window |
| 체공 | Implemented | Airborne J after Dash personalization. Apex pose. Horizontal drift. One use until landing |
| UP01 | Refined | Shared shockwave contact. Landing stays locked. Enraged remnant remains on the unshielded slam |
| UP02 | Refined | Lock after the track. Enraged re-pursuit waits at least 0.45s |
| UP03 | Present | Flame gate unchanged. Forward blade and rear escape kept |
| UP04 | Refined | Shield ends on landing. Fragments replace the shockwave |
| UP05 | Refined | Same phase order. Low slash height. Exhaustion recovery message |
| Combat feedback and polish | Demo pass | UP01/UP02 charge and lock cue, UP01/UP04 landing flash, UP01/UP04 ground wave on the existing hitbox, 체공 aura. No new character art |
| Unity compilation | Passed | Editor 6000.6.2f1 batchmode on this repository. No `error CS`. Earlier Roslyn-only pass did not see the missing knockback fields |
| Core checks | Passed | 402 assertions |
| Source reference check | Passed | 63 scripts, 240 GUIDs |
| Play Mode regression | Passed | Isolated copy after the repair: 21 pattern runs, 599 assertions |
| Human gameplay feel | Requires human playtest | Batchmode does not show the Game view |

## Demo polish — visible 상승베기 and 체공

- UP01 preparation is a gold charge. UP02 tracking stays cyan, then snaps to an orange locked path and lock flash.
- UP01 and UP04 land with a short flash, dust, and a GroundRing or Blade sprite on the existing shockwave hitbox. Camera kick does not change time scale or damage.
- 체공 keeps a PlayerVFX aura while hanging, with a start burst and a small release when the hang ends.
- Open Unity `6000.6.2f1` recompiled `Assembly-CSharp` with no `error CS`. Batchmode could not open the project because that editor already had it locked. Play Mode was not visually inspected.

## Task 2 — UI/UX

Not started. Combat HUD only gained a 체공 state label.

## Local commits

- `93ee71c` `docs: record verified workspace baseline`
- `15816c7` `feat(player): replace personalized air burst with aerial hang-time`
- `c742a6f` `fix(boss): keep rising-slash hazards single-hit and low`
- `562a973` `docs: record rising slash implementation progress`
- `dc268a6` `fix(player): resolve undefined knockback state references` — editor batch compile passed. Isolated Play Mode 21 runs, 599 assertions. The earlier Roslyn pass ran before `48f563d` removed the unused knockback fields, and the rising-slash patch kept those references.
- `554b0bc` `fix(build): require a clean Unity editor compile`
- Handoff: attack suppression is cleared when combat status resets, so a new run does not keep the previous 체공 hold lock.

## Current blockers

- Hang duration 0.72s and UP05 gaps are implementation values. v0.2 still marks those numbers `[확인 필요]`.
- Whether 체공 makes the rising-slash Guardian too easy or too hard needs a human playtest.
- `feat/boss-sword` is still `ec04362`. These commits are only on `feat/real-asset-integration`.

## Next action

Task 2 starts in a fresh session after this combat is accepted. Do not continue UI work here.

## Validation rules

- Full-project Unity 6000.6.2f1 editor compile: Passed.
- A Roslyn file-list compile is not that result. `tools/check-unity-compile.py` now fails unless the editor batch compile is clean.
- Core checks: Passed, 402 assertions.
- Isolated Play Mode regression: Passed, 21 runs, 599 assertions.
- Hand-played combat feel: Requires human playtest.

## Handoff instructions

At the start of every new Agent session:

1. Read `.cursor/rules/k4rma-core.mdc` and `.cursor/rules/k4rma-git.mdc`.
2. Read this file and `SOURCE_OF_TRUTH.md`.
3. Inspect current Git status and recent commits.
4. Read the task file for the selected task.
5. Resume from verified progress, not assumptions.
