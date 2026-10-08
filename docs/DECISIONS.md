# Decisions and open questions / 결정 및 미결 사항

| ID | Topic | Status | Current position |
| :--- | :--- | :--- | :--- |
| D-001 | Core mechanic | Agreed direction | Player sacrifices an ability on clearing a stage; later enemies can use surrendered skills. |
| D-002 | First milestone | Agreed direction | Validate the single-player combat/sacrifice/inheritance loop with a minimal playable prototype. |
| D-003 | Engine | Working choice | Unity; editor version, packages, target platform and exact project structure TBD. |
| D-004 | Visual format | To validate | Test feasibility of third-person 3D; 2D is the fallback, not a decision already ruled out. |
| D-005 | Multiplayer | Outside MVP | Keep reasonable extension points; networking is not approved current work. |
| D-006 | Repository workflow | Partially agreed | Shared GitHub repo managed by Aziz; evaluate Unity Version Control before deciding any asset-locking or large-binary workflow. |
| D-007 | Gameplay tuning | Open | Starting/total abilities, stage count, scoring rules, stat upgrades, skill synergies, last-boss balance. |
| D-008 | Team naming | Proposed | K4RMA; pending final team confirmation. |
| D-009 | Collaboration | Planned | Use a shared Notion space for game specification, assignments, and meeting notes. |
| D-010 | Prototype presentation | Prototype-branch working choice (2026-10-01) | `feat/2-5d-prototype` validates a locked side-view 2.5D graybox. This does not replace D-004. Final 2D versus 2.5D remains open. Do not merge this choice to `main` until the team accepts it. |
| D-011 | Prototype editor and path | Working choice for this branch (2026-10-01) | Unity `6000.6.2f1`, project folder `Game/`. Chosen because that editor is installed here. Not a separate team-wide vote. |
| D-012 | Prototype counter | Prototype rule (2026-10-01) | Surrendering `proto.projectile` removes it from the player, gives it to the next guardian, and unlocks `proto.projectile.deflect`. Dash, Projectile, and Guard are placeholders, not the final skill list. |

**Update rule:** When the team settles an open choice, record the decision, meeting/date, reason, and implementation implications here. Do not retroactively rewrite a proposal as if it had already been approved.
