# K4RMA 협업 규칙

작업 전에 읽고 적용한다. 현재 파일 배치는 [STRUCTURE.MD](STRUCTURE.MD), AI 진입점은 루트 [AGENTS.md](../AGENTS.md)다.

## 1. 작업 범위와 승인

- 요청·대화·브랜치로 담당과 승인 범위를 확인한다. 시작 시 `git status --short`, `git diff`로 기존 변경을 구분하고 타인의 작업을 덮어쓰지 않는다.
- 기본 작업 범위는 2절의 담당 경로다. 그 밖의 코드·공통 아트·Resources·씬·프리팹·설정·패키지·공통 검사·도구·문서는 공통 파일이다. 담당 보스의 전용 도구는 `tools/<보스명>/`에 둔다.
- **공통 파일을 추가·수정·이동·삭제해야 하면, 먼저 대상·이유·변경 내용·영향받는 보스를 설명하고 사용자 승인을 받는다. 승인 전에는 변경하지 않는다.** 보스 수정 요청, 필요성 판단, 사전 공유만으로 승인받은 것으로 간주하지 않는다. 이미지 공용화·문서 갱신·공통 실행기 변경도 같은 기준을 적용한다.
- 예외: `tests/` 아래의 검사 파일은 공통 검사를 포함해 작업에 필요한 추가·수정·이동·삭제를 별도 승인 없이 진행한다. 보스별 검사 배치·기대값 소유권은 2절을 따른다. 이 예외는 `tests/` 밖의 게임 코드·에셋·설정·문서·실행 도구 변경에는 적용하지 않는다.
- 사용자가 이미 명시적으로 승인한 대상과 범위는 재승인 없이 구현·검증·정리까지 진행한다. 범위가 추가되거나 승인 여부가 불명확하면 해당 변경을 보류하고 질문한다. 도구의 권한 승인은 별도다.
- 자신의 임시 생성물 정리는 6절에 따라 진행한다. 이를 공통 소스·에셋 변경의 승인으로 확대하지 않는다.

## 2. 보스별 담당과 구조

경로는 저장소 루트 기준이며, 새 파일도 담당 폴더에 둔다.

| 담당 | 코드·설정·이미지 연결 | 아트·효과·재질·셰이더 | 검사 |
|---|---|---|---|
| RisingSlash · 상승베기 | `Assets/K4RMA/Runtime/Enemies/Bosses/RisingSlash/` | `Assets/K4RMA/Arts/Bosses/RisingSlash/` | `tests/Unity/RisingSlash/` |
| SwordWave · 검기 | `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/` | `Assets/K4RMA/Arts/Bosses/SwordWave/` | `tests/Unity/SwordWave/` |
| Guard · 방어 | `Assets/K4RMA/Runtime/Enemies/Bosses/Guard/` | `Assets/K4RMA/Arts/Bosses/Guard/` | `tests/Unity/Guard/` |

- 보스의 공격·선택·이동·방어·피격·자세·효과·수치와 검사 기대값은 그 보스의 담당 폴더에서 수정한다. 다른 보스의 구체 타입·전용 상태·전용 애니메이션 메서드를 참조하지 않는다. `partial`은 공통 호스트 접근 수단이며 다른 보스 상태 접근 허가가 아니다.
- `Behavior`는 공격 목록(`BuildCycle`)·선택(`Eligible`)·이동(`TickMovement`)·공격 주기·연결을 담당한다. 검기 이동은 `KarmaSwordBossMovement.cs`, 개별 공격은 `Patterns/`, 전용 연출은 `Presentation/`, 방어 기본 공격은 `Patterns/GDBasic.cs`다.
- 공통 호스트는 `BossBehavior`의 피해 경계·피격 효과·접촉 결과·방어·공격 종료·카메라·패링 입력 계약을 호출한다. 보스는 자기 파일의 `[BossKind]`로 등록하고 설정 접근은 `Settings/KarmaBossTuning.*.cs`에 둔다. 공통 팩토리에 보스별 분기를 추가하지 않는다.
- 기존 `EnemyAttack` 숫자는 호환성을 위해 유지한다. 새 전용 패턴은 보스 폴더에서 이름 있는 `const EnemyAttack` 캐스트 상수로 선언한다. 확장 ID는 SwordWave=1000~1999, RisingSlash=2000~2999, Guard=3000~3999이며 공통 enum에 추가하지 않는다. 공격 목록·해금·기대값은 담당 Behavior와 소유권 검사에서 갱신한다.

## 3. 설정과 이미지

### 수치

- 저장 수치는 담당 `Settings/Resources/Karma…BossProfile.asset`에서 수정한다. 접두사는 RisingSlash=`KarmaRising`, SwordWave=`KarmaSword`, Guard=`KarmaGuard`다.
- Inspector의 `settings`가 실제 저장값이다. `Karma…BossSettings.cs` 초기값만 바꿔서는 기존 에셋에 반영되지 않는다. 피해·이동·판단·회복 배율은 Profile의 `damageScale`·`movementScale`·`decisionScale`·`recoveryScale`을 사용한다. 런타임은 복사본이므로 수정 후 Play Mode를 다시 시작한다.

### 배치와 연결

- 아트는 `Assets/K4RMA/Arts/`만 사용한다. 보스 전용은 `Bosses/<보스명>/Characters/`·`Effects/`, 플레이어·기본형·배경·UI·공용 효과는 `Common/`에 둔다. `Art/` 폴더를 다시 만들지 않는다.
- 파일·폴더·시트·아틀라스는 보스·캐릭터·동작·효과·배경·UI 등 함께 관리·로딩할 의미 단위를 유지한다. 파일 수를 줄이기 위해 관련 없는 이미지를 섞지 않는다.
- 외형 참조 atlas를 포함해 픽셀·색상 정보·슬라이스·피벗·PPU·임포트 설정·사용 목적이 호환되는 동일 이미지는 공용 원본을 공유한다. 실제 차이가 필요한 전용 이미지는 유지하고, 비슷하게 보인다는 이유만으로 합치지 않는다. 공용 이미지 변경은 모든 참조 보스에 영향을 준다.
- Sprite와 논리 이름(`poses`)은 담당 `Presentation/Resources/BossArtwork/Karma…BossSprites.asset`의 같은 인덱스에 연결한다. 보스 라이브러리는 자동 검색된다. 공통 라이브러리에 전용 이미지를 등록하거나 그룹/이름 조합을 중복 등록하지 않는다.
- 효과 그룹은 `RisingBossVFX`·`SwordBossVFX`·`GuardBossVFX`, 기본 외형은 `BossAppearance`, 기절은 `BossStun`, 동작 시트는 `Rise`·`Wave`·`Ward`다. 코드는 `KarmaArtwork.Sprite`·`KarmaCombatVFX.SpawnSprite`로 조회한다.
- 이동·이름 변경은 `.meta`도 함께 옮겨 GUID·Sprite ID를 보존한다. 새 복사본은 새 GUID를 쓰고 기존 `.meta`를 재생성하지 않는다. 계승·부활 Resources 상대 경로를 유지한다.

### 이미지 작업과 검증

1. `python3 tools/optimize-images.py --duplicates-only`로 전체 아트의 중복 후보를 찾고 기존 원본 재사용을 우선한다. 다른 인코딩의 후보는 픽셀·색상 정보도 비교한다. 통합할 파일의 참조는 6절 기준으로 확인한다.
2. 추가·변경한 의미 단위 경로만 `python3 tools/optimize-images.py --apply <경로>`로 무손실 압축한다. 전체 최적화 요청일 때만 전체를 대상으로 한다. 도구는 해제된 이미지 데이터와 나머지 청크의 동일성을 검증하고 작아진 PNG만 저장한다. 통과한 파일을 같은 작업에서 재압축·재검사하지 않는다. `--check`는 쓰기 없는 감사·CI용이다.
3. 색상·알파·해상도·프로필·Sprite 영역·피벗·PPU를 보존한다. PNG 외 형식, 손실 압축·리사이즈·아틀라스 재배치·임포트 압축·Read/Write 변경은 실제 사용처와 화질·런타임 영향을 별도 검증한다.
4. 이동·공용화·임포트 변경은 바뀐 Sprite·재질·셰이더와 Resources·논리 이름·Sprite ID 연결만 최소 확인한다. 필요하면 Unity에서 해당 연결이나 화면·전투 동작만 짧게 확인하며, 검사 범위는 7절을 따른다. 중복 유지 사유, 파일 수·용량 절감량과 결과를 보고한다.

## 4. 협업과 충돌

- 승인받은 공통 변경은 한 명이 맡아 작은 별도 PR로 분리한다. 특히 `Core`의 공격 ID·진행 규칙, `KarmaEnemy*`·`KarmaBossBehavior`의 전투 계약, 공통 전투·플레이어·연출 변경은 영향받는 보스의 변경 동작만 최소 확인한다.
- 씬·공용 에셋·설정은 동시에 편집하지 않는다. 무관한 자동 저장·임포트 변경이나 충돌 회피만을 위한 다른 보스 수정을 섞지 않는다.
- 충돌 시 양쪽 의도를 확인한다. `ours`/`theirs`로 파일 전체를 덮어쓰거나 타인 브랜치를 강제 push하지 않는다.

## 5. 문서

- 정책·작업 절차는 이 문서 한 곳에만 쓴다. `AGENTS.md`는 진입 링크만 유지하고 규칙 본문을 다른 AI 파일에 복사하지 않는다.
- `STRUCTURE.MD`는 현재 파일·폴더 트리만 적고 구조가 바뀔 때만 갱신한다. `.meta`·Git/IDE/Unity 캐시·검증 생성물·작업 방법·이력은 넣지 않는다.
- 이전 구조 문서·중복 규칙 문서를 남기지 않는다. 이름 변경 시 관련 링크도 수정한다.

## 6. 완료 전 정리

게임 실행·개발·빌드·검증·협업에 필요한 파일만 유지한다. 무관한 기능·실험·샘플·미사용 의존성이나 막연한 예비 파일을 추가하지 않는다. 완료 보고·커밋·PR 전에 승인 범위 안에서 아래를 한 번 수행한다.

1. 대체·삭제 후보의 코드·GUID·문자열 경로·Resources·shader include·씬·프리팹·도구 참조를 확인한다. 참조를 이전하고 관련 검증을 통과한 뒤 이전 파일과 `.meta`, 빈 폴더·폴더 `.meta`를 함께 정리한다. 이름이나 GUID 검색 하나만으로 삭제하지 않는다.
2. 자신의 임시 스크립트·백업·성공 검사 로그/캡처·Python 캐시·OS 파일은 사용 프로세스 종료 후 삭제한다. 필요한 검사 요약은 먼저 기록한다. 실패·미해결 진단 자료는 해결·재검증까지 보존한다.
3. 타인의 변경·생성물, 용도가 불분명한 파일, 유효한 구현·검사·전용 에셋·개발 중 기능은 보존한다. Git 이력·개인 설정·사용 중인 Unity 캐시를 지우거나 `git clean -fdx` 같은 일괄 삭제를 하지 않는다. 버전 백업은 Git 이력으로 관리한다.
4. `git status --short`, `git diff --check`, 잔여 참조·고아 `.meta`·구조/링크를 확인한다. 삭제 내역과 보존 후보·이유를 짧게 보고한다.

생성물은 `.gitignore`에 등록하고 임시 파일은 Assets 밖에 둔다. `Library/`·`Temp/`·`Logs/`·`UserSettings/`·`output/`·IDE 설정·생성 솔루션/프로젝트·캐시·비밀정보는 커밋하지 않는다. 정리는 작업 종료 절차이며 상시 감시기·예약 작업·Git hook을 설치하지 않는다.

## 7. 검증과 PR

### 검증

- Unity는 `ProjectSettings/ProjectVersion.txt` 버전을 사용한다. 무관한 Editor·패키지 업그레이드를 하지 않는다.
- **기본은 최소 검사다.** 변경한 부분의 정상 동작을 짧게 확인하는 데 필요한 검사만 수행하고, 확인되면 마무리한다. 보스 전투는 발동·명중·회복·패링·중단·사망 중 직접 영향받는 동작만 확인한다. 문서만 바뀌면 변경 내용과 필요한 경로·링크만 확인한다.
- **전체 회귀 검사·여러 보스의 일괄 검사·장시간 반복 검사는 사용자가 명시적으로 요청한 경우에만 실행한다.** 공통 파일 변경만을 이유로 검사 범위를 자동으로 넓히지 않는다. 최소 확인에 필요하지 않은 별도 Unity 프로젝트·검증 환경도 새로 준비하지 않는다.
- 실패하면 실패한 부분만 수정·재확인한다. 새 변경이나 실패 없이 통과한 검사를 반복하지 않는다. 미실행·실패 검증을 통과로 보고하지 않고, 실제로 확인한 범위만 짧게 보고한다.
- 경계 검사: `python3 -m unittest discover -s tests -p test_boss_boundaries.py`. 공통의 보스 직접 의존·보스 간 구체 타입 참조·전용 상태 접근을 확인한다.
- Unity 실행기: `python3 tools/run-boss-playchecks.py`. 경계 검사를 먼저 실행하며, `--suite`에 아래 이름을 공백으로 나열하면 같은 복제 프로젝트에서 선택 실행한다. 실행기 자체의 기본값은 전체 실행이므로 기본적으로 인수 없이 실행하지 않는다. 필요한 부분만 직접 확인하는 것을 우선하고, suite 실행이 필요한 경우에만 해당 `--suite`를 명시한다.

| Suite | 확인 범위 |
|---|---|
| `isolation` | 보스 등록·공격 연결·설정 분리·아트 참조 |
| `guard` | 방어형 GD01~GD05 회귀 |
| `sw05` | 최신 SW05 비행·검 소나기·명중·완료·중단 |
| `sword` | 검기형 이동·평타·SW01~SW04 회귀 |
| `rising` | 상승형 기본·UP01~UP05 실행·자세·정리 회귀 |

상승형의 모든 피격·패링 조합과 수동 플레이 전체를 보장하지 않는다. 결과는 `output/validation/`에 생성하며 보존·삭제는 6절을 따른다.

### 커밋과 병합

- 자기 작업 파일만 선택해 커밋한다. PR에는 변경 내용·검증 결과·공통 변경 여부를 적는다.
- 최신 `feat/2d-prototype`에서 개인 브랜치를 만들어 push하고 PR 대상도 `feat/2d-prototype`으로 지정한다. 통합 브랜치에 직접 push하지 않는다. 정민 확인 후 **Create a merge commit**으로 병합한다.
- 병합 후 자신의 작업을 먼저 커밋하고 최신 `origin/feat/2d-prototype`을 작업 브랜치에 merge한다.
