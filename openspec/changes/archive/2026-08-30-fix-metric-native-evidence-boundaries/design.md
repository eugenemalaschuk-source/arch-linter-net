## Context

See [proposal.md](proposal.md). `ArchitectureTopologyEvaluator` deliberately observes assembly
subjects and direct edges from assembly metadata, independently of `ArchitectureTypeIndex`.
`ArchitectureMetricEvaluator` currently applies the type-index completeness bit before this native
authority is considered. Separately, public surface snapshot entries retain legacy simple assembly
names even though metric contributor identity is required to bind a resolved assembly.

## Goals / Non-Goals

**Goals:**

- Fail closed only when a selected metric's own native evidence is incomplete.
- Preserve canonical public-surface contributor identity or return an unassessable result when the
  legacy simple-name contract cannot bind exactly one target assembly.
- Align in-memory typed validation with the closed YAML target shape.

**Non-Goals:**

- Change public API validation's legacy simple-name resolution behavior.
- Add new policy syntax for assembly identities or alter the report schema.
- Broaden the report-wide missing-root rule; explicit target resolution remains required for every
  selected metric report.

## Decisions

### Apply type-universe completeness per metric authority

Only type, namespace, project, and external-dependency metrics consume type-derived facts. An
assembly-topology relation or assembly-footprint metric is allowed to use complete assembly
metadata even after `Assembly.GetTypes()` is partial. Public-surface metrics rely on their own
contract capture integrity, not `ArchitectureTypeIndex`. A global guard was rejected because it
turns metadata-complete assembly results into false `missing_required_input`.

### Bind public contributors once and fail closed on ambiguity

Before public-surface capture, measurement groups session target assemblies by the contract's
legacy simple names. Exactly one candidate is required for every governed name. The measurement
then emits `(canonical assembly identity, normalized signature)` contributors. Multiple candidates
are unassessable rather than selecting the first, while names that each have one resolved assembly
remain compatible with existing contracts.

### Separate target presence from target content

The typed validator tracks a field's null/non-null presence independently of a nonblank value.
This prevents whitespace-only disallowed fields from bypassing the same mutual-exclusion rule the
JSON schema applies to YAML input.

## Risks / Trade-offs

- [A duplicate simple name causes an unavailable public metric] → this is intentional: policy
  syntax cannot select a canonical candidate safely.
- [A partially loadable assembly still contributes assembly metadata] → the metric uses only
  `GetReferencedAssemblies` and resolved assembly identity; any type-derived metric remains
  unassessable.
- [Existing public API validation still uses first-wins lookup] → this correction narrowly protects
  measure-first output without changing an established validation contract.

## Migration Plan

1. Apply per-authority checks and canonical public contributor binding with regressions.
2. Run focused Core and CLI tests plus repository checks.
3. Archive after validation; rollback is a normal code revert with no data migration.
