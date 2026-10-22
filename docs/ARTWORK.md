# 이미지 교체 방법

## image 폴더 원화 연결 (2026-10-22)

`image/K4RMA_Production_Art_v1`과 `image/K4RMA_UI_Screens_v3`에서 실제 게임에 사용할 PNG 10개를
`Assets/K4RMA/Resources/K4RMAArtwork/Production/`에 복사해 연결했다. Unity는 `Assets` 밖의 원본을 빌드에 포함하지 않으므로 런타임은 복사본과 저장된 참조를 사용한다.

| 원본 | 연결 위치 |
| --- | --- |
| `01_Player/player-keyposes.png` | 대기·이동·베기·상승·하강·피격·사망의 9개 Sprite 슬롯 |
| `02_Guardians/guardian-neutral-keyposes.png` | 고유화 전 첫 시련의 기본 수호자 |
| `02_Guardians/guardian-{wave,rise,ward}-keyposes.png` | 첫 계승 원형에 따른 검기·상승·방어 수호자 및 고유화 완료 화면 |
| `05_Environments/mountain-dojo-composite.png` | 첫 시련의 실외 배경 |
| `05_Environments/guardian-hall-composite.png` | 2~4번째 시련의 실내 배경 |
| `UI/menu-background-clean.png` (v3) | 타이틀·스토리·조작 안내의 공통 배경 |
| `UI/10-ui-component-atlas.png` (v3) | HUD·고유화 선택/완료의 원형 3개·고유 기술 3개·선택 초승달 |
| `04_VFX/hit-vfx.png` | 검 공격 적중 시 피격 효과 |

원화는 연속 애니메이션이 아닌 핵심 포즈 시트다. 플레이어는 검토한 개별 사각형과 발 Pivot으로 분리하고,
수호자는 타입별 대기 포즈를 사용한다. 기존 동작 보간·공격 판정·이펙트 타이밍은 유지한다.
UI 목업 전체 PNG의 글자나 가짜 버튼을 입력 화면으로 사용하지 않는다. 실제 텍스트와 버튼은 기존 IMGUI에서 렌더링하며,
고유화 선택은 남은 원형 3→2→1개를 중앙 정렬한다. 설정·이어하기 등 기존에 없던 기능은 추가하지 않는다.
배경은 반복 타일이 아닌 합성 원화 한 장으로 경기장 전체를 덮는다. 레이어별 패럴랙스와 계승 부품 시트는 별도 제작 자료로 남긴다.

저장된 `.meta`와 `KarmaArtworkSettings.asset`이 포함되므로 정상 체크아웃에서는 추가 연결 작업이 필요 없다.
다시 적용하려면 **K4RMA → image 폴더 Production 원화 적용**을 실행한다. 이 명령은 Production PNG를 다시 자르고
위 슬롯을 덮어쓰므로, 직접 수정한 연결을 유지하려면 실행하지 않는다. 원본 PNG를 갱신할 때는 Production 복사본도 함께 갱신한다.

명령줄 재적용 및 참조 검증: `python3 tools/run-unity-artwork-checks.py --production`.
임시 Unity 프로젝트에서 임포트한 뒤 Production 메타데이터와 설정 에셋만 작업 저장소로 가져온다.

## 이미지 설정 열기

1. Unity의 Play 모드를 종료한다.
2. 상단 메뉴 **K4RMA → 이미지 설정 선택**을 누른다.
3. Inspector에서 교체할 슬롯에 Project 창의 그림을 드래그한다.
4. 프로젝트를 저장하고 Play를 다시 실행한다.

설정 파일은 `Assets/K4RMA/Resources/KarmaArtworkSettings.asset`이다.
이 설정 에셋의 이름과 Resources 안의 경로는 유지한다.
코드는 이 설정 에셋 하나를 불러온 뒤, Inspector에 저장된 Sprite/Texture 참조를 사용한다.
그림 파일 이름이나 위치를 검색해서 캐릭터에 연결하지 않는다.
기존 `KarmaArtwork.bytes` 이미지 묶음은 제거했다. 다시 만들 필요가 없다.
`기본 이미지 설정 생성 (최초 1회)` 메뉴는 최초 설정용이며, 이미 있는 설정이나 사용자 연결은 덮어쓰지 않는다.

## 배경이나 캐릭터 그림 한 장을 교체하기

1. 새 PNG를 Project 창의 원하는 `Assets` 하위 폴더에 넣는다. 새 그림은 `Resources` 안에 있을 필요가 없다.
2. PNG를 선택하고 Inspector의 **Texture Type → Sprite (2D and UI)**, **Sprite Mode → Single**을 선택한 뒤 **Apply**를 누른다.
3. `KarmaArtworkSettings`의 원하는 슬롯에 PNG의 Sprite를 넣는다.

| 설정 영역 | 슬롯 / 용도 |
| --- | --- |
| 플레이어 동작 | 대기, 이동 1/공중, 이동 2, 검 공격 1/2, 상승 공격, 하강 공격, 피격, 사망 |
| 수호자 외형 | 기본 수호자, 검 수호자, 상승 수호자, 결계 수호자 |
| 스테이지 | 실내 도장 배경, 실외 도장 배경 |
| UI 일러스트 | 타이틀/세계관, 플레이어 소개, 기술 설명, 수호자 소개, 계승 일러스트 |

플레이어 동작은 9개 슬롯을 모두 채운다. 동작 이미지가 아직 없다면 같은 Sprite를 여러 슬롯에 넣어도 된다.
캐릭터 PNG에는 투명 배경을 사용한다. 화면에서의 크기는 **Pixels Per Unit**과 그림의 투명 여백에 영향을 받는다.
기존 플레이어는 418픽셀짜리 프레임에 **Pixels Per Unit = 167.2**,
수호자는 724픽셀짜리 프레임에 **약 353.17**을 사용한다. 각각 프레임 높이가 약 2.5, 2.05 월드 단위다.
새 그림도 원하는 월드 높이로 표시하려면 `Pixels Per Unit = 프레임 픽셀 높이 / 원하는 월드 높이`로 맞춘다.
발 위치를 맞출 때는 Sprite의 Pivot을 조절한다. 기존 동작들은 각 프레임의 발 높이에 맞춘 커스텀 Pivot을 사용한다.
배경은 가로 50 월드 단위로 한 번 배치되며 그림 비율을 유지한다. 바닥 위치와 그림 비율은 Play로 확인한다.

## 한 PNG에 동작이 여러 개 있는 경우

기존 `PlayerAnimation.png`는 가로 3칸 × 세로 3칸,
`GuardianSprites.png`는 가로 3칸으로 **이미 Unity에 잘라 등록되어 있다**.
Project 창에서 PNG 왼쪽의 펼치기 화살표를 누르면 자른 Sprite가 나타난다.

새 시트를 넣을 때는 다음과 같이 한다.

1. **Texture Type → Sprite (2D and UI)**, **Sprite Mode → Multiple → Apply**를 선택한다.
2. **Sprite Editor**를 열고 **Slice**로 동작별 영역을 나눈다. Pivot과 **Pixels Per Unit**도 맞춘다.
3. Sprite Editor에서 **Apply**를 누른다.
4. PNG를 펼쳐 나온 각 Sprite를 이미지 설정의 해당 동작 슬롯에 넣는다.

Sprite Editor는 함께 추가한 `2D Sprite` 패키지가 제공한다.
새 이미지 파일은 다른 이름으로 만들어도 된다. 슬롯에 직접 연결하므로 코드의 문자열을 수정할 필요가 없다.

## UI 그림 교체하기

UI 일러스트 슬롯은 **Texture2D**를 받는다. PNG를 **Texture Type → Default**로 가져와 슬롯에 넣으면 된다.
코드가 그림의 비율을 유지해 UI 영역 안에 맞춘다.

- **타이틀 표시 영역**은 이전 표지용 설정이다. 현재 타이틀은 `menuBackground` 전체를 쓴다. 이전 표지의 영역은 왼쪽 위 기준 0~1 비율이다. 기존 콜라주 그림은 왼쪽 위 1/4만 쓰므로 `X=0, Y=0, W=0.5, H=0.5`다.
  새 PNG 전체를 표지로 쓰려면 `X=0, Y=0, W=1, H=1`로 바꾼다. 스토리 화면은 세계관 그림 전체를 사용한다.
- **수호자 계승 일러스트**는 이전 3×2 시트용 슬롯으로 보존한다. 현재 완료 화면은 계승 타입에 맞는 수호자 Sprite를 표시한다.

## 기존 파일에 덮어쓰기 / Git 공유

같은 그림을 새 버전으로 덮어쓸 때는 **PNG만 교체하고 기존 `.meta`를 유지**한다.
시트는 프레임 크기와 배치를 유지하면 기존 자르기와 슬롯 연결을 그대로 쓸 수 있다.
프레임 배치를 바꾼 경우 Sprite Editor에서 다시 확인하고, 슬롯 참조도 확인한다.

다음 파일을 함께 커밋한다.

- 새 그림 또는 수정한 그림과 각각의 `.meta`
- `KarmaArtworkSettings.asset`과 `.meta`
- 새 폴더를 만들었다면 해당 폴더의 `.meta`
- 이번 방식 변경 시 추가된 스크립트와 `.meta`, `Packages/manifest.json`, `Packages/packages-lock.json`

Unity는 `.meta`의 GUID와 Sprite ID로 연결을 저장하므로, 팀원은 pull 후 직접 재연결할 필요가 없다.
`Library`, `Temp`, `output/validation`은 공유하지 않는다.

## 연결 검증

`python3 tools/run-unity-artwork-checks.py`는 프로젝트를 임시 폴더에 복사하고 Unity에서 새로 가져와
설정 참조, 모든 그림, 플레이어/수호자/배경 생성, 슬롯 교체 및 캐시 초기화를 검사한다.
현재 씬과 사용자 설정은 변경하지 않는다. 로그는 `output/validation/artwork/`에 남긴다.
