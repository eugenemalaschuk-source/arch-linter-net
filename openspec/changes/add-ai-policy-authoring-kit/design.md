## Context

Issue #662 asks for an AI-facing policy-authoring kit so coding agents can edit ArchLinterNet YAML policies without inventing unsupported rule layouts or weakening architecture boundaries. The repository already has general documentation, a placeholder `docs/ai/index.md`, a prose YAML schema reference, and one basic clean architecture sample policy, but it does not yet provide operational guidance for agents or machine-readable policy metadata.

The current engine supports namespace-based layers, strict and audit contract groups, allow-only rules, ordered layer rules, cycles, independence, method-body forbidden API checks, Unity `.asmdef` checks, and ignored violation baselines. The YAML loader currently ignores unmatched properties, so documentation and external schema/capability metadata are important guardrails for AI-authored policies.

## Goals / Non-Goals

**Goals:**

- Give AI agents a clear, grounded process for authoring policies from real assemblies and namespaces.
- Document the current contract families, YAML fields, semantics, and known limits in AI-facing form.
- Provide reusable policy recipes for common .NET and Unity architecture shapes.
- Provide machine-readable metadata so agents and tools can inspect supported policy structure without scraping prose.
- Keep examples aligned with the current engine model.

**Non-Goals:**

- Do not generate or change any First Ice project-specific architecture policy.
- Do not change runtime validation semantics, CLI flags, package APIs, or dependency analysis behavior.
- Do not add a hosted AI service, MCP server, or vendor-specific AI integration.
- Do not redesign the YAML policy model.

## Decisions

### Add Both JSON Schema and Capability Manifest

Add `schema/dependencies.arch.schema.json` to describe the supported YAML structure and add `archlinternet.capabilities.json` to describe higher-level linter capabilities, contract families, modes, and limits.

Alternatives considered:

- JSON Schema only: good for YAML shape, but weak for explaining semantic capabilities such as strict versus audit usage and method-body matching limits.
- Capability manifest only: useful for agents, but less useful for validating examples and catching invented fields.

Using both gives agents a structural contract and a semantic capability map while staying small and dependency-free.

### Treat Samples as Standalone Policy Recipes

Add standalone policy files under `samples/policies/` instead of creating runnable sample projects. These files should be schema-valid and designed for agents to adapt, but they will use illustrative assembly and namespace names.

Alternatives considered:

- Runnable sample projects: stronger executable validation, but larger scope and unrelated to the main AI authoring guidance.
- Documentation snippets only: lower maintenance, but harder for agents to reuse and harder to validate against the schema.

Standalone policy files match issue #662 and keep the change focused.

### Keep AI Guidance Separate from General User Docs

Use `docs/ai/` for agent-specific behavior, review checklist, and recipes. Keep `docs/reference/yaml-schema.md` as the human reference and link it to the machine-readable schema.

Alternatives considered:

- Merge all guidance into existing policy-format pages: simpler navigation, but mixes human onboarding with agent-specific safety rules.
- Put all guidance in `AGENTS.md`: useful locally, but not part of the published documentation site and too narrow for package consumers.

## Risks / Trade-offs

- Schema drift from C# models -> Mitigate by deriving schema fields directly from `ArchitectureContractModels.cs` and adding tasks to validate sample policies against the schema.
- Samples may look executable even though assemblies are illustrative -> Mitigate with explicit sample comments/docs explaining they are policy recipes to adapt.
- Agents may trust docs over runtime behavior -> Mitigate by documenting that the current loader ignores unknown fields and that schema validation should be used before PRs.
- Machine-readable manifest may become stale -> Mitigate by keeping it compact and aligned with the capability docs rather than duplicating long prose.

## Migration Plan

This is a documentation and metadata addition. Implementation can be merged without migration or rollback concerns. Rollback is removal of the added docs, samples, schema, manifest, and navigation links.

## Open Questions

- Should runtime YAML schema validation be introduced in a future change so unknown fields fail during CLI/test execution instead of only external schema validation?
