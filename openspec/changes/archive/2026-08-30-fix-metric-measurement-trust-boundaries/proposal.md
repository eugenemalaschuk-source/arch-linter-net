## Why

The measure-first metrics report must never convert an ambiguous ownership
binding, an unknown contributor universe, or a cancelled lazy materialization
into an apparently trusted Core result. The current public and snapshot seams
still permit those three cases despite the metric semantics' fail-closed
contract.

## What Changes

- Bind project metric subjects to one resolved project artifact identity rather
  than a simple output assembly name, and make missing or ambiguous bindings
  unassessable.
- **BREAKING** Represent contributor evidence as unavailable for an
  unassessable Core measurement instead of a proven empty contributor set.
- Make a cancelled `Measure()` operation irreversibly cancel its analysis
  snapshot just as `Evaluate()` already does.
- Add regression coverage for duplicate output assembly names, unknown Core
  contributor evidence, and measurement cancellation reuse.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-metric-semantics`: require project metric ownership to bind a
  unique resolved project artifact and fail closed on ambiguity.
- `architecture-metric-measurement`: require the public Core measurement model
  to keep contributor evidence unavailable for unassessable results.
- `analysis-snapshot`: apply the existing cancelled-snapshot lifecycle to
  `Measure()` as well as `Evaluate()`.

## Impact

Core metric topology/evaluation and session metadata indexes, public Core
measurement models and approved API surface, analysis snapshot lifecycle
handling, NUnit regression suites, and the measure-first OpenSpec artifacts.
