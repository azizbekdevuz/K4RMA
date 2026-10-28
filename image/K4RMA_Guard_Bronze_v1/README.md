# 방어형 보스 B · 청동 인장 이펙트

기존 방어형 수호자의 청동 갑옷과 B 시안에 맞춘 투명 PNG 6종.
원본 알파는 보존했으며, Unity Sprite Editor에서 여백을 제외한 영역을 지정한다.
가져오기 규격은 `tools/sprite-slices.json`에 기록되어 있다.

| 원본 PNG | Unity 시트 / 포즈 | 사용 위치 |
| --- | --- | --- |
| ShieldRing.png | GuardShield / ShieldRing | GD01 자동방어막, 방어형 보스의 방어 표시 |
| Bash.png | GuardBash / Cleave | GD02 강공격 |
| Shard.png | GuardShard / Ward | GD03 발사·분열·피해 없는 회수 조각 |
| Pillar.png | GuardPillar / Pillar | GD04 상승·완성 방벽, GD05 양쪽 경계 장식 |
| Wave.png | GuardWave / Wave | GD02 분노 충격파, GD05 생존 시험 탄 |
| GroundSeal.png | GuardSeal / GroundRing | GD05 바닥 결계 장식 |

이동하는 조각과 탄의 기본 방향은 오른쪽(+X), 방벽은 위쪽(+Y), 중심점은 중앙이다.
회전과 잔상은 기존 코드가 붙인다. 예고선·피해량·공격 시간·충돌 범위는 유지한다.
회수 조각은 기존의 낮은 불투명도와 청색 계열 틴트를 적용하며 공격 컴포넌트가 없다.
완성 방벽의 장식은 별도 오브젝트이고, 기존 이동 차단 콜라이더를 유지한다.
다른 유형의 보스와 플레이어는 기존 GuardianVFX/PlayerVFX 이미지를 사용한다.

Unity의 `K4RMA > 방어형 연습`에서 GD01~GD05를 각각 확인할 수 있다.
재가져오기는 Pipeline의 `script run`으로 `tools/unity/ImportSpriteSheets.cs`의
`ImportSpriteSheets.Guard`를 실행한다. 전체 이미지 재가져오기는 기존 `Main`을 사용한다.
가져온 편집 가능 스프라이트는 `Assets/K4RMA/Resources/K4RMAArtwork/Sheets/Guard*.png`에 있다.

생성 사양과 시안 참조는 generation.json에 기록했다. PNG는 애니메이션 프레임이 아닌 단일 포즈이며,
발광·이동·소멸 애니메이션은 현재 KarmaCombatVFX와 패턴 코루틴이 담당한다.
