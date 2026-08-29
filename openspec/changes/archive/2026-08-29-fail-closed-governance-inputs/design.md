## Context

`ValidationOutcome.Passed` currently has only a Boolean conformance meaning,
and the CLI maps it directly to exit `0` or `1`. The #505 OpenSpec contract
defines the expected-membership/produced-record distinction but deliberately
did not add a runtime model, schema fields, output envelope, or exit behavior.
No v0.8 topology, exposure, budget, or imported-diagnostic evaluator exists
yet, so the shared seam must be useful to those future families without
changing current policy behavior.

## Goals / Non-Goals

**Goals:**

- Materialize a small Core applicability/completion model that preserves
  canonical control identity, membership, state, reason code, and provenance.
- Derive one trusted `pass`, trusted `fail`, or valid-but-unassessable outcome
  from canonical expected entries and produced records without using finding
  counts as a proxy for evidence completeness.
- Carry completion data through `ValidationOutcome`, the Testing adapter, and
  CLI exit selection while keeping invalid invocation, invalid policy, runtime,
  cancellation, and output failures on their existing exit-2 paths.
- Keep empty applicability collections neutral for existing policy families;
  only an effective v0.8 family that supplies expected membership participates.

**Non-Goals:**

- No generic YAML setting, generic filesystem discovery, or family-specific
  schema/evaluator is introduced. Each future family validates its own explicit
  required/optional policy fields and creates canonical expected membership.
- No Architecture Health aggregation, policy-control inventory, baseline
  identity change, or Human/JSON/SARIF normalized-finding projection is added;
  #507/#679 own those seams.
- An unassessable condition is not manufactured as an `ArchitectureViolation`.

## Decisions

### 1. Use canonical applicability inputs, not an inferred outcome flag

Core will introduce immutable applicability expected-entry, produced-record,
reason/provenance, and completion types. The evaluator accepts expected entries
and records, validates duplicate and orphan identity integrity, performs the
required left-join semantics specified by #505, and returns stable ordered
completion evidence. `ValidationOutcome` carries this resulting completion as
an additive property.

This makes the denominator explicit and preserves the provenance future output
consumers need. A Boolean `isComplete` supplied by each family was rejected
because it could hide the missing-record/zero-match cases and would duplicate
aggregation logic in every evaluator.

### 2. Make trust completion orthogonal to ordinary conformance

Completion has three states: `pass`, `fail`, and `unassessable`. The evaluator
first detects required applicability/integrity insufficiency; any such result
wins over trusted conformance. If all participating required entries are
evaluable, existing `Passed` conformance determines `pass` versus `fail`.
Optional/not-applicable entries remain visible but do not create a required
denominator. Existing no-opt-in outcomes create an empty applicability input
and preserve their established `Passed` result.

Using a fourth exit code was rejected by the issue's public contract. Mapping
unassessable to `fail` was rejected because it would claim architecture was
successfully evaluated and found non-conformant.

### 3. Keep policy parsing family-owned and explicit

This shared change exposes the types and evaluation boundary but does not add a
catch-all `analysis` option. A family that opts in will validate its own policy
field(s), choose `required`, `optional`, or `not_applicable` membership, and
supply only family-native evidence. It must create unassessable records for
missing, unexpectedly empty, stale, unmapped, ambiguous, malformed, or wrong-
context required input.

A generic switch was rejected because it could make a required topology file,
SARIF artifact, and metric subject universe look semantically interchangeable.

### 4. Route completed trust status at the host boundary

After successful rendering, the CLI maps completion `pass` to `0`, `fail` to
`1`, and `unassessable` to the existing `2`. For a valid completed assessment,
it appends a compact Human completion line and adds a typed non-finding
`assessment_completion` object to JSON and a namespaced run property to SARIF.
The Testing adapter maps the same completion evidence onto its result. Existing
exception/error-routing paths continue to return `2` without a completion
object, preserving the distinction between an invalid request and a valid
assessment with insufficient evidence.

The shared normalized applicability-finding/identity projection stays out of
this change: the typed Core and Testing data is the #507 input seam, and its
output work must not be duplicated here. The direct completion status is an
additive host result property, not a parallel normalized finding envelope.

## Implementation slices

```text
Core applicability models + completion evaluator + ValidationOutcome
                         │
                         ├── Testing adapter mapping and focused tests
                         └── CLI exit + additive completion-status rendering and focused tests
```

The Core slice is the dependency root and has exclusive ownership of Core model,
validation, execution-result, and Core-test files. Once it is integrated, the
Testing and CLI slice owns only `src/ArchLinterNet.Testing/`,
`src/ArchLinterNet.Cli/`, and their respective tests; it must not alter Core
contracts.

## Risks / Trade-offs

- [Future family forgets to publish expected membership] → collection
  construction is explicit and reviewed per family; #507 will additionally
  project missing-record integrity rather than silently shrinking a denominator.
- [Public Core surface expansion] → add only immutable typed records/enums,
  preserve positional constructors with additive properties, update reviewed
  API snapshots intentionally, and retain compatibility tests.
- [No currently active v0.8 family exercises the seam] → use direct Core and
  CLI/Testing synthetic fixtures to pin required, optional, missing-record,
  duplicate/orphan, pass/fail, and unassessable behavior; later family tasks
  add their own end-to-end fixtures.
- [Output scope overlaps #507] → this change limits itself to exit selection
  and typed result transport; it does not add a second normalized finding or
  output envelope.

## Migration Plan

1. Add the additive Core model and derive completion from empty/opt-in
   applicability collections.
2. Carry the result through Testing and map it to CLI's existing exit category.
3. Update the reviewed public API snapshot explicitly after the public surface
   is verified.
4. Future family changes opt in only after their policy schemas and native
   evidence are implemented; rollback is safe because an empty collection
   retains existing behavior.
