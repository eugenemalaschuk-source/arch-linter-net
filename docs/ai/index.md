# AI Policy Authoring Kit

ArchLinterNet is YAML-first, which makes it suitable for AI-assisted
architecture governance. Agents can inspect a repository, propose a policy, and
review policy changes without writing custom architecture tests.

This section is written for coding agents and humans reviewing AI-authored
policy changes. It explains what the linter can validate, how to author safe
policies, and how to avoid plausible-looking YAML that the engine does not
support.

Start here:

- [Agent Guide](agent-guide.md) for the investigation workflow before editing a policy.
- [Policy Authoring Guide](policy-authoring-guide.md) for YAML authoring rules.
- [Capabilities](capabilities.md) for supported contract families and limits.
- [Policy Review Checklist](policy-review-checklist.md) before opening a PR.
- [Pull Request Governance](pull-request-governance.md) before creating a PR for a GitHub issue.

Machine-readable references:

- `schema/dependencies.arch.schema.json` describes supported YAML structure.
- `archlinternet.capabilities.json` describes supported contract families, matching semantics, and limits.

Important guardrail: start from real assemblies and namespaces. Do not invent
ideal architecture words, unsupported YAML fields, fake contract families, or
broad `ignored_violations` entries to make a policy pass.
