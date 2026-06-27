## Why

Architecture coverage contracts (`strict_coverage`, `audit_coverage`, including `rule_input`-scoped variants) already detect uncovered namespaces, unresolved/empty-input rule references, and stale rule-input coverage, but their findings are never routed through the baseline mechanism: `CheckCoverageContract` and `CheckRuleInputCoverageContract` never call into `ignored_violations` filtering or baseline-candidate tracking, and the baseline YAML model has no `strict_coverage`/`audit_coverage` groups to hold entries even if they were tracked. Teams adopting coverage gates on a real repository with pre-existing uncovered areas have no incremental adoption path — they must either fix all coverage debt immediately or skip coverage gating entirely. This mirrors the problem the existing dependency-violation baseline already solves, just for a different finding type.

## What Changes

- Add `strict_coverage` and `audit_coverage` groups to the baseline YAML schema (`ArchitectureBaselineContractGroups`), reusing the existing `id` + `ignored_violations` (`source_type` + `forbidden_reference`) entry shape — no new schema concepts.
- Thread `ignored_violations` filtering and baseline-candidate tracking through `CheckCoverageContract` and `CheckRuleInputCoverageContract`, the same way other `Check*Contract` methods already do, so coverage findings become baseline-able and `--baseline` gate filtering suppresses previously-accepted coverage debt.
- Extend `ArchitectureBaselineGenerator` (`baseline generate`) to emit `strict_coverage`/`audit_coverage` entries for current coverage findings, following the same deterministic ordering and dedup rules as other contract groups.
- Extend stale-baseline detection (`unmatched_ignored_violations`) so a coverage baseline entry whose underlying finding has been resolved (namespace now covered, rule reference now resolved) is reported as stale, using the existing `ArchitectureUnmatchedIgnoredViolation` mechanism.
- No changes to baseline behavior for `strict`, `audit`, or any other existing contract group — purely additive.

## Capabilities

### New Capabilities
(none — this extends an existing capability rather than introducing a new one)

### Modified Capabilities
- `baseline-generation`: baseline file format gains `strict_coverage`/`audit_coverage` groups; `baseline generate` and `validate --baseline` now also cover coverage contracts; stale-entry detection extends to coverage findings.

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureBaselineModels.cs` — add `StrictCoverage`/`AuditCoverage` fields to `ArchitectureBaselineContractGroups`.
- `src/ArchLinterNet.Core/Contracts/ArchitectureBaselineGenerator.cs` — handle `strict_coverage`/`audit_coverage` in `SetGroupEntries`.
- `src/ArchLinterNet.Core/Contracts/ArchitectureBaselineLoader.cs` / `ArchitectureBaselineMerger.cs` — merge new groups into runtime ignore lists for coverage contracts.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractRunner.Coverage.cs` — apply ignore filtering and baseline-candidate tracking in `CheckCoverageContract` / `CheckRuleInputCoverageContract`.
- `src/ArchLinterNet.Core/Validation/ArchitectureBaselineService.cs` — ensure coverage contracts run as part of baseline generation.
- Existing tests under `tests/ArchLinterNet.Core.Tests/ArchitectureBaseline*.cs` and `tests/ArchLinterNet.Cli.Tests/CliIntegrationTests.Baseline*.cs` plus coverage test fixtures — new tests for initial baseline, new uncovered item, resolved uncovered item, stale baseline entry, audit-only coverage.
- `docs/guides/migration-baselines.md` and `docs/contracts/coverage.md` — document the new baseline coverage for coverage contracts.
