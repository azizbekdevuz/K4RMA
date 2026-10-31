# K4RMA 전체 파일 구조

```text
K4RMA/
  Assets/K4RMA/
    Core/                         Unity 비의존 진행·고유화·기술·공통 전투 규칙
    Runtime/
      Abilities/                  원형 기술 및 고유화 상태
      Combat/                     플레이어 투사체, 보스 공격 객체, 판정 기하
      Configuration/              게임·기술·공통 보스 밸런스 설정
      Enemies/
        KarmaEnemy.cs             보스 컴포넌트와 외부 API
        KarmaEnemy.Combat.cs      피격·방어·사망 처리
        KarmaEnemy.StateMachine.cs 전투 시계·실행·회복·기절·중단
        KarmaEnemy.Presentation.cs 공통 전조·이동 공격·접촉 판정
        KarmaBossBehavior.cs      유형 선택·패턴 등록·선택 계약
        Patterns/
          KarmaBossPattern.cs     독립 패턴 클래스의 실행·사거리·중단 계약
          KarmaGroundStrikePattern.cs 기본형/방어형 공통 연속 베기
        Bosses/
          SwordWave/
            KarmaSwordBossBehavior.cs 선택 정책과 공격 주기
            KarmaSwordBossMovement.cs 접근·후퇴·근접 압박
            KarmaSwordBossPattern.cs  검기 패턴 공통 의존성
            Patterns/             SWBasic.cs, SW01.cs ~ SW05.cs,
                                  SwordWaveVolley.cs, KarmaAerialSwordPlanner.cs
            Settings/             KarmaSwordBossSettings.cs
            Presentation/         KarmaWaveArtwork.cs, KarmaWaveMotion.cs
          RisingSlash/
            KarmaRisingBossBehavior.cs 접근·선택 정책
            KarmaRisingBossPattern.cs  충전·경로 공통 연출
            Patterns/             UPBasic.cs, UP01.cs ~ UP05.cs
            Settings/             KarmaRisingBossSettings.cs
            Presentation/         KarmaRisingArt.cs, KarmaRisingPolish.cs,
                                  KarmaRisingCharge.cs
          Guard/
            KarmaGuardBossBehavior.cs 방어막 우선 선택·패턴 연결
            KarmaGuardBossPattern.cs  공통 장식·파동 크기
            Patterns/             GD01.cs ~ GD05.cs
            Settings/             KarmaGuardBossSettings.cs
            Presentation/         KarmaGuardDome.cs
      Flow/                       시작·진행·카메라·고유화·승패 흐름
      Input/                      Input System 입력
      Player/
        KarmaPlayer.cs            컴포넌트 초기화·입력·업데이트
        KarmaPlayer.Combat.cs     일반 검 공격·관통베기·상승베기
        KarmaPlayer.AirSlash.cs   공중 도약베기 상태·이동·연속 판정·중단
        KarmaPlayer.Defense.cs    방어·반격·피격·회복
        KarmaPlayer.Movement.cs   물리 이동·접지·사망 준비·방 초기화
        KarmaPlayer.Status.cs     기절·결계 구속·공간 분리
        KarmaPlayer.Ceremony.cs   고유화 의식 동작
      Presentation/
        Animation/                캐릭터 애니메이션 (KarmaCharacterAnimator)
          KarmaCharacterAnimator.cs 초기화·공통 동작·플레이어 포즈
          KarmaCharacterAnimator.Guardian.cs 보스 포즈·검기 공격 준비·외형
          KarmaCharacterAnimator.Empowerment.cs 부활·고유화 포즈
        Artwork/                  이미지 접근·캐릭터 생성·이미지 설정·스프라이트 라이브러리
        Effects/                  공통 전투 VFX·공격 전조·잔상·고유화 효과
        KarmaVisuals.cs           렌더링 재질과 기본 도형 생성
      UI/                         HUD·메뉴·안내·결과 화면
      World/                      경기장·지형·공간 기준
    Art/
      Resources/K4RMAArtwork/
        Characters/Player/        플레이어 통합 동작 atlas
        Characters/Guardians/
          Base/                   기본 수호자 4종
          Inherited/              계승·최종형 동작 atlas
          Revival/                부활 동작 atlas
          ReferencePoses.png      실제 계승 외형에 사용되는 런타임 atlas
        Effects/                  공통 전투·피격·고유화 효과
          Sword/                  검기형 효과 6장
          Rising/                 상승베기형 효과 15장
          Guard/                  방어형 효과 6장
        Environments/             경기장 배경·파노라마
        UI/                       UI atlas·메뉴 배경
        Props/                    월드 소품 atlas
      Resources/KarmaAerialSword.mat 검우 전용 재질
      Shaders/
        KarmaAerialSword.shader   검우 전용 셰이더
        KarmaCombatGlow.shader    공통 전투 발광 셰이더
    Resources/                    KarmaArtworkSettings·KarmaSpriteLibrary 및 공통 재질
    Scenes/KarmaPrototype.unity    실행 씬
    Settings/KarmaConfig.asset    저장된 게임·기술·보스별 밸런스
    Settings/Rendering/            URP 파이프라인·렌더러·후처리 설정
  Packages/                       실행 의존 패키지와 잠금 파일
  ProjectSettings/                Unity 버전·빌드 씬·입력·물리·렌더링 설정
  docs/PROJECT_STRUCTURE.md        이 파일
  README.md                       실행 안내
```

`*.meta`는 Unity 에셋의 GUID·Sprite ID·임포트 설정을 보관하므로 해당 파일과 함께 유지한다. 각 패턴은 실제 별도 클래스이며 공격별 상태와 실행을 소유한다. 행동 클래스는 이동·선택·주기를, KarmaEnemy는 공통 피해·패링·상태·중단을 담당한다.

새 효과도 KarmaSpriteLibrary의 `SwordBossVFX`, `RisingBossVFX`, `GuardBossVFX` 그룹으로 조회한다. 파일별 직접 로딩·중복 원본·미사용 대체 외형은 없다. 보스 본체는 기존 기본/계승 동작 atlas를 사용한다. 그림 교체 시 이미지 `.meta`와 라이브러리 참조를 보존한다.

밸런스는 KarmaConfig의 Boss Tuning → sword/rising/guard에서 조정한다. 패턴 수정은 해당 보스의 Patterns 폴더에서 한다. 관통베기 보호 시간은 Techniques → pierceInvulnerabilitySeconds(기본 0.1초)다.

`Library`, `Temp`, `Logs`, `UserSettings`는 Unity가 생성하는 로컬 캐시·실행 상태이며 배포 소스가 아니다. Git 이력도 실행 에셋과 별도로 보존한다.

## 코드 배치 기준

- 게임 규칙과 진행 계산은 `Core`, Unity 오브젝트를 다루는 코드는 `Runtime`에 둔다.
- 특정 보스에만 쓰는 행동·패턴·설정·연출은 해당 `Enemies/Bosses/<유형>` 안에 둔다.
- 여러 캐릭터가 공유하는 포즈·이미지·효과는 `Presentation`의 역할별 폴더에 둔다. 이미지 참조 설정 타입도 `Artwork`에 둔다.
- `partial` 파일은 같은 컴포넌트의 책임을 나눈다. 새 컴포넌트나 상속 계층을 추가하는 용도로 쓰지 않는다.
- `.cs`·셰이더 등 에셋을 옮길 때 `.meta`를 함께 옮겨 GUID를 보존한다. `Resources` 내부 경로는 로딩 API에서 쓰므로 별도 확인 없이 바꾸지 않는다.

현재 설정 에셋은 `sword`·`rising`·`guard`의 보스별 구조와 현재 기술 필드명을 직접 저장한다. 구버전 설정 이관·필드 별칭·이전 실행 세션 복구 코드는 유지하지 않는다. 캐릭터는 실제 스프라이트를 사용하며, 초기 프로토타입의 도형 캐릭터 생성·애니메이션 코드는 제거했다. 스테이지와 전투 효과가 사용하는 기본 도형 생성은 유지한다.

고유화 관련 이름은 `LastPersonalizedEssence`(마지막으로 고유화한 기술), `PersonalizationOrder`(고유화 순서), `healAfterPersonalization`(고유화 후 회복량)을 사용한다.
