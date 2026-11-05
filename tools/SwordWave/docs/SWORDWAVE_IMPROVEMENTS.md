# 검기형 보스 개선 구현 보고

작업 브랜치: `feat/boss-sword2`  
통합 기준: `origin/feat/2d-prototype` (fetch/merge 없이 기존 원격 추적 참조 확인)

## 1. 전체 작업

SW01, SW03, SW04의 이미지·연출, SWBasic의 거리별 공격, 검기형 이동·선택 AI를 실제 구현했다. 신규 PNG 4종은 내장 imagegen으로 생성해 프로젝트에 복사하고, Unity 실제 렌더에서 적용을 확인했다. 기존 PNG와 `.meta`는 교체하지 않았다.

SW05.cs, KarmaAerialSwordPlanner.cs, SW05 전용 이미지·재질·셰이더, Guard 및 RisingSlash 코드는 변경하지 않았다. 공통 상태 머신·충돌·전조 시스템도 변경하지 않았다. commit/push/merge/reset/clean/restore/stash는 실행하지 않았다.

작업 시작 전부터 있던 `ProjectSettings/QualitySettings.asset` 변경과 `Assets/K4RMA/Editor*`, `Assets/K4RMA/Resources/K4RMAComplete*` 미추적 항목을 보존했다. 이 항목들은 아래의 작업 변경 목록에 포함하지 않는다.

## 2. SW01

- `SW01_StaggeredWave_v2.png`: 오른쪽을 향한 날카로운 절삭부, 밝은 중심선, 청백색 외곽과 뒤쪽 궤적을 새로 제작했다. 기존 이미지의 색상 변경이 아니다.
- `KarmaWaveArtwork.Pose()`에서 SW01에만 신규 이미지를 지정한다. SWBasic, SW02, SW05가 사용하는 기존 라이브러리 이미지에는 영향을 주지 않는다.
- 검기 전용 청백색 발광·잔상과 0.14초 발사 섬광을 적용했다. 섬광 자체에는 피해 판정이 없다.
- `SwordWaveVolley.cs`는 변경하지 않았다. 2발, 낮은/높은 순서, 속도, 수명, 판정 크기, 전조 시간을 보존한다.

현재 설정에서 속도 32 유닛/초, 수명 1.65초, 판정 크기 3.24 × 1.3 유닛, 발사 높이 2.1 / 6.6 유닛이다.

## 3. SW03

기존 선행 검기와 2지점 장기 유지 공격을 경기장 전체의 반복 상승 공격으로 변경했다.

1. 실제 `KarmaStage.ArenaLeft = -15`, `ArenaRight = 55`를 읽어 폭 70을 10개 구간으로 나눈다. 각 칸은 7유닛이고, 중심은 `left + (index + 0.5) * cellWidth`다.
2. 부분 Fisher–Yates 셔플로 10개 중 중복 없는 5개를 뽑는다. 각 차례마다 새로 추첨한다.
3. 플레이어 Collider2D의 실제 bounds를 사용한다. 공격 폭 절반에 플레이어 폭 절반과 0.2유닛 여유를 더해 위험 구간을 확장하고, 경기장 안에 몸 전체가 들어가는 안전 중심 구간을 구한다.
4. 안전 구간까지의 수평 거리를 플레이어 이동속도의 80%로 나누고 반응 시간 0.28초를 더해 전조 안에 도달 가능한지 확인한다. 실제 스테이지 표면에 대한 BoxCast로 이동 경로도 확인한다. 현재 경기장은 평지이며 점프로만 회피해야 하는 배치에 의존하지 않는다.
5. 안전하지 않은 추첨은 최대 32회 다시 뽑는다. 이후 252가지 조합을 검사해 안전 배치를 선택하고 필요하면 전조를 늘린다. 안전 배치를 찾지 못하면 피해를 생성하지 않고 회복으로 전환한다.
6. 지면 검흔 PNG 5개가 점차 밝아진 후 같은 X 위치에서 5개 검기가 동시에 상승한다. 활성 첫 프레임부터 전체 판정 높이를 보여주며, 내부의 옅은 잔상만 위로 올라간다.
7. 일반 2차례, 기존 강화 상태에서 3차례 실행한다. 차례 사이에 모든 이전 판정과 VFX를 비활성화하고 제거한 뒤 다음 전조를 시작한다.

플레이어가 기절한 동안 전조 시간을 소모하지 않는다. 게임의 기존 일시정지 시계와 owner 기반 공격·코루틴 정리 경로를 사용한다. 보스 사망·중단 시 전조·판정·잔상이 함께 제거된다.

## 4. SW04

- `SW04_CounterBlade_v2.png`를 회전 검과 실제 반격 투사체에 적용했다.
- 4개 검의 Sin/Cos 공전, 피격 발동, 추적 0.35초 → 고정 0.45초 → 순차 발사, 중복 실행 방지, 독립 효과 root와 수명은 보존했다.
- 검마다 억제된 청백색 발광과 0.075초 간격의 짧은 잔상을 적용했다. 잔상 수명은 기존 공통 restrained VFX의 0.11초다.
- 추적 중에는 청색 강조, 고정 중에는 청백색 맥동, 발사 순간에는 0.14초 섬광을 사용한다. 기존 경로 전조와 피해 판정은 그대로다.
- 일반 패턴 정리가 독립 검막을 지우지 않는 동작도 실제 실행으로 확인했다.

## 5. SWBasic

공통 첫 칼질의 기존 기하(크기 6.6 × 3.6, 전방 중심 오프셋 3.2 유닛)와 Cleave/Slash 애니메이션을 재사용했다. 근접 범위는 이 공격 사각형과 실제 플레이어 bounds의 겹침으로 판단한다.

현재 플레이어 폭 1.4유닛에서 근접 바깥 경계는 중심 기준 수평 7.2유닛이다. 처음 근접으로 진입할 때는 0.35유닛 히스테리시스를 적용해 6.85유닛 이하에서 전환한다. 기존 근접 모드는 실제 판정 범위 7.2유닛까지 유지한다. 높이 겹침도 검사한다.

중거리에서는 기존 SWBasic 검기를 재사용한다. 최대 중심 직선거리 10유닛과 실제 방향성 swept 판정 도달 여부를 함께 확인한다. 속도는 25.6유닛/초, 수명은 0.390625초다.

공격 선택 순간 근접/검기 모드를 고정한다. 전조 도중 플레이어가 거리 경계를 넘어도 공격이 취소되거나 다른 종류로 바뀌지 않는다. 기본 공격 쿨다운 2.4초와 특수 패턴 사이의 평타 요청 정책은 유지한다. 넓어진 근접 우선 범위가 준비된 SW05의 기존 차례를 앞서지 않도록 별도 확인한다.

## 6. 이동 AI 및 실제 튜닝값

| 항목 | 실제 적용값 / 계산 |
| --- | --- |
| 근접 진입 / 이탈 | 기본 플레이어 기준 6.85 / 7.2유닛, 실제 칼질·플레이어 bounds 겹침 필요 |
| 중거리 기본 검기 | 최대 중심 직선거리 10유닛 + swept 판정 검사 |
| 접근 목표 | `min(swordPreferredDistance, BasicWaveRange × 0.9)` = 9유닛 |
| 접근 시작 | 공격이 가능하면 기본 10.5유닛 바깥, 도달 가능한 공격이 없으면 선호 거리까지 접근 |
| 접근 최고 속도 | `min(enemyMoveSpeed × moveMultiplier × movementScale, playerSpeed × 0.8)` = 8.16유닛/초 |
| 후퇴 시작 | 기본 6.25유닛 안 또는 피격 압박, 완료한 공격 토큰과 쿨다운 필요 |
| 후퇴 목표 간격 | 기본 검기 도달 범위 안으로 제한: 현재 9.5유닛 |
| 후퇴 최고 속도 | `min(기존 swordRetreatSpeed, 새 상한 12, playerSpeed × 0.9)` = 12유닛/초 |
| 가속 / 감속 | 36 / 48유닛/초², 접근에도 적용 |
| 후퇴 이동 예산 | 최대 6유닛, 현재 기존 설정 0.6초는 0.85초로 제한해 적용 |
| 후퇴 재사용 대기 | 기존 5초, 다음 완료 공격 없이 재후퇴 불가 |
| 후퇴 이후 | 평타 요청 및 최소 0.18초 정지, 실제 거리에 따라 다음 행동 선택 |
| SW03 칸 / 선택 수 | 10 / 매 차례 중복 없는 5 |
| SW03 실제 판정 | 폭 4.5 × 높이 13.5유닛, 지면부터 수직 |
| SW03 차례 수 | 일반 2, 강화 3 |
| SW03 기본 전조 설정 | 0.85초, 현재 배율 적용 후 일반 0.765초 / 강화 최소 0.65초 |
| SW03 실제 활성 시간 | 0.5초 |
| SW03 차례 사이 회복 | 0.45초, 이전 공격 판정과 겹치지 않음 |
| SW03 시작 간격 | 일반 약 1.715초 / 강화 약 1.6초, 프레임 및 안전 전조 연장에 따라 증가 가능 |
| SW03 안전 계산 | 몸통 전체 + 0.2유닛 여유, 반응 0.28초, 이동속도 80% = 현재 11.2유닛/초 |

기존의 `swordRetreatSpeed = 32`, 정지 간격 11.75, 이동 거리 16 등의 저장값을 강제로 덮어쓰지 않고 보스 전용 코드에서 유효 상한을 적용한다. 벽 BoxCast, 바닥 Raycast, 경기장 경계 제한을 유지했다. 생성 직후 평타보다 먼저 후퇴하지 않으며, 중거리에서 불필요한 접근을 줄였다.

## 7. 검증 결과

- .NET 실제 SW03 플래너 소스 검사: **28,472개 배치 통과**, 서로 다른 조합 202종. 5개 중복 방지, 경기장 내 전체 판정, 몸 전체 안전 폭, 반응 시간 이후 이동 가능성, 속도 0, 벽으로 막힌 경로, 불가능한 체형·너비를 확인했다.
- Unity **6000.6.2f1**의 실제 Roslyn 컴파일러로 런타임 코드와 테스트 코드 컴파일: 오류 없음.
- 격리 Unity 프로젝트 Play Mode: **2,158개 항목 통과, 종료 코드 0**. `output/validation/sword-play-validation.txt`에 기록했다. 실제 공격 개수·속도·판정, 일반/강화 반복, 전조와 실제 공격 X 일치, 실제 피격, 일시정지, 중단·사망 정리, 4검 반격 및 중복 방지, 거리별 기본 공격, 궁극기 우선순위를 확인했다. 실제 스테이지 충돌 경로에서 30/60/120 FPS의 가속 36 및 감속 48 상한, 완전 정지도 통과했다.
- `git diff --check` 통과. 기존 `.meta` 변경 없음. 신규 `.meta` 6개의 GUID가 전체 Assets 안에서 각각 한 번만 존재함을 확인했다. 신규 PNG의 실제 알파 영역과 core 중심 pivot을 측정해 크기·위치를 맞췄다.
- Unity 실제 렌더 결과: `output/validation/sw01-release.png`, `sw03-warning.png`, `sw03-rise.png`, `sw03-enraged-rise.png`, `sw04-orbit.png`, `sw04-aim.png`, `swbasic-melee.png`, `swbasic-wave.png`.

첫 테스트는 런타임 공통 픽셀 스프라이트 생성과 동시에 강제로 전조를 실행해 `OverrideGeometry` 오류를 기록했다. 실제 게임처럼 첫 결정을 시작하기 전에 준비 프레임을 둔 재실행에서는 이 오류가 발생하지 않았다. 공통 전조 코드는 수정하지 않았다.

격리 프로젝트의 에디터 시작 로그에는 Unity Search 인덱서의 `ArgumentOutOfRangeException`이 있다. 게임 런타임 테스트에서 발생하는 예외와 구분하며, 원본 프로젝트나 Search 패키지는 변경하지 않았다.

재실행 방법:

```powershell
dotnet run --project tests/SwordWavePlanner/SwordWavePlanner.csproj --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/SwordWave/run-sword-playchecks.ps1
```

Unity 스크립트는 추적된 프로젝트 파일과 새 검기형 파일을 `output/validation/sword-project-<guid>/`에 복사해 실행한다. 원본 씬·설정·Library를 열거나 변경하지 않는다. 라이선스 클라이언트와 사용자 Unity 캐시에 접근 가능한 환경이 필요하다. 검증용 복사본은 자동 삭제하지 않는다.

## 8. SW05 및 다른 보스 보호

`git diff --name-only`로 SW05.cs, KarmaAerialSwordPlanner.cs, SW05 전용 PNG/재질/셰이더 및 Guard/RisingSlash 경로의 변경이 없음을 확인했다. SW05의 패턴 코드, 설정값, 리소스 경로, 공통 전용 재질 분기는 유지했다. 지연된 SW05와 준비된 일반 SW05의 차례가 평타보다 기존 우선순위를 지키는 회귀 검사도 통과했다.

## 9. 전체 작업 변경 파일

수정 7개:

```text
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossBehavior.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossMovement.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/SW03.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/SW04.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/SWBasic.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaWaveArtwork.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/KarmaSwordBossSettings.cs
```

신규 18개:

```text
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/KarmaSwordScarPlanner.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/KarmaSwordScarPlanner.cs.meta
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordScarRise.cs
Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordScarRise.cs.meta
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW01_StaggeredWave_v2.png
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW01_StaggeredWave_v2.png.meta
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW03_GroundScar_v2.png
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW03_GroundScar_v2.png.meta
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW03_RisingBlade_v2.png
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW03_RisingBlade_v2.png.meta
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW04_CounterBlade_v2.png
Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/SW04_CounterBlade_v2.png.meta
tests/SwordWavePlanner/SwordWavePlanner.csproj
tests/SwordWavePlanner/NuGet.Config
tests/SwordWavePlanner/Program.cs
tests/Unity/SwordWave/KarmaSwordPlayChecks.cs
tools/SwordWave/run-sword-playchecks.ps1
tools/SwordWave/docs/SWORDWAVE_IMPROVEMENTS.md
```

`SW01.cs`, `SW02.cs`, `SwordWaveVolley.cs`, `KarmaSwordBossPattern.cs`, `KarmaWaveMotion.cs`는 기존 구현을 재사용하여 수정하지 않았다. `output/`의 테스트 로그·렌더·컴파일 산출물·격리 프로젝트는 Git ignore 대상이다.

## 10. 남은 수동 확인

실제 입력으로 반복 회피했을 때의 난이도, 장시간 추격 시 후퇴의 체감, 저사양 GPU에서 다섯 검기의 잔상·발광 비용, 서로 다른 해상도에서 전조 가독성은 팀의 수동 플레이로 최종 튜닝해야 한다. 안전 배치의 수학적 도달 가능성은 자동 검사했으며 모든 입력 지연·플레이 습관을 보장하는 평가는 아니다. 전용 프로파일러를 이용한 장시간 메모리·GPU 측정은 수행하지 않았다.

## 이미지 생성 프롬프트

내장 imagegen 사용, 네 요청 모두 `transparent_background: true`, 새 생성 모드였다. 출력 알파를 유지해 원본 PNG를 그대로 복사했으며 기존 에셋에는 덮어쓰지 않았다. 아래가 실제 사용한 프롬프트다.

### SW01_StaggeredWave_v2.png

Use case: stylized-concept. Asset type: single 2D action game energy sword projectile sprite PNG. Primary request: one long, sharply tapered blade of blue-white energy traveling RIGHT. Horizontal silhouette roughly 4:1, narrow bright central cutting ridge, crisp icy blue outline, curved crescent cutting tip at right, two fine tapered motion streaks extending left, subtle cyan aura separated from the sharp core. Painterly clean fantasy game VFX, readable at small sizes, no ornament, no physical hilt, no characters, no scenery, no text, no border. Entire isolated effect within frame with generous transparent padding, genuinely transparent background. No huge white bloom; blade core must be solid and well defined.

### SW03_GroundScar_v2.png

Use case: stylized-concept. Asset type: one single 2D side-view action game ground telegraph sprite PNG. A long horizontal sword-cut scar torn into the ground, sharp jagged fissure with branching tiny cracks, dark navy lips and an electric blue inner seam flowing toward a white-blue central slit. Low flat 5:1 silhouette viewed from side with slight top visibility, entire horizontal extent is the dangerous lane. Clean painterly fantasy combat VFX, crisp defined edge, soft restrained cyan glow. No ground plane or rock texture background, no eruption, no sword weapon, no symbols, no text. Only one isolated crack effect on genuinely transparent background with padding.

### SW03_RisingBlade_v2.png

Use case: stylized-concept. Asset type: one single 2D side-view action game ground eruption sprite PNG. A single vertical spear-shaped sword slash of icy blue-white energy violently rising from ground, tall narrow 1:3 silhouette, pointed vertical tip at TOP, sharp luminous central blade, dark blue defined edge, faint upward streaks and small translucent shards at its foot. Restrained blue aura, bright readable core without white blob. Fantasy painterly game VFX, no physical sword handle, no scenery, no ground plane, no text. Isolated complete effect on genuinely transparent background, generous padding.

### SW04_CounterBlade_v2.png

Use case: stylized-concept. Asset type: one single 2D action game orbiting magical sword sprite PNG. Horizontal elegant sharp double-edged ethereal sword pointing RIGHT, 4:1 silhouette, narrow icy silver-blue blade and small dark blue energy guard at left, crisp white-blue cutting ridge, pointed spear tip, blue edge, restrained cyan halo. Solid readable sword silhouette to remain legible when four copies orbit a boss. Fine leftward energy trails, no giant bloom, no ornaments, no characters, no scene, no text. Isolated on genuinely transparent background with padding.
