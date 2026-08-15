## Why

Handwritten `partial` declarations have become a substitute for decomposing responsibilities:
`ArchitectureContractGroups` has 32 declarations, `ArchitectureAnalysisSession` 30, and
`ArchitectureDiagnosticFormatter` 15. File-size lint therefore reports small files while the
compiled types remain high-coupling god classes that are difficult to understand, test, and evolve.

## What Changes

- Add source-backed policy support for detecting a type split across handwritten partial
  declarations, reporting its declaration count and paths deterministically.
- Add a self-policy that makes production partial types an explicit, reviewed exception rather than
  the default decomposition mechanism.
- Replace the largest production partial aggregates with collaborating, purpose-named types while
  preserving public API and diagnostic output.
- Treat direct `Cli.Commands` children as independent command modules, while retaining the common
  `Abstractions`/`Models`/`Exceptions` conventions at every nesting level.
- Reduce test-suite partial aggregates where they hide unrelated scenarios behind one fixture;
  keep only narrowly scoped test fixtures and generated-code scenarios that require `partial`.
- Retain the existing file-size lint as a complementary per-file signal; it is not a measure of
  compiled-type cohesion.

## Capabilities

### New Capabilities

- `partial-type-governance`: Policy evaluation and diagnostics for handwritten partial-type
  declaration limits and reviewed exceptions.

### Modified Capabilities

- `self-architecture-policy`: The repository's strict self-policy governs production partial types
  and proves that a new unreviewed aggregate fails the architecture gate.
- `layout-convention-contracts`: Source-fact collection exposes the partial-declaration evidence
  required by type-layout policy checks without treating it as an ambiguous file-path match.

## Impact

- Affects Core source parsing/indexing, policy schema and validation, self-policy YAML, tests, and
  the large `ArchitectureAnalysisSession`, `ArchitectureContractGroups`, and reporting aggregates.
- No new runtime dependencies and no intentional public API changes; reviewed snapshots remain
  exact and are updated only if a necessary surface correction is demonstrated.
