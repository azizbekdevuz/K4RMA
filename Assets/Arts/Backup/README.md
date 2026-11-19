# 통합 전 이미지 사용처

기준은 12 FPS 이미지 통합 직전의 `feat/2d-prototype` 커밋 `14e57565`이다. 표의 이전 경로는 당시 `Assets/Arts/Resources/`를 기준으로 한다. 백업 파일을 누르면 원본 이미지를 열 수 있다. 프레임 이름은 당시 Unity 슬라이스 이름이며 번호가 붙은 시트는 재생 순서대로 표기했다. 여러 자세를 담은 아틀라스는 동작에 따라 해당 슬라이스를 선택했다.

공식 외형 원화는 [GameScale_HD_StageStyle](../Reference)에 있다.

[Player](#player-53개) · [Master](#master-1개) · [Neutral](#neutral-3개) · [SwordWave](#swordwave-32개) · [RisingSlash](#risingslash-30개) · [Guard](#guard-42개) · [Shared](#shared-5개)

## Player (53개)

### Player · 동작

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Player_ActionPoses](Player/Player_ActionPoses.png) | 플레이어 생성 시 외형 및 공통 동작 포즈 · `Player` | `Player/Actions.png` | Idle, Run, Jump, Apex, Fall, Dash, Land, Windup, Slash, FollowThrough, Rising, Guard, Parry, Hurt, Death, Recovery, Pierce, AirSlash, Riposte, Crouch |
| [Player_AirDiagonal_24fps](Player/Player_AirDiagonal_24fps.png) | 플레이어 공중 대각 공격 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirDiagonal.png` | 4장 · AirDiagonal_00 → AirDiagonal_03 |
| [Player_AirDown_24fps](Player/Player_AirDown_24fps.png) | 플레이어 공중 하단 공격 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirDown.png` | 4장 · AirDown_00 → AirDown_03 |
| [Player_AirHorizontal_24fps](Player/Player_AirHorizontal_24fps.png) | 플레이어 공중 수평 공격 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirHorizontal.png` | 4장 · AirHorizontal_00 → AirHorizontal_03 |
| [Player_AirHover_24fps](Player/Player_AirHover_24fps.png) | 플레이어 공중 체공 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirHover.png` | AirHover_00, AirHover_01 |
| [Player_AirKnockback_24fps](Player/Player_AirKnockback_24fps.png) | 플레이어 공중 넉백 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirKnockback.png` | 6장 · AirKnockback_00 → AirKnockback_05 |
| [Player_AirRecovery_24fps](Player/Player_AirRecovery_24fps.png) | 플레이어 공중 공격 후 복귀 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirRecovery.png` | 4장 · AirRecovery_00 → AirRecovery_03 |
| [Player_AirUp_24fps](Player/Player_AirUp_24fps.png) | 플레이어 공중 상단 공격 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirUp.png` | 4장 · AirUp_00 → AirUp_03 |
| [Player_AirborneApex_24fps](Player/Player_AirborneApex_24fps.png) | 플레이어 공중 피격 정점 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirborneApex.png` | 4장 · AirborneApex_00 → AirborneApex_03 |
| [Player_AirborneFall_24fps](Player/Player_AirborneFall_24fps.png) | 플레이어 공중 피격 낙하 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirborneFall.png` | 4장 · AirborneFall_00 → AirborneFall_03 |
| [Player_AirborneRise_24fps](Player/Player_AirborneRise_24fps.png) | 플레이어 공중 피격 상승 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/AirborneRise.png` | 4장 · AirborneRise_00 → AirborneRise_03 |
| [Player_Attack_24fps](Player/Player_Attack_24fps.png) | 플레이어 기본 공격 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Attack.png` | 6장 · Attack_00 → Attack_05 |
| [Player_Crouch_24fps](Player/Player_Crouch_24fps.png) | 플레이어 웅크리기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Crouch.png` | 3장 · Crouch_00 → Crouch_02 |
| [Player_Death_24fps](Player/Player_Death_24fps.png) | 플레이어 사망 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Death.png` | 16장 · Death_00 → Death_15 |
| [Player_DownLoop_24fps](Player/Player_DownLoop_24fps.png) | 플레이어 넘어진 상태 유지 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/DownLoop.png` | 4장 · DownLoop_00 → DownLoop_03 |
| [Player_Fall_24fps](Player/Player_Fall_24fps.png) | 엔딩 낙하 전용 시트 · `KarmaPlayer.Ending / BeginEndingPoses` | `Player/Native24/Fall.png` | 4장 · Fall_00 → Fall_03 |
| [Player_GD04_AirborneHit](Player/Player_GD04_AirborneHit.png) | GD04 지진 도약·착지 또는 플레이어 공중 피격 · `GuardPlayerAirborneHit` | `Guard/GD04/PlayerAirborneHit.png` | 4장 · Frame00 → Frame03 |
| [Player_GetUp_24fps](Player/Player_GetUp_24fps.png) | 플레이어 기상 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/GetUp.png` | 12장 · GetUp_00 → GetUp_11 |
| [Player_Guard_24fps](Player/Player_Guard_24fps.png) | 플레이어 방어 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Guard.png` | 6장 · Guard_00 → Guard_05 |
| [Player_Hurt_24fps](Player/Player_Hurt_24fps.png) | 플레이어 피격 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Hurt.png` | 4장 · Hurt_00 → Hurt_03 |
| [Player_Idle_24fps](Player/Player_Idle_24fps.png) | 플레이어 대기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Idle.png` | 24장 · Idle_00 → Idle_23 |
| [Player_Jump_24fps](Player/Player_Jump_24fps.png) | 전투 점프 시작·상승·정점·낙하에 함께 쓰던 시트 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Jump.png` | 9장 · Jump_00 → Jump_08 |
| [Player_Kneel_24fps](Player/Player_Kneel_24fps.png) | 플레이어 무릎 꿇기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Kneel.png` | 8장 · Kneel_00 → Kneel_07 |
| [Player_Knockback_24fps](Player/Player_Knockback_24fps.png) | 플레이어 넉백 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Knockback.png` | 6장 · Knockback_00 → Knockback_05 |
| [Player_Knockdown_24fps](Player/Player_Knockdown_24fps.png) | 플레이어 넘어짐 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Knockdown.png` | 8장 · Knockdown_00 → Knockdown_07 |
| [Player_Land_24fps](Player/Player_Land_24fps.png) | 플레이어 착지 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Land.png` | Land_00, Land_01 |
| [Player_ParrySuccess](Player/Player_ParrySuccess.png) | 패링 성공 시 캐릭터가 취하는 반격 자세 · `KarmaCharacterAnimator.SampleParrySuccess` | `Player/ParrySuccess.png` | 8장 · Frame00 → Frame07 |
| [Player_Parry_24fps](Player/Player_Parry_24fps.png) | 플레이어 패링 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Parry.png` | 6장 · Parry_00 → Parry_05 |
| [Player_PersonalizeGuard_24fps](Player/Player_PersonalizeGuard_24fps.png) | 플레이어 방어 고유화 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/PersonalizeGuard.png` | 12장 · PersonalizeGuard_00 → PersonalizeGuard_11 |
| [Player_PersonalizeRising_24fps](Player/Player_PersonalizeRising_24fps.png) | 플레이어 상승베기 고유화 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/PersonalizeRising.png` | 12장 · PersonalizeRising_00 → PersonalizeRising_11 |
| [Player_PersonalizeWave_24fps](Player/Player_PersonalizeWave_24fps.png) | 플레이어 검기 고유화 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/PersonalizeWave.png` | 12장 · PersonalizeWave_00 → PersonalizeWave_11 |
| [Player_PierceDiagonal_24fps](Player/Player_PierceDiagonal_24fps.png) | 플레이어 대각선 찌르기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/PierceDiagonal.png` | 6장 · PierceDiagonal_00 → PierceDiagonal_05 |
| [Player_PierceDown_24fps](Player/Player_PierceDown_24fps.png) | 플레이어 하단 찌르기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/PierceDown.png` | 6장 · PierceDown_00 → PierceDown_05 |
| [Player_PierceUp_24fps](Player/Player_PierceUp_24fps.png) | 플레이어 상단 찌르기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/PierceUp.png` | 6장 · PierceUp_00 → PierceUp_05 |
| [Player_Pierce_24fps](Player/Player_Pierce_24fps.png) | 플레이어 찌르기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Pierce.png` | 6장 · Pierce_00 → Pierce_05 |
| [Player_Recovery_24fps](Player/Player_Recovery_24fps.png) | 플레이어 공격 후 회복 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Recovery.png` | 6장 · Recovery_00 → Recovery_05 |
| [Player_Riposte_24fps](Player/Player_Riposte_24fps.png) | 플레이어 반격 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Riposte.png` | 8장 · Riposte_00 → Riposte_07 |
| [Player_Rising_24fps](Player/Player_Rising_24fps.png) | 플레이어 상승베기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Rising.png` | 6장 · Rising_00 → Rising_05 |
| [Player_RunRecovery_24fps](Player/Player_RunRecovery_24fps.png) | 플레이어 달리기 감속 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/RunRecovery.png` | 8장 · RunRecovery_00 → RunRecovery_07 |
| [Player_Run_24fps](Player/Player_Run_24fps.png) | 플레이어 달리기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Run.png` | 13장 · Run_00 → Run_12 |
| [Player_Salute_24fps](Player/Player_Salute_24fps.png) | 플레이어 경례 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Salute.png` | 16장 · Salute_00 → Salute_15 |
| [Player_ShockLoop_24fps](Player/Player_ShockLoop_24fps.png) | 플레이어 감전 유지 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/ShockLoop.png` | 8장 · ShockLoop_00 → ShockLoop_07 |
| [Player_ShockRecovery_24fps](Player/Player_ShockRecovery_24fps.png) | 플레이어 감전 해제 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/ShockRecovery.png` | 6장 · ShockRecovery_00 → ShockRecovery_05 |
| [Player_ShockStart_24fps](Player/Player_ShockStart_24fps.png) | 플레이어 감전 시작 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/ShockStart.png` | 4장 · ShockStart_00 → ShockStart_03 |
| [Player_StandFromKneel_24fps](Player/Player_StandFromKneel_24fps.png) | 플레이어 무릎 꿇은 상태에서 일어서기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/StandFromKneel.png` | 8장 · StandFromKneel_00 → StandFromKneel_07 |
| [Player_StunLoop_24fps](Player/Player_StunLoop_24fps.png) | 플레이어 기절 유지 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/StunLoop.png` | 8장 · StunLoop_00 → StunLoop_07 |
| [Player_StunRecovery_24fps](Player/Player_StunRecovery_24fps.png) | 플레이어 기절 해제 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/StunRecovery.png` | 6장 · StunRecovery_00 → StunRecovery_05 |
| [Player_StunStart_24fps](Player/Player_StunStart_24fps.png) | 플레이어 기절 시작 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/StunStart.png` | 4장 · StunStart_00 → StunStart_03 |
| [Player_SwordWave_24fps](Player/Player_SwordWave_24fps.png) | 플레이어 검기 발사 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/SwordWave.png` | 6장 · SwordWave_00 → SwordWave_05 |
| [Player_Turn_24fps](Player/Player_Turn_24fps.png) | 플레이어 방향 전환 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Turn.png` | 4장 · Turn_00 → Turn_03 |
| [Player_Walk_24fps](Player/Player_Walk_24fps.png) | 플레이어 걷기 동작 · `KarmaCharacterAnimator / LoadNativePlayerPose` | `Player/Native24/Walk.png` | 16장 · Walk_00 → Walk_15 |

### Player · 이펙트

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Player_ParrySuccess](Player/Effects/Player_ParrySuccess.png) | 플레이어 패링 성공 섬광 · `KarmaCharacterAnimator.SampleParrySuccess` | `Shared/Effects/ParrySuccess.png` | 8장 · Frame00 → Frame07 |
| [Player_SkillEffects](Player/Effects/Player_SkillEffects.png) | 플레이어 스킬 효과 · `PlayerVFX` | `Shared/Effects/Player.png` | Slash, FollowThrough, Wave, Rising, AirSlash, Parry, Guard, Barrier, Counter, Pierce, PierceTrail, Recovery |

## Master (1개)

### Master · 동작

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Master_TutorialAndEndingIdle](Master/Master_TutorialAndEndingIdle.png) | 튜토리얼 스승과 엔딩 등장 시 기본 자세 · `KarmaTutorialStage.Picture / KarmaEndingPresentation` | `Tutorial/Master.png` | 단일 이미지 |

## Neutral (3개)

### Neutral · 동작

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Neutral_FormPoses](Neutral/Neutral_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `Neutral/Forms/Neutral.png` | Idle, Move, Windup, Slash, Recovery, Hurt, Kneel, Death |
| [Neutral_Hesitate](Neutral/Neutral_Hesitate.png) | 첫 성장 연출에서 손을 뻗고 망설이는 동작 · `KarmaGuardianGrowth` | `Neutral/Growth/Hesitate.png` | Hesitate_0 |
| [Neutral_Revival](Neutral/Neutral_Revival.png) | 패배 후 부활 · `KarmaArtwork.RevivalPoses / KarmaGuardianGrowth` | `Neutral/Revival/Neutral.png` | 6장 · Revive00 → Revive05 |

## SwordWave (32개)

### SwordWave · Wave

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Wave_FormPoses](SwordWave/Wave/Wave_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `SwordWave/Forms/Wave.png` | Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death |
| [Wave_InspectWeapon](SwordWave/Wave/Wave_InspectWeapon.png) | 계승 연출 중 무기 관찰 · `KarmaGuardianGrowth` | `SwordWave/Growth/Reflection.png` | Reflection_0 |
| [Wave_Revival](SwordWave/Wave/Wave_Revival.png) | 패배 후 부활 · `KarmaArtwork.RevivalPoses / KarmaGuardianGrowth` | `SwordWave/Revival/Wave.png` | 6장 · Revive00 → Revive05 |
| [Wave_SW02_Transitions](SwordWave/Wave/Wave_SW02_Transitions.png) | SW02 조준·발사 전환 · `KarmaCharacterAnimator / SW02 전환` | `SwordWave/SW02/Wave_SW02_Transitions.png` | Prepare01, Prepare02, Swing01, Swing02, Recover01, Recover02 |

### SwordWave · WaveFinal

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WaveFinal_FormPoses](SwordWave/WaveFinal/WaveFinal_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `SwordWave/Forms/WaveFinal.png` | Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |
| [WaveFinal_SW02_Transitions](SwordWave/WaveFinal/WaveFinal_SW02_Transitions.png) | SW02 조준·발사 전환 · `KarmaCharacterAnimator / SW02 전환` | `SwordWave/SW02/WaveFinal_SW02_Transitions.png` | Prepare01, Prepare02, Swing01, Swing02, Recover01, Recover02 |

### SwordWave · WaveRise

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WaveRise_FormPoses](SwordWave/WaveRise/WaveRise_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `SwordWave/Forms/WaveRise.png` | Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |
| [WaveRise_SW02_Transitions](SwordWave/WaveRise/WaveRise_SW02_Transitions.png) | SW02 조준·발사 전환 · `KarmaCharacterAnimator / SW02 전환` | `SwordWave/SW02/WaveRise_SW02_Transitions.png` | Prepare01, Prepare02, Swing01, Swing02, Recover01, Recover02 |

### SwordWave · WaveWard

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WaveWard_FormPoses](SwordWave/WaveWard/WaveWard_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `SwordWave/Forms/WaveWard.png` | Idle, Side, Back, Move, Windup, Slash, FollowThrough, Wave, Guard, Rising, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |
| [WaveWard_SW02_Transitions](SwordWave/WaveWard/WaveWard_SW02_Transitions.png) | SW02 조준·발사 전환 · `KarmaCharacterAnimator / SW02 전환` | `SwordWave/SW02/WaveWard_SW02_Transitions.png` | Prepare01, Prepare02, Swing01, Swing02, Recover01, Recover02 |

### SwordWave · 이펙트

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [SwordWave_SW01_SW02_EnergyFrames](SwordWave/Effects/SwordWave_SW01_SW02_EnergyFrames.png) | 전투 효과 SW01_SW02_EnergyFrames · `SwordBossVFX` | `SwordWave/Shared/SW01_SW02_EnergyFrames.png` | SwordEnergy_Charge_0, SwordEnergy_Charge_1, SwordEnergy_Charge_2, SwordEnergy_Charge_3, SwordEnergy_Charge_4, SwordEnergy_SW01Flight_0, SwordEnergy_SW01Flight_1, SwordEnergy_SW01Flight_2, SwordEnergy_SW01Flight_3, SwordEnergy_SW01Flight_4, SwordEnergy_SW02Flight_0, SwordEnergy_SW02Flight_1, SwordEnergy_SW02Flight_2, SwordEnergy_SW02Flight_3, SwordEnergy_SW02Flight_4, SwordEnergy_Impact_0, SwordEnergy_Impact_1, SwordEnergy_Impact_2, SwordEnergy_Impact_3, SwordEnergy_Impact_4 |
| [SwordWave_SW01_StaggeredWave](SwordWave/Effects/SwordWave_SW01_StaggeredWave.png) | 전투 효과 SW01_StaggeredWave · `SwordBossVFX` | `SwordWave/SW01/SW01_StaggeredWave.png` | SW01_StaggeredWave_0 |
| [SwordWave_SW03_GroundImpact](SwordWave/Effects/SwordWave_SW03_GroundImpact.png) | 전투 효과 SW03_GroundImpact · `SwordBossVFX` | `SwordWave/SW03/SW03_GroundImpact.png` | SW03_GroundImpact_0 |
| [SwordWave_SW03_GroundScar_5f](SwordWave/Effects/SwordWave_SW03_GroundScar_5f.png) | 전투 효과 SW03_GroundScar_5f · `SwordBossVFX` | `SwordWave/SW03/SW03_GroundScar_5f.png` | 5장 · SW03_GroundScar_5f_0 → SW03_GroundScar_5f_4 |
| [SwordWave_SW03_GroundScar_v7](SwordWave/Effects/SwordWave_SW03_GroundScar_v7.png) | 전투 효과 SW03_GroundScar_v7 · `SwordBossVFX` | `SwordWave/SW03/SW03_GroundScar_v7.png` | SW03_GroundScar_v7_0 |
| [SwordWave_SW03_HitCut_5f](SwordWave/Effects/SwordWave_SW03_HitCut_5f.png) | 전투 효과 SW03_HitCut_5f · `SwordBossVFX` | `SwordWave/SW03/SW03_HitCut_5f.png` | 5장 · SW03_HitCut_5f_0 → SW03_HitCut_5f_4 |
| [SwordWave_SW03_RisingBlade_Tall_5f](SwordWave/Effects/SwordWave_SW03_RisingBlade_Tall_5f.png) | 전투 효과 SW03_RisingBlade_Tall_5f · `SwordBossVFX` | `SwordWave/SW03/SW03_RisingBlade_Tall_5f.png` | 5장 · SW03_RisingBlade_Tall_5f_0 → SW03_RisingBlade_Tall_5f_4 |
| [SwordWave_SW03_RisingBlade_v7](SwordWave/Effects/SwordWave_SW03_RisingBlade_v7.png) | 전투 효과 SW03_RisingBlade_v7 · `SwordBossVFX` | `SwordWave/SW03/SW03_RisingBlade_v7.png` | SW03_RisingBlade_v7_0 |
| [SwordWave_SW04_CounterBlade_v6](SwordWave/Effects/SwordWave_SW04_CounterBlade_v6.png) | 전투 효과 SW04_CounterBlade_v6 · `SwordBossVFX` | `SwordWave/SW04/SW04_CounterBlade_v6.png` | SW04_CounterBlade_v6_0 |
| [SwordWave_SW04_OrbitingSwordGuard_16f](SwordWave/Effects/SwordWave_SW04_OrbitingSwordGuard_16f.png) | 전투 효과 SW04_OrbitingSwordGuard_16f · `SwordBossVFX` | `SwordWave/SW04/SW04_OrbitingSwordGuard_16f.png` | 16장 · SW04_OrbitingSwordGuard_16f_0 → SW04_OrbitingSwordGuard_16f_15 |
| [SwordWave_SW04_OrbitingSwordGuard_Base](SwordWave/Effects/SwordWave_SW04_OrbitingSwordGuard_Base.png) | 전투 효과 SW04_OrbitingSwordGuard_Base · `SwordBossVFX` | `SwordWave/SW04/SW04_OrbitingSwordGuard_Base.png` | SW04_OrbitingSwordGuard_Base_0 |
| [SwordWave_SW05GroundImpact_v2](SwordWave/Effects/SwordWave_SW05GroundImpact_v2.png) | SW05 착지 지면 충격 4프레임 · `KarmaSW05VFX / SW05_Impact` | `SwordWave/SW05/SW05GroundImpact_v2.png` | 4장 · SW05_Impact_0 → SW05_Impact_3 |
| [SwordWave_SW05_AerialSword](SwordWave/Effects/SwordWave_SW05_AerialSword.png) | 전투 효과 SW05_AerialSword · `SwordBossVFX` | `SwordWave/SW05/SW05_AerialSword.png` | SW05_AerialSword_0 |
| [SwordWave_SW05_FallingSwordAura_6f](SwordWave/Effects/SwordWave_SW05_FallingSwordAura_6f.png) | 전투 효과 SW05_FallingSwordAura_6f · `SwordBossVFX` | `SwordWave/SW05/SW05_FallingSwordAura_6f.png` | 6장 · SW05_FallingSwordAura_6f_0 → SW05_FallingSwordAura_6f_5 |
| [SwordWave_SW05_FinalDetonation_8f](SwordWave/Effects/SwordWave_SW05_FinalDetonation_8f.png) | 전투 효과 SW05_FinalDetonation_8f · `SwordBossVFX` | `SwordWave/SW05/SW05_FinalDetonation_8f.png` | 8장 · SW05_FinalDetonation_8f_0 → SW05_FinalDetonation_8f_7 |
| [SwordWave_SW05_HeavyJudgementSword_optional](SwordWave/Effects/SwordWave_SW05_HeavyJudgementSword_optional.png) | 전투 효과 SW05_HeavyJudgementSword_optional · `SwordBossVFX` | `SwordWave/SW05/SW05_HeavyJudgementSword_optional.png` | SW05_HeavyJudgementSword_optional_0 |
| [SwordWave_SW05_LandingShock_6f](SwordWave/Effects/SwordWave_SW05_LandingShock_6f.png) | 전투 효과 SW05_LandingShock_6f · `SwordBossVFX` | `SwordWave/SW05/SW05_LandingShock_6f.png` | 6장 · SW05_LandingShock_6f_0 → SW05_LandingShock_6f_5 |
| [SwordWave_Sword_Crescent_v6](SwordWave/Effects/SwordWave_Sword_Crescent_v6.png) | 전투 효과 Sword_Crescent_v6 · `SwordBossVFX` | `SwordWave/Shared/Sword_Crescent_v6.png` | Sword_Crescent_v6_0 |
| [SwordWave_Sword_GuardRing_v6](SwordWave/Effects/SwordWave_Sword_GuardRing_v6.png) | 전투 효과 Sword_GuardRing_v6 · `SwordBossVFX` | `SwordWave/Shared/Sword_GuardRing_v6.png` | Sword_GuardRing_v6_0 |
| [SwordWave_Sword_Impact_v6](SwordWave/Effects/SwordWave_Sword_Impact_v6.png) | 전투 효과 Sword_Impact_v6 · `SwordBossVFX` | `SwordWave/Shared/Sword_Impact_v6.png` | Sword_Impact_v6_0 |
| [SwordWave_Sword_Release_v6](SwordWave/Effects/SwordWave_Sword_Release_v6.png) | 전투 효과 Sword_Release_v6 · `SwordBossVFX` | `SwordWave/Shared/Sword_Release_v6.png` | Sword_Release_v6_0 |
| [SwordWave_Sword_Shard_v6](SwordWave/Effects/SwordWave_Sword_Shard_v6.png) | 전투 효과 Sword_Shard_v6 · `SwordBossVFX` | `SwordWave/Shared/Sword_Shard_v6.png` | Sword_Shard_v6_0 |

## RisingSlash (30개)

### RisingSlash · Rise

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [RiseBasicStrike](RisingSlash/Rise/RiseBasicStrike.png) | 상승베기형 RiseBasicStrike 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseBasicStrike.png` | 6장 · Strike00 → Strike05 |
| [RiseDescent](RisingSlash/Rise/RiseDescent.png) | 상승베기형 RiseDescent 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseDescent.png` | Drop00, Drop01, Drop02, Drop03, Drop04, Drop05, Drop06, Land00, Land01 |
| [RisePursuit](RisingSlash/Rise/RisePursuit.png) | 상승베기형 RisePursuit 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RisePursuit.png` | 3장 · Frame00 → Frame02 |
| [Rise_FormPoses](RisingSlash/Rise/Rise_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/Rise.png` | Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death |
| [Rise_InspectWeapon](RisingSlash/Rise/Rise_InspectWeapon.png) | 계승 연출 중 무기 관찰 · `KarmaGuardianGrowth` | `RisingSlash/Growth/Reflection.png` | Reflection_0 |
| [Rise_Revival](RisingSlash/Rise/Rise_Revival.png) | 패배 후 부활 · `KarmaArtwork.RevivalPoses / KarmaGuardianGrowth` | `RisingSlash/Revival/Rise.png` | 6장 · Revive00 → Revive05 |

### RisingSlash · RiseFinal

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [RiseFinalCrossing](RisingSlash/RiseFinal/RiseFinalCrossing.png) | 상승베기형 RiseFinalCrossing 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseFinalCrossing.png` | 3장 · Frame00 → Frame02 |
| [RiseFinalDescent](RisingSlash/RiseFinal/RiseFinalDescent.png) | 상승베기형 RiseFinalDescent 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseFinalDescent.png` | Drop00, Drop01, Drop02, Land00, Land01 |
| [RiseFinalStrike](RisingSlash/RiseFinal/RiseFinalStrike.png) | 상승베기형 RiseFinalStrike 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseFinalStrike.png` | 3장 · Frame00 → Frame02 |
| [RiseFinalWaveRelease](RisingSlash/RiseFinal/RiseFinalWaveRelease.png) | 상승베기형 RiseFinalWaveRelease 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseFinalWaveRelease.png` | 3장 · Frame00 → Frame02 |
| [RiseFinal_FormPoses](RisingSlash/RiseFinal/RiseFinal_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseFinal.png` | Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |

### RisingSlash · RiseWard

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [RiseWardDescent](RisingSlash/RiseWard/RiseWardDescent.png) | 상승베기형 RiseWardDescent 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWardDescent.png` | Drop00, Drop01, Drop02, Land00, Land01 |
| [RiseWardPursuit](RisingSlash/RiseWard/RiseWardPursuit.png) | 상승베기형 RiseWardPursuit 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWardPursuit.png` | 3장 · Frame00 → Frame02 |
| [RiseWardStrike](RisingSlash/RiseWard/RiseWardStrike.png) | 상승베기형 RiseWardStrike 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWardStrike.png` | 3장 · Frame00 → Frame02 |
| [RiseWard_FormPoses](RisingSlash/RiseWard/RiseWard_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWard.png` | Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |

### RisingSlash · RiseWave

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [RiseWaveDescent](RisingSlash/RiseWave/RiseWaveDescent.png) | 상승베기형 RiseWaveDescent 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWaveDescent.png` | Drop00, Drop01, Land00, Land01 |
| [RiseWavePursuit](RisingSlash/RiseWave/RiseWavePursuit.png) | 상승베기형 RiseWavePursuit 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWavePursuit.png` | 3장 · Frame00 → Frame02 |
| [RiseWaveRelease](RisingSlash/RiseWave/RiseWaveRelease.png) | 상승베기형 RiseWaveRelease 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWaveRelease.png` | 3장 · Frame00 → Frame02 |
| [RiseWaveStrike](RisingSlash/RiseWave/RiseWaveStrike.png) | 상승베기형 RiseWaveStrike 연속 동작 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWaveStrike.png` | 3장 · Frame00 → Frame02 |
| [RiseWave_FormPoses](RisingSlash/RiseWave/RiseWave_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / ConfigureRisingPresentation` | `RisingSlash/Forms/RiseWave.png` | Idle, Side, Back, Dash, Windup, Rising, AirSlash, Fall, Landing, Slash, Hurt, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |

### RisingSlash · 이펙트

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [RisingSlash_Fragment0](RisingSlash/Effects/RisingSlash_Fragment0.png) | 전투 효과 Fragment0 · `RisingBossVFX` | `RisingSlash/Effects/Fragment0.png` | Fragment0 |
| [RisingSlash_Fragment1](RisingSlash/Effects/RisingSlash_Fragment1.png) | 전투 효과 Fragment1 · `RisingBossVFX` | `RisingSlash/Effects/Fragment1.png` | Fragment1 |
| [RisingSlash_Fragment2](RisingSlash/Effects/RisingSlash_Fragment2.png) | 전투 효과 Fragment2 · `RisingBossVFX` | `RisingSlash/Effects/Fragment2.png` | Fragment2 |
| [RisingSlash_Impact](RisingSlash/Effects/RisingSlash_Impact.png) | 전투 효과 Impact · `RisingBossVFX` | `RisingSlash/Effects/Impact.png` | Impact |
| [RisingSlash_Shield](RisingSlash/Effects/RisingSlash_Shield.png) | 전투 효과 Shield · `RisingBossVFX` | `RisingSlash/Effects/Shield.png` | Shield |
| [RisingSlash_SlashEcho](RisingSlash/Effects/RisingSlash_SlashEcho.png) | 전투 효과 SlashEcho · `RisingBossVFX` | `RisingSlash/Effects/SlashEcho.png` | SlashEcho |
| [RisingSlash_Slash](RisingSlash/Effects/RisingSlash_Slash.png) | 전투 효과 Slash · `RisingBossVFX` | `RisingSlash/Effects/Slash.png` | Slash |
| [RisingSlash_StunStars](RisingSlash/Effects/RisingSlash_StunStars.png) | 전투 효과 StunStars · `RisingStunStars` | `RisingSlash/Effects/StunStars.png` | 48장 · Frame00 → Frame47 |
| [RisingSlash_WaveEcho](RisingSlash/Effects/RisingSlash_WaveEcho.png) | 전투 효과 WaveEcho · `RisingBossVFX` | `RisingSlash/Effects/WaveEcho.png` | WaveEcho |
| [RisingSlash_Wave](RisingSlash/Effects/RisingSlash_Wave.png) | 전투 효과 Wave · `RisingBossVFX` | `RisingSlash/Effects/Wave.png` | Wave |

## Guard (42개)

### Guard · Ward

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Ward_FormPoses](Guard/Ward/Ward_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `Guard/Forms/Ward.png` | Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death |
| [Ward_GD04](Guard/Ward/Ward_GD04.png) | GD04 지진 도약·착지 또는 플레이어 공중 피격 · `GuardSlamWard` | `Guard/GD04/Ward.png` | Windup, Descent, Contact, Rebound |
| [Ward_HeavyStrike](Guard/Ward/Ward_HeavyStrike.png) | GD02 강타 준비·타격 · `GuardHeavyWard` | `Guard/HeavyStrike/Ward.png` | Windup, Downstroke, FollowThrough |
| [Ward_InspectWeapon](Guard/Ward/Ward_InspectWeapon.png) | 계승 연출 중 무기 관찰 · `KarmaGuardianGrowth` | `Guard/Growth/Reflection.png` | Reflection_0 |
| [Ward_MoltenStrike](Guard/Ward/Ward_MoltenStrike.png) | 용융 강타 준비·타격 · `GuardHeavyMoltenWard` | `Guard/MoltenStrike/Ward.png` | Windup, Downstroke, FollowThrough |
| [Ward_Revival](Guard/Ward/Ward_Revival.png) | 패배 후 부활 · `KarmaArtwork.RevivalPoses / KarmaGuardianGrowth` | `Guard/Revival/Ward.png` | 6장 · Revive00 → Revive05 |
| [Ward_Stagger](Guard/Ward/Ward_Stagger.png) | 방어 붕괴 후 경직 · `GuardStaggerWard` | `Guard/Stagger/Ward.png` | Reference, Stagger |

### Guard · WardFinal

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WardFinal_FormPoses](Guard/WardFinal/WardFinal_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `Guard/Forms/WardFinal.png` | Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |
| [WardFinal_GD04](Guard/WardFinal/WardFinal_GD04.png) | GD04 지진 도약·착지 또는 플레이어 공중 피격 · `GuardSlamWardFinal` | `Guard/GD04/WardFinal.png` | Windup, Descent, Contact, Rebound |
| [WardFinal_HeavyStrike](Guard/WardFinal/WardFinal_HeavyStrike.png) | GD02 강타 준비·타격 · `GuardHeavyWardFinal` | `Guard/HeavyStrike/WardFinal.png` | Windup, Downstroke, FollowThrough |
| [WardFinal_MoltenStrike](Guard/WardFinal/WardFinal_MoltenStrike.png) | 용융 강타 준비·타격 · `GuardHeavyMoltenWardFinal` | `Guard/MoltenStrike/WardFinal.png` | Windup, Downstroke, FollowThrough |
| [WardFinal_ShieldAway](Guard/WardFinal/WardFinal_ShieldAway.png) | 방패 투척 중 방패 없는 자세 · `GuardShieldlessWardFinal` | `Guard/ShieldAway/WardFinal.png` | Hurt, Thrust, ShieldBash, Idle, Move, Crouch |
| [WardFinal_Stagger](Guard/WardFinal/WardFinal_Stagger.png) | 방어 붕괴 후 경직 · `GuardStaggerWardFinal` | `Guard/Stagger/WardFinal.png` | Reference, Stagger |

### Guard · WardFinalHeavy

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WardFinalHeavy_ShieldAway](Guard/WardFinalHeavy/WardFinalHeavy_ShieldAway.png) | 방패 투척 중 방패 없는 자세 · `GuardTrialHeavyWardFinal` | `Guard/ShieldAway/WardFinalHeavy.png` | Windup, Downstroke, FollowThrough |

### Guard · WardFinalHeavyMolten

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WardFinalHeavyMolten_ShieldAway](Guard/WardFinalHeavyMolten/WardFinalHeavyMolten_ShieldAway.png) | 방패 투척 중 방패 없는 자세 · `GuardTrialHeavyMoltenWardFinal` | `Guard/ShieldAway/WardFinalHeavyMolten.png` | Windup, Downstroke, FollowThrough |

### Guard · WardRise

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WardRise_FormPoses](Guard/WardRise/WardRise_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `Guard/Forms/WardRise.png` | Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |
| [WardRise_GD04](Guard/WardRise/WardRise_GD04.png) | GD04 지진 도약·착지 또는 플레이어 공중 피격 · `GuardSlamWardRise` | `Guard/GD04/WardRise.png` | Windup, Descent, Contact, Rebound |
| [WardRise_HeavyStrike](Guard/WardRise/WardRise_HeavyStrike.png) | GD02 강타 준비·타격 · `GuardHeavyWardRise` | `Guard/HeavyStrike/WardRise.png` | Windup, Downstroke, FollowThrough |
| [WardRise_MoltenStrike](Guard/WardRise/WardRise_MoltenStrike.png) | 용융 강타 준비·타격 · `GuardHeavyMoltenWardRise` | `Guard/MoltenStrike/WardRise.png` | Windup, Downstroke, FollowThrough |
| [WardRise_Stagger](Guard/WardRise/WardRise_Stagger.png) | 방어 붕괴 후 경직 · `GuardStaggerWardRise` | `Guard/Stagger/WardRise.png` | Reference, Stagger |

### Guard · WardWave

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [WardWave_FormPoses](Guard/WardWave/WardWave_FormPoses.png) | 외형별 기본·계승 자세 · `KarmaCharacterAnimator / 수호자 외형과 계승 자세` | `Guard/Forms/WardWave.png` | Idle, Side, Back, Guard, Move, ShieldBash, Hurt, Thrust, Crouch, Windup, Recover, Death, RevivePush, ReviveKneel, ReviveRise, ReviveStand |
| [WardWave_GD04](Guard/WardWave/WardWave_GD04.png) | GD04 지진 도약·착지 또는 플레이어 공중 피격 · `GuardSlamWardWave` | `Guard/GD04/WardWave.png` | Windup, Descent, Contact, Rebound |
| [WardWave_HeavyStrike](Guard/WardWave/WardWave_HeavyStrike.png) | GD02 강타 준비·타격 · `GuardHeavyWardWave` | `Guard/HeavyStrike/WardWave.png` | Windup, Downstroke, FollowThrough |
| [WardWave_MoltenStrike](Guard/WardWave/WardWave_MoltenStrike.png) | 용융 강타 준비·타격 · `GuardHeavyMoltenWardWave` | `Guard/MoltenStrike/WardWave.png` | Windup, Downstroke, FollowThrough |
| [WardWave_ShieldAway](Guard/WardWave/WardWave_ShieldAway.png) | 방패 투척 중 방패 없는 자세 · `GuardShieldlessWardWave` | `Guard/ShieldAway/WardWave.png` | Hurt, Thrust, ShieldBash |
| [WardWave_Stagger](Guard/WardWave/WardWave_Stagger.png) | 방어 붕괴 후 경직 · `GuardStaggerWardWave` | `Guard/Stagger/WardWave.png` | Reference, Stagger |

### Guard · 이펙트

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Guard_Basalt](Guard/Effects/Guard_Basalt.png) | 전투 효과 Basalt · `GuardBasalt` | `Guard/GD04/Basalt.png` | 6장 · Step1 → Step6 |
| [Guard_Bash](Guard/Effects/Guard_Bash.png) | 전투 효과 Bash · `GuardBossVFX` | `Guard/Effects/Bash.png` | Cleave |
| [Guard_Debris](Guard/Effects/Guard_Debris.png) | 전투 효과 Debris · `GuardBasaltDebris` | `Guard/GD04/Debris.png` | Burst |
| [Guard_DomeShield](Guard/Effects/Guard_DomeShield.png) | 전투 효과 DomeShield · `GuardDomeShield` | `Guard/Effects/DomeShield.png` | DomeShield_0 |
| [Guard_FanShock](Guard/Effects/Guard_FanShock.png) | 전투 효과 FanShock · `GuardSlamFan` | `Guard/GD04/FanShock.png` | FanShock_0 |
| [Guard_FootSparks](Guard/Effects/Guard_FootSparks.png) | 전투 효과 FootSparks · `GuardBasaltHeat` | `Guard/GD04/FootSparks.png` | Sparks |
| [Guard_GD05_GoldenFoot](Guard/Effects/Guard_GD05_GoldenFoot.png) | 전투 효과 GD05_GoldenFoot · `KarmaGuardDome` | `Guard/Effects/GD05_GoldenFoot.png` | GD05_GoldenFoot_0 |
| [Guard_GD05_GoldenSeal](Guard/Effects/Guard_GD05_GoldenSeal.png) | 전투 효과 GD05_GoldenSeal · `KarmaGuardDome` | `Guard/Effects/GD05_GoldenSeal.png` | GD05_GoldenSeal_0 |
| [Guard_GD05_InternalBlast](Guard/Effects/Guard_GD05_InternalBlast.png) | 전투 효과 GD05_InternalBlast · `KarmaGuardDome` | `Guard/Effects/GD05_InternalBlast.png` | GD05_InternalBlast_0 |
| [Guard_Pillar](Guard/Effects/Guard_Pillar.png) | 전투 효과 Pillar · `GuardBossVFX` | `Guard/Effects/Pillar.png` | Pillar |
| [Guard_Seal](Guard/Effects/Guard_Seal.png) | 전투 효과 Seal · `GuardBossVFX` | `Guard/Effects/Seal.png` | GroundRing |
| [Guard_Shard](Guard/Effects/Guard_Shard.png) | 전투 효과 Shard · `GuardBossVFX` | `Guard/Effects/Shard.png` | Ward |
| [Guard_Shield](Guard/Effects/Guard_Shield.png) | 전투 효과 Shield · `GuardBossVFX` | `Guard/Effects/Shield.png` | ShieldRing |
| [Guard_StunStars](Guard/Effects/Guard_StunStars.png) | 전투 효과 StunStars · `GuardStunStars` | `Guard/Effects/StunStars.png` | 48장 · Frame00 → Frame47 |
| [Guard_Wave](Guard/Effects/Guard_Wave.png) | 전투 효과 Wave · `GuardBossVFX` | `Guard/Effects/Wave.png` | Wave |
| [Guard_WholeShield](Guard/Effects/Guard_WholeShield.png) | 전투 효과 WholeShield · `GuardBossVFX` | `Guard/Effects/WholeShield.png` | Hot |

## Shared (5개)

### Shared · 동작

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Guardian_InheritancePoses](Shared/Guardian_InheritancePoses.png) | 수호자 계승 전환과 외형별 기본 포즈 · `Inheritance` | `Shared/ReferencePoses.png` | Wave, WaveRise, WaveWard, WaveFinal, Rise, RiseWave, RiseWard, RiseFinal, Ward, WardWave, WardRise, WardFinal, Neutral, SwordPart, RisePart, WardPart |

### Shared · 이펙트

| 백업 이미지 | 이전 사용처 | 이전 경로 | 프레임·포즈 |
|---|---|---|---|
| [Shared_Guardian](Shared/Effects/Shared_Guardian.png) | 공통 수호자 스킬 효과 · `GuardianVFX` | `Shared/Effects/Guardian.png` | Wave, AimedWave, Rising, Cleave, Ward, Counter, GroundRing, AirSlash, Landing, Blade, Pillar, Beam |
| [Shared_Hit](Shared/Effects/Shared_Hit.png) | 전투 효과 Hit · `HitVFX` | `Shared/Effects/Hit.png` | Spark, Burst, Cross, Crescent, Wood, Slash, Ring, Wave |
| [Shared_Parry](Shared/Effects/Shared_Parry.png) | 전투 효과 Parry · `RisingBossVFX, BossStun` | `Shared/Effects/Parry.png` | Parry |
| [Shared_Personalization](Shared/Effects/Shared_Personalization.png) | 전투 효과 Personalization · `PersonalizationVFX` | `Shared/Effects/Personalization.png` | Pierce, Rising, Ward, Core, Sweep, Counter, Pillar, Awaken, Trail, AirSlash, Impact, Fusion |
