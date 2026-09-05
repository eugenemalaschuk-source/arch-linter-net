## Why

`ArchitectureTopologyEvaluator` currently combines topology-policy evaluation with two intentionally
different observation and identity projections. This makes the validation/capture compatibility
path and metric ownership path harder to evolve and leaves a reviewed partial aggregate in
production Core code.

## What Changes

- Extract internal topology observation and identity projection into purpose-named Execution
  collaborators.
- Keep normal validation and topology capture on their existing shared compatibility projection.
- Keep metric measurement on its existing canonical resolved-artifact ownership projection.
- Reduce `ArchitectureTopologyEvaluator` to one handwritten production declaration and remove its
  exact reviewed declaration-count exception.

## Capabilities

No externally observable capability or contract changes. This is an internal refactor that
preserves the existing topology policy, capture, metric, evidence, and public API behavior.

## Impact

Core Execution topology evaluation and observation code, focused Core topology/metric tests, the
self-policy declaration-count exception, and the active architecture-cleanup task evidence.
