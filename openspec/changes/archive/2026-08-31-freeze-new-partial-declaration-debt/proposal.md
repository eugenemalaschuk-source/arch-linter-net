## Why

`production-types-have-one-source-declaration` (`architecture/policy/audit-conventions.arch.yml`)
already detects handwritten production partial-type aggregates via `max_declarations_per_type: 1`,
but it runs in `audit_layout_conventions`, so it never fails `make lint-architecture`. During v0.8,
`ArchitectureDiagnosticFormatter` and other aggregates have kept growing (currently 19 declarations
for `ArchitectureDiagnosticFormatter` alone; 19 distinct production types exceed the limit today).
The active `decompose-god-classes` OpenSpec change owns removing that debt; this change only stops
it from growing further while v0.8 work continues, without pulling that refactor forward or
weakening the audit signal it already provides.

## What Changes

- Add a second, **strict** `layout_conventions` rule reusing the same `max_declarations_per_type: 1`
  evidence, so any handwritten production partial aggregate not already reviewed fails
  `make lint-architecture` immediately.
- Freeze today's 19 known offending types as reviewed debt using the engine's existing
  `ignored_violations` exact-match mechanism: one entry per type, with the exact current
  declaration count and file list. No baseline file, no new engine capability, no metric.
- Leave the existing audit rule untouched: it keeps reporting the full debt inventory as the
  target for `decompose-god-classes`' eventual full cleanup.
- Record the new strict rule's negative regression and update the self-policy capability matrix.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `self-architecture-policy`: The repository's strict self-policy additionally blocks any new or
  grown handwritten production partial-type aggregate, while a frozen, reviewed exception list
  keeps today's known aggregates from failing the gate until `decompose-god-classes` removes them.

## Impact

- Affects `architecture/policy/audit-conventions.arch.yml` (new `strict_layout_conventions` entry),
  `tests/ArchLinterNet.Core.Tests/SelfPolicyNegativeRegressionTests.cs` (new regression), and
  `docs/internal/self-policy-capability-matrix.md`.
- No Core/CLI/Testing source changes: `ignored_violations` exact `source_type`/`forbidden_reference`
  matching is existing, already-shipped engine behavior.
- No public API, schema, or dependency changes.
- Reducing or removing an aggregate's declaration count makes its frozen `ignored_violations` entry
  stop matching (visible as an unmatched ignored violation, which itself fails the gate by default
  until the stale entry is deleted) rather than silently staying green — improvement is expected to
  land together with removing the now-unnecessary reviewed exception in the same PR.
