# 검기형 보스 개선 보고 (2026-10-29)

`feat/boss-sword`에서 기존 미커밋 작업 위에 요청한 범위만 수정했다. Git add/commit/push/pull/fetch/reset/restore/switch는 실행하지 않았다. 기존 참조 4장과 검기 VFX PNG 6장의 SHA256이 작업 전과 동일함을 확인했다.

## 변경 파일과 주요 함수

| 파일 | 변경한 함수·데이터 |
|---|---|
| `Runtime/Configuration/KarmaArtworkSettings.cs` | 외형 슬롯 4개, `WaveGuardian`: 주 능력이 검기일 때 보유 능력 조합으로 선택 |
| `Resources/KarmaArtworkSettings.asset` | 새 Sprite 4종 GUID 연결 |
| `Runtime/Configuration/KarmaSpriteLibrary.cs` | `Get`: WaveGuardians Resources 조회 |
| `Runtime/Presentation/KarmaArtwork.cs` | `Guardian`, `GuardianPreview`, `LegacyGuardianPreview`: 새 표시 외형과 기존 피격 외형 분리 |
| `Runtime/Presentation/KarmaCharacterAnimator.cs` | `SetWaveAppearance`, `ApplyWaveAppearance`, `ShowPose`, `GuardianPose`: 모든 상태에서 새 외형 유지, 논리 포즈·기존 피해 실루엣 보존 |
| `Runtime/Presentation/KarmaWaveMotion.cs` | `Start`, `AdvanceMotion`, `GetDamageBounds`, `ResetVisualTransform`: 표시 방향·발 기준점·생성 첫 프레임 피격 범위 보존 |
| `Runtime/Presentation/KarmaWaveArtwork.cs` | `Apply`: SW02에만 flipX 적용 |
| `Runtime/Combat/KarmaBossAttack.cs` | `Spawn`: 이동 중 별도 판정 테두리 생성 제거 |
| `Runtime/Enemies/Patterns/KarmaEnemy.SwordWave.cs` | `PlaceSwordScars`, `StaggeredWaves`, `AerialSwords`, `FormAerialSword`, `AerialSwordReachLife` |
| `Runtime/Enemies/Patterns/KarmaAerialSwordPlanner.cs` (신규) | 시드·묶음 배분, 방향 추첨, 실제 지형·플레이어 물리 복사, 누적 검기 회피 경로 검사, 정리 |
| `Runtime/Configuration/KarmaBossTuning.cs`, `Settings/KarmaConfig.asset` | SW05 발수·묶음·각도 편차·고정 시드 설정 |
| `Runtime/Enemies/KarmaEnemy.StateMachine.cs` | SW05 도달성 검사만 새 사출 원점에 맞춤; 기존 AI 선택·접근 정책 유지 |
| `tools/unity/ApplyCombatTuning.cs` | 신규 SW05 초기 설정 일치 |
| `tests/Unity/KarmaBossPlayChecks.cs` | SW03 유지·한 번 피격·정리, SW05 실제 사출·전조·시간·정리, 기존 회귀 검사 |
| `tests/Unity/KarmaWaveVFXChecks.cs`, `KarmaWaveExpansionChecks.cs`, `KarmaWavePolicyChecks.cs` | 방향·판정·경고·새 발수·시드·실제 물리 회피 검사 |
| `tests/Unity/KarmaWaveGuardianAppearanceChecks.cs` (신규) | 4외형·계승 순서·RGBA·Pivot·모든 상태·피격 범위·여러 FPS 검사 |
| `tests/Unity/KarmaWaveCadenceChecks.cs` | Stage별 실제 외형 기대값 갱신, 기존 120초 전투 검사 유지 |
| `tools/validate-sword-polish.ps1` (신규) | Windows 컴파일/Core/격리 Unity Play·Cadence 실행 및 소스 해시 검사 |

위 런타임 경로는 `Assets/K4RMA/` 기준이다. 추가 PNG와 메타는 `Assets/K4RMA/Resources/K4RMAArtwork/WaveGuardians/`에 있다. 이번 수정 전 소스 백업과 상태 목록은 `output/validation/sword-polish-20261029/baseline/`에 보존했다.

## 외형 적용과 투명 분리

| 대상 | 실제 Sprite | 발 Pivot Y (1086×1448, X=0.5) |
|---|---|---|
| Stage 2 검기 기본형 | `Wave_Base.png` | 62/1448 |
| Stage 3 검기+상승베기 | `Wave_Rise.png` | 72/1448 |
| Stage 3 검기+베리어 | `Wave_Ward.png` | 106/1448 |
| Stage 4 검기+상승베기+베리어 | `Wave_Rise_Ward.png` | 78/1448 |

4종 모두 실제 설정 슬롯·Resources·미리보기에 연결했다. Stage 4의 상승베기/베리어 계승 순서가 반대여도 같은 최종형을 선택한다. 검기가 주 능력이 아닌 보스와 플레이어는 기존 Sprite를 사용한다.

내장 imagegen의 배경 추출 편집으로 별도 RGBA PNG를 만들었다. 제목·도장·바닥·조명·반사·주변 궤도 효과를 제거하고 갑옷·검·방패·추가 칼날·천 장식을 보존하는 방향으로 검토했다. 기본형은 리본 끝 잘림을 피하기 위해 한 번 재추출했다. 픽셀 단위 수작업 마스킹이 아닌 AI 추출이므로 원본과 완전히 동일한 픽셀을 보장하지는 않는다.

모든 자산은 Single Sprite/FullRect/100 PPU/투명 알파/밉맵 없음/자동 물리 도형 없음으로 설정했다. 알파 128 이상인 발바닥의 최하단을 Pivot으로 삼고, 기존 몸의 높이에 맞춰 표시 스케일을 조절했다. 어두운 배경과 체크보드 합성에서 제목·배경·뚜렷한 색 테두리와 장비 잘림이 없는 것을 확인했다. 알파 1~3/255인 극저투명도 색 점은 일부 남아 있으며 일반 합성에서는 보이지 않는다. 원본 알파는 임의로 수정하지 않았다.

분리 통계·출력 해시·편집 도구·기본형 재시도 프롬프트는 `output/validation/sword-polish-20261029/cutout-qa/cutout-qa-summary.json`과 `accepted-cutouts.json`에 있다. 나머지 3종의 실제 편집 프롬프트는 같은 폴더의 `CUTOUT_PROMPTS.md`에 보존했다.

## 공격과 움직임

SW02 원본은 왼쪽 날끝/오른쪽 잔상으로 그려져 있어 이 자산에만 `flipX=true`를 적용했다. Pivot은 기존 피격 코어 중심과 일치한다. 이동 벡터에 따른 회전, 실제 회전 피해 박스, 추적→고정→발사 구조는 유지했다. 다른 검기 자산의 반전은 초기화한다.

| SW03 항목 | 이전 | 변경 |
|---|---|---|
| 기본 실제 피격 경계 간격 | 6.3 | 3.0 월드 단위 |
| Collider 너비 2.6일 때 간격 | 기존 넓은 간격 | 3.1 (`max(3, Collider너비+0.5)`) |
| 검흔 크기 | 4.5×13.5 | 동일 |
| 상승 구간 | 0.25초 | 동일 |
| 상승 후 위험 유지 | 없음 | 3초 |
| 검흔 전체 수명 | 0.25초 | 3.25초 |
| 전체 패턴, 일반 | 4.96초 | 7.96초 |
| 전체 패턴, 강화 | 4.852초 | 7.852초 |

패턴 시간은 기본 설정의 이론값이며 프레임 단위 코루틴 실행에서는 조금 늘어난다. 전조 내 도달성·양 벽·실제 Collider 크기를 확인하며 기존 단일 검흔 대체 정책을 유지한다. 검흔별 기존 접촉 기록을 사용해 재접촉/매 프레임 무한 피해를 막고 이미지와 판정을 함께 정리한다.

이동은 기존 AI 속도를 바꾸지 않고 실제 이동량으로 시각 방향·걷기 위상·미세 기울기를 결정한다. 정지하면 마지막 이동 방향을 유지하며 Idle로 가라앉고, 준비 시작 때 한 번 공격 방향으로 정렬한다. 새 발 Pivot 주위로 표시 자식만 움직인다. 실제 좌표·충돌·발사점·보스 자신의 기존 DamageBounds는 생성 첫 프레임부터 분리해 보존한다. 논리 포즈 선택과 기존 Transform 공격 연출은 유지한다.

위험 경계는 준비·추적·고정 전조에만 남긴다. SW01~SW05의 실제 공격 객체는 별도 LineRenderer 없이 검기 PNG를 사용한다. SW03 지면 경고와 SW04 반격 전조는 유지하고 종료·중단 때 함께 정리한다.

## SW05 무작위 탄막

총 18~30발을 3~7묶음에 배분하고 각 묶음은 4~8발을 가진다. 360도 섹터와 각도 편차를 사용해 위·아래·좌·우·대각선, 실제 플레이어 조준과 주변 분산 조준을 섞는다. 각 검기는 별도 원점·속도·수명·피격 기록과 경로 전조를 가진다. 형성 검은 피해 컴포넌트/Collider 없이 자라며 발사 순간 동일 원점·방향의 실제 검기로 교체된다.

실제 플레이어 Collider·속도·점프·중력·현재 속도와 경기장 충돌 지형을 복사한 로컬 2D 물리 시뮬레이션에서 이동/점프 경로를 구한다. 기존 살아 있는 검기와 새 검기 모두를 연속 회전 박스로 검사해 같은 회피 경로가 열리는 방향만 선택한다. 장식과 복사 물리 월드는 보스 소유 수명에 묶어 정리한다. 잘못된 회피로 이전 검기에 이미 갇힌 상태를 무적으로 바꾸지는 않는다.

`KarmaBossTuning`의 `aerialSwordMinCount/aerialSwordCount`, `aerialSwordMinGroup/aerialSwordMaxGroup`, `aerialSwordAngleJitter`, `aerialSwordSeed`로 조정한다. 시드 0은 실행마다 새 구성을 만들고, 0 이외 값은 동일 물리 상태에서 재현한다. 공격력·검기 속도·도약/체공/방어막/착지/회복 단계는 유지했다.

| SW05 시간 | 이전 10발 | 무작위 18~30발 |
|---|---|---|
| 일반 방어막 | 5.95초 | 5.95초 |
| 일반 전체 | 9.19초 | 9.19초 |
| 강화 방어막 | 4.879초 | 4.879초 |
| 강화 전체 | 7.8598초 | 7.8598초 |

기존 7회 사출 기준 시간 예산을 묶음 간 대기에 재분배하므로 발수 증가에도 전체 시간을 연장하지 않는다. 각 발의 수명은 경기장 경계와 남은 정리 일정으로 제한하며 전조도 같은 길이를 사용한다.

## 자동 검증 결과

최종 실행: `output/validation/sword-polish-20261029/all-20261028-165606-265-utc/` (`result.txt`: `PASS: all`). 폴더명의 시간은 UTC이고 작업·보고 날짜는 한국 시간 2026-10-29이다.

| 검사 | 결과 |
|---|---|
| Unity 6000.6.2f1 Player 소스 컴파일 | PASS, 52개 소스, 경고를 오류로 처리 |
| Unity Editor 및 전체 검사 소스 컴파일 | PASS, 69개 소스, 경고를 오류로 처리 |
| Core | PASS, 419 assertions |
| 실제 Unity Play Mode | PASS, 32개 패턴 실행·38,802 assertions |
| 스테이지별 120초 전투 | PASS, 40개 시나리오·102,169 assertions |
| 검증 중 소스 일치 | 70개 SHA256 동일 |
| SW05 무작위 구성 | 1024 seed에서 18~30발·묶음 4~8발, 서로 다른 발수/묶음 구성 507개 |
| SW05 누적 회피 | 4개 고정 seed × 5개 위치(양 벽 포함) × Collider 폭 1.4/2.6 × 일반/강화 = 80/80 PASS |
| 외형 | 4종, Stage 4 두 계승 순서, RGBA·Pivot·모든 논리 포즈·피격 범위·여러 FPS 모두 PASS |
| 다른 기능 회귀 | 기존 상승베기/결계 보스·플레이어·계승·도달성·방어·일시정지·정리 검사 PASS |

40개 전투 시나리오는 Stage 2 기본형, Stage 3 상승베기형/베리어형, Stage 4 최종형 각각에 근거리·상대 거리·원거리·벽·공중 배치와 일반/강화 조건을 적용했다. 새 외형 검사와 독립 legacy 포즈 검사를 함께 유지했다.

0.05초 capture delta의 실제 코루틴 실행에서 SW03 전체는 일반 8.20초/강화 8.10초, SW05 전체는 일반 9.45초/강화 8.20초였다. 앞 표의 예정 시간과 차이는 기존 코루틴의 프레임 단위 대기 반올림이며, SW03의 추가 유지 3초와 SW05의 기존 시간 예산을 각각 별도로 검증했다. 방어막 5.95/4.879초는 예정값이며 직접 실측한 방어막 시간으로 표기하지 않았다.

SW03은 상승 후 유지 구간에서 실제 피해, 피격 무적을 지우고 20회 겹침을 검사해도 추가 피해 없음, 유지 2.5초 시점 재진입 시 추가 피해 없음, 이미지/판정 동시 종료와 중단·사망·재시작 정리를 확인했다. 개별 검흔의 유지 종료는 OnDisable 콜백 기반 3초 ±0.15초 검사로 확인했다.

SW05의 80회 실제 회피 표본은 20~27발을 포함했고 최대 동시 검기는 8개였다. 8방향 섹터 누적 발수는 `182,264,178,268,186,224,216,242`로 모든 방향을 포함했다. `BuildGroup` 최대 9.522ms, 각 사례의 최대값 평균 1.602ms였다(첫 JIT 포함, 복사 씬 생성 시간 별도). 고정 시드 `20261029`의 실제 연출은 20발/5묶음, 동시 공격 최대 4개·effects 직계 자식 최대 17개였다. 마지막 검기의 전조/실제 이동 거리는 일반 12.8/12.8, 강화 10.496/10.496로 일치했다. 이는 headless 검증 환경의 CPU 수치이며 실제 카메라/렌더링의 프레임 안정성은 Unity Profiler로 확인해야 한다.

세부 결과:

- 최종 `play/boss-play-validation.txt`, `play/unity-play.log`
- 최종 `play/SW03measurements.json`
- 최종 `cadence/wave-cadence-validation.json`, `cadence/unity-cadence.log`
- `output/validation/sword-polish-20261029/sw05-measurements.json`
- 최종 `source-hashes-before.json`, `source-hashes-after.json`

재실행 명령은 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-sword-polish.ps1 -Suite all`이다. 원본 프로젝트 대신 marker가 있는 격리 복사본에서 실행하며 이전 보고서를 덮어쓰지 않는다. 재시도에서는 검사가 끝난 이 작업의 격리 프로젝트에 `-ReuseProjectDirectory`를 지정해 임포트 캐시를 재사용했다. 각 실행의 판정·로그·소스 해시는 새 폴더에 남기며 기존 판정도 아카이브했다.

## Unity 화면에서 확인할 항목

- 네 외형의 갑옷/검/방패/천 장식 디테일과 실제 카메라 배율에서의 가독성.
- 밝은 배경에서 극저투명도 가장자리, 발 접지, 뒤돌아 걷기와 준비 방향 전환의 자연스러움.
- SW02 좌우·수직·대각선 날끝, 전조에서 발사로 넘어갈 때 테두리 제거.
- SW03 중앙 3.0 간격의 체감 난도와 3초 지속 상태의 명확성.
- SW05 여러 시드의 전조 중첩·방향 가독성·벽 근처 회피 체감·프레임 안정성.

자동 검사는 Sprite 연결·피격/회피·시간·정리를 검증하지만 미술적 완성도와 손으로 조작했을 때의 난도 평가는 화면에서 확인해야 한다.
