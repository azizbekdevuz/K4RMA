# K4RMA · Shared project context / 프로젝트 공통 컨텍스트

> Supply this document **with the current files and the concrete task** to any coding assistant. This document describes **intent**, not proof that a feature exists.

## Prototype branch note / 프로토타입 브랜치 (2026-10-01)

`feat/2-5d-prototype` is checking a locked side-view 2.5D graybox in `Game/` with Unity **6000.6.2f1**. Free-roaming 3D is outside this slice. D-004 stays open: the final 2D versus 2.5D choice is not closed. Placeholder abilities and the deflect counter are prototype data, not approved final design. Read `AGENTS.md` and `docs/PROTOTYPE_STATUS.md` before editing the playable scene.

`feat/2-5d-prototype`는 `Game/`에서 Unity **6000.6.2f1**로 고정된 측면 2.5D 그레이박스를 확인합니다. 자유 이동 3D는 이 슬라이스 밖입니다. D-004는 열려 있습니다. 능력 이름과 디플렉트 반격은 최종 설계가 아닌 프로토타입 데이터입니다.

## Project identity
- University team game; **K4RMA is a working title pending final team confirmation**.
- Unity-based action / boss-combat project; C# expected; evaluate feasible third-person 3D first, 2D fallback.
- Four collaborators; implementation roles and engine/editor version not yet finalized.
- Immediate goal: **a working, enjoyable single-player core**, not multiplayer or production-scale content.

## Agreed core mechanic
1. The player starts with multiple abilities and basic combat/statistics.
2. Fight enemies/bosses. Future enemies can use abilities previously surrendered by the player.
3. After clearing a stage, select and surrender one ability; the player loses it and it enters an enemy-available ability pool.
4. Continue through tougher fights with fewer options; tune the final stage for beatability.

## Considered, not finalized
- Ability examples: Dash, Fireball, Shield, Heal; exact abilities/counts not approved.
- Performance score inputs: kills, clear time, remaining HP, hits taken.
- Spend score to strengthen baseline stats.
- Ability-pair synergies, potential retained synergy after sacrifice, and enemy combinations.
- Number of stages/bosses, combat camera, art direction, specific assets, UI layout, platform and input mapping.

## Non-negotiable implementation guidance
- **No networking now.** Design the local player, controls, abilities, damage, enemies, stage progression, and HUD with clear boundaries so an additional player can be supported later without rewriting the entire game.
- Keep domain/gameplay logic separate from input devices, visual effects, UI, and persistence where practical. Avoid a giant central script, hard-coded global `Player` assumptions, untracked mutable singletons, and circular dependencies.
- A skill's definition/configuration, ownership, activation, cooldown, sacrifice, and enemy use should have clearly understandable interfaces; do not assume the full system is already implemented.
- UI should accept/display explicit actor data rather than globally reading only one player. Models/animations should be reusable across character instances when reasonable.
- Optimize for a small team learning Unity: prefer the **simplest testable design** that preserves these boundaries. Do not create elaborate server architecture or abstractions without a concrete need.
- Never claim a feature, folder, component, test, Unity version, or build exists unless verified from repository contents.

## Ask before inventing
If an implementation choice depends on missing project information, ask for the Unity/editor version, existing file/class names, relevant code, scene/prefab setup, expected behavior, reproduction steps/errors, runtime target, and current ownership boundaries. Offer a minimal assumption only when clearly labeled and easy to change.

## AI contribution contract
1. Read this document and relevant existing code first.
2. State intended edits and assumptions briefly; identify public interfaces affected.
3. Change only files within the assigned task; coordinate shared scene/prefab modifications.
4. Preserve Unity `.meta` files and references; avoid unnecessary generated artifacts.
5. Provide practical verification steps and mention what **was not tested**.
6. Update documentation when decisions, behaviors, or dependencies change.

## 한국어 요약
기본 싱글플레이 전투와 **능력 희생 → 이후 적이 그 능력 사용**을 먼저 검증합니다. 미래의 멀티플레이를 위해 지금 네트워크를 만들지는 않습니다. 다만 플레이어·입력·전투·UI·에셋을 불필요하게 결합하지 말고 재사용하기 쉽게 설계합니다. AI에 물을 때는 이 문서뿐 아니라 **현재 실제 코드·Unity 버전·원하는 동작·시도한 방법·오류 메시지·담당 범위**를 함께 제공하세요. 아직 결정되지 않은 사항을 확정된 사양처럼 다루지 마세요.
