## 1. Registry scaffolding

- [x] 1.1 Add `ArchitectureDiagnosticFormatter.DetailProjectionRegistry.cs` with an entry record (diagnostic `Type` + projector delegate) and a `DiagnosticDetailProjectionRegistry`-style ordered static list plus a `Dictionary<Type, ...>` lookup, mirroring `ArchitectureContractFamilyRegistry`/`ArchitectureContractHandlerRegistry`.
- [x] 1.2 Point `ApplyDiagnosticSpecificCiFields` at the registry lookup, throwing `InvalidOperationException` for an unregistered type.

## 2. Extract inline switch cases into named projectors

- [x] 2.1 Extract `ExternalDependencyDiagnostic`, `PackageDependencyDiagnostic`, `PackageAllowOnlyDiagnostic`, `CycleDiagnostic`, `UnmatchedIgnoreDiagnostic`, `PolicyConsistencyDiagnostic`, `BaselineLifecycleDiagnostic`, `ArchitecturePolicyErrorDiagnostic` into named `Apply<Kind>CiFields` methods in `.NormalizedDetails.cs`, with verbatim field-by-field bodies (no logic change).
- [x] 2.2 Extract `FrameworkReferenceDiagnostic`/`FrameworkReferenceAllowOnlyDiagnostic` into `Apply<Kind>CiFields` methods in `.FrameworkReference.cs`, each delegating to the existing `ApplyFrameworkReferenceEvidenceCiFields`.
- [x] 2.3 Move `ApplyBuildStatePreflightCiFields` from `.NormalizedDetails.cs` into `.BuildStatePreflight.cs`.
- [x] 2.4 Register all 24 diagnostic types in the registry, referencing existing or newly-extracted projector methods, in the same relative order as the former switch.

## 3. Tests

- [x] 3.1 Add a reflection-based completeness test asserting `DiagnosticDetailProjectionRegistry.All` covers exactly the sealed, non-abstract `ArchitectureDiagnostic` subtypes in the Core assembly, with no duplicates.
- [x] 3.2 Add a test asserting an unregistered diagnostic type throws `InvalidOperationException` (if feasible without a real unregistered type — otherwise cover via the completeness test's exhaustiveness guarantee alone).
- [x] 3.3 Run the full existing `ArchitectureDiagnosticFormatterTests` suite (including `.FrameworkReferenceDiagnostics.cs`, `.PackageDiagnostics.cs` partials) and `ArchitectureDiagnosticFormatterCoverageTests` unmodified; confirm zero diffs.
- [x] 3.4 Run SARIF formatter and Testing adapter tests for representative diagnostic families to confirm no downstream drift.

## 4. Validation

- [x] 4.1 Run `make fmt` and inspect the diff. (`dotnet format` produced no changes; only the intended files are touched.)
- [x] 4.2 Run focused `ArchLinterNet.Core.Tests` formatter/reporting tests, then the full `ArchLinterNet.Core.Tests` suite (cross-cutting tier: shared Core infrastructure). (Full suite: 2664 passed, 0 failed, 4 skipped — unrelated Windows/benchmark-only tests.)
- [x] 4.3 Run `make lint-architecture` to confirm the reporting/model boundary still holds. (Passed.)
- [x] 4.4 Run `openspec validate --all`. (117 specs + the active change all passed.)

## 5. Spec sync and archive

- [ ] 5.1 Confirm implementation matches the `diagnostic-detail-projection-registry` and `diagnostics-model` delta specs; adjust either the code or the spec wording if they've drifted.
- [ ] 5.2 Run `openspec archive decouple-diagnostic-detail-projection`.
- [ ] 5.3 Run `openspec validate --all` again post-archive.
