# AI Policy Authoring Kit

ArchLinterNet is YAML-first, which makes it suitable for AI-assisted architecture governance. Agents can inspect a repository, propose a policy, and review policy changes without writing custom architecture tests.

This public AI section is for product usage: authoring and reviewing ArchLinterNet policies. It is safe to publish as part of the GitHub Pages documentation site.

Internal repository-maintenance instructions, backlog governance, issue-writing rules, and implementation planning notes are not part of the published product documentation. They remain in GitHub Markdown files under internal repository locations.

## Start here

- [Agent Guide](agent-guide.md) — investigation workflow before editing a policy.
- [Policy Authoring Guide](policy-authoring-guide.md) — YAML authoring rules.
- [Semantic-role governance](semantic-role-governance.md) — AI-first workflow, diagnostics, examples, and safe exceptions for semantic roles.
- [Semantic role catalog](../policy-format/semantic-role-catalog.md) — reviewed role and metadata vocabulary for future semantic policies.
- [Capabilities](capabilities.md) — supported contract families and limits.
- [Policy Review Checklist](policy-review-checklist.md) — review checks before opening a PR.

## Machine-readable references

- `schema/dependencies.arch.schema.json` describes supported YAML structure.
- `archlinternet.capabilities.json` describes supported contract families, matching semantics, and limits.

## Guardrail

Start from real assemblies and namespaces. Do not invent ideal architecture words, unsupported YAML fields, fake contract families, or broad `ignored_violations` entries to make a policy pass.

If a desired capability is not documented in the public product docs or schema, track it as a backlog proposal instead of authoring unsupported YAML.
