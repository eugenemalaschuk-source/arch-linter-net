## Context

The initial projector treated applicability as conformance, dropped canonical
reason provenance, reduced waiver lifecycle to aggregate totals, and opened a
second analysis path through baseline verification.

## Goals / Non-Goals

**Goals:**

- Keep topology, metric, external, waiver, and baseline semantics owned by
  their existing typed authorities.
- Preserve bounded deterministic drill-down provenance in JSON.
- Share the analysis state used by current validation and baseline comparison.

**Non-Goals:**

- A score, a new policy language, a second evaluator, or a new CLI exit code.

## Decisions

### Separate evaluability from conformance

Applicability records decide only configured/not-applicable/unassessable.
Health will project topology/metric/external conformance from the existing
typed finding/evidence receipts. A generic family shortcut is rejected because
`Evaluable` contains no conformance bit.

### Preserve canonical reason references additively

Health reasons retain stable code/source and gain nullable family, control,
policy, and evidence reference properties. Projectors copy provenance from
typed inputs rather than parse display strings. Copying entire authority
receipts is rejected because it would make the public result unbounded.

### Project waiver records and share candidates

The waiver dimension consumes canonical lifecycle records and their blocking
result instead of reconstructing state from totals. Baseline verification gains
an internal shared-candidate path so Health’s snapshot and persistent-debt
comparison use one candidate collection. Resolved entries stay baseline hygiene
and do not degrade health.

## Risks / Trade-offs

- [Finding classification could accidentally parse text] → use typed payloads,
  normalized findings, and real evaluator tests.
- [Shared candidates alter internal orchestration] → preserve existing public
  debt-gate callers and add a reuse-focused test.
- [Additive reason fields expand API] → review and refresh approved snapshots.
