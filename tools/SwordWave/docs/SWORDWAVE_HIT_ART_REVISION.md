# 검기형 보스 피격·검기 아트 개선 보고

- 작업 브랜치: `feat/boss-sword2`
- 기준: 이번 작업 시작 시 작업 트리. 앞선 미커밋 변경은 유지.
- commit / push / merge / reset / restore / clean / stash 실행하지 않음.

## 1. 수정 범위

수정한 기존 런타임 파일:

- `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Combat.cs`
- `Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Presentation.cs`
- `Assets/K4RMA/Runtime/Combat/KarmaBossAttack.cs`
- `Assets/K4RMA/Runtime/Player/KarmaPlayer.Defense.cs`
- `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossBehavior.cs`
- `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/KarmaSwordBossSettings.cs`
- `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/SW03.cs`
- `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaWaveArtwork.cs`
- `Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Presentation/KarmaSwordScarRise.cs`

신규 런타임 파일: `SwordWave/Presentation/KarmaSwordHitFeedback.cs` 및 새 `.meta`.
검증 파일: `tests/Unity/SwordWave/KarmaSwordPlayChecks.cs`.
공통 파일 4곳은 검기형 피격 분기, 피해 확정 VFX 호출, 투사체의 발사 패턴 기록에 한정.
공개 플레이어 TakeDamage(float) API를 보존하고 내부 overload로 검기형 공격만 기존 사각 플래시를 생략.

## 2. 신규 PNG와 실제 연결

모두 `Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants/`에 저장.
기본 내장 imagegen으로 각각 새 원본 생성. 기존 이미지 복제·색상 변경·확대본이 아님.
기존 PNG와 meta는 변경하지 않고 새 PNG/meta 6쌍 추가.

| PNG | 적용 |
|---|---|
| SWBasic_Wave_v2.png | SWBasic 중거리 이동 투사체, 발사 순간 짧은 섬광 |
| SW01_StaggeredWave_v3.png | 낮은/높은 검기 2발 및 발사 섬광 |
| SW03_GroundScar_v4.png | 5구간 바닥 검흔 전조와 상승 순간 지면 잔광 |
| SW03_RisingBlade_v4.png | 실제 상승 검기 10개/차례 |
| SW03_HitFlash_v4.png | SW03 피해 확정 시에만 전용 피격 섬광 |
| Sword_HitBurst_v2.png | 보스 피격 오버레이, SWBasic/SW01 등 검기에 의한 플레이어 피해 확정 |

Resources 참조를 `KarmaWaveArtwork`에서 연결.
PPU 100, Single Sprite, Bilinear, mipmap 없음, 압축 없음, 최대 4096.
중심 발광부(alpha >= 128) 픽셀 경계 중심에 pivot을 맞추고 해당 경계를 기존 판정 크기에 매핑.
기존 SW02/SW04/SW05 이미지 참조 유지.
근거리 SWBasic은 기존 칼질 이미지를 유지.
이동 투사체는 속도 방향으로 회전하여 좌/우 양쪽에 같은 검날 방향 사용.
피격 섬광은 정렬 순서 40, 짧은 절삭 연출로 적용.

## 3. 피격 시 공격이 끊겨 보인 원인과 수정

`TakeDamage → ReceiveDamage → animator.Hurt()`가 매 피격마다 Hurt 예약을 갱신.
`GuardianPose`는 Hurt를 전조/공격 포즈보다 우선하므로 연타를 맞으면 공격 포즈가 계속 덮였음.
일반 피해 자체가 공격 코루틴을 중단하거나 패턴 시계를 초기화하는 경로는 없었음.

검기형 보스의 일반 피해는 `Hurt()` 대신 별도 VFX로 표시.
HP 감소·SW04 반격 요청·압박 기억은 유지.
기존 StopAllCoroutines/InterruptPatternCore/사망·패링·스턴 처리는 유지.
Guard/RisingSlash는 기존 Hurt 처리를 그대로 사용.
보스 섬광은 2.3×2.3 / 0.14초, 플레이어 섬광은 2.7×2.7 / 0.17초.
World 아래에서 짧게 끝나므로 같은 프레임의 패턴 정리에도 섬광이 잘리지 않음.
실제 렌더 검토에서 새 섬광과 겹치던 기존 흰 사각 플래시도 검기형 보스에서만 제거.
플레이어도 해당 검기형 공격의 실제 피격 때만 기존 빨간 사각 플래시를 생략하며 Hurt 포즈와 무적 시간은 유지.
SW04 독립 투사체는 보스의 다음 선택과 무관하게 발사 시 패턴을 기억하여 올바른 피격 연출 사용.
SW05 투사체는 원래 플레이어 피격 연출을 그대로 사용.
SW03은 별도 전용 피격 연출을 유지하며 공통 섬광을 중복 생성하지 않음.
무적/방어/패링으로 HP가 줄지 않았으면 플레이어 피해 섬광을 만들지 않음.

## 4. 거리 AI 변경 전후

| 항목 | 작업 시작 시 | 이번 작업 |
|---|---:|---:|
| 압박 후퇴 쿨다운 | 2.6초 | 2.4초 |
| 일반 후퇴 시작 거리 | 8.2 | 유지 |
| 피해 압박 시 목표 간격 | 9.5 | 유지 |
| 후퇴 이동 예산 / 시간 | 최대 9 / 1.05초 | 유지 |
| 후퇴 속도 상한 | 16 (플레이어 속도 ×1.15 이내) | 유지 |
| 가속 / 감속 | 56 / 72 | 유지 |
| 근접 평타 진입 / 유지 경계 | 7.0 / 7.2 | 유지 |
| 근접 / 검기 평타 쿨다운 상한 | 1.1초 / 1.5초 | 유지 |
| 후퇴 후 다음 판단 | 0.18~0.28초 | 유지 |

일반 타격 범위와 플레이어 실제 Collider를 사용하고, 전조 시작 전 평타 종류를 고정.
중거리에서 기본 검기가 준비되었고 직전 연속 평타가 없으면 우선 후보에 포함.
평타 2회 연속 뒤에는 특수 패턴 기회를 주며 기존 SW05 순환/지연 우선권을 유지.
후퇴는 공격 1회를 완료해야 다시 가능하므로 연속 도주를 방지.
후퇴 목적지는 평타 검기의 실제 도달 거리 안으로 제한하며, 직후 평타를 요청해 즉시 재접근하는 흐름을 방지.

## 5. SW03 보완

10구간 중 5구간, 구간당 검기 2개, 일반 2차례/강화 3차례 구조 유지.
차례당 10개, 전체 20/30개. 구간 판정 9×27, 검기 1개당 4.5×27 유지.
전조/상승 위치, 구간 경계 보정, 공유 피해 ledger, 안전지점 플래너 유지.
새 v4 이미지를 사용해 바닥 절삭선과 치솟는 검날·밑동 파편을 선명하게 표현.
활성 판정 첫 프레임부터 전체 높이가 보이며 짧은 내부 잔상만 상승.
SW03 피격 섬광은 실제 피해 ledger 확인 후 0.24초 실행.
위험 범위/개수/피해량/전조 시간은 이번에 추가로 올리지 않음.

## 6. 검증 결과

Unity 6000.6.2f1의 격리 프로젝트에서 검사. 원본 프로젝트를 Unity로 열지 않음.
- Editor 컴파일 통과, 실제 Windows Player 스크립트 컴파일 26개 어셈블리 통과. 실행 파일 패키징은 수행하지 않음.
- 최종 Play Mode **4,502개 assertion PASS**, Unity exit code 0. 게임 실행 중 감시된 오류/예외 없음.
- SW03 순수 플래너 **56,872개 계획 / 213개 배치 PASS**.
- 0.075초 간격 일반 피해에도 SWBasic 근접/중거리, SW01 2발, SW03 2/3차례, SW04 4발 유지.
- 강제 패링/스턴과 사망 시 공격 정리 유지.
- SW03 무적 중 거짓 피격 VFX 없음, 실제 피해에만 v4 섬광 표시.
- 신규 6종 PNG의 실제 Resources 참조, pivot/PPU/filter/compression, 좌우 방향 검사 통과.
- 검기형 보스/플레이어의 사각 플래시 제거, 다른 보스 Hurt 및 기존 플레이어 피해 API 연출 보존 검사 통과.
- 30/60/120 FPS 이동 경로의 속도 상한·가속·감속 검사 통과.
- 프레임별 근거리 추적(간격 5.5 유지, 12초): 공격 6회 중 근접 평타 5회, 후퇴 4회, 최소 관측 간격 **2.406초**.
- 중거리 관측(간격 8.7, 7초): 공격 3회 중 일반 검기 평타 2회.

15ms 외부 샘플링을 쓰던 중간 실행에서 후퇴 횟수 조건이 1회 실패했음.
추적 위치 갱신과 행동 관측을 게임 프레임 단위로 바꾸고 실패 시에도 최신 측정값을 먼저 저장하도록 보완.
최종 프레임 기록에서 후퇴 사이마다 실제 공격이 끼어 있으며, 벽 도달 후 SW02 공격으로 이어지는 것을 확인.
측정값은 자동 제어 시나리오의 결과이며 일반 수동 플레이 통계가 아님.

격리 Editor 시작 시 Unity Search 인덱서의 기존 ArgumentOutOfRangeException이 기록됨.
게임 실행 오류 감시는 테스트 시작 이후 별도로 수행했으며 최종 오류 없음.
최초 샌드박스 실행은 라이선스 IPC 연결 실패로 검증에 진입하지 못했고, 같은 격리 사본을 IPC 접근이 가능한 실행 경로로 재실행.

검증 기록:

- `output/validation/revision3/sword-play-validation.txt`
- `output/validation/revision3/sword-player-compilation.txt`
- `output/validation/revision3/sword-planner-validation.txt`
- `output/validation/revision3/sword-spacing-metrics.json`
- `output/validation/revision3/sword-spacing-trace.txt`
- `output/validation/revision3/sword-play-validation-sampling-failure.txt`
- `output/validation/unity-sword-revision3-framechecks.log`

렌더 스크린샷:

- [SW01 검기](../output/validation/revision3/sw01-release.png)
- [SWBasic 왼쪽 발사](../output/validation/revision3/swbasic-left.png)
- [SW03 전조](../output/validation/revision3/sw03-warning.png)
- [SW03 상승](../output/validation/revision3/sw03-rise.png)
- [SW03 피격](../output/validation/revision3/sw03-confirmed-hit.png)
- [보스 피격](../output/validation/revision3/sword-boss-hit.png)
- [플레이어 피격](../output/validation/revision3/swbasic-player-hit.png)

## 7. 보호 범위

SW05.cs / KarmaAerialSwordPlanner.cs / SW05 전용 에셋, Guard/RisingSlash 전용 파일 수정 없음.
SW01/SW02/SW04 패턴 실행 소스는 이번 작업에서 변경하지 않음.
기존 ProjectSettings/QualitySettings.asset 변경과 미추적 Editor/Resources 파일 보존.
기존 meta/GUID 변경 없음. 새 PNG와 신규 클래스만 새 GUID 부여.
기준 해시: `output/validation/revision3-baseline.json`.
기준 파일 315개 중 의도한 기존 파일 10개만 변경, 나머지 305개는 해시 동일.

## 8. 직접 플레이에서 추가 확인할 부분

자동화된 실제 Unity Play Mode와 렌더 이미지를 사용한 검증이며 사람의 연속 수동 플레이는 아님.
조작 중 느끼는 후퇴 빈도·공격 사이 호흡과 장시간 SW03 시인성은 팀의 실제 플레이로 최종 조정 권장.
원본 프로젝트의 실제 사용 Quality/후처리 프로필에서 밝기와 GPU 부담 확인 필요.
Settings/Quality나 타 보스 아트는 이를 위해 임의 변경하지 않음.

## 9. 이미지 생성 프롬프트

내장 imagegen 사용, 각 호출 `transparent_background: true`.
원본 생성 파일은 Codex generated_images에 보존하고 프로젝트로 복사.

### SWBasic_Wave_v2

```text
Use case: stylized-concept. Asset type: production transparent PNG VFX sprite for K4RMA 2D side-scrolling fantasy sword boss. Brand-new original art, icy white cutting core, cyan ribbons, saturated royal-blue rim, crisp sculpted energy facets, restrained soft bloom, fine trailing sparks. Clean readable silhouette at small game size. No text, no watermark, no UI, no ground scenery, no character, no black backdrop, genuine transparent alpha. Full sprite with transparent margins, never clipped. Wide horizontal image. One compact sharp right-moving magical sword slash, long tapered leftward trail and pointed rightward cutting face, slim forward crescent edge with single readable white spine. Modestly ornamented but powerful, elongated width:height about 4:1, luminous core occupies most of height. Suitable for horizontal mirroring. Less elaborate than a signature boss ultimate.
```

### SW01_StaggeredWave_v3

```text
Use case: stylized-concept. Asset type: production transparent PNG VFX sprite for K4RMA 2D side-scrolling fantasy sword boss. Brand-new original art, icy white cutting core, cyan ribbons, saturated royal-blue rim, crisp sculpted energy facets, restrained soft bloom, fine trailing sparks. Clean readable silhouette at small game size. No text, no watermark, no UI, no ground scenery, no character, no black backdrop, genuine transparent alpha. Full sprite with transparent margins, never clipped. Wide horizontal image. One huge signature boss rightward cutting wave, majestic elongated crescent-shaped blade head on right and long layered tapering afterimage ribbons trailing left, dynamic curved white cutting spine, serrated azure energy fins, few razor shards. More dramatic and ornate than basic wave. Overall solid energy silhouette width:height about 3:1, no detached fog bank.
```

### SW03_GroundScar_v4

```text
Use case: stylized-concept. Asset type: production transparent PNG VFX sprite for K4RMA 2D side-scrolling fantasy sword boss. Brand-new original art, icy white cutting core, cyan ribbons, saturated royal-blue rim, crisp sculpted energy facets, restrained soft bloom, fine trailing sparks. Clean readable silhouette at small game size. No text, no watermark, no UI, no ground scenery, no character, no black backdrop, genuine transparent alpha. Full sprite with transparent margins, never clipped. Very wide horizontal image. A single straight ground gouge viewed side-on for a platform game, long horizontal sword-carved fissure with a brilliant thin white-blue central cut, jagged faceted blue lips, small upward needle sparks and sparse stone chips. Flat base, clearly readable left/right limits, glowing line spans the whole width. Very low profile aspect 7:1. No large smoky haze or vertical explosion.
```

### SW03_RisingBlade_v4

```text
Use case: stylized-concept. Asset type: production transparent PNG VFX sprite for K4RMA 2D side-scrolling fantasy sword boss. Brand-new original art, icy white cutting core, cyan ribbons, saturated royal-blue rim, crisp sculpted energy facets, restrained soft bloom, fine trailing sparks. Clean readable silhouette at small game size. No text, no watermark, no UI, no ground scenery, no character, no black backdrop, genuine transparent alpha. Full sprite with transparent margins, never clipped. Tall portrait image. One monumental blade of magical sword energy erupting vertically upward from ground. Extremely long sharp spear-like point, sculpted asymmetrical cutting facets, white central razor, blue angular side blades curling upward from a flared foot, vertical energy streaks and a few rising shards. Not a plain cylinder, not a rectangle. Bright solid base at bottom, needle point at top. Entire energy silhouette about 1:5 width:height; readable edges rather than wide fog. Base must be visible, no horizon or scenery.
```

### SW03_HitFlash_v4

```text
Use case: stylized-concept. Asset type: production transparent PNG VFX sprite for K4RMA 2D side-scrolling fantasy sword boss. Brand-new original art, icy white cutting core, cyan ribbons, saturated royal-blue rim, crisp sculpted energy facets, restrained soft bloom, fine trailing sparks. Clean readable silhouette at small game size. No text, no watermark, no UI, no ground scenery, no character, no black backdrop, genuine transparent alpha. Full sprite with transparent margins, never clipped. Square image. A single explosive sword hit spark: diagonal crossing icy white razor slashes, a tight radiant core, angular electric-blue shards shooting outward along crossing cuts, short curved slicing arcs. Sharp high energy impact for being struck by ground-erupting blade, fine negative space between shards, compact rather than huge cloud. No circular fireball.
```

### Sword_HitBurst_v2

```text
Use case: stylized-concept. Asset type: production transparent PNG VFX sprite for K4RMA 2D side-scrolling fantasy sword boss. Brand-new original art, icy white cutting core, cyan ribbons, saturated royal-blue rim, crisp sculpted energy facets, restrained soft bloom, fine trailing sparks. Clean readable silhouette at small game size. No text, no watermark, no UI, no ground scenery, no character, no black backdrop, genuine transparent alpha. Full sprite with transparent margins, never clipped. Square image. A compact sword-on-body impact flash, asymmetric X of two clean white-blue cutting strokes, short crescent edge and 5-8 small sharp azure chips with tiny fading trails. Very short subtle overlay that leaves the attacking character readable. Tight center, minimal haze, crisp blue outline. Smaller and simpler than a ground eruption hit.
```

