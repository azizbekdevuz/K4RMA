# K4RMA

기술을 고유화하는 순서에 따라 플레이어와 다음 수호자의 전투 방식이 달라지는 2D 보스러시.
수호자 4회 전투와 고유화 선택 3회로 진행한다. 첫 선택이 보스 타입을 정하고 후속 선택이 계승 패턴을 해금한다.

## 실행

Unity Hub에서 저장소 루트를 **Unity 6000.6.2f1**로 연다. URP와 Input System은 `Packages`에 고정되어 있다.
`Assets/K4RMA/Scenes/KarmaPrototype.unity`를 열고 Play → Enter로 시작한다. 빈 씬에서도 자동으로 시작한다.
`K4RMA → 플레이 씬 생성`은 기존 플레이 씬을 열고, 없을 때만 생성한다.

| 입력 | 동작 |
|---|---|
| A/D 또는 ←/→, Space, J | 이동, 점프, 검 공격 |
| Q / 왼쪽 Shift / E | 검기 / 상승베기 / 방어 |
| 같은 방향키 또는 WASD 두 번 | 검기 고유화 후 관통베기 |
| 공중 J | 상승베기 고유화 후 체공. 잠시 머문 뒤 J를 떼거나 제한 시간이 끝나면 낙하 |
| K | 방어 고유화 후 타이밍 반격 |
| 1/2/3, Enter, Esc | 고유화 선택·확정·취소 |
| Esc | 일시정지 |
| R / T | 일시정지·결과 화면에서 재도전 / 타이틀 |
| S / H | 타이틀에서 스토리 / 조작 안내 |

고유화한 기술의 원형 입력은 사용할 수 없다. 상승베기를 고유화하면 왼쪽 Shift의 상승 공격은 꺼지고, 점프 중 J로 한 번만 체공한다.

보스의 패턴·해금 조건·밸런스는 [보스 패턴](docs/BOSS_PATTERNS.md), 원본 명세는 [요구사항](docs/requirements.pdf)을 참조한다.

## 파일 구조

| 경로 | 용도 |
|---|---|
| `Assets/K4RMA/Core` | Unity에 의존하지 않는 진행·기술·보스 규칙 |
| `Assets/K4RMA/Runtime` | Abilities, Combat, Configuration, Enemies, Flow, Input, Player, Presentation, UI, World |
| `Assets/K4RMA/Editor` | 씬 생성, 빌드, 이미지 임포트·검사 |
| `Assets/K4RMA/Scenes` | 플레이 씬 1개 |
| `Assets/K4RMA/Settings` | 게임 설정·재질, Rendering 아래 URP 설정 |
| `Assets/K4RMA/Resources` | 런타임 이미지·스프라이트 라이브러리·재질 |
| `image` | 버전 관리하는 원화·제작 자료; 임포트 도구가 사용 |
| `tools` / `tools/unity` | Python 검사·이미지 도구 / Unity에서 실행하는 C# 도구 |
| `tests/CoreChecks` / `tests/Unity` | Core 검사 / Unity 회귀 검사 |
| `output` | 재생성 가능한 검사 로그·미리보기, Git 제외 |

의존 방향은 `Core ← Runtime ← Editor`다. `partial` 파일은 같은 컴포넌트를 역할별로 나눈 것이므로 공유 상태를 함께 확인한다.
Core는 `noEngineReferences` 어셈블리로 분리한다. Runtime·Editor는 Unity 기본 어셈블리를 사용한다.

## 설정·이미지 수정

수치는 `Assets/K4RMA/Settings/KarmaConfig.asset`에서 변경한다. C# 기본값은 이미 저장한 설정을 덮어쓰지 않는다.
직렬화 필드명은 에셋 데이터와 연결되므로 변경 시 마이그레이션이 필요하다.

그림은 **K4RMA → 이미지 설정 선택**에서 교체한다. 공유 설정은 `Resources/KarmaArtworkSettings.asset`이다.
기본 캐릭터 슬롯은 Sprite, UI 일러스트는 Texture2D를 받는다.
동작별 포즈·VFX·계승 외형은 `KarmaSpriteLibrary`와 `Resources/K4RMAArtwork/Sheets`를 사용한다.
새 캐릭터는 투명 PNG와 발 기준 Pivot을 사용하고, PPU·위치·크기는 Play에서 확인한다.

시트 영역·윤곽은 `tools/sprite-slices.json`이 기준이다. Pillow가 설치된 Python에서
`tools/build-sprite-outlines.py`와 `tools/build-vfx-outlines.py`로 윤곽을 갱신한 뒤,
Unity CLI로 `tools/unity/ImportSpriteSheets.cs`의 `ImportSpriteSheets.Main`을 실행한다.
`PreviewSpriteSheets.cs`, `PreviewBossEffects.cs`는 Unity 렌더 미리보기를 만든다.
Production 원화 재적용 메뉴는 현재 슬롯을 덮어쓰므로 원본을 갱신할 때 사용한다.
`useCompleteArtwork`는 이전 원본 카탈로그 표시 옵션이며 기본값은 꺼짐이다.

## 검증·빌드

프로젝트를 Unity에서 한 번 임포트한 뒤 실행한다.

```sh
python3 tools/validate-source.py
python3 tools/check-unity-compile.py
dotnet run --project tests/CoreChecks --configuration Release
python3 tools/validate-complete-art.py
python3 tools/run-unity-artwork-checks.py
python3 tools/run-unity-playchecks.py
```

Core 검사는 .NET 8이 필요하다. 기본 SDK가 구버전이면 Unity의 `Contents/Resources/Scripting/DotNetSdk/dotnet`을 사용한다.
컴파일 도구는 `--unity-contents`, Unity 검사 도구는 `--unity`로 설치 경로를 지정할 수 있다.
Play·아트 검사는 임시 프로젝트 복사본에서 실행하며 결과는 `output/validation`에 남긴다.
달리기 프레임 검사는 `tools/unity/CheckPlayerRun.cs`의 `Main`과 `Play`로 실행한다.

수동 확인: 6가지 선택 순서, 전조·피격 범위, 일시정지, 사망·재도전 후 잔여 공격 제거를 확인한다.
화면 가독성·회피 난도·총 플레이 시간·Windows 성능은 실제 플레이로 확인해야 한다.
Windows Build Support (Mono) 설치 후 **K4RMA → Windows 64-bit 빌드**로 `Builds/Windows/K4RMA.exe`를 생성한다.
실행 파일과 데이터 폴더를 함께 배포한다.

## Git 관리

`Assets`와 모든 `.meta`, `Packages`, `ProjectSettings`, `image`, 도구·검사·문서를 함께 커밋한다.
에셋 이동 시 `.meta`를 함께 옮기고, PNG 교체 시 기존 `.meta`를 유지해야 참조가 보존된다.
`Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, IDE 생성 파일, 빌드·검사 산출물, 로컬 비밀 설정은 `.gitignore`로 제외한다.
`Resources/K4RMAComplete`와 Editor의 원본 이미지 카탈로그는 `image`에서 재생성하므로 제외한다.
