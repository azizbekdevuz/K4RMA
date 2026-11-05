# 검기형 보스 플레이 테스트 반영 — 2차 수정

브랜치 `feat/boss-sword2`, 저장소 `C:/Users/곽현성/K4RMA`에서 작업했다. 기존 1차 수정과 미추적 파일을 유지하고, 이번 요청 시작 시 444개 파일의 SHA-256을 기록해 변경 범위를 비교했다.

## 1. 구현 내용

후퇴 빈도와 평타 선택을 조정하고 SW03을 쌍검 상승 패턴으로 강화했다. SW03 전조·상승 검기·피격 PNG를 내장 imagegen으로 각각 새로 제작했다. 기존 v2 PNG와 모든 기존 `.meta`는 보존했다.

## 2. 이번 요청에서 수정한 파일

```text
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossMovement.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossBehavior.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/SWBasic.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/SW03.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/KarmaSwordScarPlanner.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordScarRise.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaWaveArtwork.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/KarmaSwordBossSettings.cs
tests/SwordWavePlanner/Program.cs
tests/Unity/KarmaSwordPlayChecks.cs
tools/run-sword-playchecks.ps1
```

신규 파일:

```text
Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword/SW03_GroundScar_v3.png
Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword/SW03_GroundScar_v3.png.meta
Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword/SW03_RisingBlade_v3.png
Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword/SW03_RisingBlade_v3.png.meta
Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword/SW03_HitFlash_v3.png
Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword/SW03_HitFlash_v3.png.meta
docs/SWORDWAVE_PLAYTEST_REVISION.md
```

## 3. 후퇴 AI 수치와 흐름

| 항목 | 1차 구현의 실제 값 | 이번 실제 값 |
| --- | --- | --- |
| 일반 근거리 후퇴 시작 | 6.25유닛 안 | 8.2유닛 안 |
| 피격 압박 대응 | 목표 간격 9.5유닛 안 | 유지 |
| 재사용 대기 | 5초 | 2.6초, 최소 1.8초 보장 |
| 최대 이동 거리 | 6유닛 | 9유닛 |
| 최대 이동 시간 | 0.85초 | 1.05초 |
| 최고 속도 | 12유닛/초 | 16유닛/초 |
| 가속 | 36유닛/초² | 56유닛/초² |
| 감속 | 48유닛/초² | 72유닛/초² |
| 목표 간격 | 9.5유닛 | 유지: 기본 검기 사거리 안 |
| 후퇴 종료 후 결정 대기 | 기존 타이머가 길면 그대로 남음 | 0.18~0.28초로 제한 |
| 후퇴 후 평타 쿨다운 | 기존 남은 쿨다운 적용 | 0.12초 안에 준비, 다음 결정에서 우선 요청 |

저장된 기존 설정을 덮어쓰지 않고 검기형 설정 클래스의 압박 대응값과 유효 상한을 사용한다. 최고 속도는 기존 속도 설정·새 상한·플레이어 속도 1.15배 중 최솟값이다. 현재 플레이어 속도 14에서 실제 상한은 16이다.

기존의 지상 BoxCast, 바닥 Raycast, 벽 경계를 유지한다. 거리가 충분해지거나 시간·이동 예산이 끝나면 제동한다. 한 번 후퇴하면 다음 공격을 완료해야 다시 후퇴할 수 있고 쿨다운도 필요하다. 첫 공격 전에는 도망가지 않아 근접 평타를 볼 수 있다. 공중 연속 공격 중이라는 이유만으로 후퇴가 금지되던 조건을 제거했다.

## 4. SWBasic 발동 조건

- 근접 공격 사각형과 실제 플레이어 bounds의 겹침을 사용한다. 기본 체형 기준 진입 7.0유닛, 유지 한계 7.2유닛이다. 높이 겹침도 필요하다.
- 히스테리시스는 0.35→0.2유닛이다. 경계에서 공격 중간에 종류가 바뀌지 않도록 선택 순간 근접/검기 모드를 고정한다.
- 근접 평타 쿨다운 상한은 2.4→1.1초, 중거리 검기는 2.4→1.5초다. 기존 설정을 더 짧게 조정했다면 그 값을 존중한다.
- 근접 Slash 포즈는 0.2→0.3초, 실제 칼질 판정은 0.18→0.26초로 늘려 식별하기 쉽게 했다. 칼질 기하와 피해 배율은 유지한다.
- 중거리 검기는 기존 최대 중심거리 10유닛과 실제 swept 도달 판정을 유지한다.
- 준비된 평타를 우선 선택하되 두 번 연속 평타 뒤에는 사용 가능한 특수 패턴에도 차례를 준다. 후퇴 직후에는 평타를 한 번 우선 요청한다. 지연·준비된 SW05의 기존 우선순위 검사는 유지한다.

## 5. SW03 크기·수량·이미지·피격

| 항목 | 이전 | 이번 |
| --- | --- | --- |
| 구간 선택 | 10칸 중 무작위 5칸 | 유지 |
| 선택 칸당 검기 | 1개 | 2개 쌍검 |
| 차례당 실제 공격 객체 | 5개 | 10개 |
| 일반/강화 차례 | 2/3 | 유지 |
| 한 사용의 총 검기 | 10/15개 | 20/30개 |
| 칸당 전체 위험 폭 | 4.5유닛 | 9유닛 |
| 검기 높이 | 13.5유닛 | 27유닛, SW02 실제 높이 이상 보장 |
| 개별 검기 판정 | 4.5 × 13.5 | 4.5 × 27, 두 개를 좌우로 붙여 전체 9 × 27 |
| 지면 전조 크기 | 4.5 × 0.65 | 9 × 1.4 |
| 활성 시간 | 0.5초 | 0.65초 |
| 차례 사이 회복 | 0.45초 | 0.32초 |
| 전조 기본 설정 | 0.85초 | 0.95초 |
| 현재 배율 적용 전조 | 일반 0.765 / 강화 최소 0.65초 | 일반 0.855 / 강화 최소 0.8초, 필요 시 안전 계산으로 연장 |

`SW03_GroundScar_v3.png`는 굵은 절삭 홈과 두 줄의 빛나는 균열로 쌍검 발생 구간을 알린다. `SW03_RisingBlade_v3.png`는 좁고 긴 절삭 중심선·짙은 외곽·상승 궤적을 가진 새 이미지다. 활성 첫 프레임부터 전체 위험 높이가 보이며 옅은 내부 잔상만 상승한다.

경기장 끝의 큰 공격은 전체 폭을 안쪽으로 보정한다. 안전 플래너도 같은 보정 중심·전체 폭 9를 사용한다. 겹친 위험 구간을 합쳐 플레이어 몸 전체가 들어갈 안전 구간을 찾고 이동속도 80%, 반응 시간 0.28초, 여유 0.2유닛과 경로 충돌을 검사한다. 기존 재추첨과 252가지 조합의 대체 검사도 유지한다.

쌍검 두 개는 피해 ledger 하나를 공유한다. 두 판정이 맞닿는 경계에서 피해·피격 효과가 중복되지 않는다. 실제 체력 감소를 기록한 ledger만 `SW03_HitFlash_v3.png`의 0.24초 섬광과 0.12초의 작은 기존 카메라 흔들림을 발생시킨다. 빗나감·무적에는 피격 섬광이 생기지 않으며 효과 자체에는 피해 판정이 없다. 발광은 검기 위에 보이도록 정렬 순서를 높였다.

일시정지·공격 중단·보스 사망은 기존 owner 정리 경로를 사용한다. 활성 판정은 자체 수명으로 종료하고 회복 시간에 마지막 피격 섬광이 끝나도록 한 후 다음 전조 전에 정리한다.

## 6. 검증 및 비교

검사는 원본 프로젝트를 열지 않고 `output/validation/sword-project-048f46c556d946df85b8eaffd1375209/` 복사본에서 실행했다.

- 안전 배치: 실제 플래너 소스로 이전 폭 4.5와 새 폭 9를 포함한 **56,872건 통과**, 서로 다른 선택 조합 213종. 벽·체형·이동속도 0·확대 공격의 경기장 보정과 실제 회피 공간을 검사했다.
- Player/Editor 조건의 실제 Unity Roslyn 컴파일 통과. Player 검사에서는 Editor define과 Editor 전용 어셈블리 참조를 제거했다.
- Unity Windows Player 스크립트 빌드 파이프라인에서도 **26개 어셈블리 컴파일 통과**. 실행파일 패키징은 요청 범위에 포함하지 않았다.
- 최종 Play Mode **2,607개 항목 통과, 종료 코드 0**. 결과와 일반 AI 비교 수치는 `output/validation/sword-revision2-validation.txt`, `sword-spacing-before.json`, `sword-spacing-after.json`에 기록했다.
- 자동 AI 비교는 12초 동안 플레이어를 근접 거리 5.5유닛에 유지하며 0.6초마다 작은 피격 압박을 주는 조건과, 7초 동안 중거리 8.7유닛에 둔 조건을 사용한다. 공격을 강제로 선택하지 않고 실제 Update·결정·회복·쿨다운을 실행한다. 조작자가 입력한 수동 플레이와는 구분한다.
- 이전 구현: 근접 12초 동안 평타 3/전체 6회, 후퇴 2회, 후퇴 최소 간격 5.65초. 중거리 7초 동안 기본 검기 2/전체 3회.
- 수정 구현: 근접 12초 동안 평타 **4/전체 5회**, 후퇴 **3회**, 후퇴 최소 간격 **2.61초**. 중거리 7초 동안 기본 검기 **2/전체 3회**. 통제된 조건에서 근접 평타 횟수 33%, 후퇴 횟수 50% 증가를 확인했다.
- SW01 2발 및 SW04 회전·4발 반격, SWBasic 거리별 선택·공격 도중 종류 고정·SW05 선택 우선순위, SW03 일반 20/강화 30검·쌍검 공유 ledger·실제 피격 전용 PNG·무적 시 피격 효과 억제·일시정지·중단·사망 정리를 통과했다. 실제 스테이지 충돌 경로에서 30/60/120 FPS의 속도·가속·감속 상한과 정지도 통과했다.
- 기존 파일 SHA-256 비교에서 변경은 위 11개 파일에 한정됐다. 모든 기존 `.meta`를 보존했고 신규 GUID 3개가 Assets 안에서 각각 한 번만 존재함을 확인했다. `git diff --check`와 PowerShell 검사 도구 구문 검사도 통과했다.

검증 이미지는 `output/validation/revision2/`에, 로그·측정값은 `output/validation/`에 보관한다. `sw03-rise.png`, `sw03-warning.png`, `sw03-confirmed-hit.png`, `live-close-basic.png`, `live-medium-wave.png`를 실제 Unity에서 렌더했다. 재실행은 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run-sword-playchecks.ps1`이며, 이 스크립트는 Editor 컴파일 이후 Windows Player 스크립트 컴파일과 Play Mode를 실행하고 결과를 수집한다.

처음 피격 검사는 효과 객체 이름의 `/`를 Transform.Find가 경로로 해석해 검색에 실패했다. 객체 이름과 검사 검색을 일치시키고 전체 검사를 재실행했다. 기존 공통 전조나 충돌 시스템은 변경하지 않았다.

격리 에디터 시작 시 기존 Unity Search 인덱서의 `ArgumentOutOfRangeException`이 로그에 남는다. 게임 런타임 예외 감시는 테스트 시작 후 별도로 수행했으며 최종 검사는 예외 없이 통과했다. Search 패키지와 원본 프로젝트 설정은 수정하지 않았다.

저사양 GPU에서의 장시간 성능 측정, 실제 입력 지연과 공격 습관에 따른 후퇴 체감, 해상도별 가독성은 수동 플레이·프로파일링으로 최종 튜닝할 항목이다. 자동 비교 수치는 통제된 재현 조건의 결과다.

## 7. 보호 대상

SW05.cs, KarmaAerialSwordPlanner.cs, SW05 전용 PNG·재질·셰이더와 Guard/RisingSlash를 수정하지 않았다. SW01.cs, SW02.cs, SW04.cs와 해당 패턴의 핵심 구조도 이번 요청에서 수정하지 않았다. 기존 SW04.cs는 1차 작업의 미커밋 수정이 있으므로 이번 요청 시작 시의 파일 해시를 기준으로 보존 여부를 확인한다.

공통 Player·Enemy 상태 머신·전투 판정·카메라·Quality/ProjectSettings·KarmaConfig.asset은 이번 요청에서 변경하지 않았다. `ProjectSettings/QualitySettings.asset`의 작업 전 변경과 기존 미추적 Editor/K4RMAComplete 파일을 유지했다. 기존 `.meta`와 GUID도 보존했다.

## 8. Git 작업

commit, push, merge, reset, restore, clean, stash는 실행하지 않았다. 브랜치 전환 없이 `feat/boss-sword2`에서 작업했다.

## 신규 이미지 생성 기록

내장 imagegen, 새 생성 모드, `transparent_background: true`로 제작했다. 출력 PNG를 알파 그대로 Sword 리소스 폴더의 v3 파일에 복사했으며 기존 이미지를 덮어쓰지 않았다. 실제 사용한 프롬프트는 아래와 같다.

### SW03_GroundScar_v3.png

Use case: stylized-concept. Asset type: standalone transparent PNG ground telegraph for a high-quality painterly 2D sword combat game. One very clearly legible horizontal deep gouge carved by a sword dragging across polished dark wood or stone, not a rectangular UI warning. A long razor-cut opening with jagged dark navy edges, bold icy blue seam and white-blue light flowing through two parallel inner slits, a few sharp branching scratches, tiny blue sparks. Whole silhouette is wide and flat, approximately 6:1. Strong contrast on dark game backgrounds, crisp silhouette and concentrated glow, no washed out bloom. No scene, no floor plane, no lettering, no weapon, no border, no checkerboard pattern. Entire single gouge effect framed with transparent padding, genuine alpha transparency. Art should read as a ground cut charging with energy, not an icy rock mound.

### SW03_RisingBlade_v3.png

Use case: stylized-concept. Asset type: one isolated upright rising energy sword slash PNG for painterly 2D action game. One exceptionally long, imposing, razor-sharp vertical energy blade erupting from the earth. Slender 1:5 silhouette, upward point at TOP, solid brilliant narrow silver-white cutting ridge bounded by clearly defined deep sapphire edges, luminous electric cyan channels and elegant curved upward motion streaks. At the foot a compact impact flare with four sharp small shards; keep the foot narrow so the body of the entire long blade stays visually substantial. Highly polished fantasy combat VFX, detailed translucent facets, crisp contour, restrained glow, no white fog. No hilt, no scene, no floor, no character, no writing. Whole complete blade contained inside frame with alpha-transparent padding. Preserve strong dark-blue to white contrast at small in-game size.

### SW03_HitFlash_v3.png

Use case: stylized-concept. Asset type: single transparent 2D game hit effect PNG. A sharp impactful blue-white sword hit flash centered in a roughly square silhouette, two razor-thin diagonal cut strokes crossing, bright compact white core, electric cyan arcs, six small sapphire crystal sparks flying outward. Clean hand-painted fantasy action VFX matching icy blue energy swords, high contrast and clear shape, restrained soft glow with no white blown-out cloud. This is a short instantaneous hit spark over a player, not a projectile, not an explosion sphere, no character, no weapon, no scenery, no words, no checkerboard. Entire isolated effect on genuine transparent background with generous padding.

