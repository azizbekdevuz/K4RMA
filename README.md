# K4RMA — 고유화 프로토타입

2026-10-05 수정본. 첨부 요구사항 명세서(2026-10-04)와 Notion `SW설계기초`의 프로젝트 개요·스토리·6주차 회의록을 기준으로 전투와 진행을 수정했다.

**이번 수정본은 Unity Hub에서 열 수 있는 프로젝트 폴더다.** Packages와 Editor 버전 설정을 추가했고, 빈 씬에서 Play를 누르면 K4RMA 타이틀을 생성한다. Unity 6000.6.2f1 기준. 기존 프로젝트에 적용할 수도 있다. Windows 실행 파일과 실제 화면 실행 검증은 포함하지 않는다.

## 실행

- 새 2D 프로젝트: `Assets/K4RMA` 및 `Assets/K4RMA.meta`를 Assets 아래 복사한다.
- 기존 프로젝트: Unity를 종료하고 아래 이전 도구를 사용한다. 폴더를 그냥 덮어쓰면 폐기한 구 패턴 파일이 남을 수 있다.

```sh
python3 tools/migrate-source.py "/기존/Unity프로젝트"
python3 tools/migrate-source.py "/기존/Unity프로젝트" --apply
```

기존 스크립트 GUID, 사용자 씬·설정 에셋을 보존한다. 폐기한 메테오·삼중합성 partial 파일은 백업 후 제거하고, 돌진·연사 모듈은 상승베기·검기 모듈로 이름을 바꾼다. 백업은 `.karma-migration-backups`에 남는다. 팀이 수정한 코드는 백업과 비교한다. 기존 설정의 저장값은 유지되며 새 설정의 HP 기본값은 100이다.

Unity에서 `K4RMA → 플레이 씬 생성` → Play → Enter. 생성한 씬과 설정을 저장한다. `K4RMA → 로직 검사`로 Unity 연결 규칙 검사를 실행한다. 기존 씬을 계속 쓸 경우 Config의 새 `spriteMaterial`에 Sprites/Default 재질을 연결해 빌드의 셰이더 참조를 확보한다.

## 조작

| 키 | 동작 |
|---|---|
| A/D, 좌우 방향키 | 이동 |
| Space | 점프 |
| J 유지 | 기본 검 공격; 상승베기 고유화 후 공중 방향키/WASD + J를 새로 눌러 도약베기 |
| 같은 방향키 또는 WASD 두 번 | 검기 고유화 후 관통베기 (←/→/↑/↓ 또는 A/D/W/S, 기본 0.25초 이내) |
| Q | 원형 검기 |
| 왼쪽 Shift | 원형 상승베기 |
| E | 원형 방어 |
| K | 방어 고유화 후 짧은 받아내기; 공격 도착 타이밍에 반격 |
| 고유화 화면 1/2/3, Enter, Esc | 선택, 확정, 취소 |
| 타이틀 S/H | 전체 스토리/조작 안내 |
| 스토리 방향키 | 이전/다음 페이지 |
| Esc | 일시정지 또는 안내에서 타이틀 |
| 일시정지·결과 R/T | Stage 1부터 새 도전/타이틀 |

도약베기는 상승베기 고유화 후 공중에서 방향키 또는 WASD를 누르고 J를 새로 눌러 발동한다. 누른 방향(아래·대각선 포함)으로 0.12초 동안 1.2만큼 부드럽게 베고 추가 점프한다. 순간이동 대신 물리 이동으로 지형 충돌과 화면 보간을 유지하며, 추가 점프의 높이는 일반 점프 속도의 90%이고 가로 추진력은 0.16초 동안 평소 이동으로 부드럽게 이어진다. 점프와 J를 동시에 누르는 입력도 지원한다. 방향키가 없으면 바라보는 방향으로 벤다. 착지 전 1회 사용 가능하며 착지하면 회복된다. 관통베기는 같은 방향키 또는 WASD를 빠르게 두 번 누를 때 발동하며 기본 검 쿨타임을 공유한다. 고유화된 Q/Shift/E 원형 입력은 비활성화된다.

## 수정 위치

| 수정하려는 것 | 담당 모듈 |
|---|---|
| HP, 이동, 원형 기술 수치 | Runtime/Configuration/KarmaConfig.cs 또는 씬 Config 에셋 |
| 연속 입력·도약베기·반격·상승 공격 수치 | Runtime/Configuration/KarmaTechniqueTuning.cs / Config의 techniques |
| 스테이지별 수호자 체력·빈도 | Runtime/Configuration/KarmaBossTuning.cs |
| 선택·취소·확정·누적 순서 | Core/KarmaProgressionState.cs |
| 방향키 연속 입력·추가 점프·반격 시간 규칙 | Core/KarmaTechniqueState.cs |
| 첫 선택 타입·패턴 순서 | Core/KarmaPatternCatalog.cs |
| Unity 이동·근접 판정 | Runtime/Player의 Movement / Combat |
| 원형·고유화 발동 연결 | Runtime/Abilities |
| 수호자 AI 상태 전환 | Runtime/Enemies/KarmaEnemy.StateMachine.cs |
| 개별 수호자 검술 | Runtime/Enemies/Patterns의 GroundStrike / SwordWave / RisingSlash / Ward |
| 키 처리 | Runtime/Input/KarmaInput.cs와 Player/Flow의 입력 연결 |
| 화면·문구 | Runtime/UI, Core/KarmaStory.cs |
| 외형·애니메이션·VFX | Runtime/Presentation |
| 수련장 지형·배경 | Runtime/World/KarmaStage.cs |

분리한 partial 파일은 같은 컴포넌트의 역할별 파일이며 각각 GameObject에 붙이지 않는다. 게임 전체 진행은 `KarmaGame`이 담당한다. 상세 의존 방향과 확장 절차는 [ARCHITECTURE](docs/ARCHITECTURE.md).

## Windows 제출

Unity Hub에서 **동일 버전 Windows Build Support (Mono)**를 설치한다. `K4RMA → 플레이 씬 생성`은 생성 씬을 Build Settings에 등록한다. 기존 팀 프로젝트는 제출할 씬 하나를 첫 시작 씬으로 정리한다. `K4RMA → Windows 64-bit 빌드` 결과는 `Builds/Windows/K4RMA.exe`이며 데이터 폴더와 함께 전달한다.

## 검증 상태

- Core 자동 검사 **355개 통과**: 6가지 순서, 선택 취소·확정, 누적 계승, 원형 비활성화, WASD/방향키 별칭·연속 입력 경계·취소, 프레임 간격별 도약베기 감속·이동량, 보스 외형 선택, 충격 정지/일시정지/화면 전환, 추가 점프 1회 제한·착지 복구, 반격 성공·실패·초기화.
- Unity 6000.6.2f1의 실제 참조 라이브러리를 사용한 전체 C# 컴파일: 오류·경고 0. Editor 코드 포함 및 플레이어 코드 각각 검사. 기존 Input Manager 및 새 Input System 조건부 컴파일 확인.
- 파일 구조·GUID 검사 통과. 기존 ZIP을 모사한 적용 검사에서 GUID·사용자 씬·설정 보존과 폐기 파일 백업 확인.
- **Unity 실제 플레이, 화면 렌더링, Windows 빌드, 60 FPS와 15~20분 체감은 미검증.** 이번 배포본은 실제 Unity 프로젝트 설정과 씬이 없는 수정 소스로, 이번 변경의 Play Mode 검사는 수행하지 않았다.

```sh
python3 tools/validate-source.py
dotnet run --project tests/CoreChecks/CoreChecks.csproj --configuration Release
```

.NET 8 SDK 필요. 현재 장비의 시스템 SDK가 7이라 검사에는 설치된 Unity의 번들 .NET 8 SDK를 사용했다. 이번 변경의 검사 결과는 [검증 기록](docs/VALIDATION.md)을 참조한다. 제출 전 [PLAYTEST](docs/PLAYTEST.md)를 실행한다. 세부 요구사항 대응과 남은 작업은 [변경 보고서](docs/CHANGELOG.md).


## 이미지 적용 및 타격 연출 — 2026-10-06

- 표지는 `WorldOverview.png`의 **왼쪽 위 첫 사분면**(K4RMA / MASTER & DISCIPLE)만 표시한다. 다른 세 장면은 표지에 나오지 않는다.
- 원본 7개 이미지를 `Assets/K4RMA/Resources/K4RMAArtwork`에 보존했다. WebP는 내용 그대로 PNG로 변환했다. 추가 보스 참고 `k.png`도 `GuardianEvolution.png`로 포함했다.
- 원본 플레이어 시트를 바탕으로 동작들이 겹치지 않는 `PlayerAnimation.png`를 준비했다. 대기/달리기/기본 베기/상승·아래 베기/피격/사망에 연결했다. 원본 시트는 스토리 화면에서도 확인할 수 있다.
- 전투 배경은 1번 그림의 고정 캐릭터/검기 효과를 제거한 `DojoBackground.png`를 사용한다. 충돌 지형은 그대로 유지한다.
- 보스는 투명한 목제/기계 수호자 그림을 사용한다. 처음 선택한 기술의 몸체를 유지하며, 추가로 계승한 검기는 푸른 궤적, 방어는 금빛 링, 상승베기는 긴 수직 칼날 효과로 더해진다. 새 참고 그림의 6가지 조합은 전환 화면에 연결했다. 장식의 금빛 링은 시각 효과이고 실제 방어 판정은 기존 방어 동작 동안에만 적용된다.
- 기본 베기는 3.4 사거리와 2.6 높이의 판정, 긴 밝은 반달 궤적/중심선/잔상을 사용한다. `Config.techniques.longSlashReach`가 이번 기본 검 판정의 사거리 설정이다. 기존 `Config.swordReach` 대신 이 값을 조정한다.
- 적에게 실제 피해가 들어가면 밝은 타격 불꽃과 기본 0.035초의 짧은 슬로, 0.09초의 작은 카메라 흔들림이 나온다. `hitStopSeconds`/`hitShakeStrength`를 0으로 설정해 각각 끌 수 있다. 허공 공격에서는 타격 효과가 나오지 않는다.
- 신규 이미지 로딩/실행용 UV 및 스프라이트 분리는 `Runtime/Presentation/KarmaArtwork.cs`, 보스의 누적 효과는 `KarmaGuardianAdornment.cs`에 있다. Unity의 기존 스프라이트 importer metadata는 수정하지 않는다.
- 원본 보존과 PNG/알파/프레임 경계 검사, 실제 Unity 참조 컴파일은 통과했다. 실제 Unity 플레이/화면 렌더링은 이번 환경에서 검증하지 않았다.


## 이미지가 네모로 나오던 문제의 로딩 수정

이전 코드의 PNG Resources 로딩은 Unity importer 상태에 의존했고, 실패하면 기존 네모 캐릭터로 조용히 돌아갔다. 사용 중인 프로젝트에서 실패한 정확한 경로는 확인하지 못했으므로 이를 확인된 현장 원인으로 단정하지 않는다.

이번에는 원본 PNG 11장을 `Resources/K4RMAArtwork/KarmaArtwork.bytes`에 함께 넣고 TextAsset에서 PNG를 직접 읽는다. 원본 크기/알파를 그대로 유지하여 TextureImporter의 크기 변경/스프라이트 설정과 무관하게 같은 이미지를 생성한다. 정적 캐시에 null을 저장하지 않으며 Play 진입 시 캐시를 초기화한다. 파일이 누락되면 표지에 한국어 오류를 표시하고 Console에 필요한 경로를 남기며, 네모 플레이어로 조용히 대체하지 않는다.

**가장 간단한 확인:** ZIP 전체 압축 해제 → Unity Hub에서 `K4RMA_Updated` 폴더 열기 → Unity에서 Play → K4RMA 표지 → Enter. 기존 프로젝트에 넣는 경우 `Assets/K4RMA` 전체를 적용해야 한다. `Runtime` 스크립트만 복사하면 이미지 묶음이 빠진다.

`KarmaArtworkValidation.Run`은 Unity에서 이미지 11장 디코딩/원본 크기/9개 Sprite.Create/캐릭터·배경 연결을 확인하는 실행 검사다. 이번 환경에서 Editor 실행을 시도했으나 라이선스 초기화가 실패하여 이 실행 검사와 렌더링은 완료하지 못했다. 컴파일과 이미지 묶음 무결성 검사는 통과했다.
