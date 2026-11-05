# 검기형 보스 후퇴 빈도·대쉬 초과 이동거리 조정

2026-11-01 (한국 시간), `feat/boss-sword2`.

1. **플레이어 실제 대쉬 최대 수평 이동거리: 8.000유닛**

   방향키 두 번 입력의 `BeginDirectionalSlash` → `MoveSlash`가 대쉬에 해당한다. 설정은 `game.Config.Techniques.pierceDistance = 8`이다. 별도의 대쉬 속도·지속시간을 적분하는 방식이 아니라, BoxCast로 막히는 지점을 찾고 Rigidbody2D와 Transform 위치를 한 번에 갱신한다. `Pierce` 자세의 0.22초는 연출 시간이며 이동 시간이 아니다. 따라서 `8 / 0.22`를 대쉬 속도로 간주하지 않았다.

   실제 Play Mode에서 이 함수 경로를 호출해 좌우 각각 8.000유닛 이동을 확인했다. 테스트 벽 앞에서는 약 1.445유닛, 경기장 오른쪽 끝에서는 1.000유닛만 이동했다. 트리거는 무시하고 `KarmaSurface` 지형, 경기장 경계, 이동 방향에 반대인 접촉면을 검사한다. 일방향 플랫폼은 아래 방향으로 위에서 통과할 때만 막는다. 별도의 공중 돌진 베기는 기본 이동량 2.4유닛·0.12초의 곡선 이동이며, 이번 기준은 8유닛 방향 대쉬다.

   [대쉬 실제 측정값](../output/validation/sword-retreat-dash-20261101/updated/dash.json). 플레이어 기능·설정 파일은 수정하지 않았다.

2. **후퇴 쿨다운: 2.4초 → 1.7초**

   유효 쿨다운은 1.6~1.8초 범위로 제한한다. 근접 또는 압박 요청은 기존처럼 다음 공격 선택 전에 평가한다. 이미 진행 중인 공격은 호스트의 `running` 경로에서 끝날 때까지 계속 실행되며, 후퇴 때문에 중단하지 않는다.

   첫 공격 전 후퇴 금지와 후퇴 사이 공격 완료 토큰을 유지했다. 후퇴 종료 후 0.65초 동안 추가 후퇴·접근을 보류하고 기존 기본 공격 요청을 사용한다. 쿨다운이 끝나더라도 공격 완료가 필요하므로, 설정 1.7초와 실제 후퇴 시작 간격은 다를 수 있다. 이번 추격 테스트의 실제 최소 간격은 약 2.200초였다.

3. **목표 후퇴 거리: 실제 대쉬 기준 최소 9.6유닛**

   계산은 다음과 같다.

   ```csharp
   float previousBudget = Mathf.Clamp(Settings.swordRetreatDistance, 2, behavior.BasicWaveRange * 0.9f);
   float dash = Mathf.Max(0, owner.game.Config.Techniques.pierceDistance);
   float target = Mathf.Max(previousBudget, dash * Mathf.Max(1.2f, Settings.swordRetreatDashMultiplier));
   ```

   기존 유효 예산 9와 대쉬 8 × 1.2 중 큰 값인 9.6을 사용한다. 플레이어 대쉬 설정이 12인 검증에서는 목표와 실제 후퇴가 14.4 이상으로 따라가는 것도 검사했다. 숫자 9.6을 이동 로직에 고정하지 않았다.

   이전에는 플레이어와의 간격이 9.5가 되면 정지해 실제 이동량이 예산보다 짧아질 수 있었다. 이제 시작 위치에서 이동 가능한 종점을 정하고, 보스 자신의 실제 이동량으로 종료한다. 지형 BoxCast와 경기장 여유 공간으로 종점을 미리 제한하고, 매 이동 스텝에서도 충돌·바닥 검사를 계속한다. 일방향 플랫폼은 지상 수평 후퇴의 벽으로 취급하지 않는다. 이동을 막지 않는 접촉면과 트리거도 제외한다.

   최고 속도 16유닛/초, 가속 180, 감속 160, 준비 시간 0.045초는 유지했다. 이동량·최고 속도·가감속으로 필요한 시간을 계산해 기존 1.05초 시간 예산과 비교한 큰 값을 사용하므로, 대쉬 설정이 길어져도 시간 제한 때문에 이동을 미리 포기하지 않는다. 목적지로 순간이동하지 않는다.

4. **수정 전후 60초 비교 및 평균 실제 후퇴 거리**

   | 측정 항목 | 수정 전 | 수정 후 |
   | --- | ---: | ---: |
   | 측정 게임 시간 | 60초 | 60초 |
   | 후퇴 시작·완료 횟수 | 25 / 25 | 27 / 27 |
   | 평균 실제 후퇴 거리 | 8.999936 ≈ **9.000** | 9.599781 ≈ **9.600** |
   | 미완료 후퇴 | 0 | 0 |
   | 설정 쿨다운 | 2.4초 | 1.7초 |
   | 실제 최소 후퇴 시작 간격 | 약 2.400초 | 약 2.200초 |
   | 공격 시작 횟수 | 25 | 28 |
   | 근거리 칼질 평타 | 25 | 28 |
   | 공격 없이 연속한 최대 후퇴 수 | 1 | 1 |

   같은 조건에서 후퇴 횟수는 **8% 증가**했다. 수정 후 한 번의 평균 실제 이동량은 대쉬 8유닛보다 약 **20% 길다**. 공격 완료와 회복 시간을 유지하므로 쿨다운 감소율만큼 횟수가 증가한다고 주장하지 않는다.

   비교 조건은 실제 Unity Play Mode, 60Hz 고정 기록 시간, 동일한 난수 시드 6101이다. 충돌 검사된 `MoveSlash` 경로를 이용하는 자동 추격자가 이동 속도 14로 간격 3.5를 추격하고, 0.6초마다 0.1 피격 압박을 가한다. 보스가 경기장 끝 근처에서 후퇴를 마친 뒤에만 추격자가 걸어서 반대편으로 이동한다. 보스의 AI Update·패턴 코루틴·공격 선택·회복은 정상 실행한다. 보스를 재배치해 측정 거리를 부풀리지 않았다.

   이는 동일 조건의 **게임 시간 60초** 비교이며, 사람의 수동 플레이나 실시간 FPS 벤치마크는 아니다. 지속 추격 덕분에 수정 전에도 유효 예산 9를 모두 사용했다. 별도의 정지 플레이어 검사에서는 시작 간격 3.5와 8 모두에서 목표 이동량 9.6을 채우는지도 확인했다.

   [비교 요약](../output/validation/sword-retreat-dash-20261101/comparison.json), [수정 전 원자료](../output/validation/sword-retreat-dash-20261101/baseline/campaign.json), [수정 후 원자료](../output/validation/sword-retreat-dash-20261101/updated/campaign.json).

5. **잔상 연출 테스트 결과**

   기존 잔상 생성·셰이더·머티리얼·보조 모션 코드는 변경하지 않았다. 속도 상한도 같으므로 잔상 간격이 이동거리 증가 때문에 벌어지지 않는다. 실제 스프라이트와 계승 외형, 청백색 색상, 월드 고정, 순차 생성·페이드, 풀 재사용을 유지한다. 기본값은 생성 간격 0.055초, 수명 0.27초, 알파 0.58, 풀 6개다.

   양방향 실제 렌더에서 최대 5개가 보스 앞쪽에 남는 것을 확인했다. 피격 이펙트와 함께 표시되는 검사도 통과했다. 좌우 각각 26장, 총 52장의 시작~종료 연속 캡처를 저장했고 이미지를 직접 열어 확인했다.

   - [오른쪽을 보며 왼쪽으로 후퇴](../output/validation/sword-retreat-dash-20261101/updated/facing-right-09.png)
   - [왼쪽을 보며 오른쪽으로 후퇴](../output/validation/sword-retreat-dash-20261101/updated/facing-left-09.png)
   - 연속 파일: `updated/facing-right-00.png` ~ `facing-right-25.png`, `updated/facing-left-00.png` ~ `facing-left-25.png`.

6. **근거리 칼질 및 무한 후퇴 방지**

   수정 후 60초에서 칼질 평타가 28회 발동했다. 공격 없이 두 번 연속 후퇴한 경우는 없었다. 후퇴가 막히면 공격 완료 토큰과 쿨다운을 소비하고 다음 공격으로 넘기므로 벽 앞에서 후퇴 준비만 반복하지 않는다. 후퇴 직후의 0.65초 보류와 다음 공격 완료 조건을 함께 검사했다. 일반 거리 변화로 실행 중인 근접/원거리 기본 공격의 모드가 바뀌지 않는 기존 회귀 검사도 통과했다.

7. **Unity 컴파일·Play Mode 검사**

   - 수정 전 3,611개, 수정 후 **7,328개 assertion 통과**, 두 실행 모두 종료 코드 0.
   - Editor 컴파일 및 Windows Player 스크립트 컴파일 26개 어셈블리 통과.
   - 실제 방향 대쉬 8유닛과 벽·경계 제한 확인.
   - 열린 경기장, 좌우 방향, 초기 간격 3.5/8, 30·60·120FPS 시간 간격에서 실제 후퇴 ≥ 대쉬 × 1.2, 속도·가감속 제한, 후퇴 종료와 공격 기회 검사 통과.
   - 대쉬 설정 12 → 실제 후퇴 ≥14.4의 동적 연동 검사 통과. 테스트용 설정 복제본만 바꾸고 원복했다.
   - 양쪽 경기장 끝과 실제 고체 지형 Collider에서 가능한 거리만 이동하고 반복하지 않는 검사 통과.
   - 양방향 실제 잔상 렌더 및 피격 공존 검사 통과.
   - 같은 60초 추격 조건에서 횟수 증가, 평균 이동량, 칼질과 후퇴 혼합, 공격 없는 후퇴 연속 금지 검사 통과.
   - 기존 기본 공격 선택·준비 중 모드 유지·SW05 선택 우선순위·강제 패링/스턴 중단 회귀 검사 통과.

   [수정 전 판정](../output/validation/sword-retreat-dash-20261101/baseline/result.txt), [수정 후 판정](../output/validation/sword-retreat-dash-20261101/updated/result.txt), [Player 컴파일 판정](../output/validation/sword-retreat-dash-20261101/updated/player-compilation.txt).

   검증용 프로젝트에서 Direct3D 12 초기화가 멈춘 시도는 측정에서 제외하고, 전후 모두 Direct3D 11로 정상 완료한 실행을 비교했다. 기록은 `Time.captureDeltaTime = 1/60`을 사용한다. 전체 Player 빌드·독립 실행 성능 벤치마크는 수행하지 않았다.

8. **이번 수정·추가 파일**

   - 수정: `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossMovement.cs`
   - 수정: `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/KarmaSwordBossSettings.cs`
   - 추가: `tests/Unity/SwordWave/KarmaSwordRetreatDashPlayChecks.cs`
   - 추가: `tools/SwordWave/run-sword-retreat-dash-checks.ps1`
   - 추가: `tools/SwordWave/docs/SWORDWAVE_RETREAT_DASH_REVISION.md`

   검증 명령은 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/SwordWave/run-sword-retreat-dash-checks.ps1 -Phase Baseline` 또는 `-Phase Updated`다. `-Project`는 `output/validation/` 아래의 캐시된 검증용 프로젝트만 허용한다. 기본값은 앞선 잔상 검증에 사용한 격리 프로젝트다. Baseline 재실행은 기록한 `movement-before.cs`와 `settings-before.cs`를 검증 프로젝트 안에만 적용하고, 실제 작업 소스는 바꾸지 않는다. 전후 소스 기록·캡처·로그는 `output/validation/sword-retreat-dash-20261101/`에 보존했다.

9. **SW05·다른 보스 및 기존 작업 보존**

   작업 시작 시 기존 635개 파일의 SHA-256을 저장했다. 최종 변경된 기존 파일은 검기형 보스 이동·설정 2개뿐이고 누락 파일은 없다. SW01~SW05 공격 패턴, SW05 전용 리소스, 다른 보스·플레이어 기능, 기존 미커밋 변경·미추적 파일과 앞선 잔상 리소스를 보존했다. 실제 최종 소스와 검증한 프로젝트 소스의 해시도 대조했다.

   [파일 보존 검사](../output/validation/sword-retreat-dash-20261101/source-preservation.json). commit, push, merge, reset, restore, clean, stash는 실행하지 않았다.
