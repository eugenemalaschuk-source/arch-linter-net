## Why

The existing attribution harness proves that independent CLI processes repeat
preparation work, but it measures only strict validation and cannot decide
whether persisted cross-process reuse is materially better than a simpler
one-process multi-projection workflow. Issue #493 needs a current, consumer-
shaped measurement gate before the prepared-analysis implementation lane can
start.

## What Changes

- Extend the canonical benchmark foundation with an explicitly invoked,
  synthetic/anonymized multi-command governance harness for both
  receipt-backed real-MSBuild and externally staged-assembly adopters.
- Model the current command families, including strict, audit, no-new-debt,
  Architecture Health, change snapshots, and applicable topology/measure and
  public-API operations without forcing commands that a representative
  workflow does not use.
- Measure independent one-shot processes, one-process multi-projection
  execution, and a persisted prepared-state expected-effect model over the
  same candidate build state; route reference/base revision work separately.
- Record per-process and aggregate phase/counter evidence, canonical-result
  equivalence, cache/prepared modes, and an issue-specific break-even/effect
  contract for the #492 decision.
- Add deterministic contract tests and public-safe evidence/documentation while
  keeping the hardware-sensitive matrix out of the normal test and acceptance
  gates.

## Capabilities

### New Capabilities

### Modified Capabilities

- `large-solution-benchmarking`: require a current multi-command,
  adopter-shaped preparation-reuse decision that compares process boundaries,
  in-process projections, and the expected persisted-state effect.

## Impact

- Extends Core test-only benchmark and adoption fixture infrastructure; no
  product runtime API or persisted-analysis implementation is introduced.
- Adds internal machine-readable and human-readable evidence for issue #493,
  reusing `analysis-profile/v1` and `benchmark-evidence/v1`.
- Updates benchmark documentation and OpenSpec requirements; no private
  adopter identity, repository metadata, or raw private logs are committed.
