## Context

`ValidationOutcome` already carries `CoverageFindings` and `CoverageConfig`.
The validation snapshot makes a coverage finding blocking only when the
configuration is `error`. Audit-mode validation outcomes already retain
violations, cycles, and imported diagnostics, but Health previously looked only
at strict failure for its current-evaluation dimension.

Health now reuses a single `ArchitectureAnalysisSnapshot` for validation and
baseline comparison. The receipt reuse flag proves the route, but consumers and
tests need the snapshot counters to prove that the expensive setup occurred
once.

## Decisions

### Project reportability separately from gate blocking

The coverage dimension reads the existing severity receipt. An `error` finding
is `fail`; a non-error finding is `degrading`, preserving its canonical
references while `ResolveGate` remains `pass`.

An `audit_evidence` dimension is `not_configured` without an audit receipt,
`pass` for a clean audit receipt, and `degrading` with audit diagnostics. It
does not alter `current_evaluation`, which remains the strict outcome, and
therefore does not turn audit-only diagnostics into a blocking gate failure.

### Expose the existing immutable counters

`ArchitectureHealthOutcome` receives an additive `AnalysisCounters` property
copied from the completed snapshot after both validation and debt comparison.
It exposes the existing typed counters rather than inventing a Health-specific
second measurement path. The integration test asserts both the shared debt-gate
receipt and single composition, graph evaluation, snapshot materialization, and
assembly load.

## Risks and mitigations

- Adding a dimension changes the ordered output shape. It is deterministic,
  documented, and protected by public API and projector ordering tests.
- Health may be `degrading` while its gate passes for warning/audit evidence.
  That is intentional: Health reports architecture attention independently of
  the established blocking validation gate.
