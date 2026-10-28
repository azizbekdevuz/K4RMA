# 민재 · 방어형 보스 작업 안내

목표: 2026-10-29(목)까지 GD01~GD05를 실제로 회피·반격할 수 있는 상태로 다듬고 검증한다.
개인 브랜치: `feat/minjae-boss-guard`.
시작 기준: `feat/2d-prototype`의 `48f563d` (2026-10-25).

## 바로 실행

Unity Hub에서 이 작업 폴더를 Unity **6000.6.2f1**로 연다.
상단 **K4RMA → 방어형 연습**에서 원하는 항목을 선택하면 Play에 진입한다.
일반 스테이지를 먼저 클리어할 필요가 없다.

| 메뉴 | 용도 |
|---|---|
| 전체 패턴 자연 전투 | 방어 → 검기 → 상승베기를 계승한 최종 방어형과 정상 AI로 전투 |
| GD01 자동방어막 | 공격 없이 생성 예고·유지·만료를 반복 관찰 |
| GD02 강공격·패링 | 방어막을 준비하고 강공격 반복; K 반격 성공 시 중단·무력화 |
| GD03 분열 방패 | 투척·조준 고정·조각 사출·무피해 회수 반복 |
| GD04 방벽 상승 | 정면 방어·상승 판정·이동 차단·뒤쪽 공격 시험 |
| GD05 결계 생존 시험 | 결계와 피격 집계·성공/실패 결과 반복 |
| 다시 시작 / F6 | 현재 연습을 처음부터 재시작 |
| 체력 자동 회복 | 패턴 관찰용 선택 기능. 실제 피격·기절·결계 피격 집계는 그대로 발생 |
| 연습 종료·타이틀 | 반복 실행을 해제하고 타이틀로 복귀 |

A/D 이동, Space 점프, J 기본 공격, K 반격, Esc 일시정지.
연습은 세 기술을 이미 고유화한 최종 상태이므로 원형 Q/Shift/E 대신 고유화 기술을 사용한다.
자동 회복은 기본 꺼짐이며 사망을 되돌리는 무적 기능은 아니다. 사망하면 F6으로 재시작한다.
Play 종료 시 연습 상태가 해제되고, 연습 중 스크립트를 다시 컴파일하면 현재 항목을 처음부터 시작한다.
연습 도구는 설정 에셋의 HP·피해·밸런스를 바꾸지 않으며 Windows 빌드에는 포함되지 않는다.

## 이번 수정

- GD01: 다른 공격 도중 새 방어막을 만들었다가 그 공격의 회복 처리에 지워지는 상황을 막았다.
  생성 전조가 끝날 때까지 다음 공격을 기다리고, 방어막이 활성 상태이며 가까운 거리이면 GD02를 우선한다.
- GD02: 첫 프레임뿐 아니라 실제 공격 판정의 전체 활성 시간 동안 받은 피해를 기준으로 방어막을 갱신한다.
  이후 체력이 회복돼도 접촉 기록이 적중 결과를 유지한다. 패링 성공은 공격 중단·방어막 해제·무력화로 이어진다.
- GD03: 사출·회수에 방패 스프라이트를 사용한다. 회수 조각은 경고/공격 컴포넌트가 없는 청색 표시다.
  투척과 모든 조각은 한 접촉 기록을 공유하므로 한 기술에서 피해·기절은 한 번만 발생한다.
- GD04: 유지 벽의 물리 크기를 표시 범위에 명시적으로 맞췄다. 유지 중 피해 없음, 정면 피해 감소,
  뒤쪽 정상 피해, 생성 시 겹침 해소, 관통베기 차단을 확인하는 검사를 추가했다.
- GD05: 노란 반원 보호막과 곡면 지붕 안에서 낮은 파동·낙하 봉인·양쪽 협공·바닥 봉인·높낮이 연속 공격을 섞는다.
  같은 묶음의 공격들은 접촉 기록을 공유해 다중 피격을 막는다. HUD에 현재 공격의 회피 안내를 표시한다.
  허용 피격 초과 시 마지막 공격까지 진행한 뒤 보호막이 균열·과부하를 거쳐 직접 폭발한다.
  성공 시 보호막이 부드럽게 사라지고 `trialStunSeconds` 전체를 보장한다.
- 방어형 HUD: 보스 체력 아래 방어 상태, 현재 행동 또는 결계 피격/허용/남은 공격을 표시한다.

## 주로 수정할 곳

| 파일 | 역할 |
|---|---|
| `Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.Ward.cs` | GD01~GD04의 실제 순서·전조·공격·회수 |
| `Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.WardTrial.cs` | GD05의 5종 내부 공격·피격 집계·결과·폭발 피해 |
| `Assets/K4RMA/Runtime/Presentation/KarmaGuardDome.cs` | 반원 보호막·곡면 지붕·과부하 균열·파열·성공 소멸 |
| `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.StateMachine.cs` | 패턴 선택·회복·패링 중단·종료 처리; 다른 보스도 공유하므로 변경 범위를 확인 |
| `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Presentation.cs` | 경고 표시·공격 생성·피격 연결 |
| `Assets/K4RMA/Runtime/Configuration/KarmaBossTuning.cs` | 새 설정의 기본 수치 |
| `Assets/K4RMA/Settings/KarmaConfig.asset` | 현재 플레이 씬에서 실제 사용하는 수치 |
| `Assets/K4RMA/Editor/KarmaGuardPractice.cs` | 에디터 전용 반복 연습 메뉴 |
| `tests/Unity/KarmaGuardPlayChecks.cs` | 실제 Play Mode 방어형 회귀 검사 |

C# 기본값을 바꿔도 기존 `KarmaConfig.asset` 값이 자동으로 바뀌지는 않는다.
밸런스를 조절할 때는 연결된 설정 에셋을 확인하고, 팀의 다른 보스 공통 배율 변경은 별도로 합의한다.

## 검증 명령

Windows에서는 Python의 UTF-8 모드를 사용한다. 아래 명령은 프로젝트 루트에서 실행한다.

```powershell
python -X utf8 tools/validate-source.py
& 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Data/DotNetSdk/dotnet.exe' run --project tests/CoreChecks --configuration Release
python -X utf8 tools/run-unity-playchecks.py --guard --unity 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe'
python -X utf8 tools/run-unity-playchecks.py --unity 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe'
```

Play 검사는 임시 복사본에서 실행하며 현재 연습 씬을 바꾸지 않는다.
결과는 `output/validation/guard-play-validation.txt`, 전체 패턴은 `boss-play-validation.txt`에 남긴다.
자동 검사가 실제 사람이 느끼는 전조 가독성·회피 난도·재미를 대신하지는 않는다.

### 2026-10-28 GD05 룸 확인 결과

- 방어형 Play Mode 검사 38개 통과: 반원 지붕과 폭발 범위, 다섯 공격 흐름, 과부하 중 일시정지,
  룸 유지 후 파열, 폭발 1회 피해, 타이밍 반격, 성공·사망 정리 및 첫 프레임 예고선 표시를 포함한다.
- Unity 컴파일 오류·경고 없음. 소스 스크립트 61개와 중복 없는 에셋 GUID 248개 확인.
- 실제 룸·낙하·바닥 봉인·높낮이 공격·균열·폭발 캡처를 로컬 `output/validation/gd05-dome-*.png`에 저장한다.
- 에디터에서 임시 C# 검사 스크립트를 여러 번 로드하면 Mono가 한글 경로를 읽는 중 오류를 낼 수 있다.
  이 경우 Play를 종료한 뒤 다시 시작해 임시 어셈블리를 초기화한다.

### 2026-10-26 Windows 확인 결과

- Unity 6000.6.2f1 컴파일: 오류·경고 없음.
- Core: 396개 assertion 통과.
- 전체 보스: 21회 패턴 실행, 599개 Play Mode assertion 통과.
- 방어형: 30개 Play Mode assertion 통과. 늦은 피격 후 회복, 전조/방벽/피격,
  강화 시 보상 시간, 성공/실패/사망 정리, 연습 메뉴 실행과 설정 보존을 포함한다.
- 소스·메타데이터: 스크립트 59개, 중복 없는 에셋 GUID 154개 확인.
- 에디터 화면: 방어형 HUD와 결계 표시를 캡처해 확인했다. 이미지·검사 로그는 로컬 `output/validation`에 둔다.

에디터의 검색 색인 초기화 중 `UnityEditor.Search.SearchDatabase`의 예외가 관찰됐다.
방어형 실행 중 발생하는 게임 코드 예외와는 스택이 다르므로 Console의 발생 위치를 구분해서 확인한다.

## 목요일 전 수동 확인

- [ ] GD02를 보고 K로 반격할 수 있고, 놓쳤을 때 방어막 갱신이 눈에 보인다.
- [ ] GD03 조준 고정 후 이동해서 피할 수 있고 회수 조각이 위험 공격으로 보이지 않는다.
- [ ] GD04 뒤쪽으로 돌아 공격할 공간과 시간이 있으며 벽 생성 시 끼이지 않는다.
- [ ] GD05 성공·실패를 각각 재현하고, 강화 상태에서도 성공 보상 시간을 체감할 수 있다.
- [ ] 자동 회복을 끄고 실제 HP로 방어형 자연 전투를 완주한다.
- [ ] 방어 → 검기 → 상승 / 방어 → 상승 → 검기 두 계승 순서에서 해금을 확인한다.
- [ ] 전조·공격·회복 도중 일시정지, 사망, F6 재시작 후 방벽·공격·결계 제한이 남지 않는다.

이 체크는 사람의 플레이 후 기록한다. 자동 검사만으로 수동 항목을 완료 표시하지 않는다.
