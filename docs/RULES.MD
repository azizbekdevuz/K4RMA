# K4RMA 협업 규칙

이 문서는 팀원과 AI 도구가 공통으로 적용하는 작업 규칙이다. 작업 시작 전에 읽는다. 파일 배치는 `STRUCTURE.MD`에서 확인한다.

## 1. 작업 범위부터 확인한다

- 작업 요청과 브랜치에서 담당 보스를 확인하고, 아래의 자기 담당 경로만 수정한다. 단순히 파일이 보이거나 함께 고칠 수 있다는 이유로 다른 보스 코드를 수정하지 않는다.
- 요청과 기존 대화에서 담당과 범위가 분명하면 바로 진행한다. 담당이 실제로 불명확하거나, 승인된 범위를 벗어나는 변경이 필요하거나, 필수 정보가 없어 작업을 진행할 수 없을 때만 질문한다. 공통 파일 변경은 대상 파일, 변경 이유, 다른 보스에 미치는 영향을 먼저 공유하고 담당자와 조율한다.
- 현재 요청에서 공통 파일 변경이나 저장소 전체 정리를 명시적으로 승인했다면 그 범위 안에서 진행한다. 이미 승인된 작업과 그 범위 안의 필요한 구현·검증·정리는 다시 확인받지 않는다. 도구가 별도로 요구하는 권한 승인은 해당 절차를 따른다.
- 다른 사람의 미완료 변경을 덮어쓰거나 되돌리지 않는다. 작업 시작과 PR 작성 전에 `git status --short`, `git diff`로 변경 범위를 확인한다.

## 2. 보스별 수정 허용 경로

아래 경로는 저장소 루트 기준이다. 폴더 전체를 자신의 담당 범위로 사용하며 새 파일도 해당 폴더에 만든다.

| 담당 | 코드·패턴·설정·이미지 연결 | 이미지·이펙트·재질·셰이더 | 담당 검사 |
|---|---|---|---|
| 상승베기 · RisingSlash | `Assets/K4RMA/Runtime/Enemies/Bosses/RisingSlash/` | `Assets/K4RMA/Arts/Bosses/RisingSlash/` | 새 검사는 `tests/Unity/RisingSlash/` |
| 검기 · SwordWave | `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/` | `Assets/K4RMA/Arts/Bosses/SwordWave/` | `tests/Unity/KarmaSW05PlayChecks.cs`, 새 검사는 `tests/Unity/SwordWave/` |
| 방어 · Guard | `Assets/K4RMA/Runtime/Enemies/Bosses/Guard/` | `Assets/K4RMA/Arts/Bosses/Guard/` | `tests/Unity/KarmaGuardPlayChecks.cs`, 새 검사는 `tests/Unity/Guard/` |

- 이름보다 요청에서 지정한 담당 보스와 경로를 우선한다. 새 보스별 검사 폴더는 필요할 때 만든다.
- 보스별 추가 도구가 필요하면 `tools/<보스명>/`에 둔다. 공통 검사 실행기는 각자가 수정하지 않는다.
- `Behavior`는 공격 목록(`BuildCycle`), 선택 조건(`Eligible`), 이동(`TickMovement`), 공격 주기와 보스별 연결을 담당한다. 검기형의 접근·후퇴는 `KarmaSwordBossMovement.cs`도 확인한다.
- 개별 공격은 `Patterns/`, 전용 연출은 `Presentation/`에서 수정한다. 방어형 기본 공격은 `Patterns/GDBasic.cs`다.
- 보스 폴더 안의 `partial KarmaEnemy`도 공통 상태에 접근할 수 있다. 폴더 분리는 다른 보스에 영향이 없음을 자동으로 보장하지 않는다. 공통 상태·함수의 의미를 바꾸지 않는다.

## 3. 수치와 이미지 연결도 담당 파일에서 한다

### 보스별 수치

| 보스 | 저장된 수치를 수정할 에셋 |
|---|---|
| RisingSlash | `Assets/K4RMA/Runtime/Enemies/Bosses/RisingSlash/Settings/Resources/KarmaRisingBossProfile.asset` |
| SwordWave | `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/Resources/KarmaSwordBossProfile.asset` |
| Guard | `Assets/K4RMA/Runtime/Enemies/Bosses/Guard/Settings/Resources/KarmaGuardBossProfile.asset` |

- Unity Inspector에서 해당 Profile의 `settings`를 수정한다. `Karma…BossSettings.cs`는 항목과 초기값을 정의하며, C# 초기값만 바꿔도 이미 저장된 에셋 값이 자동 변경되지는 않는다.
- 보스 하나의 피해·이동·판단·회복 배율은 해당 Profile의 `damageScale`, `movementScale`, `decisionScale`, `recoveryScale`을 사용한다.
- 런타임은 설정 복사본을 사용하므로 Profile 수정 후 Play Mode를 다시 시작해 확인한다.

### 이미지·이펙트

1. 이미지는 담당 `Arts/Bosses/<보스명>/Effects/` 또는 `Characters/`에 추가하고, Unity에서 Sprite 임포트·슬라이스를 설정한다. 재질·셰이더도 같은 보스 아트 폴더에 둔다.
2. 담당 코드 폴더의 `Presentation/Resources/BossArtwork/Karma…BossSprites.asset`에 Sprite와 논리 이름(`poses`)을 연결한다. 같은 인덱스의 Sprite와 pose가 한 쌍이다.
3. 효과 그룹은 각각 `RisingBossVFX`, `SwordBossVFX`, `GuardBossVFX`를 사용한다. 코드에서는 `KarmaArtwork.Sprite("그룹", "이름")` 또는 `KarmaCombatVFX.SpawnSprite(...)`로 사용한다.
4. 기본 외형은 해당 라이브러리의 `BossAppearance`, 기절 표시는 `BossStun`, 동작 시트는 `Rise`·`Wave`·`Ward`를 확인한다. 계승·부활 이미지는 기존 Resources 상대 경로를 유지한다.
5. 공통 라이브러리를 수정해서 전용 이미지를 등록하지 않는다. 다른 보스나 공용 그룹과 같은 그룹/이름 조합을 등록하지 않는다. 라이브러리는 자동으로 검색된다.

- 보스별 외형 참조 atlas는 독립 복사본이다. 다른 보스의 이미지·재질을 직접 수정해 자신의 보스에 적용하지 않는다.
- 모든 아트는 `Assets/K4RMA/Arts/` 아래에 둔다. 보스 전용은 `Bosses/<보스명>/`, 공용은 `Common/`을 사용하고 `Art/` 폴더를 다시 만들지 않는다.
- 플레이어·기본형·배경·UI·공용 효과는 `Arts/Common/`의 공통 아트다. 이 파일들을 바꾸려면 아래 공통 변경 규칙을 따른다.
- 에셋 이동·이름 변경 시 `.meta`도 함께 이동해 GUID와 Sprite ID를 보존한다. 복사한 새 에셋은 별도 GUID를 사용한다. 기존 `.meta`를 재생성해서 참조를 끊지 않는다.
- Resources 상대 경로, shader의 include 경로, 라이브러리 연결을 변경하면 실제 로딩까지 확인한다. GUID 참조 검색 결과가 없다는 이유만으로 Resources 에셋을 삭제하지 않는다.

## 4. 공통 파일은 충돌과 동작 영향을 먼저 고려한다

아래 경로와 담당 허용 경로 밖의 파일은 기본적으로 공통 변경이다.

| 공통 대상 | 영향·충돌을 고려할 내용 |
|---|---|
| `Assets/K4RMA/Runtime/Enemies/KarmaEnemy*.cs`, `KarmaBossBehavior.cs`, `Enemies/Patterns/` | 전투 상태, 공격 시작·종료, 피격, 중단, 기본형과 보스 공통 계약 |
| `Assets/K4RMA/Core/` | `KarmaTypes.cs`의 `EnemyAttack` ID, 기술·고유화·진행·공통 판정 규칙. 새 공격 ID 추가도 공통 변경 |
| `Assets/K4RMA/Runtime/Combat/` | 공통 공격 객체, 플레이어 투사체, 충돌·명중 처리 |
| `Assets/K4RMA/Runtime/Configuration/`, `Assets/K4RMA/Settings/KarmaConfig.asset` | 게임·기술·모든 보스의 공통 기준값과 설정 형식 |
| `Assets/K4RMA/Runtime/Player/`, `Abilities/`, `Flow/`, `Input/`, `UI/`, `World/` | 플레이어 전투, 계승, 게임 진행, 입력, UI, 지형·공간 기준 |
| `Assets/K4RMA/Runtime/Presentation/` | 공통 애니메이션, 이미지 조회, 효과·렌더링 도구 |
| `Assets/K4RMA/Resources/`, `Assets/K4RMA/Arts/Common/` | 공통 이미지 연결·재질·아트. 특히 `KarmaSpriteLibrary.asset`, `KarmaArtworkSettings.asset` |
| `Assets/K4RMA/Scenes/KarmaPrototype.unity` 및 다른 씬·공유 프리팹 | 다 같이 쓰는 직렬화 데이터. 동시에 편집하지 않는다 |
| `Assets/K4RMA/Settings/Rendering/`, `ProjectSettings/`, `Packages/` | 렌더링·물리·입력·Unity 버전·패키지 및 잠금 파일 |
| `tests/Unity/KarmaBossIsolationChecks.cs`, `tools/run-boss-playchecks.py` | 보스 간 연결 검사와 공통 검사 실행기 |
| `AGENTS.md`, `docs/`, `README.md`, `.gitignore`, `.asmdef`, 상위 폴더 `.meta` | 팀 전체 규칙·구조·빌드 범위. 여러 담당자의 동시 수정으로 충돌 가능 |

- 공통 변경은 수정할 파일과 범위를 먼저 공유하고 한 명이 맡는다. 가능하면 보스별 Behavior의 override, 전용 패턴·설정·연출로 해결한다.
- 팀이 합의한 공통 변경은 별도 PR로 작게 분리하고, 관련 보스 담당자가 영향 범위를 확인한다.
- 씬·공용 에셋·설정 파일은 동시에 편집하지 않는다. 자동 저장·Unity 임포트가 만든 관련 없는 변경은 PR에 섞지 않는다.
- 텍스트 충돌이 없더라도 런타임 동작은 깨질 수 있다. 자동 병합 성공을 검증 통과로 간주하지 않는다.
- 충돌 시 `ours`/`theirs`로 파일 전체를 덮어쓰지 않는다. 양쪽 변경의 의도를 확인하고 담당자와 해결한다.

## 5. 문서 규칙

- 구조 트리에서는 `.meta`와 Git·IDE·Unity 캐시·검증 생성물을 생략한다. 에셋의 `.meta` 자체는 반드시 함께 관리한다.
- `docs/STRUCTURE.MD`에는 현재 파일·폴더 구조만 적는다. 작업 규칙, 사용법, 변경 이력, 계획, 검증 결과를 넣지 않는다.
- 협업 규칙·허용 경로·공통 변경 주의사항·정리 기준·검증 방법은 이 `docs/RULES.MD`에 적는다.
- AI 도구의 진입점은 루트 `AGENTS.md`다. 규칙 본문을 여러 AI 전용 파일에 복사해서 서로 다른 버전을 만들지 않는다.
- 구조가 실제로 달라질 때만 `STRUCTURE.MD`를 갱신한다. 여러 보스 담당자가 문서와 상위 폴더 파일을 동시에 수정하지 않도록 구조 변경 PR에서 담당자가 정리한다.
- 이전 구조 문서와 중복 규칙 문서를 남기지 않는다. 이름을 바꾸면 README와 다른 참조 링크도 함께 바꾼다.

## 6. 임시·구버전·중복 파일을 남기지 않는다

### 게임에 필요한 부분만 유지한다

- 저장소에는 현재 게임의 실행·빌드·개발·검증·협업에 필요한 코드, 에셋, 설정, 의존성, 도구, 문서만 유지한다. 게임에 필요한 테스트·검사 실행기·협업 규칙·구조 문서도 유지 대상이다.
- 요청과 무관한 기능, 사용하지 않는 실험·샘플·데모, 불필요한 범용 도구·추상화·의존성을 추가하지 않는다. 막연히 나중에 쓸 수 있다는 이유로 예비 코드·에셋·파일을 남기지 않는다.
- 현재 요청의 구현·검증에 필요한 작업 파일은 사용할 수 있지만, 최종 결과에 필요하지 않으면 작업 종료 시 아래 자동 정리 절차에 따라 삭제한다. 최종 구현으로 대체되어 더 이상 필요하지 않은 기존 파일도 담당 범위와 참조를 확인한 뒤 정리한다.
- 실행 중 직접 참조되지 않는다는 이유만으로 개발·빌드·검증·협업용 파일을 삭제하지 않는다. 사용 여부가 불명확하거나 다른 사람의 담당 파일이면 기존 보존·공통 변경 규칙을 적용한다. 사용자가 요청한 개발 중 기능과 실패·미해결 문제의 진단 자료도 보존한다.

### 작업 완료 전 자동 정리 의무

- AI 도구는 작업 완료 보고·커밋·PR 전에 아래 정리를 직접 수행한다. 사용자에게 정리를 제안하는 것으로 끝내거나 다음 작업으로 미루지 않는다.
- 작업 시작 시 변경 목록과 기존 생성물을 확인해 자기 작업으로 생긴 파일을 구분한다. 자동 삭제 범위는 현재 작업에서 생성한 임시 산출물과, 담당 범위 안에서 이번 구현으로 대체했고 사용되지 않음을 확인한 이전 파일이다. 이 범위의 정리는 별도 사용자 요청 없이 수행한다.

1. 자신의 임시 스크립트·백업·로그·검증 스크린샷·Python 캐시·OS 파일을 찾는다. 성공한 검증 결과는 필요한 요약을 먼저 기록하고, 사용 중인 프로세스가 종료된 뒤 불필요한 생성물을 삭제한다. 실패·미해결 문제의 로그·오류 보고서·재현 스크린샷은 해결될 때까지 보존하고, 해결·관련 검증 성공 후 필요한 내용을 기록한 뒤 정리한다.
2. 대체된 구버전·중복 파일의 코드, 문자열 경로, GUID, Resources, shader include, 씬·프리팹·도구 참조를 확인한다. 참조를 새 구현으로 옮기고 필요한 검증을 통과한 뒤 이전 파일을 삭제한다. 파일명 패턴만으로 자동 삭제하지 않는다.
3. 삭제한 Unity 에셋의 `.meta`와 이번 정리로 비게 된 폴더·폴더 `.meta`도 함께 정리한다. 현재 구현·필수 테스트·보스별 독립 에셋 복사본은 유지한다.
4. `git status --short`, `git diff --check`와 참조·고아 `.meta` 여부를 확인한다. 구조가 달라졌으면 파일 트리만 `docs/STRUCTURE.MD`에 반영한다.
5. 완료 보고에는 삭제한 파일 또는 종류, 확인 결과를 짧게 적는다. 삭제가 필요한지 불명확한 후보는 보존하고 경로와 이유를 보고한다.

- 다른 사람의 기존 변경·생성물, 담당 밖 파일, Git 이력, 개인 설정, 사용 중인 Unity 캐시는 자동 삭제 대상이 아니다. 실패·미해결 문제의 진단 자료와 사용자 요청으로 보존할 결과물도 삭제하지 않는다. 공통 폴더 전체 삭제나 `git clean -fdx` 같은 일괄 정리를 사용하지 않는다.
- 이 규칙은 AI가 작업 종료 시 수행하는 정리 절차다. 파일 감시기·예약 작업·Git hook이 상시 삭제하는 기능을 설치한다는 의미는 아니다.

### 삭제 판단과 보존 기준

- 최종 구현이 대체한 이전 코드·이미지·문서·검사 스크립트는 참조를 확인한 뒤 삭제한다. 버전 이력은 Git으로 관리하고 `_old`, `_backup`, `copy`, `final2`, 날짜별 복사본을 저장소에 남기지 않는다.
- 이전 파일이 Resources, 문자열 경로, GUID, shader include, 씬, 프리팹 또는 외부 도구에서 사용되는지 확인한다. 이름이 비슷하거나 GUID 참조가 없다는 이유만으로 지우지 않는다.
- 유효한 테스트·현행 에셋·의도적으로 분리한 보스별 이미지 복사본은 구버전이 아니다. 담당 밖 파일을 임의로 일괄 삭제하지 않는다.
- 임시 스크립트·성공한 검사의 불필요한 결과·스크린샷·로그·Python 캐시·OS 파일과 정리 후 빈 폴더를 제거한다. 실패·미해결 문제의 진단 자료는 보존한다. Unity 에셋을 삭제할 때 해당 `.meta`도 함께 삭제하며 고아 `.meta`를 남기지 않는다.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `output/`, IDE 설정, 생성된 솔루션·프로젝트, Python 캐시와 개인 비밀정보는 커밋하지 않는다. Unity 임포트 캐시인 `Library/`는 로컬 캐시로 유지할 수 있다. 개인 IDE·Editor 설정은 다른 사람의 동의 없이 초기화하지 않는다.
- 생성물은 `.gitignore`에 등록한다. 임시 산출물을 Assets에 넣어 Unity가 임포트하게 하지 않는다. 삭제 전 Unity나 검증 프로세스가 사용 중인지 확인한다.

## 7. 검증과 PR

- Unity는 `ProjectSettings/ProjectVersion.txt`의 버전을 사용한다. 작업과 무관한 Unity·패키지 업그레이드를 하지 않는다.
- 보스 변경은 해당 패턴의 발동·명중·회복·패링·중단·사망을 필요한 범위에서 확인한다. 이미지 이동은 Sprite·재질·셰이더와 실제 로딩을 확인한다.
- 기본적으로 변경과 직접 관련된 검사만 실행한다. 작은 보스 수정에 전체 검사를 일괄 실행하지 않는다. 같은 코드에서 관련 검사가 통과했다면 새 변경·실패·미해결 우려 없이 반복하지 않는다.
- 공통 변경·폴더 분리·통합 시에는 영향받는 보스의 연결·회귀 검사로 범위를 넓히고, 전체 보스에 영향이 있거나 통합 확인에 필요할 때 전체 검사를 실행한다. 문서·임시 산출물 정리만으로 런타임이 바뀌지 않으면 문서 링크·경로·참조 무결성을 확인한다.
- 공통 실행기: `python3 tools/run-boss-playchecks.py`. 개별 검사: `--suite isolation`, `--suite guard`, `--suite sw05`. 현재 제공되는 검사는 보스 연결과 방어형·SW05이며, 상승형 전투를 모두 검증한다는 의미는 아니다.
- 검사 결과는 `output/validation/`에 생성된다. 성공한 결과는 PR에 요약한 뒤 불필요한 로컬 생성물을 정리한다. 실패·미해결 문제의 로그와 재현 자료는 해결될 때까지 보존한다. 검사 소스와 실행기는 유지한다.
- PR 전 `git diff --check`와 변경 경로를 확인하고, 자기 작업 파일만 선택해서 커밋한다. 관련 없는 파일을 일괄 스테이징하지 않는다.
- 최신 `feat/2d-prototype`을 기준으로 개인 작업 브랜치를 만들고, 자기 브랜치에 push한 뒤 대상 브랜치를 `feat/2d-prototype`으로 지정해 PR을 생성한다. 통합 브랜치에 직접 push하지 않는다.
- 정민이 변경 내용·필요한 Unity 검증을 확인한 후 **Create a merge commit**으로 병합한다. PR에는 변경 내용, 검사 결과, 공통 파일 변경 여부를 적는다.
- 병합 후에는 자기 작업을 먼저 커밋하고 최신 `origin/feat/2d-prototype`을 작업 브랜치에 merge한다. 다른 사람의 브랜치를 강제 push하거나 이력을 덮어쓰지 않는다.
