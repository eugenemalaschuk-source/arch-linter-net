## Context

See proposal.md for motivation. #518 established `metric_budgets` as the sole
normal validation family over the shared deterministic metric evaluator. Its
baseline candidates are exact threshold-finding identities for #121; they do
not contain passing metric values and must remain independent of ratcheting.
The existing baseline YAML schema is closed and supports only versions 1 and 2.

## Goals / Non-Goals

**Goals:**

- Ratchet the current closed set of count metrics without adding a validation
  mode, second metric evaluator, or parallel result envelope.
- Make a reviewed scalar baseline comparable only when its canonical metric
  definition and subject match the current deterministic measurement.
- Retain explicit strict/audit behavior, normal baseline finding behavior, and
  Human/JSON/SARIF/Testing projection parity.

**Non-Goals:**

- Trend storage, automatic mutation of reviewed values, arbitrary formulas, or
  making a change snapshot into a metric history store.
- Replacing #121 finding-debt lifecycle or treating a metric baseline entry as
  an ignored violation.
- Introducing a lower-bound ratchet; every supported metric is a cardinality
  and this change defines growth as the only worsening direction.

## Decisions

### Use a version-3 baseline document with a distinct metric collection

Version-2 entries are structured finding identities that flow into the ignore
matcher. They cannot safely represent a scalar metric value, particularly for a
passing relative budget. Version 3 adds a top-level `metric_baselines` list
while retaining the existing `baseline` collection and its structured finding
identity requirements. Version 1 and version 2 continue to load unchanged.

Each entry contains `metric_identity_version: 1`, `metric_id`, `metric_kind`,
`native_subject`, `unit` when applicable, `effective_scope`, and `value`. The
canonical identity uses those machine fields exactly; `metric_id` is unique in
the list and the remaining fields prove that the same metric definition and
native subject are being compared. A changed identity version or any mismatched
identity field is stale, never a candidate for coercion or display-text match.

A new standalone metric-baseline file was rejected because callers already
provide one reviewed baseline input and need ordinary finding debt plus metric
ratcheting to remain reviewable atomically. Reusing `ignored_violations` was
rejected because it would conflate scalar values with #121 suppression.

### Extend the existing budget contract with bounded relative fields

`ArchitectureMetricBudgetContract` gains `baseline_mode` and `max_delta`.
`baseline_mode` is either `no_worse_than_baseline` (implicit allowed delta zero)
or `max_delta` (a required non-negative `max_delta`). In relative mode the
existing `maximum` field is an optional absolute cap; `minimum` is rejected.
Without `baseline_mode`, the current absolute `minimum`/`maximum` rules remain
unchanged and `max_delta` is rejected.

For an evaluable metric with matching baseline value `b`, allowed delta `d`,
and optional maximum `m`, the service computes `current - b` and compares
`current` to `min(b + d, m)` when a cap exists, otherwise `b + d`. Arithmetic
uses a widened intermediate before producing deterministic output so a large
reviewed value plus delta cannot overflow. An absolute cap takes precedence in
the breach label when it is the effective threshold.

Adding a third validation mode or a generic `mode` field was rejected: strict
and audit remain contract-collection semantics, and `baseline_mode` makes the
new dimension unambiguous in YAML and policy context.

### Reuse one metric measurement and common insufficiency projection

The budget family continues to select unique metric IDs and invokes
`ArchitectureMetricEvaluator` once. It creates metric-value baseline candidates
only from complete relative measurements, independently of existing
threshold-finding candidates. The baseline application/generation service
receives that separate candidate set when it explicitly generates a baseline.

An incomplete current metric retains the established metric applicability
projection. An evaluable metric with a missing or stale reviewed entry produces
unassessable applicability evidence for the owning `metric_budgets` control
using dedicated deterministic reason codes. Neither path performs a numeric
comparison or lets audit/strict policy execution look clean. A matching
baseline permits one normal metric-budget violation when its bounded threshold
is exceeded.

A second baseline-result envelope was rejected because the common applicability
model already carries control identity, provenance, mode, and completion
semantics for inputs that cannot safely be assessed.

### Add relative evidence additively to typed metric-budget projections

The existing metric-budget payload and diagnostic retain their absolute-bound
fields. Additive optional members carry baseline mode/value, current-minus-
baseline delta, allowed delta, effective threshold, and optional absolute cap
for relative findings. The canonical finding identity remains budget-control
based and distinct from the scalar metric-baseline identity. Existing mapping,
SARIF, JSON, Human, Testing, and public API approval mechanisms then project
the same typed diagnostic rather than adding formatter-specific text.

### Keep capture explicit and lifecycle writes conservative

`baseline generate` produces version 3 only when selected relative budgets
need metric values. It emits one sorted entry per complete selected metric in
addition to ordinary finding entries. `baseline update` and `baseline prune`
copy the existing metric collection unchanged; they must not refresh values or
infer a reviewed value from a new run. A normal validation run is read-only.

This supports an intentional review of generated YAML while preventing a
routine debt update or CI check from silently accepting architectural growth.

### Cover all schema and static-policy seams

Both policy schema copies receive the new closed contract fields. The baseline
schema accepts version 3 and validates its distinct collection. Packaged schema
metadata is updated to advertise the supported version. Raw and typed policy
validators, policy-context projection, and policy-weakening fact comparison are
extended for the added fields so static policy tools cannot omit or reinterpret
relative gate configuration.

## Risks / Trade-offs

- **[Risk] A value is compared after its subject changes** → bind and validate
  kind, native subject, unit, and effective scope in a versioned scalar identity.
- **[Risk] A routine lifecycle command blesses drift** → only explicit generate
  captures current metric values; update and prune preserve them verbatim.
- **[Risk] Relative arithmetic overflows** → calculate baseline-plus-delta with
  a widened integer and expose the actual bounded threshold deterministically.
- **[Risk] Existing output consumers misread a relative breach** → preserve
  existing absolute fields and add typed optional comparison evidence through
  the canonical projection registry.
- **[Risk] A policy becomes less strict through a new field unnoticed** →
  project baseline mode/delta facts and classify their changes in the existing
  policy-weakening path.

## Migration Plan

1. Existing policies and baseline versions 1/2 retain their behavior because
   no relative budget activates without new contract fields and a version-3
   baseline input.
2. An adopter measures a supported metric, adds a relative budget, explicitly
   runs and reviews `baseline generate`, and commits the generated version-3
   YAML.
3. Rollback consists of removing the relative fields or restoring the reviewed
   baseline file; no data migration, automatic rewrite, or finding-debt change
   is required.
