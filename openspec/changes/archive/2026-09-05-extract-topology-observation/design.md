## Context

See [proposal.md](proposal.md). `ArchitectureTopologyEvaluator` is currently split across a policy
evaluation declaration and partial declarations for normal validation/capture observation and metric
ownership selection. The two projections deliberately have different identities: normal validation
and capture use the historical simple project/assembly identity, while metrics use canonical
resolved-artifact identity and project ownership.

## Goals / Non-Goals

**Goals:**

- Make the two observation projections explicit and independently testable.
- Keep topology policy evaluation responsible for mapping, relationships, staleness, applicability,
  and violations after receiving observed facts.
- Retain the existing analysis session as the single authority for type, reference-graph, project,
  and assembly facts.

**Non-Goals:**

- Change topology policy/schema semantics, public topology evidence, or metric identities.
- Unify the two identity projections or introduce a second assembly/source scan or graph.
- Decompose metric-kind algorithms or any unrelated execution aggregate.

## Decisions

### Give each observation contract a named owner

`ArchitectureTopologyValidationObserver` owns normal validation observation and the seam consumed
by capture. It retains the current type-index and reference-graph behavior, simple
project/assembly identities, witnesses, and stable ordering.

`ArchitectureTopologyMetricObserver` owns metric observation. It retains resolved-artifact
project ownership, canonical assembly identity, selector compatibility identity, assembly metadata
binding, and incompleteness facts. Both observers consume the existing session facts rather than
creating separate discovery or scanning services.

This is preferred over a boolean observation mode on the evaluator because the two compatibility
contracts are intentional and need direct names and focused tests.

### Keep the evaluator policy-focused

`ArchitectureTopologyEvaluator` accepts observation DTOs and performs selector/node
classification, relationship construction, stale-declaration inference, applicability projection,
and violation creation. Observation DTOs and projection records move to purpose-named internal
types when needed so ownership and test seams do not remain nested under the evaluator.

This preserves the existing evaluation algorithm and avoids reinterpreting facts while extracting
responsibilities.

### Preserve capture through the validation observer

`ArchitectureAnalysisSnapshot` calls the validation observer directly when it projects capture
DTOs. Ordinary validation calls the same observer before sending facts to the evaluator. This makes
the shared capture/validation compatibility contract structural instead of relying on duplicated
observation logic.

## Risks / Trade-offs

- [Moving identity code changes a visible identity or witness] → Move existing algorithms without
  reinterpretation and add focused validation/capture versus metric projection parity coverage.
- [A helper reconstructs a graph from a different authority] → Limit observer inputs to the
  existing analysis session indexes, reference graph, and resolved assemblies.
- [Internal type movement creates accidental API drift] → Run the reviewed public API check after
  focused tests and policy validation.

## Migration Plan

1. Introduce named internal observation collaborators and move existing observation models/helpers
   to their appropriate owner.
2. Rewire validation, capture, and metric entry points without changing their input sources.
3. Add parity coverage, remove the exact partial-aggregate ignore, and verify topology, metric,
   capture, architecture, and public-API gates.
4. Archive this scoped OpenSpec change after implementation; retain the umbrella cleanup change for
   its remaining tasks.
