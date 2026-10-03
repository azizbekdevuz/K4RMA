# 팀 협업

## 저장소 루트

실제 Unity 프로젝트 전체를 버전 관리한다.

- 포함: `Assets` + `.meta`, `Packages`, `ProjectSettings`, 코드 검사·문서.
- 제외: `Library`, `Temp`, `Logs`, `UserSettings`, 빌드 산출물.
- 팀의 원본 씬, Config.asset, 사용 폰트·이미지도 포함. 소스 배포본에 없는 파일은 팀 프로젝트가 기준.
- 본 기획을 팀 내부에서 관리하려면 GitHub 저장소를 Private으로 설정.

Unity 프로젝트 설정에서 에셋 직렬화를 Force Text로 유지한다. `.meta`는 무시하지 않는다. Unity에서 에셋 이동 시 원본과 `.meta`를 함께 이동한다.

## 브랜치 작업

```bash
git switch main
git pull
git switch -c feature/meteor-pattern
# 코드·에셋 수정과 검사
git add Assets docs
git commit -m "Adjust meteor warning pattern"
git push -u origin feature/meteor-pattern
```

PR에서 변경 목적, 수정 모듈, 검사 결과, 씬·설정 변경을 확인한 뒤 main에 병합. 두 명이 같은 씬·설정 에셋을 동시에 편집하는 경우 담당을 조율한다. 코드 분할은 파일 충돌을 줄이지만 씬 충돌을 자동 해결하지 않는다.

## 분담 예시

| 작업 | 주로 수정할 폴더 |
|---|---|
| 플레이어 조작·공격 | Player, Abilities |
| 보스 패턴·난이도 | Enemies, Core/PatternCatalog, Configuration |
| 아트·이펙트 | Presentation, World, 실제 이미지·프리팹 폴더 |
| UI·진행·통합 | UI, Flow, Editor |

담당은 팀 협의로 결정. 여러 모듈이 함께 바뀌는 PR은 영향을 적는다.

## PR 검사

자동 CI: Core 생산 코드 검사 + 소스·meta 구조 검사. Unity 라이선스 없이 실행 가능.

팀 수동 검사:

1. 같은 Unity 버전으로 열기 → 컴파일 오류 확인.
2. `K4RMA → 로직 검사` 실행.
3. 이동·점프·화염·돌진·보호막, 선택 후 액티브 종료·패시브 발동 확인.
4. 변경 패턴의 예고·피격·회복·일시정지·사망·재도전 확인.
5. 메테오·반사·삼중 합성은 대응 가능한 빈틈이 있는지 플레이 확인.

완전한 Unity 빌드 CI는 팀 프로젝트 버전과 라이선스 환경이 확정된 뒤 별도로 연결한다. 현재 workflow는 그 빌드를 가장하지 않는다.

참고: https://docs.unity3d.com/es/2021.1/Manual/ExternalVersionControlSystemSupport.html
