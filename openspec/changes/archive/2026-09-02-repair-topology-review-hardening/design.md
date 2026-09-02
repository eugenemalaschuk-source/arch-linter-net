## Context

Topology diff is a projection of ordinary validation, while output publication must never mutate a
trusted input. The current CLI has two mismatches with those contracts: diff loses the
applicability record that qualifies evidence, and output collision detection reconstructs a broad
source-input set with a second repository walk. Lifecycle tests and custom help text do not yet
prove the same guarantees end-to-end.

## Goals / Non-Goals

**Goals:**

- Retain ordinary declared-topology applicability in the diff report and make a non-reviewable
  unassessable result explicit.
- Use analysis/validation provenance as the sole input manifest for output alias checks.
- Test atomic output, cancellation, help/parser parity, and immutable real fixture inputs.

**Non-Goals:**

- Change the ordinary validation evaluator or make review drift a second approval policy.
- Broaden topology capture's options to validation-only external-evidence inputs.
- Scan arbitrary repository files to infer additional inputs.

## Decisions

- **Carry the native applicability record into `TopologyDiffReport`.** The renderer serializes
  state, reasons, membership, and provenance alongside review categories. An unassessable record
  with `unexpected_empty_input` is a typed non-reviewable outcome and returns the ordinary
  runtime-error exit code after writing its diagnostic artifact. Mapping, ambiguity, and stale
  evidence remain review categories when the evaluator supplied projectable evidence.
- **Collect consumed paths at the analysis boundary.** Capture and validation outcomes expose
  reviewed additive provenance properties for the paths that the session actually trust-read. The
  CLI combines those paths with explicit
  baseline/external-evidence inputs; it never discovers inputs by a recursive CLI scan. A
  non-existent output cannot be a hard-link alias, so the guard avoids identity probes entirely.
- **Keep atomic publication as a single handler primitive.** It cleans up a temporary file on all
  failures but rethrows `OperationCanceledException` so the command-level cancellation contract
  reports `cancelled`.
- **Keep option registration and help textual contract together.** A shared validation-options
  help fragment is used by both diff and verify; capture lists only options registered by its own
  module. Tests assert registered parser options and advertised help stay aligned.

## Risks / Trade-offs

- [Incomplete provenance] → Add focused tests covering source, project, policy/import, asmdef,
  assembly, receipt, baseline, and external-evidence paths exposed by real outcomes.
- [Unassessable reasons evolve] → Classify the current non-projectable reason by its stable
  reason code and preserve the complete record in the artifact for reviewers.
- [Publication output unavailable after cancellation] → The temporary artifact is deleted before
  cancellation propagates; no replacement occurs before atomic rename.
