# Security Policy

## Supported versions

ArchLinterNet is currently an early-preview project distributed through `0.x` releases. Its public API, policy schema, CLI behavior, and package layout may still change between preview releases.

| Version | Security support |
|---|---|
| Latest published `0.x` preview release | Supported |
| Earlier `0.x` preview releases | Not supported; upgrade to the latest preview |
| Unreleased code on `main` | Evaluated on a best-effort basis, but not treated as a supported release |

During the preview phase, "supported" means that the maintainer will evaluate credible reports affecting the latest published preview and coordinate an appropriate fix or mitigation when the issue is confirmed. Backports to older previews are not guaranteed. A fix may be delivered in a newer preview release that requires users to upgrade.

This policy does not constitute commercial support or a fixed response-time commitment.

## Reporting a vulnerability

Use **GitHub Private Vulnerability Reporting** as the only supported channel for reporting a suspected vulnerability in ArchLinterNet:

1. Open the repository's **Security** tab.
2. Select **Report a vulnerability**.
3. Submit the report privately through the GitHub Security Advisory form.

Direct reporting link: <https://github.com/eugenemalaschuk-source/arch-linter-net/security/advisories/new>

Do **not** disclose an unresolved vulnerability through a public GitHub issue, pull request, discussion, commit, social-media post, or other public channel. Public reports may expose users before a fix is available and may be closed or removed without technical discussion.

No security email address is currently provided. A project-owned security mailbox may be introduced later if the project has an official domain and an operationally monitored mailbox.

## What to include

A useful private report should include, where applicable:

- the affected ArchLinterNet package, command, policy feature, and version;
- the operating system, .NET SDK/runtime version, and relevant configuration;
- a clear description of the vulnerability and its potential impact;
- reproduction steps or a minimal proof of concept;
- required attacker capabilities, trust boundaries, and preconditions;
- whether the issue affects local developer workflows, CI environments, generated output, package consumers, or repository automation;
- any known workaround or suggested remediation;
- whether the vulnerability has already been disclosed to anyone else or is subject to a disclosure deadline.

Please avoid including unrelated personal data, production secrets, access tokens, or destructive payloads. Use the minimum data necessary to demonstrate the issue.

## Coordination and disclosure

The maintainer will review private reports and acknowledge them when reasonably possible. Response and remediation timing depend on reproducibility, severity, project impact, and maintainer availability; no fixed service-level agreement is promised.

For confirmed vulnerabilities, the reporter and maintainer should coordinate privately on validation, remediation, release timing, advisory content, and public disclosure. Please allow a reasonable opportunity to investigate and publish a fix before disclosing technical details publicly.

The maintainer may determine that a report is not a security vulnerability, is outside the project's threat model, affects only an unsupported preview, or should instead be reported to an upstream dependency. That decision and its rationale will be communicated through the private advisory when practical.

## Good-faith research

Good-faith security research is welcome when it avoids privacy violations, service disruption, data destruction, unauthorized persistence, and access beyond what is necessary to demonstrate the issue. This policy does not authorize testing against systems or repositories that you do not own or have permission to assess.
