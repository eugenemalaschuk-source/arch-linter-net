## Context

The current source index deliberately collapses declarations of the same CLR type into one
`ArchitectureDeclaredTypeFact`; when a type spans files it records an ambiguity and drops the source
path. That behaviour is correct for file-to-type layout checks, but it makes the number of
handwritten partial declarations invisible to policy. Consequently per-file size lint can be green
while a compiled type has dozens of disconnected source fragments.

The current inventory contains 32 `ArchitectureContractGroups`, 30
`ArchitectureAnalysisSession`, and 15 `ArchitectureDiagnosticFormatter` declarations. Tests also
use `partial` extensively to group unrelated scenarios and must be reduced without removing the
small source fixtures that intentionally model partial-type semantics.

## Goals / Non-Goals

**Goals:**

- Make declaration count and every source path for a source type available to policy evaluation.
- Make an over-limit partial aggregate a strict, deterministic architecture violation.
- End with no handwritten production partial aggregate; each extracted collaborator has one clear
  responsibility and a direct test boundary.
- Keep public surface and externally observable diagnostic content stable.

**Non-Goals:**

- Do not impose a universal line-count limit on types or infer cohesion from LOC alone.
- Do not forbid compiler-generated partial declarations or partial source fixtures that explicitly
  model language semantics.
- Do not reorganize files solely to satisfy a count while retaining the same dependency graph.

## Decisions

### Preserve a declaration inventory alongside the existing type index

`ArchitectureDeclaredTypeParser` will record whether each type declaration has the `partial`
modifier. `ArchitectureSourceFileFactIndex` will retain a separate, path-complete declaration
inventory keyed by assembly and CLR full type name. The existing `ArchitectureDeclaredTypeFact`
and its ambiguity semantics remain unchanged for consumers that need a unique source file.

This separates two facts that must not be conflated: an ambiguous primary path, and an exact list
of declarations. Replacing the existing fact with a list would break file-based policy selectors.

### Extend layout conventions with a type declaration-count expectation

`strict_layout_conventions` and `audit_layout_conventions` gain an optional
`max_declarations_per_type` expectation. It evaluates the complete declaration inventory after
the file selector chooses a governed production area. A violating diagnostic reports the full type
name, observed count, configured maximum, and stable-sorted paths.

Extending the existing source-layout family reuses selectors, audit/strict behaviour, ignores,
coverage, and reporting. A standalone contract family was rejected because its only selector would
duplicate file-layout semantics and create another policy surface for the same source evidence.

### Migrate from audit to strict only after responsibility extraction

The first policy rule runs in audit mode to produce an exact baseline. Refactors remove handwritten
production partial aggregates, then the same rule becomes strict with a maximum of one source
declaration per type. Test source remains outside this production rule because a small number of
fixtures must model C# partial declarations; test aggregate cleanup is proven by targeted fixture
splits rather than a blanket rule that would prohibit the language test cases.

### Treat direct CLI commands as independent feature modules

`ArchLinterNet.Cli.Commands` is a host-level feature container rather than a domain hierarchy.
Its direct children (`Baseline`, `Cache`, `Explain`, `Graph`, `Policy`, `PublicApi`, `Schema`, and
`Validate`) are command modules. They may use the CLI's top-level abstractions, Core's public
application seams, and narrowly named shared support outside a command module, but no command may
reference another command's namespace.

The generic convention rules remain recursive: a command's interfaces belong in `Abstractions`,
its models belong in `Models` and have no first-party dependencies, and its exception classes are
self-contained and belong in `Exceptions`. Those rules are not duplicated per command. The
command-specific policy only supplies the missing sibling-boundary invariant.

### Audit baseline (2026-08-15)

`production-types-have-one-source-declaration` reports these fifteen production aggregates. The
temporary audit report is intentionally not committed; the table captures its deterministic,
reviewable baseline and provides the extraction order.

| Type | Declarations |
| --- | ---: |
| `ArchitectureContractGroups` | 32 |
| `ArchitectureAnalysisSession` | 30 |
| `ArchitectureDiagnosticFormatter` | 15 |
| `ValidateCommandHandler` | 5 |
| `ArchitectureSourceSetExpander` | 3 |
| `ArchitectureSarifFormatter` | 3 |
| `ReportCoordinator` | 2 |
| `ArchitecturePolicyDocumentLoader` | 2 |
| `ArchitectureSourceFileFactIndex` | 2 |
| `LayoutConventionChecker` | 2 |
| `IArchitectureDiagnosticFormatter` | 2 |
| `IArchitectureSarifFormatter` | 2 |
| `ArchitectureAnalysisSnapshot` | 2 |
| `ArchitectureBaselineApplicationService` | 2 |
| `ArchitecturePublicApiApplicationService` | 2 |

### Extraction progress (2026-08-15)

The first completed tranche removes the two largest aggregates and three incidental interface or
policy-loader fragments. `ArchitectureAnalysisSession` is now a one-source orchestration facade
over named coverage and policy-consistency collaborators. `ArchitectureContractGroups` is a single
non-partial YAML binding root; the contract-family files retain only their own family model types.
`ArchitecturePolicyDocumentLoader` remains `partial` in its one handwritten source file only
because the `GeneratedRegex` source generator emits the companion declaration; generated code is
not part of the source declaration inventory.

`ArchitectureSourceSetExpander` is also now a single non-partial expansion orchestrator. Its
source/source-set inclusion resolution and layer-template container expansion are separately owned
by `ArchitectureSourceSetInclusionResolver` and
`ArchitectureLayerTemplateContainerExpansionRecorder`.

The post-tranche audit has nine handwritten source aggregates remaining:

| Type | Declarations |
| --- | ---: |
| `ArchitectureDiagnosticFormatter` | 15 |
| `ValidateCommandHandler` | 5 |
| `ArchitectureSarifFormatter` | 3 |
| `ReportCoordinator` | 2 |
| `ArchitectureSourceFileFactIndex` | 2 |
| `LayoutConventionChecker` | 2 |
| `ArchitectureAnalysisSnapshot` | 2 |
| `ArchitectureBaselineApplicationService` | 2 |
| `ArchitecturePublicApiApplicationService` | 2 |

### Extract collaborators, not more fragments

`ArchitectureAnalysisSession` becomes a coordinator over existing and newly named family analysis
components. `ArchitectureDiagnosticFormatter` delegates detail rendering to renderer/projection
collaborators, and `ArchitectureContractGroups` moves contract-family binding/model concerns into
named groups or factories. No replacement type may be `partial`; private helper methods remain
with the collaborator that owns their data and dependencies.

#### `ArchitectureTopologyEvaluator` observation extraction (#773)

`ArchitectureTopologyEvaluator` evaluates an already-observed topology projection: selector and
node classification, relationship and stale-declaration evidence, applicability, and ordinary
violations. It does not own CLR/project/assembly observation or identity selection.

`ArchitectureTopologyValidationObserver` owns the historical normal-validation projection, using
the session's existing type index and reference graph with the simple project/assembly identity
format. `ArchitectureTopologyCaptureService` reaches that same observer through the validation
snapshot, so capture and normal validation retain identical subjects, dependency witnesses, and
ordering.

`ArchitectureTopologyMetricObserver` owns the distinct metric projection. It consumes the same
session facts and existing assembly metadata graph, but retains canonical resolved-artifact
identity and project ownership for metric contributors. It does not create another source or
assembly scan, and its projection is deliberately not substituted for normal validation or
capture. Shared internal observation DTOs carry facts between those collaborators and the
evaluator without changing public topology evidence or result contracts.

#### `ArchitectureMetricEvaluator` metric-kind extraction (#779)

`ArchitectureMetricEvaluator` remains the sole metric measurement coordinator: it selects
definitions, validates requested IDs, applies complete-universe gates, completes applicability,
normalizes contributor/reason ordering, and creates immutable measurement outcomes. It consumes
the canonical metric projection from `ArchitectureTopologyMetricObserver` rather than topology
evaluator implementation helpers.

`ArchitectureTopologyMetricCalculator` owns mapped type-count, footprint, and direct component
relation contributors. `ArchitectureExternalDependencyMetricCalculator` owns external-fact source
identity recovery and group contributors. `ArchitecturePublicContractMetricCalculator` owns public
surface contract/assembly resolution and reuses the session's existing public-surface capture.
They return raw internal evidence to the coordinator; none creates a graph, loads assemblies,
scans source, repeats public-surface scanning, or constructs a measurement outcome.

Direct calculator seam tests cover representative complete and unavailable topology/public-surface
evidence, while the external calculator retains its focused source-identity tests. The existing
measurement, applicability, project-ownership, and budget tests continue to prove coordinator
outcomes and public behavior.

#### `ArchitectureAnalysisSession` responsibility map

- **Session:** owns immutable policy/run inputs, run-scoped indexes and caches, selection state,
  baseline/finding identity accumulation, and the stable public facade consumed by the runner.
- **`ArchitectureConfigurationValidationService`:** owns the ordered configuration-validation
  phase (assembly, discovery, layer, dependency-group and project-metadata diagnostics) while
  consuming the session's cached facts through its narrow internal access surface.
- **`ArchitectureCoverageAnalysisService`:** owns coverage-check orchestration and delegates its
  matching, dependency-edge, semantic-coverage, and summary work to focused collaborators.
- **`ArchitecturePolicyConsistencyAnalysisService`:** owns policy-consistency analysis and its
  protected-importer consistency collaborator.
- **Remaining seams:** command handling, source indexing, layout matching, validation snapshots,
  and diagnostic/SARIF rendering still require named collaborators before the final strict rule
  can be enabled.

### Retire an unused CEL placeholder

`CelEngine` had no call sites and did not participate in the supported CEL execution path. The
actual path is `CelEnvironment` through parsing/binding and compiled expressions to
`CelEvaluator`. Keeping the empty public placeholder would preserve a misleading second execution
seam, so it and its smoke test are removed rather than expanded into another god class. The
internal CEL architecture documentation now describes the evaluator pipeline directly.

### Preserve compatibility by construction

Public entrypoints retain their signatures unless a deliberate contract correction is required.
Any public interface split uses a primary interface and internal collaborators rather than public
partial fragments. Public API snapshots are checked read-only before and after each migration
tranche; a correction (including a namespace correction) requires an explicit, reviewed snapshot
update rather than being hidden as refactoring churn.

## Risks / Trade-offs

- [A count threshold rewards artificial delegation] → require named collaborator responsibilities,
  direct tests, and preserve dependency-direction policy in every tranche.
- [Partial source fixtures are falsely governed] → scope the strict rule to `src` and prove the
  selector with both product and test fixtures.
- [Moving analysis methods changes ordering or diagnostics] → retain ordering tests and compare
  normalized human/JSON/SARIF output through existing approval and parity tests.
- [The broad refactor produces unreviewable diffs] → land the inventory/rule, session, contract
  groups, formatter, and test cleanup in separately reviewable task slices.

## Migration Plan

1. Add the parser/index inventory and audit declaration-count rule with unit and policy tests.
2. Record the production baseline and extract `ArchitectureAnalysisSession` collaborators.
3. Extract `ArchitectureContractGroups` and diagnostic-formatting collaborators.
4. Split non-semantic test aggregates; retain dedicated partial-language fixtures.
5. Switch the production rule to strict at a maximum of one declaration, run full lint/test/API
   checks, and remove the audit baseline.

Rollback is safe: retain the audit rule and revert an individual collaborator extraction without
changing any policy or public API contract.
