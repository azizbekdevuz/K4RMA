# SwordWave Appearance Consistency Rule

모든 SwordWave 수정 작업 후 다음을 확인한다. SwordWave 전용 아트와 검사 작업에도 적용한다.

1. Idle 기준 보스 신체 크기와 비율 유지.
2. Idle 기준 색감·밝기·채도 유지.
3. SWBasic~SW05 상태 전환에서 외형 변화 없음.
4. 일반형·계승형 4종과 좌우 방향 모두 확인.
5. 중복 Scale/Tint 보정 및 애니메이션 덮어쓰기 확인.
6. Unity 미검증인 경우 외형 검증 완료로 보고하지 않음.

검사는 실제 최종 SpriteRenderer의 스프라이트·배율·색상·재질과 부모 Transform을 기준으로 한다. 이동·피격·중단 후 Idle 복귀를 포함하며, 의도된 일시적 피격/상태이상 색상은 종료 후 기준색으로 복귀해야 한다.
오프라인 기본 검사: `python -X utf8 -B tests/Unity/SwordWave/test_sword_presentation.py`. 실제 화면의 비율·색감·가독성은 Unity 수동 검증과 구별하여 보고한다.

상위 [협업 규칙](../RULES.MD)을 함께 적용한다. 이 파일은 사용자가 요청한 SwordWave 전용 규칙이며 공통 보스 정책을 변경하지 않는다.

새 연출은 보스 본체 가시성을 유지한다. 공통 코드보다 SwordWave 로컬 해결을 우선한다.
