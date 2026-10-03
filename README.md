# K4RMA 소스 저장소

나의 성장 방식과 보스의 성장 방식을 함께 결정하는 Unity 2D 보스 액션 프로토타입.

첨부한 `K4RMA_Unity_Modular.zip`의 코드를 기능별로 분리한 소스 배포본이다. **Unity 프로젝트 전체가 아닌 소스와 관리 도구**이며, 실제 사용 중인 Unity 프로젝트의 `Packages`, `ProjectSettings`, 저장한 씬과 설정 에셋은 팀 프로젝트에 그대로 유지한다.

## 처음 실행

1. Unity Hub에서 팀이 사용하는 버전의 2D 프로젝트 생성 또는 기존 프로젝트 열기.
2. 신규 프로젝트에는 이 폴더의 `Assets/K4RMA`와 `Assets/K4RMA.meta`를 프로젝트의 `Assets` 아래 복사. `Assets/Assets` 형태로 넣지 않는다.
3. 컴파일 완료 후 메뉴 **K4RMA → 플레이 씬 생성**.
4. 생성한 씬 저장 → Play → Game 창 클릭 → Enter.
5. **K4RMA → 로직 검사**로 Unity 관련 검사 실행.

기존 코드가 있는 프로젝트는 그대로 추가 복사하면 클래스가 중복된다. 아래 이전 절차를 먼저 사용한다.

## 기존 프로젝트에 적용 — 씬·설정·.meta 보존

Unity 종료 후 배포 폴더에서 실행. Python 3 필요.

```bash
# 실제 수정 없이 이동할 파일 확인
python3 tools/migrate-source.py "/기존/Unity프로젝트"

# 확인한 이동 계획 적용. 기존 스크립트 .meta/GUID 보존, 변경 전 파일 백업
python3 tools/migrate-source.py "/기존/Unity프로젝트" --apply
```

기존 C# 파일은 새 모듈 경로로 옮기며, 이름이 같은 기존 스크립트의 `.meta`를 새 위치에 유지한다. `Generated`, 사용자 설정 `.asset`, 저장한 `.unity` 파일은 건드리지 않는다. 중복 클래스 파일이 이미 있으면 적용 전에 중단한다. 다른 위치의 파일 이름이 동일한 경우에도 직접 확인하도록 중단한다.

코드에서 유지한 이름: `KarmaPrototype` 네임스페이스, 기존 MonoBehaviour·ScriptableObject 클래스명, Config의 직렬화 필드명. 따라서 기존 설정을 코드 기본값으로 덮어쓰지 않는다. 기존 `.meta`가 애초에 없었다면 원래 GUID를 복구할 수는 없다. 이 경우 씬의 Missing Script 여부를 확인한다.

실제 적용 후 Unity를 열어 Console 컴파일 오류, 기존 씬의 Config 연결, Play 동작을 확인한다. 매번 플레이 씬을 생성할 필요는 없다.

백업은 프로젝트 루트 `.karma-migration-backups/실행시간/`에 저장한다. 사용자 수정 코드가 있다면 덮어쓰기 전에 Git 커밋으로 보관하고, 백업과 새 코드를 비교한다.

## 파일 구조

```text
Assets/K4RMA/
  Core/                 무의식화 상태·타입·패턴 해금·화상 시계 (Unity 의존 없음)
  Runtime/
    Flow/               게임 진행·재도전·카메라
    Configuration/      체력·공격력·난이도·색·폰트
    Player/             이동·기본 공격·피격·화상
    Abilities/          액티브 기술·패시브 효과
    Enemies/            보스 상태 머신·전투·상태 효과
      Patterns/         메테오·돌진·보호막·삼중 합성·추가 기본 공격
    Combat/             투사체·지면 강타·불길 판정
    World/              스테이지 지형
    Presentation/       도형·이펙트·패시브 발동 표시
    UI/                 한국어 HUD·선택창·타이틀·결과창
    Input/              입력 처리
  Editor/               씬 생성·설정 초기화·검사 메뉴
tests/CoreChecks/       Unity 없이 생산 Core 코드를 검사하는 C# 실행 파일
tools/                  기존 코드 이전·소스 구조 검사
docs/                   구조·협업·변경 안내·기획·플레이테스트
.github/                자동 검사·PR 템플릿
```

## 수정할 위치

| 작업 | 파일·위치 |
|---|---|
| 체력·공격력·쿨타임 | 씬에 연결된 `KarmaConfig.asset`의 Inspector |
| 보스 체력·메테오 수·화상·기절 | Config의 `bossTuning` |
| 이동·점프·독립 돌진 | `Player/KarmaPlayer.Movement.cs` |
| 검 공격·피격 | `Player/KarmaPlayer.Combat.cs` |
| Q 화염탄·Shift 돌진·E 보호막 | `Abilities/KarmaEssences.ActiveSkills.cs` |
| 불씨·관통 검기·충격파 | `Abilities/KarmaEssences.PassiveEffects.cs` |
| 무의식화 상태와 순서 | `Core/KarmaProgressionState.cs` |
| 보스 패턴 해금·공격 순환 목록 | `Core/KarmaPatternCatalog.cs` |
| 개별 보스 패턴 구현 | `Enemies/Patterns/KarmaEnemy.*.cs` |
| 상태 전환·추적·접촉 피해 | `Enemies/KarmaEnemy.StateMachine.cs` |
| 그래픽·애니메이션 교체 | `Presentation/KarmaVisuals.cs`, `KarmaRemnantVFX.cs` |
| 한국어 문구·선택창 | `UI/KarmaHUD.Screens.cs`, `KarmaHUD.Combat.cs` |

`partial` 파일들은 **동일한 Unity 컴포넌트 하나를 역할별로 나눈 것**이다. 각 파일을 GameObject에 따로 붙이지 않는다. 주 컴포넌트는 기존처럼 `KarmaPlayer.cs`, `KarmaEnemy.cs` 등에 해당한다. 구조와 의존성은 [ARCHITECTURE](docs/ARCHITECTURE.md) 참고.

## GitHub 관리 시작

**실제 Unity 프로젝트 루트를 저장소 루트로 사용**한다. 그곳에 배포본의 `.gitignore`, `.gitattributes`, `.editorconfig`, `README.md`, `docs`, `tools`, `tests`, `.github`를 복사한다. `Assets/K4RMA`는 위 이전 도구로 적용한다. 팀이 쓰던 Packages/ProjectSettings를 이 배포본의 파일로 교체할 필요는 없다.

GitHub에 팀 저장소를 만든 뒤 다음 명령 실행. 현재 폴더는 실제 Unity 프로젝트 루트여야 한다.

```bash
git init
git add Assets Packages ProjectSettings README.md docs tools tests .github .gitignore .gitattributes .editorconfig
git commit -m "Modularize K4RMA prototype source"
git branch -M main
git remote add origin <팀_GitHub_저장소_URL>
git push -u origin main
```

`.meta`는 반드시 에셋과 함께 커밋한다. `Library`, `Temp`, `Logs`, 개인 `UserSettings`는 제외한다. 생성한 씬과 `KarmaConfig.asset`도 `.meta`와 함께 커밋하면 팀원이 같은 씬·수치로 실행할 수 있다. 팀원끼리 Unity 버전과 Packages 잠금 파일을 맞춘다. 협업 절차는 [GITHUB](docs/GITHUB.md) 참고.

## 검사

```bash
python3 tools/validate-source.py
dotnet run --project tests/CoreChecks/CoreChecks.csproj --configuration Release
```

GitHub push/PR에서 위 두 검사를 자동 실행한다. .NET 8 SDK가 필요하며, 별도 NuGet 테스트 라이브러리를 사용하지 않는다. Unity 관련 판정은 Unity 메뉴 `K4RMA → 로직 검사`와 실제 플레이로 확인한다. **Core CI 통과가 Unity 전체 빌드 통과를 의미하지는 않는다.**

## 조작

| 입력 | 동작 |
|---|---|
| A/D·좌우 방향키 | 이동 |
| Space | 점프 |
| J 유지 | 연속 검 공격 |
| Q / 왼쪽 Shift / E | 의식 상태의 화염탄 / 돌진 / 보호막 |
| 보스 처치 후 문 근처 F | 무의식화 선택 |
| 1/2/3·클릭 | 화염/돌진/방어 선택 |
| Esc | 일시정지 |
| 사망 후 R | 현재 스테이지 재도전 |

## 현재 구현 범위

이번 변경은 기존 전투를 유지하는 구조 정리다. 불씨·검기 관통, 적 화상, 추적 위치 예고 메테오, 보호막 반사, 연속 돌진, 벽 충돌 삼중 합성·기절 등을 유지한다.

**첫 선택이 보스의 베이스 타입을 결정하고 이후 선택이 그 타입을 강화하는 완전한 설계는 아직 구현되지 않았다.** 현재는 기술 집합에 따른 단독·합성 패턴 해금과 일부 공격 순서 차이다. 첫 잔재 기반 타입 변형, 충전 돌진 베기, 정식 그래픽·사운드 등은 별도 구현 대상이다. [CONCEPT](docs/CONCEPT.md), [PLAYTEST](docs/PLAYTEST.md) 참고.

15~20분은 기존 밸런스의 목표이며 실측 보장은 아니다. 구조 변경 후 Unity 실제 실행·렌더링·전체 플레이 시간은 팀 환경에서 확인해야 한다.
