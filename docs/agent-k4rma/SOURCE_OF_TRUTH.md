# K4RMA — Source of Truth and Project Evidence

## Purpose

Use this document as the navigation and authority protocol for all K4RMA development work. Dates and paths below were checked on 2026-10-25. Do not assume a source is the latest merely because it appears first in the archive.

## Authority protocol

- Follow the latest explicitly approved and locked team decision.
- The in-repo requirements PDF is mandatory unless a later explicit approval changes it.
- Do not edit `요구사항_명세서_2조.pdf` or other locked design documents during implementation.
- Older presentations, chats, and `docs/CONCEPT.md` are historical evidence.
- Inspect code before treating a document as the current implementation.
- Record conflicts with exact paths. Do not guess a resolution.
- v0.2 items marked `[보충 제안]` or `[확인 필요]` are not locked requirements. The PDF appendix states this directly.

## Verified source index

| Category | Actual location | Checked |
|---|---|---|
| Official requirements | `요구사항_명세서_2조.pdf` (repo root). 23 pages. Title metadata: `K4RMA 요구사항 명세서 - 테두리 수정본`. Header: 작성 기준 2026-10-10. Producer `pypdf`. SHA-256 `7961C48EAFDC6FAFF97687FC7DD9DA092FC94E7E727ADCD9A938BE94A7E49603` | Text extracted |
| Earlier Word export | `D:\Workplace\Gaming\K4RMA_요구사항_명세서_2조(1).pdf`. 22 pages. Word metadata created 2026-10-11 00:59 +09. SHA-256 `964A70679C02253A95C589ACF03374C293AEE85B89B4B555C31C683AE1C0E6B4` | Text extracted. Not the in-repo file |
| Archive | `D:\Workplace\Gaming\K4RMA Dev.zip` (1001 entries, readable) | Indexed |
| Notion export | Inside the zip: `Resources/notion exported contents/` | HTML extracted and searched |
| Presentations | Zip: `Resources/5주차_발표자료_2조.pdf`, `Resources/6주차_발표자료_2조 (2).pptx`, `Resources/7주차_발표자료_2조_v9.pptx`, `Resources/2026_SW설계기초_6주차_발표피드백_2조 (other team students feedback).pdf` | Present. PPTX text not extracted |
| Presentation transcripts | Zip: `Presentations (Voice to TXT)/Plan Presentation - Whole Class Presentation.txt`, `Presentation 1 - Only for Professor.txt` | Keyword search only |
| Team discussion transcripts | Zip: `Team Discussions (Voice to TXT)/` — `Team discussion 1.txt`, `Meeting 2.txt`, `Meeting 3 - Part 1.txt` through `Part 9.txt` | Keyword search only. Not read line by line |
| KakaoTalk | Zip: `KakaoTalk Chat Group History/KakaoTalk_20261025_1848_14_800_group.txt` (export 2026-10-25 18:48, lines through that evening) | Keyword search of the whole file. Full read of 2026-10-08–11 and 2026-10-22–25 |
| Branch snapshots in the archive | Zip: `current github source/K4RMA-feat-boss-sword/`, `current github source/K4RMA-feat-2d-prototype/` | Folder names confirmed. Not byte-compared to HEAD |
| Live Unity project | This repository. Source under `Assets/K4RMA/{Core,Runtime,Editor}`. Tests under `tests/` | Inspected |
| Canonical / production art in repo | `image/K4RMA_Production_Art_v1/`, `image/K4RMA_UI_Screens_v3/`, `image/HSIMAGE/` (4 PNGs), runtime copies in `Assets/K4RMA/Resources/K4RMAArtwork/` | Directories listed. Frames not re-sliced |
| Outside-repo scene copies | `D:\Workplace\Gaming\K4RMA-scene-recovery\KarmaPrototype-current.unity`, `KarmaPrototype-stashed.unity` | Names only. Not treated as baseline |
| Local implementation notes | `docs/ARCHITECTURE.md`, `docs/BOSS_PATTERNS.md`, `docs/ARTWORK.md`, `docs/VALIDATION.md`, `docs/PLAYTEST.md`, `README.md` | Read. `docs/CONCEPT.md` is older and superseded |
| Agent task scope | `docs/agent-k4rma/TASK_1_BOSS_ASCENDING_SLASH.md`, `docs/agent-k4rma/TASK_2_UI_UX.md` | Read |

Notion minutes `[9주차]` and `[10주차]` through `[15주차]` are empty templates (page body about 224 characters). `[5주차]`, `[6주차]`, and `[7주차]` contain notes. `[7주차]` is dated 2026-10-08 and lists 김정민, 곽현성, 김민재, Azizbek. Its full text was not copied into this index.

The two PDFs are not byte-identical. Normalized text first diverges at the boss-pattern section heading: the in-repo file omits the sibling heading's `(Boss Pattern v0.2)`. Extracted BPR-006 through BPR-010 wording matches. Use the in-repo PDF as the specification inside this project.

## Chronological decisions

`Decision | Source path/date | Status | Affected implementation`

- Four Guardian stages. Original techniques 검기, 상승베기, 방어. Personalized 관통베기, 체공, 반격. First inherited original sets the Guardian type. Six orders. BPR-001–BPR-015. No unavoidable untelegraphed loops. No mid-stage checkpoint retry. Combat feedback and HUD required. Windows PC primary. | `요구사항_명세서_2조.pdf`, 작성 기준 2026-10-10. Kakao 2026-10-10 23:17 김정민 approved the pattern-detail draft; file `K4RMA_요구사항_명세서_2조.pdf` sent 2026-10-11 01:01; 김정민 acknowledged 2026-10-11 08:29 | Locked | `KarmaPatternCatalog`, enemy pattern partials, `KarmaStory.cs`, HUD |
- Boss-pattern mechanisms belong in the requirements, without balance numbers. Pattern names were aligned and then kept. | Kakao 2026-10-10 22:40–23:01, 김정민 | Approved | PDF BPR section and catalog labels |
- v0.2 `[원안]` and shared judgement rules are in the spec. `[보충 제안]` and `[확인 필요]` stay unfixed until a later team decision. | PDF 부록, 2026-10-10; Notion `Resources/notion exported contents/보스 패턴 v0 2 ...html`, dated 2026-10-08 | Locked boundary | `docs/BOSS_PATTERNS.md` states its numbers are initial implementation values, not final balance |
- UP05 order is center preparation, ground traversal, aerial pursuit, second traversal, final slam, exhaustion. Pursuit trajectory, step gaps, exhaustion time, and trigger stay `[확인 필요]`. Do not add shockwaves or shields that the original does not state. | Notion v0.2 UP05; PDF BPR-010 | Sequence locked. Timing not locked | `KarmaEnemy.RisingSlash.Crossings` |
- Develop from the Notion boss patterns after the 2026-10-08 discussion. | Kakao 2026-10-08 22:11–22:14 | Approval to implement | Current boss FSM |
- This implementation task must not modify the requirements PDF. | `docs/agent-k4rma` task brief. No later Kakao order through 2026-10-25 18:48 repeats that sentence. Earlier Kakao messages are the edits that produced the 2026-10-10/11 PDF | Workspace constraint | Leave the PDF unchanged |
- A UP05 pursuit-behavior and duration mismatch was named in the previous workspace brief. | Prior text in this file. Not found in the Kakao export, Notion v0.2, or a keyword pass of the transcripts | Unresolved. Not a locked numeric change | Do not retune UP05 timing from that sentence alone |
- `feat/real-asset-integration` is Azizbek's active branch. `feat/boss-sword` is 곽현성's branch for the font and boss-height fix. 김정민 said he would merge into his line after others confirmed, and that he only inspected `feat/boss-sword`. | Kakao 2026-10-24 22:37–22:46 and 2026-10-25 14:31–14:50 | Discussion. No per-file owner for UP03, UP04, or UP05 | See baseline below |
- Flame / dash / unconscious prototype wording | `docs/CONCEPT.md` | Superseded by the 2026-10-10 PDF and `Assets/K4RMA/Core/KarmaStory.cs` | Do not implement from `CONCEPT.md` |
- Personalized 상승베기 in the running game is still the existing rising burst, not spec 체공. | `README.md`, `docs/PLAYTEST.md`, `KarmaPlayer.Combat.BeginRisingSlash` | Known gap versus SFR-006 | UP avoidance cannot be called spec-complete until 체공 exists or a later decision changes SFR-006 |

## Latest task instruction

김정민's Kakao record through 2026-10-25 18:48 does not contain a new combat-task assignment. The agent brief says the basic boss already exists and should be refined against the requirements without editing the specification. That brief is the current work order for this workspace. It does not replace BPR text.

UP05 pursuit duration remains an open v0.2 `[확인 필요]` item. No later locked duration was found in the archive search covered above.

## Scope

Task 1, in `TASK_1_BOSS_ASCENDING_SLASH.md`, is the rising-slash Guardian and its inherited patterns. The archive does not assign UP03, UP04, or UP05 to a different author. Do not invent a narrower ownership split. Shared enemy, config, scene, and HUD files stay high-conflict; change them only when the pattern work requires it.

Task 2 waits until Task 1 has completed the verification it can actually run.

## Baseline findings (2026-10-25)

- Unity `6000.6.2f1` (`770e33f6875c`) in `ProjectSettings/ProjectVersion.txt`. The same editor is installed at `C:\Program Files\Unity\Hub\Editor\6000.6.2f1`.
- Packages: `com.unity.2d.sprite` 1.0.0, `com.unity.pipeline` 0.9.0-exp.1, and built-in audio, image conversion, IMGUI, JSON, and Physics 2D modules. No uGUI, TextMeshPro, or Input System package in `Packages/manifest.json`.
- Branch `feat/real-asset-integration` and `feat/boss-sword` both point at `ec0436215c540f42c20e3114039b1fe7fd9be49e`. `git rev-list --left-right --count` is `0 0`. The baseline is that commit, not an older ancestor. Author KHS2003, 2026-10-25 14:29 +09, subject `fix: resolve boss position and UI font issues`. It changes four `KarmaPrototype*.unity` scenes and `KarmaHUD.cs` only.
- Before this setup commit, the tracked tree was clean. Untracked paths were `.cursor/rules/` and `docs/agent-k4rma/`.
- Entry: `KarmaBootstrap` creates `KarmaGame` on an empty scene. Build settings enable `KarmaPrototype 1.unity`, `2.unity`, and `3.unity`. `KarmaPrototype.unity` exists and is not in the build list.
- Boss flow: `KarmaEnemy.StateMachine` selects, `KarmaEnemy.Patterns` dispatches, `KarmaEnemy.RisingSlash` runs UP01–UP05. Availability is `KarmaPatternCatalog.BuildCycle` when the first remnant is `Essence.Dash`. UP03 needs Flame. UP04 needs Ward. UP05 needs all three remnants.
- UP01/UP04 share `RiseSlam`. UP02 is `AirPursuit` (tracks, then locks). UP03 is `GroundBlade`. UP05 is `Crossings`, which calls `AirPursuit(false)`.
- Player original rising slash is an upward velocity burst in `BeginRisingSlash`. It is not a hover.
- UI is IMGUI in `KarmaHUD.cs`, `KarmaHUD.Screens.cs`, and `KarmaHUD.Combat.cs`.
- Art wiring is documented in `docs/ARTWORK.md` (`KarmaSpriteLibrary.asset`, `KarmaArtworkSettings.asset`, Sheets and Production). This setup did not re-count sprites.
- Historical validation in `docs/VALIDATION.md` (2026-10-23) predates `ec04362`. It was not re-run in this setup. Play Mode was not run.
- High-conflict files: `KarmaEnemy` partials, `KarmaPatternCatalog.cs`, `KarmaBossRules.cs`, `KarmaBossTuning.cs`, `Assets/K4RMA/Generated/KarmaConfig.asset`, the four prototype scenes, `KarmaHUD` partials, and artwork settings. Player movement/combat is shared if 체공 is implemented.
