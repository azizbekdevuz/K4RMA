# 모듈 구조와 확장

## 의존 방향

`Core ← Runtime ← Editor`

- Core: 일반 C# 타입, 무의식화 상태, 패턴 해금 규칙, 화상 시계.
- Runtime: Unity 입력·씬·물리·시간·효과. Core의 결과를 사용해 전투 실행.
- Editor: 씬과 설정 생성, 생산 코드 검사.

Core에는 `K4RMA.Core.asmdef`를 두고 `noEngineReferences: true` 적용. Runtime과 Editor는 기존 Unity 기본 어셈블리를 사용한다. 현재 코드의 선택적 Input System 참조와 기존 컴포넌트 어셈블리를 유지하기 위한 결정이다. Core는 Runtime이나 UnityEngine을 참조하지 않는다.

## 컴포넌트 구성

| 컴포넌트 | 역할별 분할 |
|---|---|
| KarmaGame | 초기화·입력 진행 / Progression / Camera |
| KarmaPlayer | 초기화·입력 / Movement / Combat / Status |
| KarmaEssences | 진행 상태 연결·쿨타임 / ActiveSkills / PassiveEffects |
| KarmaEnemy | 초기화·공유 상태 / StateMachine / Patterns / Combat / Status / Presentation / 개별 패턴 |
| KarmaHUD | 스타일·분기·공용 그리기 / Combat / Screens |

`partial`은 컴포넌트 사이의 독립 플러그인 인터페이스가 아니다. 큰 클래스를 안전하게 역할별 파일로 나누면서 Unity 참조와 기존 동작을 보존한다. 공유 상태를 변경할 때 같은 클래스의 다른 분할 파일에도 영향이 있음을 확인한다.

## 무의식화 데이터

- `KarmaProgressionState`: 무의식화 여부와 순서만 저장. 중복 선택 거절, 외부에서 변경할 수 없는 순서 뷰 제공.
- `KarmaEssences`: Core를 소유하는 Unity 컴포넌트. 액티브 쿨타임·검 적중 횟수·VFX·`Sacrificed` 이벤트 처리.
- `ResetTemporaryState()`: 쿨타임·발동 횟수만 초기화. 재도전 시 무의식화 순서 유지.
- 기존 API `Sacrifice`, `SacrificeOrder`, `HasActive`, `HasRemnant` 유지. 화면 용어는 무의식화.

## 보스 패턴 추가

1. `Core/KarmaTypes.cs`의 `EnemyAttack`에 항목 추가. 기존 enum 값 순서는 유지.
2. `Core/KarmaPatternCatalog.cs`에서 해금 조건·순환 순서·한국어 라벨 정의.
3. `Enemies/Patterns/KarmaEnemy.새패턴.cs`에 같은 partial 클래스의 메서드 추가.
4. `KarmaEnemy.Patterns.cs`의 ExecuteAttack에서 새 메서드 호출.
5. 지속 상태가 필요하면 `ActionState` 및 `StateMachine`의 상태 처리 추가.
6. 취소·사망·재도전·일시정지 시 정리가 되는지 확인.

새 패턴이 독립적으로 성장하면 인터페이스와 별도 컨트롤러로 추가 분리할 수 있다. 현재는 공유 상태를 갑자기 바꾸는 전면 재작성보다 기존 구현 유지에 초점을 둔다.

## 그래픽 교체

- `KarmaVisuals.Character`: 플레이어·보스 기본 도형 생성.
- `KarmaRemnantVFX`: 패시브 발동·투사체 모양·관통 궤적.
- `KarmaEnemy.Presentation`: 예고·보호막·회복 표시.
- `KarmaHUD`: IMGUI. Canvas/TMP로 바꾸려면 Game과 Essence의 공개 상태를 읽는 새 UI 구현으로 교체.

당장은 전투 판정을 바꾸지 않고 위 표시 메서드의 구현을 교체할 수 있다. 새 Sprite/Prefab을 사용하면 설정 참조와 씬 연결을 함께 커밋한다.

## 수치 변경

씬의 `KarmaGame.config`에 연결된 에셋이 기준이다. Inspector에서 체력·검 공격력·보스 난이도를 수정하고 저장한다. C# 초기값은 새 설정 에셋의 기본값일 뿐 이미 저장한 설정을 덮어쓰지 않는다. `Config`와 `BossTuning` 필드명 변경 시 데이터 마이그레이션이 필요하므로 단순 라벨 변경과 구분한다.

## 참고

- Unity 어셈블리 정의: https://docs.unity.com/en-us/engine/6000.6/manual/scripting/compilation-and-code-reload/script-compilation/assembly-definition-files/file-format
- Unity Auto Referenced: https://docs.unity.com/en-us/engine/6000.6/manual/scripting/compilation-and-code-reload/script-compilation/assembly-definition-files/assembly-definitions-referencing
