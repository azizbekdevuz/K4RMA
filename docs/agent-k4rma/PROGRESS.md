# K4RMA Development Progress

## Project state

- Active branch: `feat/real-asset-integration`
- Baseline commit: `ec0436215c540f42c20e3114039b1fe7fd9be49e` — `fix: resolve boss position and UI font issues`
- Same commit as local and `origin/feat/boss-sword` (`0` ahead / `0` behind)
- Unity: `6000.6.2f1` (`770e33f6875c`), installed in Unity Hub
- Current task: Task 1, not started
- Task 1 status: Audit recorded. No gameplay edit
- Task 2 status: Not started. Blocked on Task 1 by `TASK_2_UI_UX.md`
- Working tree before this setup commit: tracked files clean. Untracked `.cursor/rules/` and `docs/agent-k4rma/`
- Play Mode this session: Not run

## Verified source decisions

Details and paths: `docs/agent-k4rma/SOURCE_OF_TRUTH.md`.

- In-repo authority is `요구사항_명세서_2조.pdf`, 작성 기준 2026-10-10. Do not edit it.
- `feat/real-asset-integration` and `feat/boss-sword` are the same commit. Archive snapshots were not byte-compared to HEAD.
- UP01–UP05 already have coroutines in `Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.RisingSlash.cs`.
- UP05 sequence is specified. Pursuit trajectory, step timing, exhaustion length, and trigger are still v0.2 `[확인 필요]`. No later locked duration was found in the 2026-10-25 18:48 Kakao export.
- Personalized 상승베기 is still `BeginRisingSlash`, not 체공.
- `docs/CONCEPT.md` is superseded.
- Notion week 9–15 minutes in the archive are empty templates. Week 5–7 notes exist. Meeting transcripts were keyword-searched, not fully read.

## Task 1 — Boss 상승베기

| Component | Status | Evidence / blocker |
|---|---|---|
| Source and implementation audit | Done | `SOURCE_OF_TRUTH.md`. Code read, not playtested |
| Canonical asset inventory | Partial | Art directories located. Frames not re-audited |
| UP01 | Present, not revalidated | `RiseSlam(false)`. Last play record is 2026-10-23, before the boss-Y commit |
| UP02 | Present, not revalidated | `AirPursuit`. Lock-after-track is in code |
| UP03 | Present, not revalidated | Flame remnant gate in `KarmaPatternCatalog` |
| UP04 | Present, not revalidated | `RiseSlam(true)` |
| UP05 | Present, timing not locked | `Crossings`. Do not retune duration without a new decision |
| Combat feedback and polish | Not started | |
| Unity compilation/integration | Not run | Editor `6000.6.2f1` is installed |
| Actual gameplay validation | Not run | Requires Play Mode |

## Task 2 — UI/UX

| Component | Status | Evidence / blocker |
|---|---|---|
| Visual identity audit | Not started | IMGUI HUD confirmed. Art sheets located |
| Reusable UI components | Not started | Blocked until Task 1 verification |
| Title and buttons | Not started | |
| Story and controls | Not started | |
| Combat HUD | Not started | `KarmaHUD.cs` changed in `ec04362` (font) |
| Personalization selection | Not started | |
| Stage transitions | Not started | |
| Pause | Not started | |
| Victory and defeat | Not started | |
| Visual/runtime validation | Not run | |

## Local commits

`this commit | docs: record verified workspace baseline | Paths, branch identity, and PDF text checked. Play Mode not run.`

## Current blockers

- No locked UP05 pursuit duration beyond BPR-010 and v0.2 `[확인 필요]`. Cause: the workspace brief names a mismatch, and the searched archive does not contain that later decision. Need the original statement before changing timing.
- 체공 is specified and not implemented. Cause: player code still uses the rising burst. Need a Task 1 decision on when that dependency is in scope.
- 2026-10-23 Play Mode results do not cover `ec04362`.

## Next action

Re-check UP01 `RiseSlam` against BPR-006 on the current boss ground position, then fix the first verified lifecycle defect. Do not edit the PDF, canonical art, or UP05 timing in that pass.

## Validation rules

- Passed.
- Failed.
- Not run.
- Blocked.
- Requires human playtest.

This setup: compilation, Core checks, artwork checks, and Play Mode are Not run. A compiled script is not gameplay evidence.

## Handoff instructions

At the start of every new Agent session:

1. Read `.cursor/rules/k4rma-core.mdc` and `.cursor/rules/k4rma-git.mdc`.
2. Read this file and `SOURCE_OF_TRUTH.md`.
3. Inspect current Git status and recent commits.
4. Read `TASK_1_BOSS_ASCENDING_SLASH.md` before editing combat.
5. Resume from verified progress, not assumptions.
