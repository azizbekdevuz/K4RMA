# Third-Party Notices and Asset Provenance

[한국어](THIRD_PARTY_NOTICES.md) | [English](THIRD_PARTY_NOTICES.en.md)

> **Status: audit incomplete.** This inventory does not establish the provenance, license or compliance of all repository files. An omitted component is not presumed original or freely reusable.

## Technically verified dependencies

| Component | Version/source in repository | Licensing action |
| --- | --- | --- |
| Unity Editor | `6000.6.2f1` (README) | Check Unity terms; the editor is not relicensed here |
| Unity Input System | `com.unity.inputsystem` `1.20.1` | Check the package's applicable terms |
| Universal Render Pipeline | `com.unity.render-pipelines.universal` `17.6.0` | Check the package's applicable terms |
| 2D Sprite | `com.unity.2d.sprite` `1.0.0` | Check the package's applicable terms |
| Unity built-in modules | `Packages/manifest.json` | Check Unity terms |

These versions come from the README and `Packages/manifest.json`; no legal-compliance certification of individual packages is implied.

## Provenance register (unverified)

| Files/path | Author/provider | Acquisition evidence | License/terms | Raw files redistributable? | Embedded build allowed? | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `Assets/Arts/**` | Unknown | Unknown | Unknown | Unknown | Unknown | **Review required** |
| Fonts/audio/external code | Audit each file | Unknown | Unknown | Unknown | Unknown | **Review required** |
| AI-generated/assisted assets | Audit creators/tools | Work records needed | Check tool terms | Unknown | Unknown | **Review required** |

## Before adding materials

1. Record file path, creator/provider, source URL and acquisition or generation evidence.
2. Verify exact license text/version, attribution, modifications, redistribution, commercial use and public-source rights.
3. **Separately verify permission to publish raw source assets and to embed them in distributed builds.**
4. Preserve required license texts and copyright notices; this summary does not replace them.
5. Quarantine or exclude unverified material until reviewed. Remove unauthorized material before any public release.

Unity Asset Store licenses may permit embedding assets in games while restricting standalone/raw redistribution.

**Related:** [LICENSE.en.md](LICENSE.en.md) · [GOVERNANCE.en.md](../collaboration/GOVERNANCE.en.md)
