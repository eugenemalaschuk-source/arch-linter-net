## 1. Identity computation fix

- [x] 1.1 In `ArchitectureFindingMapper.IdentityParts`, replace the `PolicyConsistencyDiagnostic` case's `RepresentativeType ?? CheckKind` fallback with a call to a new `PolicyConsistencyDistinguisher` helper.
- [x] 1.2 In `ArchitectureFindingMapper.SourceTypeOf`, do the same for its `PolicyConsistencyDiagnostic` case.
- [x] 1.3 Implement `PolicyConsistencyDistinguisher`: return `RepresentativeType` when set; otherwise join `Layers` + `ConflictingContractIds` (falling back to `ConflictingContractNames`), ordinal-sorted, plus `PolicyLocation.YamlPath` as a tail disambiguator when present.

## 2. Snapshot projection completeness

- [x] 2.1 Add a `PolicyLevelFindings` helper to `ArchitectureChangeSnapshotProjector` that yields `ArchitectureFinding`s for `PolicyConsistencyFindings` (via `ArchitectureFindingMapper.FromDiagnostic`) and `UnmatchedIgnoredViolations` (via `ArchitectureDiagnosticMapper.FromUnmatchedIgnore` + `FromDiagnostic`), each gated on its contract family's config not being `"off"`.
- [x] 2.2 Concat `PolicyLevelFindings` into `Project`'s findings list alongside the existing `Violations`/`CoverageFindings` mapping.

## 3. Tests

- [x] 3.1 Add `Project_DistinguishesPolicyConsistencyOccurrencesOfTheSameCheckKind` — two `unmatched-layer-exclusion` findings on different layers plus one more on the same layer as one of them (different `PolicyLocation`); assert 3 distinct identities and that `SerializeSnapshot` does not throw.
- [x] 3.2 Add `Project_IncludesUnmatchedIgnoredViolationsAsFindings` — assert an `ArchitectureUnmatchedIgnoredViolation` appears in the snapshot's findings with kind `unmatched_ignore`.
- [x] 3.3 Add `Project_ExcludesPolicyLevelFindingsWhenTheirContractFamilyIsOff` — assert findings are empty when both configs are `"off"` even though the outcome carries findings.
- [x] 3.4 Verify all three new tests fail against the pre-fix code (confirmed via `git stash` of the two source files) and pass with the fix applied.

## 4. Validation

- [x] 4.1 `dotnet build` on `ArchLinterNet.Core` and `ArchLinterNet.Core.Tests` — clean, no warnings.
- [x] 4.2 `dotnet test tests/ArchLinterNet.Core.Tests --filter "FullyQualifiedName~ArchitectureChangeSnapshotProjectorTests"` — pass.
- [x] 4.3 Full `dotnet test tests/ArchLinterNet.Core.Tests` run for the cross-cutting risk tier — 3027 passed, 0 failed, 13 pre-existing skips.
- [x] 4.4 `make fmt` on changed files — `dotnet format --include` on the three changed files reported no changes needed.
- [ ] 4.5 `openspec validate --all` after archiving.
