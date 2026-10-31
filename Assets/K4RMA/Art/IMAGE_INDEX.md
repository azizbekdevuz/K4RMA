# 현재 게임 이미지 목록

현재 게임에서 사용하는 PNG **47개**. 이 목록과 CSV는 `python3 tools/update-artwork-index.py`로 갱신한다.

기준 목록: `Assets/K4RMA/Resources/KarmaImageCatalog.json`. 이미지 원본은 아래 경로의 PNG 한 벌로 관리한다.

플레이어: `Actions.png` 한 장에 웅크리기를 포함한 20개 동작. 검기는 캐릭터 포즈와 별도 VFX를 조합한다.

## player

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Characters/Player/Actions.png](Resources/K4RMAArtwork/Characters/Player/Actions.png) | 현재 플레이어 20동작 통합 시트: 이동·점프·공격·방어·피격·사망·웅크리기 | 20 |

## guardian-base

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Characters/Guardians/Base/Neutral.png](Resources/K4RMAArtwork/Characters/Guardians/Base/Neutral.png) | 기본 수호자 동작 시트 | 8 |
| [Resources/K4RMAArtwork/Characters/Guardians/Base/Wave.png](Resources/K4RMAArtwork/Characters/Guardians/Base/Wave.png) | 검기형 수호자 동작 시트 | 12 |
| [Resources/K4RMAArtwork/Characters/Guardians/Base/Rise.png](Resources/K4RMAArtwork/Characters/Guardians/Base/Rise.png) | 상승형 수호자 동작 시트 | 12 |
| [Resources/K4RMAArtwork/Characters/Guardians/Base/Ward.png](Resources/K4RMAArtwork/Characters/Guardians/Base/Ward.png) | 방어형 수호자 동작 시트 | 12 |

## effects

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Effects/Player.png](Resources/K4RMAArtwork/Effects/Player.png) | 플레이어 기술 이펙트 | 12 |
| [Resources/K4RMAArtwork/Effects/Guardian.png](Resources/K4RMAArtwork/Effects/Guardian.png) | 보스 공격 이펙트 | 12 |
| [Resources/K4RMAArtwork/Effects/Personalization.png](Resources/K4RMAArtwork/Effects/Personalization.png) | 고유화·계승 이펙트 | 12 |
| [Resources/K4RMAArtwork/Effects/Hit.png](Resources/K4RMAArtwork/Effects/Hit.png) | 피격·타격 이펙트 | 8 |

## guardian-reference

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Characters/Guardians/ReferencePoses.png](Resources/K4RMAArtwork/Characters/Guardians/ReferencePoses.png) | 계승 조합의 기준 외형·선택 창 미리보기 | 16 |

## props

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Props/Props.png](Resources/K4RMAArtwork/Props/Props.png) | 수련장 배경 소품 | 16 |

## ui

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/UI/Components.png](Resources/K4RMAArtwork/UI/Components.png) | 메뉴·체력바·선택 창 UI 요소 | 24 |
| [Resources/K4RMAArtwork/UI/MenuBackground.png](Resources/K4RMAArtwork/UI/MenuBackground.png) | 메뉴 배경 | 단일 이미지 |

## environment

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Environments/MountainDojo/Background.png](Resources/K4RMAArtwork/Environments/MountainDojo/Background.png) | 실외 도장 기준 배경 | 단일 이미지 |
| [Resources/K4RMAArtwork/Environments/MountainDojo/Panorama.png](Resources/K4RMAArtwork/Environments/MountainDojo/Panorama.png) | 실외 도장 가로 스크롤 배경 | 단일 이미지 |
| [Resources/K4RMAArtwork/Environments/TrialHall/Background.png](Resources/K4RMAArtwork/Environments/TrialHall/Background.png) | 실내 도장 기준 배경 | 단일 이미지 |
| [Resources/K4RMAArtwork/Environments/TrialHall/Panorama.png](Resources/K4RMAArtwork/Environments/TrialHall/Panorama.png) | 실내 도장 가로 스크롤 배경 | 단일 이미지 |

## guardian-revival

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Characters/Guardians/Revival/Neutral.png](Resources/K4RMAArtwork/Characters/Guardians/Revival/Neutral.png) | 기본 유형의 쓰러짐·일어나기 시트 | 6 |
| [Resources/K4RMAArtwork/Characters/Guardians/Revival/Rise.png](Resources/K4RMAArtwork/Characters/Guardians/Revival/Rise.png) | 기본 유형의 쓰러짐·일어나기 시트 | 6 |
| [Resources/K4RMAArtwork/Characters/Guardians/Revival/Ward.png](Resources/K4RMAArtwork/Characters/Guardians/Revival/Ward.png) | 기본 유형의 쓰러짐·일어나기 시트 | 6 |
| [Resources/K4RMAArtwork/Characters/Guardians/Revival/Wave.png](Resources/K4RMAArtwork/Characters/Guardians/Revival/Wave.png) | 기본 유형의 쓰러짐·일어나기 시트 | 6 |

## guardian-inherited

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/RiseFinal.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/RiseFinal.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/RiseWard.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/RiseWard.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/RiseWave.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/RiseWave.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/WardFinal.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/WardFinal.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/WardRise.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/WardRise.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/WardWave.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/WardWave.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/WaveFinal.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/WaveFinal.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/WaveRise.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/WaveRise.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |
| [Resources/K4RMAArtwork/Characters/Guardians/Inherited/WaveWard.png](Resources/K4RMAArtwork/Characters/Guardians/Inherited/WaveWard.png) | 계승 강화형 전투·피격·쓰러짐·부활 16포즈 | 16 |

## guard-pattern

| 파일 | 사용처 | 포즈 |
|---|---|---|
| [../Resources/K4RMAArtwork/Sheets/GD02/ParryRed.png](../Resources/K4RMAArtwork/Sheets/GD02/ParryRed.png) | GD02 강공격 전용 자세 또는 패링 예고 | 단일 이미지 |
| [../Resources/K4RMAArtwork/Sheets/GD02/Ward.png](../Resources/K4RMAArtwork/Sheets/GD02/Ward.png) | GD02 강공격 전용 자세 또는 패링 예고 | 3 |
| [../Resources/K4RMAArtwork/Sheets/GD02/WardFinal.png](../Resources/K4RMAArtwork/Sheets/GD02/WardFinal.png) | GD02 강공격 전용 자세 또는 패링 예고 | 3 |
| [../Resources/K4RMAArtwork/Sheets/GD02/WardRise.png](../Resources/K4RMAArtwork/Sheets/GD02/WardRise.png) | GD02 강공격 전용 자세 또는 패링 예고 | 3 |
| [../Resources/K4RMAArtwork/Sheets/GD02/WardWave.png](../Resources/K4RMAArtwork/Sheets/GD02/WardWave.png) | GD02 강공격 전용 자세 또는 패링 예고 | 3 |
| [../Resources/K4RMAArtwork/Sheets/GD03/HeatedWholeShield.png](../Resources/K4RMAArtwork/Sheets/GD03/HeatedWholeShield.png) | GD03 달궈진 방패 및 추적·복귀 파편 | 1 |
| [../Resources/K4RMAArtwork/Sheets/GD03/ShardDB.png](../Resources/K4RMAArtwork/Sheets/GD03/ShardDB.png) | GD03 달궈진 방패 및 추적·복귀 파편 | 2 |
| [../Resources/K4RMAArtwork/Sheets/GuardBash.png](../Resources/K4RMAArtwork/Sheets/GuardBash.png) | 방어형 전용 전투 효과 | 1 |
| [../Resources/K4RMAArtwork/Sheets/GuardPillar.png](../Resources/K4RMAArtwork/Sheets/GuardPillar.png) | 방어형 전용 전투 효과 | 1 |
| [../Resources/K4RMAArtwork/Sheets/GuardSeal.png](../Resources/K4RMAArtwork/Sheets/GuardSeal.png) | 방어형 전용 전투 효과 | 1 |
| [../Resources/K4RMAArtwork/Sheets/GuardShard.png](../Resources/K4RMAArtwork/Sheets/GuardShard.png) | 방어형 전용 전투 효과 | 1 |
| [../Resources/K4RMAArtwork/Sheets/GuardShield.png](../Resources/K4RMAArtwork/Sheets/GuardShield.png) | 방어형 전용 전투 효과 | 1 |
| [../Resources/K4RMAArtwork/Sheets/GuardWave.png](../Resources/K4RMAArtwork/Sheets/GuardWave.png) | 방어형 전용 전투 효과 | 1 |
| [../Resources/K4RMAArtwork/Sheets/Stagger/Ward.png](../Resources/K4RMAArtwork/Sheets/Stagger/Ward.png) | 방어형 패링 성공 경직 및 체형 기준 | 2 |
| [../Resources/K4RMAArtwork/Sheets/Stagger/WardFinal.png](../Resources/K4RMAArtwork/Sheets/Stagger/WardFinal.png) | 방어형 패링 성공 경직 및 체형 기준 | 2 |
| [../Resources/K4RMAArtwork/Sheets/Stagger/WardRise.png](../Resources/K4RMAArtwork/Sheets/Stagger/WardRise.png) | 방어형 패링 성공 경직 및 체형 기준 | 2 |
| [../Resources/K4RMAArtwork/Sheets/Stagger/WardWave.png](../Resources/K4RMAArtwork/Sheets/Stagger/WardWave.png) | 방어형 패링 성공 경직 및 체형 기준 | 2 |

## 이름으로 선택하는 포즈

- `sheet/Player`: Idle, Run, Jump, Apex, Fall, Dash, Land, Windup, Slash, FollowThrough, Rising, Guard, Parry, Hurt, Death, Recovery, Pierce, AirSlash, Riposte, Crouch
- `sheet/Neutral`: Idle, Move, Windup, Slash, Recovery, Hurt, Kneel, Death
- `sheet/Wave`: Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death
- `sheet/Rise`: Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death
- `sheet/Ward`: Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death
- `sheet/PlayerVFX`: Slash, FollowThrough, Wave, Rising, AirSlash, Parry, Guard, Barrier, Counter, Pierce, PierceTrail, Recovery
- `sheet/GuardianVFX`: Wave, AimedWave, Rising, Cleave, Ward, Counter, GroundRing, AirSlash, Landing, Blade, Pillar, Beam
- `sheet/PersonalizationVFX`: Pierce, Rising, Ward, Core, Sweep, Counter, Pillar, Awaken, Trail, AirSlash, Impact, Fusion
- `sheet/HitVFX`: Spark, Burst, Cross, Crescent, Wood, Slash, Ring, Wave
- `sheet/Inheritance`: Wave, WaveRise, WaveWard, WaveFinal, Rise, RiseWave, RiseWard, RiseFinal, Ward, WardWave, WardRise, WardFinal, Neutral, SwordPart, RisePart, WardPart
- `sheet/Props`: Lantern, TrainingPost, WaveBanner, RiseBanner, WardBanner, Steps, Well, Rope, Gate, Wood, LeftTree, RightTree, Rail, Leaves, Rocks, Bush
- `sheet/UI`: Health, HealthOutline, Crescent, BossTrack, BossFill, Choice, Wave, Rise, Ward, Pierce, Air, Parry, Divider, DividerDiamond, EmptySlot, FilledSlot, Cooldown, DividerLong, Accent, PanelCorner, ShortRule, LongRule, VerticalRule, Spark
- `revival/Neutral`: Revive00, Revive01, Revive02, Revive03, Revive04, Revive05
- `revival/Rise`: Revive00, Revive01, Revive02, Revive03, Revive04, Revive05
- `revival/Ward`: Revive00, Revive01, Revive02, Revive03, Revive04, Revive05
- `revival/Wave`: Revive00, Revive01, Revive02, Revive03, Revive04, Revive05
- `inherited/RiseFinal`: Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/RiseWard`: Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/RiseWave`: Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/WardFinal`: Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/WardRise`: Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/WardWave`: Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/WaveFinal`: Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/WaveRise`: Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `inherited/WaveWard`: Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand
- `guard/GD02/Ward`: Windup, Downstroke, FollowThrough
- `guard/GD02/WardFinal`: Windup, Downstroke, FollowThrough
- `guard/GD02/WardRise`: Windup, Downstroke, FollowThrough
- `guard/GD02/WardWave`: Windup, Downstroke, FollowThrough
- `guard/GD03/HeatedWholeShield`: Hot
- `guard/GD03/ShardDB`: Hot, Cooled
- `sheet/GuardBash`: Cleave
- `sheet/GuardPillar`: Pillar
- `sheet/GuardSeal`: GroundRing
- `sheet/GuardShard`: Ward
- `sheet/GuardShield`: ShieldRing
- `sheet/GuardWave`: Wave
- `guard/Stagger/Ward`: Reference, Stagger
- `guard/Stagger/WardFinal`: Reference, Stagger
- `guard/Stagger/WardRise`: Reference, Stagger
- `guard/Stagger/WardWave`: Reference, Stagger
