## Context

See [proposal.md](proposal.md). `ArchitectureMetricEvaluator` is the existing sole authority for
metric measurement, but it currently contains the independent topology relation/footprint,
external-dependency-group, and public-contract-surface algorithms. #773 moved canonical metric
topology observation into `ArchitectureTopologyMetricObserver`; the metric evaluator must consume
that projection rather than topology-evaluator implementation helpers.

## Goals / Non-Goals

**Goals:**

- Give each metric-kind algorithm a purpose-named internal collaborator with a focused test seam.
- Preserve the existing `ArchitectureAnalysisSession`, its cached facts and scanners, and the
  single metric topology projection for every selected definition.
- Keep common contributor ordering, applicability records/completion, and immutable measurement
  construction in `ArchitectureMetricEvaluator`.

**Non-Goals:**

- Change metric formulas, completeness or identity semantics, policy/YAML syntax, public API, or
  metric-budget reuse.
- Extract or redesign `ArchitectureMetricBudgetAnalysisService`, topology observation, the public
  API scanner, or session caches.

## Decisions

### Keep a single coordinator and pass raw calculation evidence back to it

`ArchitectureMetricEvaluator` remains responsible for ordered definition selection, selected-ID
validation, complete-universe gating, topology scope preconditions, applicability completion, and
the normalized `ArchitectureMetricMeasurement`/evidence/result envelope. Collaborators return
only raw scope, unit, contributor identities, and canonical reason codes through a small internal
calculation result. This prevents each algorithm from constructing incompatible outcomes or
normalizing contributors differently.

This is preferred to a separate metric engine or per-kind public interface because the current
issue requires one authority and no extension mechanism.

### Partition algorithms by their existing fact authority

- A topology metric calculator owns mapped topology type-count, component-footprint, and
  incoming/outgoing direct component-relation contributor calculation.
- An external dependency-group calculator owns external-fact source identity recovery through
  `ArchitectureTopologyMetricObserver` and declared group contributors.
- A public contract-surface calculator owns public-surface contract lookup, resolved assembly
  identity binding, and reuse of `ArchitectureAnalysisSession.CapturePublicApiSurface`.

Each collaborator consumes the supplied session and topology projection. None can load
assemblies, scan source, build a graph, or call a scanner outside the existing session authority.
This grouping follows distinct data sources and completeness rules rather than merely splitting
the coordinator by line count.

### Retain the #773 observation/identity boundary

Metric collaborators consume `ArchitectureTopologyMetricObserver` APIs and observed topology
subjects for canonical project ownership, assembly identity, and source identity. They do not call
`ArchitectureTopologyEvaluator` observation/identity implementation helpers or reconstruct an
equivalent projection. Normal validation/capture remain outside this change.

### Keep focused tests at collaborator seams and retain outcome coverage

Existing end-to-end measure, applicability, project-ownership, and budget tests remain the
behavioral regression suite. Focused tests will directly exercise each extracted family where its
internal calculation seam exposes source/ownership or contributor behavior. Tests continue to
assert values, units, native subjects, scopes, contributor ordering, and unassessable reason codes
through the coordinator's public outcome.

## Risks / Trade-offs

- [Moving a reason check changes an unassessable outcome] → Centralize final normalization and
  retain the current focused completeness/applicability test families.
- [A collaborator independently repeats analysis work] → Pass the already-created session and
  topology projection only; do not add constructor dependencies or scanners.
- [A canonical project/assembly identity changes] → Reuse the #773 metric observer methods and
  run project-ownership and topology-identity tests.
- [Extraction is mechanical file splitting] → Use calculators named for distinct algorithms and
  remove those algorithms from the coordinator until it is below the 500-line threshold.

## Migration Plan

1. Add the internal raw calculation result and the three metric-kind collaborators; move existing
   algorithms without semantic reinterpretation.
2. Rewire the coordinator to dispatch to the collaborators while retaining selection,
   applicability/output construction, and budget reuse.
3. Add or move focused tests for collaborator seams, synchronize the umbrella cleanup evidence,
   then run metric, policy, API, formatting, code-size, and OpenSpec validation.
4. Archive this scoped no-delta change after synchronization; retain the umbrella cleanup change
   for its remaining work.
