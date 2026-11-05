# K4RMA

고유화 순서에 따라 보스의 유형과 계승 패턴이 달라지는 2D 보스러시.

Unity Hub에서 **Unity 6000.6.2f1**로 저장소를 열고 `Assets/K4RMA/Scenes/KarmaPrototype.unity`를 실행한다. Play 후 Enter로 시작한다.

- 이동: A/D 또는 ←/→ · 점프: Space · 검 공격: J
- 원형 기술: Q 검기 / 왼쪽 Shift 상승베기 / E 방어
- 공중 방향+J: 공중 베기. 아래 베기 명중 시 짧은 체공과 작은 반동.
- 고유화 후: U + 방향키로 관통베기 / 공중 Space로 더블점프 / K로 반격
- 고유화 선택: 1/2/3 → Enter · 취소/일시정지: Esc · 재도전: R · 타이틀: T

[파일 구조](docs/STRUCTURE.MD) · [협업 규칙](docs/RULES.MD)

보스별 연습: Unity 메뉴 `K4RMA > 보스 연습 > 상승베기형 / 검기형 / 방어형`에서 전체 전투, 개별 패턴 반복, 전체 순회 검사를 실행합니다.

타이틀·일시정지 화면의 환경설정(O)에서 키와 볼륨·화면 흔들림을 변경합니다. Esc는 키 변경 취소 및 메뉴 닫기입니다.

패턴 가중치는 아래 전용 에셋을 선택해 Inspector에서 변경합니다.

| 보스 | 가중치 파일 |
|---|---|
| 상승베기 | [KarmaRisingPatternWeights.asset](Assets/K4RMA/Runtime/Enemies/Bosses/RisingSlash/Settings/Resources/KarmaRisingPatternWeights.asset) |
| 검기 | [KarmaSwordPatternWeights.asset](Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/Resources/KarmaSwordPatternWeights.asset) |
| 방어 | [KarmaGuardPatternWeights.asset](Assets/K4RMA/Runtime/Enemies/Bosses/Guard/Settings/Resources/KarmaGuardPatternWeights.asset) |

발동 가능한 후보의 가중치 합을 기준으로 추첨하며 0은 해당 후보를 제외합니다. 예: 상황 보정 후 가중치가 1과 3이면 후보 둘이 모두 가능할 때 약 25%와 75%입니다. 공중·근접 보정과 연속 사용 규칙도 같은 파일에서 조정하며, 체력 문턱 필수 패턴은 유지됩니다. 변경 후 Play Mode를 다시 시작하세요.
