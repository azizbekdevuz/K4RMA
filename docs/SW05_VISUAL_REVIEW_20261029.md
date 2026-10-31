# SW05 시각 개선 및 검증 — 2026-10-29

현재 `feat/boss-sword`의 미커밋 상태를 기준으로 원인을 확인하고 렌더링만 수정했다. 영상 파일은 작업 공간에 제공되지 않았으므로 특정 영상 프레임의 오브젝트를 직접 식별한 결과는 아니다. 아래 원인은 현재 코드·PNG·Unity 실행 검사에 근거한다.

## 1. 주황색 화살표 및 윤곽선의 원인

- `KarmaAttackWarning.SetPath()`가 전조의 `Locked danger outline`과 `Attack direction` LineRenderer를 생성한다. 실제 투사체나 추가 피해 판정이 아니다.
- 현재 시작 코드에도 SW05 발사 직전 해당 묶음의 전조와 형성 중인 검을 `SetActive(false)`한 뒤 삭제하는 처리가 이미 있었다. 이번 작업이 기존에 없던 발사 후 경계선을 제거했다고 해석해서는 안 된다.
- `SW05_AerialSword.png` 원본은 청백색이 아닌 주황색이다. 오른쪽이 날카로운 끝, 왼쪽이 둥근 잔광이다. Resources → SpriteLibrary fallback → ExtraWaveVFX 경로로 정상 연결된다.
- 시작 코드의 `KarmaBossAttack.Spawn()`은 이 PNG에 따뜻한 tint/알파 0.86을 적용하고, `AttachAttack()`의 주황색 additive radiance·잔상과 일반 launch Burst도 겹쳐 생성했다. 다음 묶음의 주황색 전조와 시각적으로 구분하기 어려운 구성이다.

수정 후 SW05만 전용 `KarmaAerialSword` Material로 원본 색을 청백색으로 변환한다. 원본 PNG·source alpha·mesh·pivot·core 크기는 그대로다. 실제 검은 순백 tint/알파 1/sorting 26을 사용하며, SW05의 별도 radiance·잔상·중복 launch Burst는 만들지 않는다. 형성 단계의 시간·크기·알파 곡선은 유지하고 최종 tint만 중립색으로 바꿨다. 날끝은 원본 +X를 실제 속도 각도로 회전하며 flipX/flipY를 사용하지 않는다.

## 2. 붉은색·분홍색 사각형의 원인

`KarmaPlayer.TakeDamage()`가 실제 피해 적용 뒤 `KarmaVisuals.Flash(..., Color(1, 0.2, 0.3))`를 호출한다. `Flash()`는 1×1 흰 `K4RMA_RuntimePixel`을 정사각형으로 확대해 `Effect` 오브젝트를 만들고 sorting 10으로 표시한다. `KarmaEffect`가 0.18초 동안 알파를 낮추므로 반투명 붉은색·분홍색 사각형으로 보일 수 있다.

이 경로는 의도된 플레이어 피격 피드백이며 그대로 보존했다. 무적·방어·반격 분기는 피해 적용 전에 반환한다. 보스의 정상 피격은 별도의 흰 Flash, 캐릭터 `Hurt()` tint는 원본 실루엣이다. 런타임 판정을 그리는 디버그 사각형 경로는 발견되지 않았다.

ExtraWaveVFX 6종은 외곽 alpha 0이다. SW05는 1455×382, alpha 0~253이며 불투명 사각 배경이 없다. 외형 PNG 4종과 기존 PlayerVFX/GuardianVFX/HitVFX도 투명 알파를 가지고 있다. 기존 Sprite Material은 URP Sprite-Unlit-Default이며, 해당 사각형의 원인을 오류 셰이더나 PNG 배경으로 볼 근거는 없다.

## 3. SW05 전조가 겹치는 이유와 수정

묶음당 4~8개의 서로 다른 경로가 동시에 표시된다. 시작 코드의 화살표는 각 긴 경로의 70% 지점에 위치해 중앙·먼 영역의 윤곽선과 겹쳤다. 전조 끝은 이미 `SwordShotLife()`로 수명·경기장 경계에 제한되며, 끝의 실제 피해 상자 반폭까지 표시하는 것이 맞다. 도달 범위나 폭을 줄여 표시하지 않았다.

SW05 전용 스타일만 아래처럼 변경했다. 다른 패턴의 전조는 유지했다.

| 표현 | 시작 코드 | 수정 후 |
|---|---|---|
| 채움 알파 | 0.072~0.096 | 0.030~0.042 |
| 전체 윤곽선 알파 | 0.72~0.88 | 0.34~0.44 |
| 윤곽선 두께 | Units(0.025~0.035) | Units(0.018~0.022) |
| 방향 화살표 | 경로의 70% 지점 | 시작점에서 min(경로 25%, Units(1.2)) |
| 화살표 알파/두께 | 0.72~0.88 / Units(0.04) | 0.80~0.90 / Units(0.03) |
| 전체 위험 hull·길이·폭 | 실제 swept oriented box | 동일 |

발사 직전의 전조 비활성화와 소유자 기반 정리는 유지하며, 실제 발사 프레임에 전조가 꺼지고 검에 별도 LineRenderer가 없는지 새 검사로 확인한다.

## 4. 수정 파일 및 함수

- `Assets/K4RMA/Runtime/Combat/KarmaBossAttack.cs`: `Spawn()`의 SW05 전용 색/장식 레이어 처리.
- `Assets/K4RMA/Runtime/Presentation/KarmaWaveArtwork.cs`: `Apply()`의 SW05 Material 연결 및 이미지 방향 초기화. 잘못 남아 있던 발사 후 경계선 관련 주석 정정.
- `Assets/K4RMA/Runtime/Presentation/KarmaAttackWarning.cs`: `SetPath()`, `ApplyStyle()`의 SW05 전용 전조 스타일.
- `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Presentation.cs`: `PathMark()`에서 SW05 스타일 선택.
- `Assets/K4RMA/Runtime/Enemies/Patterns/KarmaEnemy.SwordWave.cs`: `FormAerialSword()`의 최종 tint만 중립색으로 변경.
- 새 `KarmaAerialSword.shader` 및 `Resources/KarmaAerialSword.mat`와 각 meta: source alpha를 보존하는 SW05 전용 청백색 변환.
- `tests/Unity/KarmaWaveVFXChecks.cs`: 8방향×보스 facing, Material/alpha/크기/전조 hull/기존 패턴 보존 검사.
- `tests/Unity/KarmaBossPlayChecks.cs`: 실제 SW05 발사 프레임의 전조 비활성 및 PNG 단독 표시 검사.
- 새 `tests/Unity/KarmaHitFlashChecks.cs`: 실제 피격·무적·방어로 정상 사각형 생성 경로 검증.
- 새 `tests/Unity/KarmaAerialSwordRenderChecks.cs`: 실제 Play Mode의 Update에서 GPU 렌더·alpha·8방향·전조 겹침 검사 및 QA 이미지 생성. 대각선 hull 바깥 안전한 모서리의 투명도와 모든 Error/Exception/Assert도 검사한다.
- `tools/validate-sword-polish.ps1`: GPU 검증 소스 컴파일에 필요한 기존 URP 어셈블리 참조 추가. Editor 종료 직후 import/ILPP worker가 로그 핸들을 유지할 때 결과 수집이 실패하던 파일 공유 처리를 보완했다. 검사 조건이나 PASS 판정은 완화하지 않았다.
- `tools/run-unity-playchecks.py`: 현재 main/VFX 검사가 참조하는 외형·피격 검사 복사 목록 보완.

`KarmaCombatVFX.cs`는 생성 경로를 분석했으며 이번 작업에서 수정하지 않았다.

## 5. 보존한 공격 메커니즘 및 작업 상태

SW05 planner·seed·18~30발·묶음 4~8발·방향 선택·안전 경로 검사·공중 방어막/무적·착지/회복을 변경하지 않았다. 피해 판정·DamageBounds·속도·공격력·쿨타임·전조 시간을 변경하지 않았다. SW01 2발 및 확대된 크기, SW02 추적→고정→강베기/회전 판정, SW03 안전 간격/상승 후 3초, SW04 반격 제한과 방어막을 보존했다. 외형 4종·투명 보스 PNG·기존 모션과 플레이어/다른 보스 기능도 보존했다.

시작 시점 파일 65개의 SHA256과 수정 대상 파일 복사본은 `output/validation/sw05-visuals-20261029/`에 보존했다. 기존 PNG를 덮어쓰거나 새 보스 원화를 만들지 않았다. Git add/commit/push/pull/fetch/reset/restore/switch를 실행하지 않았다.

## 6. 새 자동 검증 결과

최종 전체 실행: `output/validation/sword-polish-20261029/all-20261029-024230-177-utc/` (`result.txt`: **PASS: all**). 기존 PASS를 재사용하지 않고 현재 소스와 테스트를 복사해 새 결과를 생성했다. 임포트 캐시만 재사용하며, 이전 verdict는 해시 확인 후 별도 폴더에 보관한다.

| 새 검사 | 결과 |
|---|---|
| Unity 6000.6.2f1 Player 소스 컴파일 | PASS, 52개 소스, warnings as errors |
| Unity Editor 및 전체 검사 소스 컴파일 | PASS, 71개 소스, warnings as errors |
| Core | PASS, 419 assertions |
| 실제 Unity Play Mode | PASS, 32개 패턴 실행 / 39,450 assertions |
| 스테이지별 전투 흐름 | PASS, 40개 시나리오 / 102,169 assertions |
| 실행 중 소스 일치 | 72개 SHA256 전후 동일 |
| SW05 수량 분포 | 1,024 seed, 18~30발 / 묶음 4~8발, 507개 구성 |
| SW05 실제 물리 회피 경로 | 80/80 PASS; 4 seed × 5위치 × Collider 너비 2종 × 일반/강화 |
| 마지막 SW05 전조/실제 이동 | 일반 12.8/12.8, 강화 10.496/10.496 |
| 원래 파일·PNG 보존 | 초기 65개 파일 누락 0; PNG 14개 및 수정 대상 외 파일 SHA256 동일 |

Play Mode에는 새로운 8방향×보스 facing 검사, 실제 발사 시 전조 비활성, PNG 단독 표시, 전체 전조 hull/폭 유지, 피격 사각형 재현 및 무적/방어 검사, 기존 SW01~04·외형 4종·플레이어/다른 보스·정지/사망/재시작 정리가 포함된다.

이번 검증 도중 발생한 환경/도구 문제도 성공 결과와 구분해 보관했다. 첫 제한 권한 실행은 라이선스 접근 실패, 첫 전체 임포트는 Unity 내부 assembly post-processing 요청 시간 초과로 중단했다. 그다음 실행은 Play Mode 자체가 PASS였으나 종료 시 로그 핸들 공유 문제로 전체 runner가 FAIL했다. 위 최종 실행은 필요한 로컬 권한·기존 임포트 캐시·보완된 로그 수집으로 새로 완료한 결과다.

최종 GPU 실행은 `output/validation/sw05-visuals-20261029/gpu-20261029-024024/`에 있다. Intel UHD Graphics / Direct3D12의 실제 Play Mode에서 **45 assertions PASS**, runtime Error/Exception/Assert 0, 원본/청백색 출력의 최대 픽셀 알파 차이 **0/255**를 확인했다. 8방향 모두 선명한 중심과 투명한 외곽을 유지했고, 잘림·오류 셰이더의 분홍색 픽셀이 없었다. 단일 대각선 전조에서 실제 swept hull 바깥 AABB 모서리는 투명하고 위험 영역 내부는 채워지는 것도 확인했다.

직접 확인한 QA 이미지:

- [동일 PNG의 기본 Material / 청백색 Material 비교](../output/validation/sw05-visuals-20261029/gpu-20261029-024024/aerial-sword-render.png)
- [8방향 렌더](../output/validation/sw05-visuals-20261029/gpu-20261029-024024/aerial-sword-directions.png)
- [기존 스타일 / SW05 새 스타일의 8경로 중첩](../output/validation/sw05-visuals-20261029/gpu-20261029-024024/aerial-sword-warning-overlap.png)

이 QA 배치의 누적 RGB 합은 10,754.14 → 5,349.66으로 줄었으며 전체 위험 hull은 동일하다. 첫 번째 비교는 동일 PNG와 동일 크기에 Material만 바꾼 비교이고, 기존 장식 레이어까지 포함한 플레이 영상 비교는 아니다.

중간 GPU 실행 `gpu-20261029-023300`은 Edit Mode에서 `Sprite.OverrideGeometry`가 거부되어 전조 채움이 AABB 사각형으로 대체되었다. 이것을 게임 런타임 결함으로 판정하지 않았다. 해당 실행의 PASS 표시는 최종 검증으로 인정하지 않고 `review-status.txt`에 무효 사유를 남겼다. 검증 도구를 실제 Play Mode로 옮긴 위 최종 실행에서 정확한 hull 렌더와 무오류를 다시 확인했으며, 이 조사 때문에 게임 코드를 추가로 변경하지 않았다.

재실행 명령은 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-sword-polish.ps1 -Suite all -TimeoutSeconds 1200`이다. 이번 최종 실행에는 안전 검사와 기존 verdict 보관을 수행하는 `-ReuseProjectDirectory`로 격리 임포트 캐시를 지정했다. GPU 검사는 준비된 동일 격리 사본에서 Unity의 `-executeMethod KarmaAerialSwordRenderChecks.Run`을 사용하며 `-nographics`를 제외한다. GPU 검증 도구 수정 후에도 위 전체 suite를 다시 완료했고, 최종 소스 72개의 해시가 현재 파일과 일치하는 것을 확인했다.

## 7. 실제 Unity 플레이에서 추가 확인할 부분

- 실제 게임 카메라 배율과 밝은/어두운 배경에서 청백 검기의 날끝·흰 중심 가독성.
- 기존 검기가 이동하는 동안 다음 묶음 전조가 표시될 때 방향을 바로 구별할 수 있는지.
- 벽 근처·상하·대각선에서 표시가 잘리지 않는지, 얇은 전체 위험 윤곽선이 충분히 보이는지.
- 정상 붉은 피격 Flash와 청백색 검기를 함께 보았을 때 피해 시점을 인지할 수 있는지.
- 실제 입력을 사용하는 여러 seed 플레이의 회피 체감과 프레임 안정성.

자동 검사는 연결·기하·피해/시간 보존·cleanup·물리 회피·GPU 출력에 대한 근거를 제공한다. 체감 가독성과 미술적 완성도는 실제 플레이 화면에서도 확인해야 한다.
