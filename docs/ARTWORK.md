# 이미지 교체 방법

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
| 수호자 외형 | 검 수호자, 상승 수호자, 결계 수호자 |
| 스테이지 | 도장 배경 |
| UI 일러스트 | 타이틀/세계관, 플레이어 소개, 기술 설명, 수호자 소개, 계승 일러스트 |

플레이어 동작은 9개 슬롯을 모두 채운다. 동작 이미지가 아직 없다면 같은 Sprite를 여러 슬롯에 넣어도 된다.
캐릭터 PNG에는 투명 배경을 사용한다. 화면에서의 크기는 **Pixels Per Unit**과 그림의 투명 여백에 영향을 받는다.
기존 플레이어는 418픽셀짜리 프레임에 **Pixels Per Unit = 167.2**,
수호자는 724픽셀짜리 프레임에 **약 353.17**을 사용한다. 각각 프레임 높이가 약 2.5, 2.05 월드 단위다.
새 그림도 원하는 월드 높이로 표시하려면 `Pixels Per Unit = 프레임 픽셀 높이 / 원하는 월드 높이`로 맞춘다.
발 위치를 맞출 때는 Sprite의 Pivot을 조절한다. 기존 동작들은 각 프레임의 발 높이에 맞춘 커스텀 Pivot을 사용한다.
도장 배경은 기존과 같이 정해진 스테이지 크기에 맞춰 반복 배치된다. 바닥 위치와 그림 비율은 Play로 확인한다.

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

- **타이틀 표시 영역**은 왼쪽 위 기준 0~1 비율이다. 기존 콜라주 그림은 왼쪽 위 1/4만 쓰므로 `X=0, Y=0, W=0.5, H=0.5`다.
  새 PNG 전체를 표지로 쓰려면 `X=0, Y=0, W=1, H=1`로 바꾼다. 스토리 화면은 세계관 그림 전체를 사용한다.
- **수호자 계승 일러스트**는 가로 3칸 × 세로 2칸 배치를 유지한다. 전체 해상도는 달라도 된다.

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
