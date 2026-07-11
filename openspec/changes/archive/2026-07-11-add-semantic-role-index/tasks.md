## 1. Role index implementation

- [x] 1.1 Add `ArchitectureRoleIndex` in `src/ArchLinterNet.Core/Execution/ArchitectureRoleIndex.cs`: constructor takes `(ArchitectureClassificationConfiguration classification, ArchitectureTypeIndex typeIndex)`, exposes a `Lazy<T>`-backed single pass computing a `Dictionary<Type, ArchitectureTypeClassificationResult>` plus deduplicated `Conflicts`/`MetadataFailures` collections, short-circuiting to empty results when `classification` has no `attributes`/`assembly_attributes` entries.
- [x] 1.2 Add lookup APIs: `TryGetRole(Type type, out ArchitectureTypeClassificationResult descriptor)` and `ClassifiedTypes()` (enumerate types with a resolved role).
- [x] 1.3 Expose `Conflicts`/`MetadataFailures` as read-only properties on the index.
- [x] 1.4 Wire `ArchitectureAnalysisSession.RoleIndex` property, constructed alongside `TypeIndex`/`ReferenceGraph` in `ArchitectureAnalysisSession.cs`.
- [x] 1.5 Refactor `ArchitectureAnalysisSession.Classification.cs`'s `CheckClassificationFacts()` to return `RoleIndex.Conflicts`/`RoleIndex.MetadataFailures` instead of re-running the extractor.

## 2. Output wiring

- [x] 2.1 Add `ClassificationRoles` (or equivalent) to `ValidationOutcome` in `src/ArchLinterNet.Core/Validation/ValidationOutcome.cs`.
- [x] 2.2 Populate it in `ArchitectureValidationApplicationService.Validate` from `runner.Session.RoleIndex`.
- [x] 2.3 Add `classification_roles` array (subject, role, metadata, source; sorted by subject) to `ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts`.
- [x] 2.4 Decide and implement whether role descriptors also appear in human-readable output (`FormatClassificationFactsForHumans` or a new formatter method) — keep it consistent with the existing "Classification findings:" section style; only emit the section when at least one role/conflict/failure exists. Decision: JSON/CI-artifact output only (matches acceptance criteria's "if aligned with existing output conventions"); human output is left unchanged to avoid unbounded per-type noise in text output.
- [x] 2.5 Update `ICliRuntime`/`CliRuntime` pass-through methods and `ValidateCommandHandler` JSON/human paths for the new field.

## 3. Tests

- [x] 3.1 Add `ArchitectureRoleIndexTests.cs` in `tests/ArchLinterNet.Core.Tests/`: role descriptor lookup (hit/miss), metadata lookup, single-pass caching (extraction runs once across repeated queries), deterministic ordering of `ClassifiedTypes()`/conflicts/failures, empty-classification short-circuit.
- [x] 3.2 Update `ArchitectureAnalysisSessionClassificationTests.cs` to assert `CheckClassificationFacts()` still returns identical conflict/failure content post-refactor, and add a test that repeated calls do not re-run extraction (e.g. via a call-count fixture or behavioral assertion already used by `ArchitectureTypeIndexTests.cs`'s memoization tests).
- [x] 3.3 Add/extend `ArchitectureDiagnosticFormatterTests.cs` for `classification_roles` JSON shape and ordering.
- [x] 3.4 Add/extend CLI tests (`tests/ArchLinterNet.Cli.Tests/`) for the new output field, mirroring existing `classification_conflicts`/`classification_metadata_failures` coverage (both are compile-time signature coverage only in this codebase; no dedicated CLI-level JSON assertion existed for the prior fields either).
- [x] 3.5 Add a regression test proving an existing namespace-only policy (no `classification` section) produces an empty role index and unchanged validation output — covered by `CheckClassificationFacts_NoClassificationConfigured_ReturnsEmpty`, `CheckClassificationRoles_NoClassificationConfigured_ReturnsEmpty`, and `EmptyClassificationConfiguration_ShortCircuitsWithoutScanningTypes`.

## 4. Spec sync and archive

- [x] 4.1 Run `openspec validate add-semantic-role-index --strict`.
- [x] 4.2 Run `openspec validate --all`.
- [x] 4.3 Run `openspec archive add-semantic-role-index`.

## 5. Validation gate

- [x] 5.1 Run `make fmt`.
- [x] 5.2 Run `make acceptance`.
