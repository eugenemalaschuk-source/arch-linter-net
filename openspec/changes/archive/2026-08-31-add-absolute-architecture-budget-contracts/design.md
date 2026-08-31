## Context

See proposal.md for motivation and
specs/architecture-metric-budgets/spec.md for the behavior contract. The
current `metrics` collection intentionally defines only an observable native
metric; `ArchitectureMetricEvaluator` is the single authority for values,
contributors, and completeness evidence. Normal validation already executes
mode-specific contract families and carries normal diagnostics through canonical
finding, applicability, baseline, and format adapters.

## Goals / Non-Goals

**Goals:**

- Add an explicit strict/audit contract family that gates a previously declared
  metric using one or both absolute integer bounds.
- Reuse one authoritative metric result for the budget comparison and convert
  incomplete metric evidence into the established applicability projection.
- Preserve normal result and baseline integration rather than adding a budget
  report envelope or command.

**Non-Goals:**

- Relative-to-baseline budgets, formulas, arbitrary scopes, or new metric
  kinds.
- Changing `measure`, rebuilding a graph, or changing thresholds for policies
  without budget contracts.

## Decisions

### Model budgets as strict/audit contract-family entries

Budget declarations belong below `contracts` as `strict_metric_budgets` and
`audit_metric_budgets`, following every other enforced family. The concrete
contract has `id`, `metric`, optional `minimum`, and optional `maximum`.
`metric` refers to the existing top-level `metrics` ID; it does not duplicate
the metric target or kind.

This retains explicit strict/audit behavior and makes `--contract-id`, family
cataloguing, result ordering, and policy provenance behave like established
contracts. A top-level `metric_budgets` list with a per-entry mode was rejected
because it would create a second mode convention and bypass contract selection.

### Treat metric evaluation as evidence, not a second budget calculator

The budget family selects distinct referenced metrics and uses the existing
metric evaluator to obtain their values, effective scope, sorted contributors,
and applicability records. A budget adapter compares only an evaluable value
against its declared inclusive bounds; equal values pass. Several budgets can
reuse one metric result. No contract handler may call a scanner or derive its
own graph or contributor set.

Reimplementing the count in a budget checker was rejected because it could
diverge from `measure` in ownership, mapping, ordering, or completeness.

### Adapt insufficiency to budget control identities through the shared projection

For an unassessable referenced metric, the family creates the normal required
control evidence for the owning budget and preserves its deterministic reason
and policy provenance. The existing applicability evaluator/projector then
produces the common normalized finding and completion state. An incomplete
scope therefore has no numeric comparison and cannot appear as a passing low
value.

Leaving the metric's measure-only control identity unchanged was rejected:
normal validation must identify which configured budget could not be assessed
and preserve that contract's strict/audit mode.

### Use a typed normal diagnostic payload for threshold findings

A typed metric-budget diagnostic/payload contains the budget ID, metric ID and
kind, native subject, effective scope, measured value, breached limit type and
limit, and canonical contributors. The family emits it through the normal
violation/diagnostic mapping path, with a canonical identity derived from the
budget contract and metric subject. Existing baseline, Human, JSON, SARIF, and
Testing projections consume that finding instead of a parallel serializer.

Formatting a budget finding ad hoc in the CLI was rejected because it would
lose canonical identity, baseline matching, and adapter parity.

### Validate at schema and typed policy seams

Both current policy-schema copies and their fragment schemas receive the new
contract properties and bounded object shape. A typed validator rejects
duplicate IDs, unknown metric IDs, absent bounds, negative numbers, and inverted
bounds after imports are composed. Contract-group/catalog/registry membership
is updated alongside this validator, so normal validation and policy-check
discover the family consistently.

Schema-only validation was rejected because imported effective policies and
cross-reference validation need the composed typed document.

### Keep user-facing additions focused

The existing architecture-metrics policy guide gains strict and audit examples,
bound semantics, and the distinction between a neutral measure result,
threshold violation, and unassessable scope. Tests cover Core behavior first,
then selected CLI/Testing and normalized-output/baseline projections only where
the new typed diagnostic requires them.

## Risks / Trade-offs

- **[Risk] Budget controls reuse a metric whose scope is incomplete** → Adapt
  each metric applicability reason to its owning budget before comparison and
  test unmapped, ambiguous, and empty inputs explicitly.
- **[Risk] Different entry points serialize different evidence** → Route a
  single typed diagnostic through the existing finding mapper and test JSON,
  SARIF, and Testing projections from the same canonical finding.
- **[Risk] Public Core policy models drift from review snapshots** → Run the
  explicit reviewed public-API update lifecycle only after implementation and
  inspect its diff.
- **[Risk] One budget ID is reused across modes or an imported policy** → Use
  existing contract ID catalogue/duplicate validation and test the selected
  strict/audit contract surfaces.

## Migration Plan

The fields are additive and default to empty collections, so existing policies
remain behaviorally unchanged. Authors first declare a metric and inspect it
with `measure`, then add a strict or audit budget that references that metric.
Rollback is safe by removing the budget entries; no persisted data or baseline
format migration is required.
