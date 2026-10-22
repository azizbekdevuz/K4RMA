# 검증 결과 — 2026-10-22

## Inspector 이미지 연결 변경

이미지는 `KarmaArtworkSettings.asset`의 Sprite/Texture 참조로 연결한다. PNG 11개의 가져오기 설정과 플레이어 9개/수호자 3개의 Sprite 자르기 정보를 Unity API로 저장했다. 기존 이미지 묶음은 런타임에서 읽지 않는다.

`python3 tools/run-unity-artwork-checks.py`는 Library가 없는 임시 복사본에서 설정과 Sprite 참조를 다시 가져와 검사한다. 설정 로딩, 11개 텍스처, 모든 플레이어/수호자 동작 참조, 플레이어·수호자·배경 생성, Inspector 참조 교체 반영, 캐시 초기화 시 원본 에셋 유지가 검증 대상이다. 검증 로그는 `output/validation/artwork/`에 저장한다. 화면 렌더링과 새 그림의 크기·발 위치·가독성은 별도 Play 확인이 필요하다.

## 통과한 검사

- `python3 tools/validate-source.py`: C# 49개, 고유 에셋 GUID 79개. 스크립트 `.meta` 누락·중복 및 직렬화 에셋 참조 검사 통과.
- `python3 tools/check-unity-compile.py`: 설치된 Unity 6000.6.2f1의 실제 참조 어셈블리와 번들 C# 컴파일러로 플레이어/에디터 전체 컴파일. 경고를 오류로 처리해 두 대상 모두 통과.
- `tests/CoreChecks`: 생산 Core 파일을 직접 사용한 **394개 assertion 통과**. .NET 8.0.318/8.0.21 사용. 6가지 순서의 모든 누적 단계, 독립 계승, 타입 유지, 중복/잘못된 해금 방지, 선택 조건·궁극기 제한, 연속 박스 충돌·안전 모서리, 기술 전체 1회 접촉, 결계 최종 결과/취소를 검사했다.
- `tests/Unity/KarmaBossPlayChecks.cs`: 격리 복사본에서 Unity 6000.6.2f1 **실제 Play Mode 16회 패턴 실행, 99개 assertion 통과**, 프로세스 종료 코드 0.

Play Mode에서는 근접 기본 패턴과 SW01~05, UP01~05, GD02~05, GD05 실패 분기를 실제 코루틴/Update로 진행했다. 모든 실행에서 회복 진입·바닥 착지·위험 오브젝트 소멸·무적/결계 해제 및 워치독이 아닌 정상 종료를 확인했다. SW04 공격 반응, SW05/UP04 공중 방어 상태도 확인했다.

추가로 GD01 생성 전조→자동방어막, 일시정지 중 전투/방어 시간 정지, GD02의 실제 플레이어 패링→패턴 중단/방어 해제/무력화, GD03의 실제 1초 기절/중복 갱신 방지, GD05 도중 사망 시 이동 제한/공격 즉시 해제, 재시작의 임시 상태 초기화, 보스 사망 시 공격 제거/고유화 화면 진입을 확인했다.

생성된 검증 로그와 결과 파일은 정리했다. `python3 tools/run-unity-playchecks.py`로 임시 복사본에서 재현하면 `output/validation/unity-boss-play.log`와 `output/validation/boss-play-validation.txt`에 다시 기록된다. 테스트용 높은 HP·키네마틱 플레이어·반사 호출은 테스트 하네스에만 있으며 게임 코드에는 넣지 않는다.

## 검증 중 발견한 환경 문제

Unity의 `UnityEditor.Search.SearchDatabase` 시작 인덱싱에서 `ArgumentOutOfRangeException`이 발생했다. 게임 코드 예외와 구분했다. 첫 실행에서는 백그라운드 Play 프레임이 진행하지 않아 검사 시간 제한에 도달했다. 하네스에서 백그라운드 실행, 플레이어 루프 갱신 요청, 에디터 일시정지 해제를 적용한 뒤 프레임 카운터/게임 시간이 증가하며 최종 검사가 통과했다. 이 보정은 임시 테스트 프로젝트에만 적용된다.

검사 로그에 검색 인덱서 예외가 존재하므로 Unity 에디터 환경 전체가 오류 없이 실행되었다고 주장하지 않는다. 최종 검사에서 게임 코드 예외와 C# 컴파일 경고/오류는 관찰되지 않았다.

## 검증하지 않은 것

- `-batchmode -nographics` 실행이므로 실제 화면 렌더링, HUD/전조의 체감 가독성, 아트/애니메이션 품질.
- 실제 키보드 조작으로 모든 패턴을 회피하는 난이도와 재미, 기존 도약베기와 최종 체공 사양의 차이를 해소한 플레이.
- Windows 실행 파일 생성·배포·성능, 1920×1080 평균 60 FPS, 15~20분 플레이 시간.
- 모든 기존 팀 프로젝트로의 이식. 유지하는 스크립트 GUID와 활성 Config 필드명은 보존했지만 삭제된 컴포넌트를 참조하는 외부 씬은 별도 확인이 필요하다.

기존에 사용자가 수정한 `Core/KarmaBurnState.cs`는 수정하지 않았다. 해당 파일의 기존 공백 변경 때문에 저장소 전체 `git diff --check`에는 기존 trailing-whitespace가 보고되며, 이번 작업 파일의 공백 검사는 별도로 통과했다.
