## 1. Session and indexes

- [x] 1.1 Add `ArchitectureTypeIndex` under `src/ArchLinterNet.Core/Execution/`: lazily memoizes all loadable types across target assemblies once, exposes `FindTypesInLayer`/`FindTypesInNamespace` filtered from the cached set.
- [x] 1.2 Add `ArchitectureReferenceGraph` under `src/ArchLinterNet.Core/Execution/`: lazily memoizes `GetReferencedTypes(type)` per type, exposes a lookup method returning the cached/materialized result.
- [x] 1.3 Add `ArchitectureAnalysisSession` under `src/ArchLinterNet.Core/Execution/`: wraps `ArchitectureAnalysisContext` plus the type index and reference graph instances.

## 2. Wiring

- [x] 2.1 Construct `ArchitectureAnalysisSession` in `ArchitectureRunnerFactory.BuildRunner` alongside the existing `ArchitectureAnalysisContext`.
- [x] 2.2 Thread the session into `ArchitectureContractRunner`'s constructor and expose it for use by the check methods.

## 3. Handler migration

- [x] 3.1 Migrate `ArchitectureContractRunner.CheckContract` (dependency family) to resolve source-layer types and referenced types via the session.
- [x] 3.2 Migrate `ArchitectureContractRunner.CheckLayerContract` (layer family) to resolve layer types via the session.
- [x] 3.3 Migrate `ArchitectureContractRunner.CheckCycleContract` (cycle family) to resolve layer types and referenced types via the session.

## 4. Tests

- [x] 4.1 Add unit tests for `ArchitectureTypeIndex` covering memoization (repeated calls reuse the cached type set) and result parity with `ArchitectureTypeScanner`.
- [x] 4.2 Add unit tests for `ArchitectureReferenceGraph` covering memoization (repeated calls for the same type reuse the cached result) and result parity with `ArchitectureReferenceScanner`.
- [x] 4.3 Run existing dependency/layer/cycle contract tests and confirm unchanged results.

## 5. Validation

- [x] 5.1 Run `make fmt`.
- [x] 5.2 Run `task acceptance:fresh` and fix any failures.

## 6. Spec sync and archive

- [x] 6.1 Confirm `openspec/changes/add-analysis-session-indexes/specs/analysis-session-indexes/spec.md` reflects the final implementation.
- [x] 6.2 Run `openspec archive add-analysis-session-indexes` and `openspec validate --all`.
