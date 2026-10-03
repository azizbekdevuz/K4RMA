<div align="center">
  <img src="assets/wordmark.png" alt="K4RMA — What you give up comes back" width="470" />
  <br /><strong>WHAT YOU GIVE UP COMES BACK.</strong>
  <br /><sub>잃어버린 능력이 다음 적의 힘이 된다.</sub>
  <br /><br />
  <img alt="Project status: pre-production" src="https://img.shields.io/badge/status-pre--production-ef4444?style=flat-square" />
  <img alt="Engine: Unity" src="https://img.shields.io/badge/engine-Unity-222222?style=flat-square&logo=unity" />
  <img alt="Language: C sharp (planned)" src="https://img.shields.io/badge/language-C%23_(planned)-6b4eff?style=flat-square" />
  <img alt="Mode: single player first" src="https://img.shields.io/badge/mode-single--player_first-2563eb?style=flat-square" />
  <br /><br />
  <a href="README.md"><strong>🇰🇷 한국어</strong></a> &nbsp;|&nbsp; <a href="README.en.md"><strong>🇺🇸 English</strong></a>
  <br /><sub>GitHub README: 언어별 문서 이동 · 같은 화면에서 즉시 전환: <a href="https://azizbekdevuz.github.io/K4RMA/">GitHub Pages</a></sub>
</div>

<div align="center">
  <img src="assets/concept-arena.png" alt="Illustrative concept art: a player facing a towering boss in a fantasy arena" width="820" />
  <br /><sub>Concept art / 콘셉트 이미지 · Not actual gameplay / 실제 게임 화면이 아닙니다.</sub>
</div>

> [!IMPORTANT]
> **K4RMA는 개발 준비 단계의 가제입니다.** 게임의 핵심 루프는 팀이 논의한 방향이지만 능력/스테이지 수, 시너지 규칙, 2D/3D 및 최종 그래픽은 아직 결정되지 않았습니다. 계획된 기능을 이미 구현된 것처럼 표시하지 않습니다.

## 게임 소개

**K4RMA**는 플레이어가 보스를 이길 때마다 자신의 능력 하나를 희생하고, 그 능력이 이후의 적에게 넘어가는 **싱글플레이 우선 액션·보스 전투 게임**입니다. 스테이지가 진행될수록 내 선택지가 줄어들고 적의 공격 패턴이 변화합니다. 남은 기술, 기본 공격, 회피 및 성장 요소를 활용해 마지막까지 살아남는 것이 핵심입니다.

**핵심 질문:** *다음 적이 사용할 기술을 알고도, 어떤 능력을 포기할 것인가?*

### 핵심 게임 루프

<div align="center"><img src="assets/game-loop.svg" alt="Game loop: select abilities, fight, earn score, sacrifice an ability, strengthen a later enemy, repeat" width="850" /></div>

1. **시작:** 기본 스탯(HP·공격력·이동 속도·공격 속도)과 여러 특수 능력을 가진 캐릭터로 시작합니다. Dash, Fireball, Shield, Heal은 **예시**이며 최종 구성은 미정입니다.
2. **전투:** 기본 공격과 남은 능력으로 적/보스와 싸웁니다. 적은 이동·추적·공격 등의 기본 AI를 사용하며, 이후 단계에서는 희생된 능력도 사용할 수 있습니다.
3. **평가:** 처치 수, 클리어 시간, 남은 HP, 피격 횟수 등의 플레이 결과를 점수에 반영하는 방안을 검토합니다. 점수를 기본 스탯 성장에 사용하는 방식도 논의 중입니다.
4. **희생:** 스테이지 클리어 후 보유 능력 중 하나를 선택해 포기합니다. 플레이어는 그 능력을 잃고, 능력은 이후 적이 사용할 수 있는 풀에 추가됩니다.
5. **반복:** 다음 전투에서는 이전 선택의 결과를 마주합니다. 마지막 단계까지의 난이도와 승리 가능성은 플레이 테스트로 조정합니다.

### 기획 범위와 결정 상태

| 구분 | 현재 방향 |
| :--- | :--- |
| 핵심 차별점 | **확정된 방향:** 능력 희생 → 이후 적/보스가 해당 능력을 활용 |
| 게임 방식 | **싱글플레이를 먼저 완성**하는 조작 중심 전투 |
| 엔진 | **Unity**. 구현 언어는 Unity용 C#을 예상 |
| 시점·그래픽 | 3인칭 3D 가능성을 먼저 조사하고, 범위·성능·제작 난도가 맞지 않으면 2D로 전환 |
| 콘텐츠 수량 | 시작 능력 수, 전체 능력 종류, 보스/스테이지 수 **미정** |
| 추가 메커니즘 | 능력 조합 시너지·점수 기반 스탯 강화 **검토 중** |
| 멀티플레이 | **현재 개발 범위 밖**. 시간이 남을 경우를 위해 확장 여지만 고려 |

### 첫 번째 플레이 가능한 버전

우선 **캐릭터 1명 → 전투 공간 1개 → 기본 공격과 소수의 능력 → 보스 1종 → 클리어 후 능력 희생 → 다음 전투에서 적이 그 능력 사용**까지 구현해 핵심 재미를 검증합니다. 멀티플레이, 많은 스테이지, 고급 모델링과 복잡한 물리 효과는 이 단계의 필수 조건이 아닙니다.

### 개발 원칙

- **간단하게 시작하되 확장을 막지 않기.** 멀티플레이 코드를 미리 만들자는 뜻이 아닙니다. 플레이어·능력·적·점수·UI를 한 곳에 뒤섞지 않고, 나중에 플레이어가 추가되어도 가능한 한 재사용할 수 있게 설계합니다.
- **역할별 동일한 원칙.** 코드는 입력과 캐릭터/전투 동작을 분리하고, UI는 플레이어 수나 상태 표시가 늘어날 수 있게 구성하며, 모델·애니메이션은 특정 캐릭터 하나에만 고정하지 않고 재사용성을 고려합니다. 미래 기능 때문에 현재 MVP를 불필요하게 복잡하게 만들지는 않습니다.
- **AI는 공통 맥락을 보고 사용.** AI에 작업을 맡기기 전 [`docs/PROJECT_CONTEXT.md`](docs/PROJECT_CONTEXT.md), 현재 코드, Unity 버전, 본인 작업 범위 및 관련 인터페이스를 함께 전달합니다. 질문할 때는 원하는 동작·현재 구조·시도한 방법·오류 내용·제약 조건을 구체적으로 설명합니다. 생성된 코드는 직접 이해하고 실행·검증합니다.
- **작고 검증 가능한 단위로 개발.** 핵심 동작을 먼저 확인한 뒤 기능을 추가합니다. 변경 사항은 기록하고 서로 다른 파트를 일찍 통합합니다.

### 협업 및 저장소

- **GitHub:** 공동 코드 저장소 및 변경 이력 관리. Aziz가 저장소/협업 설정과 통합을 담당합니다. 역할별 개발 담당자는 추후 확정합니다.
- **Unity Version Control (구 Plastic SCM):** 대용량 바이너리 에셋·잠금이 필요한 경우를 포함해 적용 방식을 검토합니다. **Git과 Unity Version Control을 동시에 별도 정본으로 운영하지 않습니다.** 정본 저장소와 에셋 관리 방식은 팀이 합의해 문서화합니다.
- **Notion:** 기획안, 회의록, 작업 분담, 개발 일정과 결정 사항 공유를 위한 예정 공간입니다.
- **협업 가이드:** [`docs/CONTRIBUTING.md`](docs/CONTRIBUTING.md) · **결정 기록:** [`docs/DECISIONS.md`](docs/DECISIONS.md)

### 개발 환경과 시작 방법

이 브랜치의 프로토타입은 `Game/`에 있습니다. 에디터는 **Unity 6000.6.2f1**입니다. 이것은 D-011의 작업용 선택이며, 팀 전체의 최종 버전 투표는 아직 열려 있습니다.

1. Unity `6000.6.2f1`과 Windows Build Support(Mono) 모듈을 설치합니다.
2. `Game` 폴더를 프로젝트로 엽니다.
3. `Assets/_Project/Scenes/PrototypeArena.unity`를 열고 Play를 누릅니다.

조작: A/D 또는 화살표 이동, Space 점프, J 기본 공격, K 프로토타입 투사체(희생 후에는 디플렉트), E 제단, 패배 또는 클리어 후 R 재시작.

배치 컴파일, EditMode 테스트, Windows 플레이어 경로는 [`docs/PROTOTYPE_STATUS.md`](docs/PROTOTYPE_STATUS.md)에 있습니다. `Game/Builds/`는 커밋하지 않습니다. 능력 이름과 2D/2.5D 최종 결정은 아직 확정이 아닙니다.

### 로드맵

| 단계 | 목표 | 상태 |
| :--- | :--- | :--- |
| 기획·검증 | 규칙 정리, 2D/3D 가능성 확인, 역할 분담 | 진행 중 |
| 핵심 프로토타입 | 희생 → 적 능력 승계 루프 검증 | `feat/2-5d-prototype`에서 진행 중 |
| 플레이 가능한 MVP | 입력·전투·적 AI·UI·스테이지 전환 통합 | 예정 |
| 콘텐츠·밸런스 | 능력/보스 확장, 점수·성장·시너지 여부 확정 | 예정 |
| 테스트·발표 | 버그 수정, 플레이 테스트, 시연/문서 정리 | 예정 |

### 팀 및 사용 안내

**SW설계기초 · 4인 팀 프로젝트.** 팀원 네 명의 한국 이름이 모두 ㄱ/K로 시작하고, `4`는 네 명을 의미합니다. 프로젝트 제목 **K4RMA는 팀의 최종 확인 전까지 가제**입니다.

팀 외부에 공유할 때는 내부 기획·코드·에셋을 팀의 동의 없이 공개하지 않습니다. 제3자 에셋을 사용할 경우 출처와 라이선스를 별도로 기록합니다. **저장소 전체의 공개 라이선스는 아직 결정되지 않았습니다.**
