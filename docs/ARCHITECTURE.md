# 모듈 구조와 확장

## 의존 방향

`Core ← Runtime ← Editor`

- Core: 일반 C# 타입, 무의식화 상태, 패턴 해금·선택 규칙, 1회 접촉·결계 집계. 기존 사용자 수정 화상 시계 파일은 보존.
- Runtime: Unity 입력·씬·물리·시간·효과. Core의 결과를 사용해 전투 실행.
- Editor: 씬과 설정 생성, 생산 코드 검사.

Core에는 `K4RMA.Core.asmdef`를 두고 `noEngineReferences: true` 적용. Runtime과 Editor는 기존 Unity 기본 어셈블리를 사용한다. 현재 코드의 선택적 Input System 참조와 기존 컴포넌트 어셈블리를 유지하기 위한 결정이다. Core는 Runtime이나 UnityEngine을 참조하지 않는다.

## 컴포넌트 구성

| 컴포넌트 | 역할별 분할 |
|---|---|
| KarmaGame | 초기화·입력 진행 / Progression / Camera |
| KarmaPlayer | 초기화·입력 / Movement / Combat / Status |
| KarmaEssences | 진행 상태 연결·쿨타임 / ActiveSkills / PassiveEffects |
| KarmaEnemy | 초기화·공유 상태 / StateMachine / Patterns / Combat / Presentation / 개별 패턴 |
| KarmaHUD | 스타일·분기·공용 그리기 / Combat / Screens |

`partial`은 컴포넌트 사이의 독립 플러그인 인터페이스가 아니다. 큰 클래스를 안전하게 역할별 파일로 나누면서 Unity 참조와 기존 동작을 보존한다. 공유 상태를 변경할 때 같은 클래스의 다른 분할 파일에도 영향이 있음을 확인한다.

## 무의식화 데이터

- `KarmaProgressionState`: 무의식화 여부와 순서만 저장. 중복 선택 거절, 외부에서 변경할 수 없는 순서 뷰 제공.
- `KarmaEssences`: Core를 소유하는 Unity 컴포넌트. 액티브 쿨타임·검 적중 횟수·VFX·`Sacrificed` 이벤트 처리.
- `ResetTemporaryState()`: 쿨타임·발동 횟수만 초기화. 재도전 시 무의식화 순서 유지.
- 기존 API `Sacrifice`, `SacrificeOrder`, `HasActive`, `HasRemnant` 유지. 화면 용어는 무의식화.

## 보스 패턴 실행 구조

- `KarmaPatternCatalog`: 첫 계승 타입별 패턴 집합과 선택 제한. GD01은 공격 순환에 넣지 않는 독립 자동방어 기능이다.
- `KarmaEnemy.StateMachine`: 접근·패턴 코루틴·회복·패링 중단·워치독. 공격을 끝낸 뒤 회복 시간 동안 다음 행동을 선택하지 않는다.
- `KarmaEnemy.Patterns`: 패턴 ID와 구체 코루틴의 연결.
- `Patterns/KarmaEnemy.SwordWave`, `RisingSlash`, `Ward`: SW/UP/GD 각각의 전조·고정·공격·후속 효과.
- `KarmaBossAttack`: 보스 소유의 유한 수명 공격. 표시 크기를 실제 swept AABB 판정에 그대로 사용한다.
- `KarmaAttackContact`: 같은 공격/분열 스킬의 접촉을 한 번만 소비한다.
- `KarmaTrialState`: GD05 공격 완료 수·실제 피격 수·최종 결과. Runtime의 공격 접촉 처리에서 실제 HP 감소가 확인될 때만 피격 수를 올린다.
- `KarmaAttackWarning`: 이동 공격 양 끝 박스의 볼록 껍질을 전조로 그린다.

모든 패턴 오브젝트는 보스별 `effectsRoot`의 자식이다. 종료/중단 시 즉시 비활성화하고 제거하며, `StopAllCoroutines`와 함께 결계 제한도 해제한다. 사망·방 교체·타이틀 복귀는 `CancelCombat`에 연결된다. 단순 일시정지는 파괴 없이 시간을 정지한다.

새 패턴은 enum의 새 번호, 카탈로그 해금/선택/라벨, 코루틴, 실행 분기, 중단/회복 검사 순으로 추가한다. 자세한 기존 구현과 초기 정책은 [BOSS_PATTERNS](BOSS_PATTERNS.md)를 참조한다.

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
