## Context

ArchLinterNet already documents semantic classification sources, role selectors, contextual contracts, coverage, and static-analysis limits in separate pages. Issue #115 needs these pieces assembled into an AI-first governance workflow that remains explainable and safe as agents add many files. The change is documentation-only and must fit the existing MkDocs navigation and sample-policy conventions.

## Goals / Non-Goals

**Goals:**

- Give an AI agent a repeatable inspect-classify-propose-validate-review workflow.
- Show how role, metadata, evidence, coverage state, and contextual edges become actionable diagnostics.
- Cover new feature, new bounded context, shared-kernel exception, legacy migration, Sales/Inventory/SharedKernel, and Unity/client layouts.
- Make examples valid against the current schema and clearly separate audit discovery from strict gates.

**Non-Goals:**

- Changing classification extraction, selector evaluation, coverage, schema, CLI, JSON contracts, or runtime behavior.
- Proving DI resolution, runtime behavior, security correctness, or semantic data flow.
- Automatically editing code or weakening policies.

## Decisions

- **Extend the existing AI documentation tree.** Add a focused semantic-role governance page and link it from the AI index and authoring guide, rather than creating a new documentation subsystem.
- **Use existing vocabulary and examples.** Reuse the semantic role catalog, `classification`, `layers.*.selector`, contextual contracts, `semantic_role` coverage, strict/audit modes, and current sample policy syntax. This keeps the guidance schema-backed.
- **Represent diagnostics as a stable explanation shape.** Document expected fields—expected role, actual role, evidence/source, metadata, context, coverage state, and suggested narrow action—while stating that suggestions are review input, not automatic weakening.
- **Validate with static fixtures.** Add or update sample YAML only where it demonstrates the required layouts; validate it with the repository's docs/schema checks and retain runtime implementation as a non-goal.

## Risks / Trade-offs

- [Risk] Examples can drift from the evolving semantic-role schema. → Keep snippets limited to documented fields and run schema/docs validation.
- [Risk] Agents may treat suggestions as permission to broaden policy. → Repeat fail-closed, narrow-exception, and human-review guardrails in workflow and review examples.
- [Risk] More guidance can be mistaken for runtime guarantees. → State static-analysis boundaries beside every diagnostic and validation workflow.
