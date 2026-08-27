## Why

`change snapshot` fails with `Could not create architecture change snapshot: Architecture change snapshot contains duplicate or empty finding identities.` whenever a policy has more than one finding of the same `policy_consistency` check kind under one contract. Independently, `PolicyConsistencyFindings` and `UnmatchedIgnoredViolations` were never projected into the snapshot's findings at all, so drift in those two contract families was silently invisible to every `change snapshot`/`change report` run. This closes GitHub issue [#683](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/683).

## What Changes

- `ArchitectureChangeSnapshotProjector.Project` now includes `PolicyConsistencyFindings` and `UnmatchedIgnoredViolations` in the snapshot's findings, gated on each contract family's config not being `"off"` (mirroring the existing gating used by `ReportCoordinator`'s human/JSON output).
- `ArchitectureFindingMapper`'s identity computation for `PolicyConsistencyDiagnostic` no longer falls back to a bare `CheckKind` string when `RepresentativeType` is absent. It now folds `Layers`, `ConflictingContractIds`/`ConflictingContractNames`, and `PolicyLocation.YamlPath` into the distinguishing identity field, so two findings of the same check kind under the same contract (e.g. two `unmatched-layer-exclusion` findings on different layers, or two `independence-conflict` findings against the same independence contract) get distinct, stable identities instead of colliding.

Neither change is breaking: the snapshot schema version, entry/finding envelope shape, and CLI surface are unchanged. Existing snapshots without policy-level findings are read and compared exactly as before.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `architecture-change-report`: Requirement 1 ("Complete architecture analysis can be persisted as a change snapshot") currently says the snapshot contains stable entries for, among other things, "normalized findings" — but the projection silently omitted policy-consistency and unmatched-ignored-violation findings, and the identity computation for policy-consistency findings did not actually guarantee stability/uniqueness across multiple occurrences of one check kind. The requirement is strengthened to make both guarantees explicit and enforced.

## Impact

- `src/ArchLinterNet.Core/Change/ArchitectureChangeSnapshotProjector.cs` — findings projection.
- `src/ArchLinterNet.Core/Reporting/ArchitectureFindingMapper.cs` — `PolicyConsistencyDiagnostic` identity computation (`IdentityParts`, `SourceTypeOf`).
- `tests/ArchLinterNet.Core.Tests/ArchitectureChangeSnapshotProjectorTests.cs` — new regression coverage.
- No public API, CLI surface, schema version, or dependency changes.
