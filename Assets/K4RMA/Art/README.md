# K4RMA 이미지

현재 게임에서 쓰는 PNG 30개만 보관한다. 파일별 용도·포즈는 [IMAGE_INDEX.md](IMAGE_INDEX.md), 파일 목록은 [IMAGE_INDEX.csv](IMAGE_INDEX.csv)를 참조한다.

| 폴더 (`Resources/K4RMAArtwork` 아래) | 용도 |
|---|---|
| `Characters/Player` | Actions 20동작 (웅크리기 포함) |
| `Characters/Guardians/Base` | 기본 수호자 4종 |
| `Characters/Guardians/Inherited` | 계승·최종 강화 9종, 각 16포즈 |
| `Characters/Guardians/Revival` | 기본 수호자 부활 4종, 각 6포즈 |
| `Characters/Guardians/ReferencePoses.png` | 현재 계승 외형 기준 및 런타임 대체 포즈 |
| `Effects` | 플레이어·수호자·고유화·피격 효과 |
| `UI` | 공용 UI 시트, 메뉴 배경 |
| `Environments` | MountainDojo·TrialHall의 기본 배경·파노라마 |
| `Props` | 현재 월드 소품 시트 |

`Editor/DesignNotes`에는 현재 이미지의 생성 프롬프트만 보관한다. 이전 버전·중복 원본·미사용 레퍼런스는 삭제했다. 현재 PNG가 편집 원본이자 게임 리소스다.

기준 목록은 `Assets/K4RMA/Resources/KarmaImageCatalog.json`이다. 변경 후 `python3 tools/update-artwork-index.py`로 Markdown·CSV를 갱신하고 `python3 tools/validate-complete-art.py`로 누락·미사용 파일을 확인한다. 삭제한 이미지를 에디터가 다시 생성하지 않는다.

시트 정의는 `tools/sprite-slices.json`에 있다. Unity CLI에서 `tools/unity/ImportSpriteSheets.cs`의 `ImportSpriteSheets.Main`을 실행하면 현재 경로의 이미지에 포즈를 적용하고 라이브러리·설정을 연결한다. 부활·계승 시트는 각각 `ImportGuardianRevival.cs`, `ImportInheritedGuardians.cs`를 사용한다.

이동은 Unity AssetDatabase로 처리해 GUID와 기존 스프라이트 ID를 보존했다. PNG를 교체할 때 `.meta`를 유지한다. 게임의 Resources 경로는 `K4RMAArtwork/...`이며 추가 원본 사본을 요구하지 않는다.
