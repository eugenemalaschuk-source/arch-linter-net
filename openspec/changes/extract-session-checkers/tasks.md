## 1. AssemblyIndependence extraction

- [x] 1.1 Create `src/ArchLinterNet.Core/Execution/Checkers/AssemblyIndependenceChecker.cs` with the algorithm body moved verbatim from `ArchitectureAnalysisSession.AssemblyIndependence.cs`, taking `(ArchitectureAssemblyIndependenceContract contract, IEnumerable<Assembly> targetAssemblies, ArchitectureContractExecutionContext executionContext)`.
- [x] 1.2 Reduce `ArchitectureAnalysisSession.CheckAssemblyIndependenceContract` to the selection-gate + `CreateExecutionContext` + delegate-to-checker + `CollectUnmatchedIgnores` wrapper.
- [x] 1.3 Confirm `AssemblyIndependenceContractTests.cs` and `AssemblyIndependenceStrictAuditTests.cs` pass unmodified.

## 2. PublicApiSurface extraction

- [x] 2.1 Create `src/ArchLinterNet.Core/Execution/Checkers/PublicApiSurfaceChecker.cs` with the algorithm body moved verbatim from `ArchitectureAnalysisSession.PublicApiSurface.cs`.
- [x] 2.2 Reduce `ArchitectureAnalysisSession.CheckPublicApiSurfaceContract` to the selection-gate + coverage-deferral-gate + `CreateExecutionContext` + delegate-to-checker + `CollectUnmatchedIgnores` wrapper.
- [x] 2.3 Confirm `PublicApiSurfaceContractTests.cs` passes unmodified.

## 3. Inheritance extraction

- [x] 3.1 Create `src/ArchLinterNet.Core/Execution/Checkers/InheritanceChecker.cs` with the algorithm body moved verbatim from `ArchitectureAnalysisSession.Inheritance.cs`.
- [x] 3.2 Reduce `ArchitectureAnalysisSession.CheckInheritanceContract` to the selection-gate + coverage-deferral-gate + `CreateExecutionContext` + delegate-to-checker + `CollectUnmatchedIgnores` wrapper.
- [x] 3.3 Confirm `InheritanceContractTests.cs` passes unmodified.

## 4. New isolation test coverage

- [x] 4.1 Add `tests/ArchLinterNet.Core.Tests/Checkers/AssemblyIndependenceCheckerTests.cs` exercising `AssemblyIndependenceChecker.Check` directly against a hand-built `ArchitectureContractExecutionContext` and a couple of real reflected assemblies — no `ArchitectureAnalysisSession` constructed.
- [x] 4.2 Add equivalent minimal-collaborator tests for `PublicApiSurfaceChecker` and `InheritanceChecker`.

## 5. Documentation

- [x] 5.1 Update `docs/internal/core-architecture-blueprint.md` to describe `ArchitectureAnalysisSession`'s narrowed role (context/cache/shared-state owner) versus `ArchLinterNet.Core.Execution.Checkers` (family algorithms), listing the three extracted families and naming the remaining session partials (`Checking.cs`, `Coverage.cs`, `PolicyConsistency.cs`, `Composition.cs`, and the contributor-coupled families) as scoped follow-up work.

## 6. Validation

- [ ] 6.1 Run `make fmt`.
- [ ] 6.2 Run `make acceptance` and confirm it is green.
- [ ] 6.3 Run `openspec archive extract-session-checkers` and `openspec validate --all`.
