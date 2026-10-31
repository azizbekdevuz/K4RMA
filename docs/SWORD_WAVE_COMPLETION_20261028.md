# 검기형 보스 확대 작업 재개 기록 — 2026-10-28

## 재개 시점

`feat/boss-sword`에서 이어서 작업했다. 최초 `git status`에는 추적 파일 22개와 신규 PNG·메타·모션·아트 코드·테스트가 있었다. 내용 차이가 있는 추적 파일은 18개였고 나머지 4개 설정은 실질 diff가 없었다. 따라서 과거 UI 로그의 `Edited 10 files +188 -49`를 현재 전체 변경 목록으로 간주하지 않았다. 최초 상태·전체 diff·파일별 SHA256은 `output/validation/sword-resume-20261028/initial-*`에 보관했다.

이미 구현된 코드는 초기화하거나 중복 추가하지 않았다. 재개 시점에 다음 구현이 존재했다.

- `SwordSize`/`ScarSize`의 확대 수치, 검기 전용 연속 회전 SAT, 회전 경로 전조와 실시간 피해 경계선.
- `ExtraWaveVFX` PNG 6개와 Sprite 메타, `KarmaWaveArtwork`의 코어 기준 스케일, 기존 Sprite Library의 Resources 보완 로딩.
- SW01 2발, SW02 추적 후 방향 고정, SW03 선행 연사와 상승 검흔, SW04 유한 검막·반격 제한.
- SW05 기본 10발, 두 발씩 묶는 루프, 기존 7회 시간 예산을 재분배하는 대기, 마지막 검기의 남은 일정에 따른 수명 제한.
- 장거리 도달 판단 AI, 0.75 선택 대기, 이동량 기반 `KarmaWaveMotion`, 포즈 우선순위, 보스 `DamageBounds` 보존, 절제된 잔상·발광.

이것들은 존재 여부를 확인한 것이며 과거 PASS를 검증 결과로 사용한 것이 아니다. 재개 당시 SW03 간격·SW05 배치는 고정된 `KarmaSpace.PlayerSize`를 사용했고, SW05 테스트에는 Blade 이미지·전조 1개·순차 단발·최대 8발을 가정한 부분이 남아 있었다.

## 이번에 완료한 코드와 검증

- 검기형의 실제 피격 검사·AI 도달 검사·검흔 배치·공중 쌍 간격이 실제 `Collider2D.bounds`와 중심 오프셋을 사용한다. 다른 타입은 기존 몸 크기·AABB 경로를 유지한다.
- `PlaceSwordScars`가 피해 경계 사이 플레이어 너비 + 0.5 월드 단위를 확보하고, 전조에서 물리 한 프레임을 뺀 이동 예산 안에 안전 구간으로 들어갈 수 있는지 검사한다. 부족하면 기존 단일 검흔 정책으로 전환한다.
- SW05 오른쪽 벽 근처에서 보조 검을 경기장 안쪽에 놓으면 실제 이동·점프 경로가 모두 막히는 실행 실패를 발견했다. 주 검의 조준을 유지하고 보조 검을 가까운 외벽 쪽의 평행 경로에 놓도록 수정해 안쪽 탈출 경로를 남겼다.
- Play 검사가 실제 고유화 순서·StageIndex·방을 로드해 Neutral/Sword/Rise/Ward 보스를 검사한다. SW05는 동시 2발, 5개 서로 다른 사출 프레임, 10개 독립 접촉 기록, 각 장식 검·전조·수명·정리, 공중 보스 사망까지 검사한다.
- PNG를 직접 디코딩한 알파 코어와 Unity 임포트 결과·Pivot·PPU·Resources·GUID를 비교한다. 0°/30°/45°/90° 실제 피해를 Unity ColliderDistance와 비교하고, 고속 관통 누락·회전 전조 폭을 검사한다. Box2D 접촉 스킨에 매우 가까운 표본은 안/밖 표본을 더 추가해 구별하며, 수학적 0.001 경계는 별도 Core 검사로 확인한다.
- 실제 플레이어 크기와 더 넓은 2.6 크기, Collider 중심 오프셋 0.2를 사용해 벽·중앙·정상/강화 상태의 검흔 이동과 공중 2발 회피를 로컬 PhysicsScene2D에서 시뮬레이션한다.
- 추가로 시작점 x=-13, 6, 20, 42, 53 × 너비 1.4/2.6 × 일반/강화의 20개 조건에서 실제 접지·중력·벽 충돌·이동 속도를 사용해 10발 전체가 종료될 때까지 생존하는 이동 전략을 확인했다. 고정 이동만으로 실패하는 위치에서는 묶음별 경기장 안쪽 방향 전환이 필요했다.

## 크기와 이미지

아래는 기본 설정의 월드 단위다. 움직이는 공격의 크기는 회전 전 로컬 길이 × 두께이며, 대각선의 월드 AABB는 이보다 커진다. 판정은 그 AABB 전체가 아닌 실제 회전 사각형이다. 코어 내부의 투명 모서리도 표시된 사각 경계의 피해 판정을 따른다.

| 공격 | 이전 피해 크기 | 새 피해 크기 및 PNG 코어 | 배율 | PNG |
|---|---|---|---|---|
| SW01 | 2.4 × 1 | 3.24 × 1.3 | 1.35 × 1.30 | SW01_StaggeredWave |
| SW02 | 3.24 × 1.35 | 4.536 × 1.8225 | 1.40 × 1.35 | SW02_AimedSlash |
| SW03 선행 연사 | 2.4 × 1 | 3.24 × 1.3 | 재개 시점 확대 유지 | SW01_StaggeredWave |
| SW03 상승 | 3.6 × 10 | 4.5 × 13.5 | 1.25 × 1.35 | SW03_RisingBlade |
| SW03 지면 충격 | 독립 피해 없음 | 코어 4.5 × 2.7, 독립 피해 없음 | 상승 검흔 폭에 맞춤 | SW03_GroundImpact |
| SW04 사출 | 2.4 × 1 | 3.12 × 1.3 | 1.30 × 1.30 | SW04_CounterWave |
| SW05 각 발 | 2.4 × 1 | 3.12 × 1.3 | 1.30 × 1.30 | SW05_AerialSword |

기존 GuardianVFX 그림은 종횡비를 보존해 박스 안에 맞추므로 표 전체가 불투명한 그림 영역은 아니었다. 새 PNG는 알파 128 이상의 코어를 위 피해 크기에 맞춘다. 투명 여백을 포함한 원본 픽셀 크기는 다음과 같고 기존 PNG를 덮어쓰지 않았다.

실제 Unity Renderer 측정값은 아래와 같다. 기존 열은 확대 전 크기로 보존된 `ConfigureVisual(..., restrainedVisual: true)`를 실행한 값이며, 새 전체 그림 열은 투명 여백·발광도 포함한다. 피해는 새 코어 크기다. 움직이는 그림은 수평 방향으로 비교했다.

| 공격 | 기존 그림 Bounds | 새 전체 그림 Bounds | 새 공격 코어 |
|---|---|---|---|
| SW01 | 2.40 × 1.00 | 3.45 × 1.68 | 3.24 × 1.30 |
| SW02 | 3.24 × 1.23 | 5.10 × 2.15 | 4.536 × 1.8225 |
| SW03 상승 | 3.24 × 9.00 | 5.87 × 14.49 | 4.50 × 13.50 |
| SW03 지면 | 신규 별도 이미지 | 5.36 × 3.20 | 4.50 × 2.70, 별도 피해 없음 |
| SW04 사출 | 2.40 × 1.00 | 3.35 × 1.66 | 3.12 × 1.30 |
| SW05 각 발 | 1.01 × 1.00 | 3.32 × 1.76 | 3.12 × 1.30 |

기존 SW05 Blade의 종횡비 맞춤 그림은 피해 박스보다 짧았으므로 새 그림의 표시 길이는 그 이전 Bounds에 단순 1.3배한 값과 다르다. 새 이미지는 기존 피해 길이 2.4의 1.3배인 3.12에 코어를 맞춰 이 불일치를 없앤다.

| PNG | 원본 px | Unity 좌표의 알파 ≥128 코어 (x,y,w,h) |
|---|---|---|
| SW01 | 1461 × 361 | 43,35,1373,279 |
| SW02 | 1457 × 449 | 40,28,1296,380 |
| SW03 상승 | 304 × 1422 | 39,41,233,1325 |
| SW03 지면 | 428 × 408 | 39,40,359,344 |
| SW04 | 1422 × 297 | 41,32,1325,233 |
| SW05 | 1455 × 382 | 44,51,1366,282 |

6개 모두 Sprite Single, 100 PPU, 알파 투명도, 밉맵 없음, 코어 중앙 Custom Pivot을 사용한다. 경로는 `Assets/K4RMA/Resources/K4RMAArtwork/ExtraWaveVFX/`다. 스프라이트 라이브러리에서 이름으로 `Resources.Load<Sprite>`하므로 신규 공격 리소스는 별도 씬 GUID 연결이 필요 없다. `.meta` GUID의 유일성과 기존 직렬화 참조도 별도 검사한다.

SW03 기본 플레이어 Collider 너비는 **1.4**, 검흔 간 실제 피해 경계의 기본 간격은 **6.3**이며 최소 요구 **1.9**를 만족한다. 일반 전조 0.675초, 강화 전조 0.5535초에서 이동 속도 14로 계산하고 물리 한 프레임 0.02초를 확보한다. 중심이 안전 구간에 있으면 필요한 이동은 0이며 벽 근처는 배치 보정/단일 대체를 검사한다.

## SW05 변경 명세와 시간

기존 BPR-005의 **7발 순차 사출**을 **2발 동시 × 5묶음 = 10발**로 변경했다. 10발은 모두 별도 객체와 접촉 기록을 갖는다. 피해·속도·각 묶음 전조·도약·착지·회복·궁극기 재사용 20초를 유지한다.

| 예정 시간 | 이전 | 변경 후 |
|---|---|---|
| 일반 전조 | 매 발 0.45초 | 매 묶음 0.45초 |
| 일반 사출 뒤 대기 | 7회 각 0.4초 | 앞 4회 각 0.825초 + 마지막 0.4초 |
| 일반 공중 완전방어 | 5.95초 | 5.95초 |
| 일반 전체 패턴 | 9.19초 | 9.19초 |
| 강화 전조 | 매 발 0.369초 | 매 묶음 0.369초 |
| 강화 사출 뒤 대기 | 7회 각 0.328초 | 앞 4회 각 0.6765초 + 마지막 0.328초 |
| 강화 공중 완전방어 | 4.879초 | 4.879초 |
| 강화 전체 패턴 | 7.8598초 | 7.8598초 |

프레임 반올림 전 예정값이다. 마지막 두 발의 최대 수명/전조 거리는 일반 0.4초/강화 0.328초로 제한하고 경기장 이탈 시 더 짧아진다. 실제 실행값은 아래 검증 결과와 현재 로그를 참조한다. Inspector에서 7묶음을 넘도록 바꾸는 비기본 설정은 기존 7회 예산 보장 범위 밖이다.

## 변경 파일과 함수

이번 재개에서 직접 바꾼 파일:

| 파일 | 함수/항목 |
|---|---|
| `Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.SwordWave.cs` | `SwordPlayerBounds`, `PlaceSwordScars`, `StaggeredWaves`, `AerialSwordPair`, `FormAerialSword` 설명 |
| `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.StateMachine.cs` | `SwordShotCanReach` |
| `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Presentation.cs` | `TryAttackHit` |
| `Assets/K4RMA/Runtime/Combat/KarmaBossAttack.cs` | `ConfigureVisual`/`ConfigureFootprint`의 오래된 AABB 설명 수정 |
| `tests/CoreChecks/Program.cs` | `Main`의 정확한 회전 경계 검사 |
| `tests/Unity/KarmaBossPlayChecks.cs` | `Tick`, `StartCase`, `ObserveAerialFormation`, `ObserveAerialSword`, `CheckAerialHandoff`, `AerialCleanupCheck`, `AerialCue`/사망 케이스 |
| `tests/Unity/KarmaWaveExpansionChecks.cs` (신규) | `Run`, `ImportedArtwork`, `RotatedDamage`, `ScarPlacement`, `AerialEscape`, `FullAerialEscape`, `SimulatedSword` |
| `tests/Unity/KarmaWavePolicyChecks.cs` | 7발로 남아 있던 설명을 10발로 수정 |
| `tools/run-unity-playchecks.py` | 새 확대 검사 파일을 Play suite 의존성에 추가 |
| `docs/BOSS_PATTERNS.md` | 확대/회전/안전 구간/SW05 10발 및 시간 명세 |
| 이 보고서 | 재개 상태·결과·수치·검증 범위 |

재개 전에 수정되어 있었고 보존한 나머지 구현/에셋:

| 파일 | 기존 변경 함수/항목 |
|---|---|
| `Assets/K4RMA/Core/KarmaBossRules.cs` | `SweptOrientedBox` |
| `Assets/K4RMA/Runtime/Configuration/KarmaBossTuning.cs` | 선택 대기/재선택 필드, `aerialSwordCount=10` |
| `Assets/K4RMA/Runtime/Configuration/KarmaSpriteLibrary.cs` | `Get`의 ExtraWaveVFX Resources 로딩 |
| `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Combat.cs` | `DamageBounds`의 모션 보정 |
| `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.cs` | `Initialize` 시작 대기 |
| `Assets/K4RMA/Runtime/Presentation/KarmaArtwork.cs` | `Guardian`의 검기형 모션 연결 |
| `Assets/K4RMA/Runtime/Presentation/KarmaAttackWarning.cs` | `SetPath`, `Update`, `ApplyStyle` |
| `Assets/K4RMA/Runtime/Presentation/KarmaCharacterAnimator.cs` | `PlayPose`, `PrepareWaveAttack`, `PlayWavePhase`, `ReleaseWavePhase`, `ShowPose`, `Attack`, `GuardianPose` 및 표시 전용 상태 |
| `Assets/K4RMA/Runtime/Presentation/KarmaCombatVFX.cs` | `Burst`, `AttachAttack`, `Update`, `OnDisable` |
| `Assets/K4RMA/Runtime/Presentation/KarmaWaveArtwork.cs` + `.meta` | `Pose`, `CorePixels`, `Apply` |
| `Assets/K4RMA/Runtime/Presentation/KarmaWaveMotion.cs` + `.meta` | `Start`, `LateUpdate`, `AdvanceMotion`, `GetDamageBounds`, `ResetVisualTransform`, `OnDisable` 등 표시 전용 모션 |
| `Assets/K4RMA/Settings/KarmaConfig.asset` | 기존 직렬화 `aerialSwordCount: 10` |
| `Assets/K4RMA/Resources/K4RMAArtwork/ExtraWaveVFX.meta` 및 폴더 내부 6 PNG/6 `.meta` | 원본·GUID·임포트 설정 보존 |
| `tests/Unity/KarmaWaveCadenceChecks.cs`, `KarmaWaveMotionPolishChecks.cs`, `KarmaWaveVFXChecks.cs` | 기존 검증 코드 보존 |

StateMachine의 장거리 AI·선택 정책, SwordWave의 확대 수치·기존 패턴·묶음 루프·예산 재분배, BossAttack의 Spawn/회전 이동/경계선, Presentation의 Mark/PathMark/Attack, Core/Play 검사와 Python runner의 이전 변경도 함께 보존했다. URP Global Settings, EditorBuildSettings, QualitySettings, ShaderGraphSettings에 있던 기존 상태도 되돌리지 않았다.

## 새 실행 결과

검증 산출물 위치: `output/validation/sword-resume-20261028/`. 다음은 **이번 최종 소스의 새 실행 결과**다. 이전 작업 폴더의 PASS는 인용하지 않았다.

| 검사 | 새 결과 | 산출물 |
|---|---|---|
| Player 컴파일, 경고를 오류로 처리 | PASS, 51개 소스 | `player-compile.txt`, `player.rsp` |
| Editor 및 모든 Unity 검사 소스 컴파일, 경고를 오류로 처리 | PASS, 67개 소스 | `editor-compile.txt`, `editor.rsp` |
| Core | PASS, 419개 검증 | `core-checks.log`, `core-checks.rsp` |
| Unity Play Mode, 실제 Stage 1~4 및 각 보스 타입 | PASS, 32회 패턴 실행 / 10,709개 검증 | `play/boss-play-validation.txt`, `play/unity-play.log` |
| 확대 아트·회전·검흔 이동·공중 회피 추가 검사 | PASS, 위 Play 검사 중 711개 | 동일 Play 로그 |
| 10발 전체 연속 회피 | PASS, 20개 시작점/몸 크기/페이즈 조합 | `SW05 FULL ESCAPE` 로그, 각 `launches=10` |
| 소스·GUID·직렬화 참조 | PASS, 60개 스크립트 / 248개 유일 GUID / 1개 빌드 씬 / 510개 직렬화 참조 / 7,941개 패키지 GUID | `source-metadata-checks.log` |
| PNG 직접 디코딩 | PASS, 6개 원본의 투명도·코어·Pivot | `png-core-analysis.txt` 및 Unity `WAVE ART` 로그 |
| 기존 작업 보존 | PASS, 기존 496개 파일 SHA256 동일 / 의도한 기존 파일 9개 수정 / 신규 보고서·검사 2개 | `preservation-and-tested-source.txt`, `changes-since-resume.json` |
| 실제 검증 소스 동일성 | PASS, Play 입력 66개 소스의 시작/최종 SHA256 동일 | `play/source-hashes.json`, `preservation-and-tested-source.txt` |
| 공백·충돌 기본 검사 | `git diff --check` PASS | Git은 읽기 전용 검사만 사용 |

정상/강화 SW05의 실제 사출은 각각 2발씩 5개 서로 다른 프레임, 총 10개 독립 접촉 기록으로 확인했다. 장식 검 10개가 먼저 형성되고 각 발의 고정 조준·각 전조의 회전 크기·경기장 제한·약속한 수명·실제 사출 인계를 검사했다. 공중 무적, 방어 해제, 착지, 회복, 형성/투사체 각각의 일시정지, 중단·플레이어 사망·재시작·공중 보스 강제 사망 시 정리를 모두 통과했다. 공중 보스는 원래 무적이므로 사망 정리 검사는 내부 사망 경로에 치명타를 주입했다.

고정 프레임 0.05초에서 새 SW05의 실측 전체 시간은 일반 **9.450001초**, 강화 **8.199997초**다. 기존/변경의 예정값 9.19초/7.8598초와의 차이는 코루틴의 프레임 반올림이다. 기존 7발의 실제 실행 시간을 이번에 별도 실행한 것은 아니므로 이전 열은 수식으로 검증한 예정값이며 과거 실측 PASS를 재사용하지 않았다. 마지막 검기의 새 전조/실제 이동 거리는 일반 **12.8/12.8**, 강화 **10.496/10.496** 월드 단위로 일치했다(기존 전체 수명 거리 52.8을 예고하지 않음).

원본 및 신규 PNG·메타, `KarmaConfig.asset`, 씬·ProjectSettings, 다른 보스 패턴 파일, 기존 모션과 `DamageBounds` 코드가 초기 SHA256과 동일하다. 이번 재개에서 변한 기존 파일은 표의 9개이며 별도로 신규 검사/보고서 2개를 추가했다.

실패 이력과 중간 실행은 삭제하지 않고 `attempt-1`~`attempt-5`에 보관한다. 첫 실행에서 SW05 외벽 회피 봉쇄를 찾았고, 두 번째에서는 넓은 Collider의 Unity 접촉 스킨 경계에 걸리는 비교 표본을 찾아 내/외부 표본과 Core 0.001 경계 검사를 보강했다. 세 번째에서는 새 묶음 간격상 형성/살아 있는 투사체가 동시에 존재해야 한다는 옛 테스트 전제가 성립하지 않아, 두 실제 단계를 각각 일시정지하고 별도 정리 시점에서 검사하도록 수정했다. 네 번째 전체 Play가 통과한 뒤 다섯 번째 연속 10발 검사에서 고정 방향 이동만으로 실패하는 시작점이 있어, 실제 접지와 묶음별 방향 전환 전략을 추가해 최종 20개 조건을 통과했다. 검증을 위해 게임의 피해·속도·전조·회복시간을 바꾸지 않았다.

기본 `dotnet run --no-restore`는 설치된 SDK가 요구하는 .NET 8.0.32 참조 패키지를 찾지 못해 실패했다. 네트워크 복원/프로젝트 변경 대신 설치된 Unity의 .NET 8.0.21 참조와 Roslyn으로 같은 Core 소스/검사 프로그램을 직접 컴파일해 실행했다. Python이 없어 소스·GUID 검사는 기존 Python 검사와 같은 PowerShell 검사로 실행했다. 샌드박스 내 Unity 라이선스 접근 실패는 격리 프로젝트를 권한 있는 실행으로 다시 열어 해결했다.

## 사람이 직접 확인할 항목

- 실제 화면에서 밝기·발광·곡선형 SW02의 사각 코어 경계 가독성, 두 검기의 겹침, 6개 신규 PNG의 체감 길이·두께.
- 키보드 반응 시간과 다른 묶음의 잔여 투사체까지 겹치는 상황에서 벽 회피 난도. 자동 검사는 개별 쌍과 20개 조건의 10발 연속 이동 전략을 확인했으며 모든 가능한 입력·위치·속도 설정을 완전 탐색한 것은 아니다.
- SW03 안전 구간 진입의 체감 여유와 확대된 상승 상단, SW04 공격 반응·검막 종료, 정상/강화의 시간 체감.
- 6가지 고유화 순서의 장시간 플레이, 저프레임/가변 프레임·실제 렌더링 성능. 자동 Play는 고정 0.05초 프레임과 `-nographics`로 실행하므로 시각 품질과 실사용 입력 감각은 직접 확인한다.

Git add/commit/push/pull/fetch/reset/restore/switch는 실행하지 않았다. 브랜치와 미커밋 작업을 유지하고 원본 PNG·다른 보스 패턴·씬·Config를 이번 재개에서 임의로 변경하지 않았다.
