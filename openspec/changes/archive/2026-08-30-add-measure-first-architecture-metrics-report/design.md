## Context

See proposal.md for the motivation. #516 established a closed catalog and
native counting contract but intentionally added no policy schema, evaluator,
report, or public API. The current Core session already owns one analysis
snapshot and authoritative topology, dependency, ownership, public-surface,
and generic applicability evidence; none can be reimplemented by a reporting
command. The topology mapper and public-surface selected exports require a
small internal projection seam because their current helpers are private.

## Goals / Non-Goals

**Goals:**

- Introduce one reusable Core measurement result, backed by the current
  analysis snapshot, for all six approved metric kinds.
- Surface metric-native scope/contributor evidence via the existing generic
  applicability collection rather than a parallel result model.
- Keep the CLI a thin read-only request/formatting layer with stable Human and
  JSON envelopes.

**Non-Goals:**

- Budget rules, thresholds, baselines, debt identity, SARIF for healthy
  values, dashboards, trends, or custom formulas.
- A project graph, synthetic assembly external nodes, repeated source scans,
  topology selector duplication, or a second public-API scanner.

## Decisions

### 1. Definitions are top-level policy data, separate from contracts

Add an optional top-level `metrics` collection. A definition is not a
strict/audit contract: it has an identifier, one closed `kind`, and a native
target (`topology_node`, a footprint `unit`, or `public_api_surface`). This
allows `measure` to work without an enforcement threshold and gives #518/#519
one stable metric identity to reuse.

The raw and effective schema validators reject malformed, duplicated, or
kind/target-incompatible definitions as ordinary policy configuration errors.
An absent collection remains a valid legacy policy. Putting metrics in a new
contract family was rejected because that would imply a strict/audit finding
and conflate neutral measurement with enforcement.

### 2. One Core evaluator returns both values and existing applicability evidence

Add a Core measurement application service, request/outcome models, and a
metric evaluator. It builds the same prepared analysis session/snapshot facts
used by normal analysis, evaluates selected definitions in canonical ID order,
and returns a report separate from `ValidationOutcome`. Each measurement has a
value only when evaluable; its contributors are canonical identities ordered
ordinally, and its effective scope is native evidence rather than display text.

The evaluator emits one existing `ArchitectureApplicabilityExpectedEntry` and
one existing `ArchitectureApplicabilityRecord` per definition, then uses the
shared evaluator/projector. Metric-native evidence is an additive field on the
shared applicability record/projection, not a metrics-only applicability type.
This is necessary to preserve #507’s common identity, reason, and completion
rules while making scope explainable.

The Core service reuses a narrowly extracted topology observation/mapping
projection and the existing public API selected-export authority. It does not
reuse graph display nodes or violation text: those lack the project projection
and complete external/public-surface facts required by #516. Extracting the
internal projection is justified by the concrete requirement to give topology
and metrics the same unmapped, ambiguous, reviewed-out-of-scope, stale, and
canonical ownership semantics.

### 3. `measure` reports evidence but does not validate a gate

Add a discovered CLI `Measure` command module with `--policy`, optional repeatable
`--metric`, `--format human|json`, `--condition-set`, and contributor-display
controls. `--max-contributors` defaults to a bounded deterministic value;
`--all-contributors` explicitly selects the full ordered set. JSON has a
top-level `schema_version: 1`, an overall completion state, ordered
measurements, and scope/applicability evidence. Human output is a concise
per-metric report over the same data.

Configuration/invocation errors use the normal typed error pathway. A report
with one or more unassessable selected metrics is emitted but returns the
existing exit-2 untrusted-result category. A complete report, including a
trusted zero, returns zero. SARIF is not an accepted measure format; no healthy
measurement enters the architecture finding pipeline.

### 4. Preserve current APIs and outputs unless a reusable model must be public

Core’s existing public composition/engine seam will expose the measurement
request/outcome only as needed by the CLI and Testing adapters; new public
surface is kept minimal and recorded in the reviewed Core API snapshot. All
existing validate, graph, policy, and SARIF result schemas remain unchanged.

## Risks / Trade-offs

- [Metric evaluator drifts from topology/public-surface authorities] → Extract
  narrow internal projections and use fixtures that prove parity for mapping,
  ownership, and selected exports.
- [An incomplete scope is rendered as a partial low count] → make value and
  trusted contributors absent for unassessable records; test every applicable
  reason family.
- [Large contributor sets make reports unwieldy] → provide a bounded default,
  full count/truncation metadata, and an explicit all-contributors option.
- [Public Core models grow inadvertently] → prefer internal helpers and update
  the approved public API snapshot only for intentionally reusable contracts.

## Migration Plan

The change is additive. Existing policies parse and validate unchanged because
`metrics` is optional; users opt in by declaring metrics and invoking
`arch-linter-net measure`. Removing the policy definitions or not invoking the
command restores prior behavior. The output schema starts at version 1, so a
later incompatible report change requires an explicit version decision.
