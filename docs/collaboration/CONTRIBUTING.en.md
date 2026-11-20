# K4RMA Contribution and Team Workflow

[한국어](CONTRIBUTING.md) | [English](CONTRIBUTING.en.md)

> Public visibility does **not** make this an open-source project accepting unrestricted contributions. Coordinate outside contributions with maintainers **before** submitting changes. See [LICENSE](../legal/LICENSE.en.md) and [GOVERNANCE](GOVERNANCE.en.md).

## Before starting
1. Read [README](../../README.en.md), [AGENTS](../../AGENTS.md), [team rules](../RULES.MD) and [structure](../STRUCTURE.MD). Inspect the actual scene and code; don't treat obsolete planning notes as current behavior.
2. Confirm task ownership, scope, acceptance criteria and coordination before changing shared scenes, prefabs or core combat.
3. Use Unity `6000.6.2f1` and the dependencies pinned in `Packages/manifest.json`.

## Branches and pull requests
- Use focused branches rather than editing `main`: `feat/awakening`, `fix/boss-hitbox`, `docs/credits`.
- Keep commits focused; remove unrelated files and generated noise.
- Use the [PR template](PULL_REQUEST_TEMPLATE.md) to explain purpose, changes, affected systems/assets, reproduction, actual tests, unverified points, and visuals when applicable.
- Obtain reviews from owners of shared logic and art. PR approval is not a copyright assignment or public-distribution authorization.

## Unity and assets
- Preserve Unity `.meta` references. Exclude `Library/`, `Temp/`, builds and secrets.
- Check sprite/animation/VFX consistency and readability in the actual game.
- For every sourced or generated asset, record creator/tool, acquisition evidence, terms and source-publication rights in [third-party notices](../legal/THIRD_PARTY_NOTICES.en.md). Don't distribute unverified assets.
- Do not casually add test/practice tools to shared runtime code.

## Definition of done
- Validate in Unity when available; otherwise state exactly what could not be tested.
- Check relevant regressions across abilities, inheritance orders, deaths/retries and shared combat.
- Review GUIDs, scene references, build impact, performance and input behavior.
- Update user-facing docs and decision records for changed behavior.

Follow the [Code of Conduct](../community/CODE_OF_CONDUCT.en.md) and [Security Policy](../community/SECURITY.en.md). Never expose vulnerabilities or secrets in public PRs.
