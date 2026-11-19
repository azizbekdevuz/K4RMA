# Security Policy

[한국어](SECURITY.md) | [English](SECURITY.en.md)

## Support status
K4RMA is an evolving Unity prototype. There is no guaranteed security-support window or remediation SLA. Maintainers will assess reports against currently supported development versions.

## Private reporting
1. If **Private vulnerability reporting** is enabled, use GitHub **Security → Report a vulnerability**.
2. Otherwise ask a maintainer for an **agreed private team contact channel**. Never post credentials, personal information or actionable exploit details to public issues, public PRs or general team chat.
3. Include the affected version/commit, minimal reproduction steps, impact and redacted supporting evidence.

Maintainers will coordinate acknowledgement, remediation and disclosure where practical. No response-time guarantee is made.

## Repository hygiene
- Never commit tokens, API keys, account recovery codes, personal purchase records or private asset access URLs.
- If exposed, revoke and rotate secrets promptly and review Git history and CI logs.
- Verify provenance of dependencies, third-party assets and AI tooling.

For ordinary defects use the [bug-report form](../.github/ISSUE_TEMPLATE/bug_report.yml).
