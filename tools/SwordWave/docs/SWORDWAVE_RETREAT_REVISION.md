# 검기형 보스 슬라이딩 후퇴 및 잔상 개선

검증일: 2026-11-01 (한국 시간). 브랜치: `feat/boss-sword2`.

1. **기존 후퇴 AI와 연출의 문제점**

   기존 후퇴도 근접 거리, 공격 완료 토큰, 쿨다운, 지형 검사로 제한되어 있었다. 그러나 접근과 같은 `Move` 자세·보행 흔들림을 사용했고, 본체 잔상이 없었다. 후퇴 가속도 56으로는 최고 속도에 도달하는 데 약 0.286초가 필요했다. 최고 속도에는 설정 상한과 플레이어 이동 속도의 1.15배 상한이 함께 적용됐다. 기본 설정에서는 최종 상한이 16이었다.

2. **변경한 후퇴 이동 방식**

   0.045초 준비 후 후퇴 전용 가속도 180으로 빠르게 출발하고, 감속도 160으로 정지한다. 최고 속도는 16을 유지한다. 걷기 대신 계승 외형을 보존한 고정 준비 자세와 작은 후방 기울기를 사용한다. 접근의 가속·감속 설정은 그대로다.

   기존 `MoveSwordOnGround`의 BoxCast, 바닥 Raycast, 경기장 경계 검사를 통과한 작은 이동량만 기존 이동 함수에 전달한다. 좌표를 목적지로 건너뛰지 않는다. 후퇴 종료 프레임에서는 접근을 함께 실행하지 않으며, 기존 기본 공격 요청·짧은 대기 흐름으로 복귀한다. 첫 공격 전 회피 금지, 공격 완료 토큰, 근접 트리거, 2.4초 쿨다운은 유지했다. 시작 시에는 기존 방향 전환 무시 구간 안에서도 플레이어를 향한다.

3. **잔상 생성 알고리즘**

   검기형 보스 Artwork에만 `KarmaSwordRetreatAfterimages`를 붙인다. 보조 모션이 끝난 뒤 LateUpdate에서 실제 SpriteRenderer의 스프라이트, 표시 여부, Flip, 알파, 정렬과 변환 계층을 저장한다. 다음 프레임에 실제 후퇴 이동이 확인되면 이전 표시 프레임을 풀 슬롯에 복제한다.

   부모 계층의 로컬 위치·회전·스케일까지 복제하므로 반전, 회전, 비균일 스케일에 따른 월드 변환을 보존한다. 여러 렌더러도 함께 저장한다. 풀은 본체와 독립된 월드 루트에 있어 생성 후 본체를 따라가지 않는다. 생성 간격 외에 최소 이동량 0.12를 적용해 정지·미세 이동 중 중복 잔상을 피한다. 슬롯 6개를 재사용하며 매 잔상마다 Instantiate/Destroy하거나 머티리얼을 복제하지 않는다.

   원본 스프라이트의 메시·알파·내부 명암을 사용하는 전용 청백색 셰이더를 잔상에만 적용했다. 금색 계승 외형도 푸르게 보이며, 원본 이미지와 본체 머티리얼은 변경하지 않는다. 정렬 순서는 원본보다 한 단계 낮아 본체가 앞에 표시된다.

   후퇴 종료 시 생성만 중단하고 기존 잔상은 페이드한다. 인터럽트·비활성화·전투 종료·사망 시 즉시 비활성화하고, Artwork 파괴 시 월드 풀도 파괴한다. 종료 시 파괴 순서가 달라져도 이미 파괴된 슬롯을 다시 참조하지 않는다.

4. **플레이어 쪽에 남는지 여부**

   양방향 실제 Play Mode에서 확인했다. 플레이어가 오른쪽이면 본체는 왼쪽으로 이동하고 잔상은 오른쪽에 남는다. 플레이어가 왼쪽이면 반대다. 렌더 프레임마다 잔상의 위치가 본체의 플레이어 쪽인지, 슬롯이 재사용되기 전까지 월드 좌표가 그대로인지 assertion으로 검사했다.

5. **잔상 간격·유지 시간·알파**

   | 항목 | 기본값 |
   | --- | --- |
   | 생성 간격 | 0.055초 이상 |
   | 유지 시간 | 0.27초 |
   | 초기 알파 | 원본 알파 × 0.58 |
   | 최대 슬롯 | 6개 |
   | 실제 캡처의 최대 동시 표시 | 좌우 각각 5개 |
   | 페이드 | `초기 알파 × (1 - (경과 시간 / 유지 시간)²)` |

   60Hz에서는 프레임 단위 양자화로 보통 약 0.0667초마다 생성된다. 낮은 FPS에서 밀린 잔상을 한 위치에 여러 개 생성하지 않는다. 일시정지 중에는 생성과 페이드를 멈춘다.

6. **거리·속도·가감속 변경 전후**

   | 항목 | 변경 전 실제 기본값 | 변경 후 |
   | --- | --- | --- |
   | 근접 트리거 | 8.2유닛 | 동일 |
   | 후퇴 쿨다운 | 2.4초 | 동일 |
   | 최대 이동 예산 | 9유닛 | 동일 |
   | 목표 간격 상한 | 일반 검기 사거리의 95%, 기본 9.5유닛 | 동일 |
   | 후퇴 시간 예산 | 1.05초 | 동일, 준비 시간 포함 |
   | 준비 시간 | 없음 | 0.045초 |
   | 최고 속도 | 16유닛/초 | 동일 |
   | 후퇴 가속 | 56유닛/초² | 180유닛/초² |
   | 후퇴 감속 | 72유닛/초² | 160유닛/초² |

   저장된 거리 16은 기존에도 실제 로직에서 9로 제한됐다. 실제 이동량은 목표 간격·시간·경계·지형에 따라 줄어든다. 이번 캡처에서는 초기 간격 3.5에서 좌우 각각 6유닛 이동했다. 준비 후 첫 이동은 약 0.0667초, 마지막 이동은 약 0.55초에 관측됐다. 원본 프레임 CSV는 소수점 네 자리로 기록되어 파생 속도 계산에 반올림 오차가 있다. 속도 상한 검사는 반올림 전 실제 좌표로 별도 수행했다.

7. **실제 Unity 실행 캡처 경로**

   결과물: `output/validation/sword-retreat-20261101/artifacts/`

   - [오른쪽을 보며 왼쪽으로 후퇴하는 중간 프레임](../output/validation/sword-retreat-20261101/artifacts/facing-right-06.png)
   - [왼쪽을 보며 오른쪽으로 후퇴하는 중간 프레임](../output/validation/sword-retreat-20261101/artifacts/facing-left-06.png)
   - [오른쪽을 보는 후퇴 후반 프레임](../output/validation/sword-retreat-20261101/artifacts/facing-right-09.png)
   - [왼쪽을 보는 후퇴 후반 프레임](../output/validation/sword-retreat-20261101/artifacts/facing-left-08.png)
   - 연속 캡처: `facing-right-00.png` ~ `facing-right-19.png`, `facing-left-00.png` ~ `facing-left-19.png` (총 40장).
   - [실행 프레임 기록](../output/validation/sword-retreat-20261101/artifacts/frames.csv), [캡처 측정값](../output/validation/sword-retreat-20261101/artifacts/capture-metrics.json).

   Unity 6000.6.2f1 / Direct3D 12 실제 Play Mode의 Game 카메라를 렌더했다. PNG 저장 지연과 관계없이 동작을 기록하도록 캡처 중에는 `Time.captureDeltaTime = 1/60`을 사용했다. 캡처 이미지를 직접 열어 본체와 청백색 잔상의 표시·방향·겹침을 확인했다.

8. **컴파일·Play Mode 테스트 결과**

   - Unity Editor 컴파일 통과.
   - Windows Player 스크립트 컴파일 통과: 26개 어셈블리.
   - 후퇴 assertion 6,539개 및 기존 기본 공격·강제 중단·실시간 AI 회귀 검사 통과. 최종 Unity 종료 코드 0.
   - 30/60/120FPS 시간 간격, 좌우 방향, 초기 간격 5/8에서 속도·가속·감속·정지·연속 후퇴 방지·쿨다운 검사 통과.
   - 양쪽 경기장 끝과 추가한 실제 지형 Collider에 대한 충돌 검사 통과.
   - 실제 렌더의 양방향 최대 잔상 5개, 피격 중 표시, 월드 좌표 고정, 생성 간격, 알파 페이드, 일시정지 검사 통과.
   - 실제 계승 스프라이트·Flip·회전·비균일 스케일과 복수 렌더러 복제, 인터럽트·disable·사망·destroy 정리 통과.
   - 실제 이동·충돌·잔상 컴포넌트를 고정 스텝으로 100회 교대 방향 반복: 풀 슬롯 6개와 Transform 43개가 동일하게 재사용되고 누적되지 않음. 이는 실시간 전투 100회 또는 FPS 벤치마크는 아니다.
   - 기존 공격 회귀: 근접 칼질 / 중거리 일반 검기, 준비 중 거리 변화에 대한 공격 모드 유지, SW05 선택 우선순위, 강제 패링·스턴 중단 검사 통과.
   - 실시간 근접 압박 12초: 공격 시작 6회 중 근접 칼질 5회, 후퇴 4회. 최소 후퇴 간격 **2.404초**. 중거리 7초: 공격 시작 3회 중 일반 검기 2회.
   - 검증 중 런타임 예외 없음. 전체 Player 빌드와 독립 실행 성능 측정은 수행하지 않았다.

   [최종 판정](../output/validation/sword-retreat-20261101/artifacts/result.txt), [컴파일 판정](../output/validation/sword-retreat-20261101/artifacts/player-compilation.txt), [실시간 AI 측정](../output/validation/sword-retreat-20261101/artifacts/live-ai.json).

   재현 명령: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/SwordWave/run-sword-retreat-playchecks.ps1`. 원본 작업 프로젝트 대신 별도 검증 프로젝트를 생성한다. 이번 최종 실행 로그는 `output/validation/sword-retreat-20261101-122534/unity-run3.log`다.

9. **이번 작업의 수정·추가 파일 전체 목록**

   기존 파일 수정 3개:

   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossMovement.cs`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/KarmaSwordBossSettings.cs`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaWaveMotion.cs`

   신규 파일 12개:

   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaCharacterAnimator.SwordRetreat.cs`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaCharacterAnimator.SwordRetreat.cs.meta`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordRetreatAfterimages.cs`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordRetreatAfterimages.cs.meta`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordRetreatEcho.shader`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordRetreatEcho.shader.meta`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/Resources.meta`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/Resources/KarmaSwordRetreatEcho.mat`
   - `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/Resources/KarmaSwordRetreatEcho.mat.meta`
   - `tests/Unity/SwordWave/KarmaSwordRetreatPlayChecks.cs`
   - `tools/SwordWave/run-sword-retreat-playchecks.ps1`
   - `tools/SwordWave/docs/SWORDWAVE_RETREAT_REVISION.md`

   캡처·로그·임시 검증 프로젝트는 gitignore 대상인 `output/validation/` 아래에 보관했다.

10. **SW05·다른 보스 및 기존 작업 보호**

    작업 전 620개 기존 파일의 SHA-256을 저장해 최종 비교했다. 변경된 기존 파일은 위 3개뿐이며 누락 파일은 없다. 기존 `KarmaSwordBossBehavior.cs`를 비롯한 미커밋 변경과 미추적 파일, SW01~SW05 패턴, SW05 전용 셰이더·머티리얼, 다른 보스·플레이어 코드, 신규 검기 이미지·피격 효과·QualitySettings를 보존했다.

    [보존 검사 결과](../output/validation/sword-retreat-20261101/source-preservation.json). commit, push, merge, reset, restore, clean, stash는 실행하지 않았다.
