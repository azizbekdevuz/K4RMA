# K4RMA 협업 규칙

작업 전 이 문서를 읽는다. 파일 배치는 `STRUCTURE.MD`, AI 진입점은 루트 `AGENTS.md`다.

## 1. 작업 범위

- 요청·대화·브랜치로 담당과 승인 범위를 확인한다. 공통 변경·전체 정리를 요청받았다면 필요한 구현·검증·정리까지 진행하며 재승인받지 않는다. 범위가 불명확하거나 승인 밖 변경·필수 정보가 필요할 때만 질문한다. 도구의 권한 승인은 별도다.
- 시작 시 `git status --short`, `git diff`로 기존 변경을 구분하고 다른 사람의 작업을 덮어쓰지 않는다.
- 기본 범위는 2절의 담당 경로다. 그 밖은 공통 변경이므로 대상·이유·영향받는 보스를 먼저 공유한다. 이미지 공용화에 필요한 참조·공통 아트·검사·문서는 3절 범위에 포함한다. 충돌 회피만을 위해 다른 보스 파일을 수정하지 않는다.

## 2. 보스별 담당 경로

경로는 저장소 루트 기준이며, 새 파일도 해당 폴더에 둔다.

| 담당 | 코드·설정·이미지 연결 | 아트·효과·재질·셰이더 | 검사 |
|---|---|---|---|
| RisingSlash · 상승베기 | `Assets/K4RMA/Runtime/Enemies/Bosses/RisingSlash/` | `Assets/K4RMA/Arts/Bosses/RisingSlash/` | `tests/Unity/RisingSlash/` |
| SwordWave · 검기 | `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/` | `Assets/K4RMA/Arts/Bosses/SwordWave/` | `tests/Unity/KarmaSW05PlayChecks.cs`, `tests/Unity/SwordWave/` |
| Guard · 방어 | `Assets/K4RMA/Runtime/Enemies/Bosses/Guard/` | `Assets/K4RMA/Arts/Bosses/Guard/` | `tests/Unity/KarmaGuardPlayChecks.cs`, `tests/Unity/Guard/` |

- 요청에서 지정한 보스·경로를 우선한다. 전용 도구는 `tools/<보스명>/`에, 새 검사 폴더는 필요할 때만 만든다. 공통 실행기 변경은 공통 작업으로 다룬다.
- `Behavior`는 공격 목록(`BuildCycle`)·선택(`Eligible`)·이동(`TickMovement`)·공격 주기·연결을 담당한다. 검기 이동은 `KarmaSwordBossMovement.cs`, 개별 공격은 `Patterns/`, 전용 연출은 `Presentation/`, 방어 기본 공격은 `Patterns/GDBasic.cs`다.
- 보스 폴더의 `partial KarmaEnemy`도 공통 상태를 사용한다. 폴더 분리나 텍스트 병합 성공은 동작 격리를 보장하지 않는다.

## 3. 설정과 이미지

### 수치

- 저장 수치는 담당 코드 폴더의 `Settings/Resources/Karma…BossProfile.asset`에서 수정한다. 파일 접두사는 RisingSlash=`KarmaRising`, SwordWave=`KarmaSword`, Guard=`KarmaGuard`다.
- Inspector의 `settings`가 실제 저장값이다. `Karma…BossSettings.cs` 초기값만 바꿔서는 기존 에셋에 반영되지 않는다. 피해·이동·판단·회복 배율은 Profile의 `damageScale`·`movementScale`·`decisionScale`·`recoveryScale`을 사용한다. 런타임은 복사본이므로 수정 후 Play Mode를 다시 시작한다.

### 이미지 연결과 의미 단위

- 아트는 `Assets/K4RMA/Arts/`만 사용한다. 보스 전용은 `Bosses/<보스명>/Characters/`·`Effects/`, 플레이어·기본형·배경·UI·공용 효과는 `Common/`에 둔다. `Art/` 폴더를 다시 만들지 않는다.
- 파일·폴더·시트·아틀라스는 보스·캐릭터·동작·효과·배경·UI 등 함께 관리·로딩할 의미 단위를 유지한다. 관련 없는 이미지를 파일 수 절감만을 위해 한 장에 섞지 않는다.
- 외형 참조 atlas를 포함해 픽셀·색상 정보·슬라이스·피벗·PPU·임포트 설정·사용 목적이 호환되는 동일 이미지는 공용 원본을 공유한다. 실제 차이가 필요한 전용 이미지는 유지하고, 비슷하게 보인다는 이유만으로 합치지 않는다. 공용 이미지 수정은 모든 참조 보스에 영향을 준다.
- Sprite와 논리 이름(`poses`)은 담당 `Presentation/Resources/BossArtwork/Karma…BossSprites.asset`의 같은 인덱스에 연결한다. 보스 라이브러리는 자동 검색되며 공통 라이브러리에 전용 이미지를 등록하거나 그룹/이름 조합을 중복 등록하지 않는다.
- 효과 그룹은 `RisingBossVFX`·`SwordBossVFX`·`GuardBossVFX`, 기본 외형은 `BossAppearance`, 기절은 `BossStun`, 동작 시트는 `Rise`·`Wave`·`Ward`다. 코드는 `KarmaArtwork.Sprite`·`KarmaCombatVFX.SpawnSprite`로 조회한다.
- 이동·이름 변경은 `.meta`도 함께 옮겨 GUID·Sprite ID를 보존한다. 새 복사본은 새 GUID를 쓰고 기존 `.meta`를 재생성하지 않는다. 계승·부활 Resources 상대 경로를 유지한다.

### 이미지 작업 순서와 필수 검증

1. 전체 아트는 `python3 tools/optimize-images.py --duplicates-only`로 빠르게 중복 후보를 찾고 기존 원본 재사용을 우선한다. 다른 인코딩의 후보는 픽셀·색상 정보도 비교한다. 실제 통합은 6절의 참조 확인을 거친다.
2. 새로 추가·변경한 의미 단위 경로만 `python3 tools/optimize-images.py --apply <경로>`로 무손실 압축한다. 전체 최적화 요청일 때만 전체를 대상으로 한다. 도구는 작아진 PNG만 저장하며 해제된 이미지 데이터와 나머지 청크의 동일성을 저장 전에 검증한다. 이 검증을 통과한 파일을 같은 작업에서 다시 재압축·검사하지 않는다. `--check`는 쓰기 없는 감사·CI용으로 사용한다.
3. 색상·알파·해상도·프로필·Sprite 영역·피벗·PPU를 보존한다. PNG 외 형식, 손실 압축·리사이즈·아틀라스 재배치·임포트 압축·Read/Write 변경은 실제 사용처와 화질·런타임 영향을 별도 검증한다.
4. 이동·공용화·임포트 변경은 Unity에서 Sprite·재질·셰이더의 실제 로딩, Resources·논리 이름·Sprite ID 연결을 필수 검증한다. 보스 연결은 7절의 `isolation`, 화면·전투 영향은 해당 플레이 검사로 확인한다. 중복 유지 사유, 파일 수·용량 절감량과 검증 결과를 보고하고 정리는 6절에서 한 번 수행한다.

## 4. 공통 변경과 충돌

- 2절 밖의 코드·공통 아트·Resources·씬·프리팹·설정·패키지·공통 검사·도구·문서는 공통 파일이다. 특히 `Core`의 공격 ID·진행 규칙, `KarmaEnemy*`·`KarmaBossBehavior`의 전투 계약, 공통 전투·플레이어·연출 코드 변경은 영향받는 보스를 함께 검증한다.
- 가능하면 보스별 Behavior override·패턴·설정·연출에서 해결한다. 합의된 공통 변경은 한 명이 맡아 작은 별도 PR로 분리한다. 씬·공용 에셋·설정은 동시에 편집하지 않고 무관한 자동 저장·임포트 변경을 섞지 않는다.
- 충돌 시 양쪽 의도를 확인한다. `ours`/`theirs`로 파일 전체를 덮어쓰거나 타인 브랜치를 강제 push하지 않는다.

## 5. 문서

- 정책·작업 절차는 이 문서 한 곳에만 쓴다. `AGENTS.md`는 진입 링크만 유지하고 규칙 본문을 다른 AI 파일에 복사하지 않는다.
- `STRUCTURE.MD`는 현재 파일·폴더 트리만 적고 구조가 바뀔 때만 갱신한다. `.meta`·Git/IDE/Unity 캐시·검증 생성물·작업 방법·이력은 넣지 않는다.
- 이전 구조 문서·중복 규칙 문서를 남기지 않는다. 이름 변경 시 관련 링크도 수정한다.

## 6. 완료 전 자동 정리

게임 실행·개발·빌드·검증·협업에 필요한 파일만 유지한다. 무관한 기능·실험·샘플·미사용 의존성이나 막연한 예비 파일을 추가하지 않는다. 완료 보고·커밋·PR 전에 아래를 한 번 수행하며, 자신의 임시 생성물과 이번 작업으로 대체한 미사용 파일 정리는 재승인받지 않는다.

1. 대체·삭제 후보의 코드·GUID·문자열 경로·Resources·shader include·씬·프리팹·도구 참조를 확인한다. 참조를 이전하고 관련 검증을 통과한 뒤 이미지 등 이전 파일과 `.meta`, 빈 폴더·폴더 `.meta`를 함께 정리한다. 이름이나 GUID 검색 하나만으로 삭제하지 않는다.
2. 자신의 임시 스크립트·백업·성공 검사 로그/캡처·Python 캐시·OS 파일을 사용 프로세스 종료 후 삭제한다. 필요한 검사 요약은 먼저 기록한다. 실패·미해결 진단 자료는 해결·재검증까지 보존한다.
3. 타인의 변경·생성물, 용도가 불분명한 파일, 현재 구현·유효한 검사·실제 차이가 있는 전용 에셋·요청된 개발 중 기능은 보존한다. Git 이력·개인 설정·사용 중인 Unity 캐시를 지우거나 `git clean -fdx` 같은 일괄 삭제를 하지 않는다. 버전 백업은 Git 이력으로 관리한다.
4. `git status --short`, `git diff --check`, 잔여 참조·고아 `.meta`·구조/링크를 확인하고 삭제 내역·보존 후보와 이유를 짧게 보고한다.

생성물은 `.gitignore`에 등록하고 임시 파일은 Assets 밖에 둔다. `Library/`·`Temp/`·`Logs/`·`UserSettings/`·`output/`·IDE 설정·생성 솔루션/프로젝트·캐시·비밀정보는 커밋하지 않는다. 이 정리는 작업 종료 절차이며 상시 감시기·예약 작업·Git hook을 설치하는 지시가 아니다.

## 7. 검증과 PR

- Unity는 `ProjectSettings/ProjectVersion.txt` 버전을 사용하고 무관한 Editor·패키지 업그레이드를 하지 않는다.
- 변경과 관련된 검사만 실행한다. 보스 전투는 발동·명중·회복·패링·중단·사망 중 영향받는 동작, 공통 변경·통합은 영향받는 보스의 연결·회귀를 확인한다. 문서만 바뀌면 경로·링크를 확인한다. 새 변경·실패·미해결 우려 없이 통과한 검사를 반복하지 않는다. 미실행·실패 검증을 통과로 보고하지 않는다.
- 실행기: `python3 tools/run-boss-playchecks.py`, 개별 선택: `--suite isolation|guard|sw05`. 현재 검사는 보스 연결·방어형·SW05이며 상승형 전투 전체를 보장하지 않는다. 결과는 `output/validation/`에 생성되고 보존·삭제는 6절을 따른다.
- 커밋은 자기 작업 파일만 선택한다. PR에는 변경 내용·검증 결과·공통 변경 여부를 적는다.
- 최신 `feat/2d-prototype`에서 개인 브랜치를 만들어 push하고 대상도 `feat/2d-prototype`으로 지정한다. 통합 브랜치에 직접 push하지 않는다. 정민 확인 후 **Create a merge commit**으로 병합한다.
- 병합 후 자신의 작업을 먼저 커밋하고 최신 `origin/feat/2d-prototype`을 작업 브랜치에 merge한다.
