# Contributing to K4RMA / 협업 가이드

This is a working team protocol. On `feat/2-5d-prototype`, the Unity project is `Game/` and the playable scene is `Assets/_Project/Scenes/PrototypeArena.unity`. Adjust the protocol together if the team changes the editor version, roles, or asset workflow.

## 1. Before starting / 작업 전
- Read `README.md`, `docs/PROJECT_CONTEXT.md`, and `docs/DECISIONS.md`.
- Agree on a small task, owner, acceptance criteria, and any shared scene/prefab changes.
- Use the **same team-approved Unity editor version and project settings** when they become available.
- Ask for missing technical context rather than letting an AI assistant invent a parallel architecture.

## 2. Branches and reviews / 브랜치·리뷰
- Keep `main` in a usable state when a runnable project exists.
- Work on short-lived branches: `feat/ability-sacrifice`, `fix/boss-hitbox`, `docs/game-loop`.
- Make focused commits with plain-English or Korean intent: `feat: add skill sacrifice state`.
- Open a pull request (PR) with **what changed, why, how to test, and screenshots/video if visual**. Request review before merging shared gameplay/scene changes.
- Coordinate ownership of shared Unity scenes and prefabs before simultaneous edits; text merges do not guarantee scene correctness.

## 3. Unity and binary assets / 유니티 파일
- Commit source assets, project settings, and Unity-generated `.meta` files for any committed Unity project.
- Do **not** commit `Library/`, `Temp/`, caches, build outputs, local IDE files, or personal credentials.
- For large binary files, agree on Git LFS **or** Unity Version Control/asset locking where appropriate. Avoid two independently edited authoritative copies of the same project. Document where source of truth lives.
- Attribute third-party code/assets and check whether the license permits the intended classroom demo and repository visibility.

## 4. Definition of done / 완료 기준
- The assigned behavior works in the agreed editor/build target.
- Relevant edge cases are tested (e.g., sacrificed skill unavailable to player but available to later enemy).
- Existing movement/combat/stage transitions still work.
- No missing scene, prefab, package, `.meta`, or asset references; no unrelated changes.
- Update the project context/decision log for user-visible design changes.

## 5. AI usage / AI 활용
Provide the current source files, relevant scene/prefab structure, engine version, exact error, desired behavior, and `docs/PROJECT_CONTEXT.md`. Review output for made-up APIs, hard-coded single-player assumptions, redundant duplicate systems, and unrelated edits. **AI-written ≠ tested**; the assigned teammate owns validation.

## 6. Communication / 의사소통
Use the agreed chat/Notion space for tasks and decisions. Surface blockers early. Discuss a major scope change (3D → 2D, new networking requirements, different source-control system) with the team before implementing it.
