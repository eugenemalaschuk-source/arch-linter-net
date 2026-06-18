## Why

ArchLinterNet is intentionally YAML-first, which makes it useful for AI-assisted architecture governance, but agents currently have only a placeholder AI page and prose-oriented references to guide policy authoring. Without explicit AI-facing guidance and machine-readable capability metadata, agents can generate plausible policy YAML that the engine does not actually support or that weakens architecture boundaries through broad ignores and future-state strict rules.

## What Changes

- Add an AI policy-authoring kit that explains how agents should discover real assemblies, namespaces, layers, modules, and migration debt before editing `architecture/dependencies.arch.yml`.
- Document current ArchLinterNet capabilities and limits in AI-facing language, including strict versus audit behavior, supported contract families, frozen-debt ignores, method-body checks, and Unity `.asmdef` checks.
- Add a policy review checklist for AI-generated policy changes before opening a PR.
- Add standalone sample policy recipes for clean architecture, modular monolith boundaries, and Unity `.asmdef` boundaries.
- Add at least one machine-readable artifact that AI tools can inspect to understand supported YAML structure and/or contract capabilities without scraping prose.
- Align documentation with the current engine model and explicitly warn against unsupported fields, invented rule families, broad ignored violations, and idealized layer names that do not map to real code.

## Capabilities

### New Capabilities
- `ai-policy-authoring`: AI-facing guidance, recipes, review checklist, and capability documentation for producing safe ArchLinterNet policies.
- `machine-readable-policy-metadata`: machine-readable schema and/or capability metadata describing the supported policy structure and contract families.

### Modified Capabilities
- None.

## Impact

- Affected docs include `docs/ai/`, existing policy/schema references, and MkDocs navigation.
- Affected samples include new standalone policy recipe files under `samples/policies/`.
- New machine-readable metadata may be added under `schema/` and/or the repository root.
- No runtime validator behavior, CLI behavior, public API, package dependency, or First Ice project-specific architecture policy changes are intended.
