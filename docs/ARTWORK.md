# 이미지 교체 방법

## 시트 내부 그림을 개별 스프라이트로 사용하기 (2026-10-23)

`Assets/K4RMA/Resources/K4RMAArtwork/Sheets/`의 PNG는 **Sprite Mode: Multiple**이다.
Project 창에서 PNG 왼쪽 화살표를 펼치면 포즈·효과·부품을 각각 드래그해서 사용할 수 있다.
`KarmaSpriteLibrary.asset`에는 37개 시트/레이어의 이름과 Sprite 참조 208개가 저장되어 있고,
`KarmaArtworkSettings.asset`의 `Sprite Library` 필드가 이 에셋을 참조한다.

| 분리된 자료 | Sprite 수 | 실제 연결 |
| --- | ---: | --- |
| Player | 20 | 대기·달리기·점프·정점·낙하·착지·공격 준비/베기/후속/회복·검기·가드·반격·피격·사망·고유 기술 |
| Neutral / Wave / Rise / Ward | 8 / 12 / 12 / 12 | 수호자 전조·공격·회복·방어·기절·피격·사망; 측면/후면도 개별 선택 가능 |
| PlayerVFX / GuardianVFX / PersonalizationVFX / HitVFX | 12 / 12 / 12 / 8 | 공격·투사체·상승·방어·고유화·적중 효과, 나머지 변형도 재사용 가능 |
| Inheritance | 16 | 13개 외형은 고유화 완료 화면에서 순서에 맞춰 선택, 3개 부품은 계승 수호자에 장착 |
| Props | 16 | 실외·실내 가장자리의 장식; 충돌 판정 없음 |
| HUD / SkillIcons / UI | 15 / 6 / 24 | 체력 다이아몬드·고유 기술 아이콘·선택 표시·타이틀 장식; 다른 UI 조각도 선택 가능 |
| ExteriorLayers / HallLayers | 13 / 10 | 배경 레이어를 각각 등록; 현재 플레이 배경은 합성 파노라마 사용 |

핵심 포즈는 연속 프레임 애니메이션이 아니므로 상황에 맞춰 포즈를 전환한다.
캐릭터·VFX·UI 시트의 모든 슬롯을 분리했으며, 한 화면에서 모든 변형을 동시에 표시하지 않는다.
원본 사진·아트디렉션·전체 화면 목업은 제작 참고 자료로 보존한다.

분리 사각형·발 Pivot·겹친 포즈를 분리하는 렌더 윤곽선은 `tools/sprite-slices.json`에 기록되어 있다.
원본 PNG와 기존 `.meta`는 보존했다. 새 `Sheets/` PNG와 `.meta`, 두 설정 에셋을 함께 공유한다.
이미지 연결만 바꾸려면 라이브러리의 해당 Sprite 참조를 교체한다. 같은 포즈 이름을 유지하면 코드가 새 그림을 사용한다.

시트 재분리는 연결된 Unity 에디터에서 실행한다. 같은 이름의 Sprite ID를 재사용하므로 재적용해도 참조가 유지된다.

```sh
unity command script.run --caller plugin --skill sprite-editor --timeout 120 -- --file tools/ImportSpriteSheets.cs --entry ImportSpriteSheets.Main --timeout 110
unity command eval --caller plugin --skill sprite-editor 'KarmaPrototype.Editor.KarmaArtworkValidation.Run();'
unity command script.run --caller plugin --skill sprite-editor -- --file tools/PreviewSpriteSheets.cs --entry PreviewSpriteSheets.Main
```

미리보기는 `output/validation/sprites/Unity-*.png`에 실제 Unity 메시로 렌더링한다.
윤곽선을 다시 추출하려면 Pillow를 설치한 Python에서 `python3 tools/build-sprite-outlines.py`를 실행한 뒤 재분리한다.

플레이어/고유화/적중 VFX 32개는 균일한 칸이 아닌 실제 그림 범위로 분리한다.
`tools/build-vfx-outlines.py`는 긴 꼬리와 분리된 빛 조각을 포함한 여러 렌더 윤곽을 생성하며,
잘라 둔 영역 안에 이웃 이펙트가 섞이지 않게 한다. 원본 PNG는 변경하지 않는다.
재생성 후 위 Unity 임포트 명령을 실행하면 같은 Sprite ID로 갱신된다.
`output/validation/sprites/Unity-PlayerVFX.png`, `Unity-PersonalizationVFX.png`, `Unity-HitVFX.png`에서
실제 Unity 메시의 완전한 끝부분을 확인할 수 있다. 일회성 스킬 이펙트와 플레이어 검기는 원본 비율을 유지한다.

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

## 보스 이펙트 방향

`KarmaBossAttack.ConfigureVisual`은 투사체의 +X축을 속도 방향에 맞추고, 제자리 참격/기둥은 수직을 유지하면서 시선에 따라 `flipX`를 적용한다. 그림은 공격 루트 아래 별도 Transform에 둬 회전이 충돌 박스를 바꾸지 않는다. 발사 섬광과 잔상도 같은 방향/반전을 사용한다. `tools/build-vfx-outlines.py`는 보스 효과 12개도 원본 픽셀 범위에서 분리한다.
