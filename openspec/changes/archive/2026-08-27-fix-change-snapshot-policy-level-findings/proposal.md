## Why

`change snapshot` fails with `Could not create architecture change snapshot: Architecture change snapshot contains duplicate or empty entry identities.` (or the analogous `finding identities` wording) for policies that exercise coverage or policy-level contracts. This closes GitHub issue [#683](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/683).

Two independent, compounding root causes, the second found and confirmed during PR review of the first fix:

1. `PolicyConsistencyFindings`/`UnmatchedIgnoredViolations` were never projected into the snapshot's findings at all, so drift in those contract families was silently invisible; and once wired in, `PolicyConsistencyDiagnostic` findings of the same check kind under one contract collided into one finding identity.
2. `BuildRuleInputSummary`'s stale/unknown rule-input coverage items key `Item` on the referenced contract id alone, dropping which specific rule input/layer made them stale or unknown; a contract with two such problematic inputs produced two coverage-blind-spot **entries** sharing one identity — the exact failure mode and exact error wording ("entry identities") originally reported.

## What Changes

- `ArchitectureChangeSnapshotProjector.Project` now includes `PolicyConsistencyFindings` and `UnmatchedIgnoredViolations` in the snapshot's findings, gated on each contract family's config not being `"off"` (mirroring the existing gating used by `ReportCoordinator`'s human/JSON output).
- `ArchitectureFindingMapper`'s identity computation for `PolicyConsistencyDiagnostic` no longer falls back to a bare `CheckKind` string when `RepresentativeType` is absent. It now folds `Layers`, `ConflictingContractIds`/`ConflictingContractNames`, and `PolicyLocation.YamlPath` into the distinguishing identity field, so two findings of the same check kind under the same contract (e.g. two `unmatched-layer-exclusion` findings on different layers, or two `independence-conflict` findings against the same independence contract) get distinct, stable identities instead of colliding. The double string-join this added per diagnostic is now computed once, not twice, via a `BuildIdentity` special case.
- `ArchitecturePolicyConsistencyAnalysisService.CreateUnmatchedExclusionFinding` now sets `RepresentativeType` to the exclusion's own namespace pattern (not a YAML list position), so identity for this check kind is stable if the policy author reorders a layer's `exclude` entries.
- `ArchitectureCoverageAnalysisService.BuildRuleInputSummary` now encodes the rule-input role (`source`/`forbidden`/etc.) into stale/unknown items' `Evidence`, and `ArchitectureChangeSnapshotProjector`'s coverage-blind-spot entry identity now folds `Evidence` in for stale/unknown items — fixing the actual root cause of the originally reported "duplicate or empty entry identities" error.

Neither change is breaking: the snapshot schema version, entry/finding envelope shape, and CLI surface are unchanged. Existing snapshots without policy-level findings are read and compared exactly as before. The `evidence` text for stale/unknown coverage items is enriched (now prefixed with the rule-input role); no schema or test elsewhere pins its previous exact wording as a machine contract.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `architecture-change-report`: Requirement 1 ("Complete architecture analysis can be persisted as a change snapshot") currently says the snapshot contains stable entries for, among other things, "normalized findings" — but the projection silently omitted policy-consistency and unmatched-ignored-violation findings, and the identity computation for policy-consistency findings did not actually guarantee stability/uniqueness across multiple occurrences of one check kind. The requirement is strengthened to make both guarantees explicit and enforced.

## Impact

- `src/ArchLinterNet.Core/Change/ArchitectureChangeSnapshotProjector.cs` — findings projection and coverage-blind-spot entry identity.
- `src/ArchLinterNet.Core/Reporting/ArchitectureFindingMapper.cs` — `PolicyConsistencyDiagnostic` identity computation (`BuildIdentity`, formerly `IdentityParts`/`SourceTypeOf`).
- `src/ArchLinterNet.Core/Execution/ArchitecturePolicyConsistencyAnalysisService.cs` — `RepresentativeType` for unmatched-layer-exclusion findings.
- `src/ArchLinterNet.Core/Execution/ArchitectureCoverageAnalysisService.RuleInputCoverage.cs` — rule-input role folded into stale/unknown item `Evidence`.
- `tests/ArchLinterNet.Core.Tests/ArchitectureChangeSnapshotProjectorTests.cs`, `ArchitectureFindingMapperTests.cs`, `PolicyConsistencyCheckTests.cs`, `ArchitectureCoverageSummaryTests.cs` — new/updated regression coverage.
- `tests/ArchLinterNet.Cli.Tests/CliIntegrationTests.CoverageSummary.cs` — updated evidence-text assertions.
- No public API, CLI surface, schema version, or dependency changes (`make public-api-check` confirmed).
