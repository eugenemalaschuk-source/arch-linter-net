## Context

The dependency-violation baseline mechanism follows one consistent pattern across every `Check*Contract` method in `ArchitectureContractRunner.Checking.cs`:

1. `ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);`
2. Inside the per-finding loop, call `executionContext.IsIgnored(sourceType, forbiddenReference)`. This single call does three things: matches against `ignored_violations` (suppressing the finding), records a `ArchitectureBaselineCandidate(contractGroup, contractId, sourceType, forbiddenReference)` into `_baselineCandidates` when *not* ignored, and tracks which baseline/manual ignore entries were matched.
3. `executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);` after the loop — any `ignored_violations` entry never matched becomes an `ArchitectureUnmatchedIgnoredViolation` (today reported via `unmatched_ignored_violations` config), which is exactly "stale baseline entry."

`ArchitectureContractCatalog` already resolves coverage contracts to groups `strict_coverage`/`audit_coverage` (`ArchitectureContractCatalog.cs:56-57`), so `ResolveContractGroup`/`CreateExecutionContext` will return the right group name for free once coverage contracts go through this same path.

`CheckCoverageContract` and `CheckRuleInputCoverageContract` (`ArchitectureContractRunner.Coverage.cs`) currently build `ArchitectureViolation` lists directly from the coverage inventory with no `ignored_violations` involvement at all. `ArchitectureCoverageContract` (`ArchitectureContractModels.cs:372-388`) has no `IgnoredViolations` property — unlike every other contract type, which all expose `List<ArchitectureIgnoredViolation> IgnoredViolations`. The baseline YAML model (`ArchitectureBaselineContractGroups`) and the merger's `GetContracts`/`GetIgnoredViolations` switches likewise have no `strict_coverage`/`audit_coverage` cases.

## Goals / Non-Goals

**Goals:**
- Make coverage findings (`uncovered namespace`, `unresolved`, `empty-input` from `CheckCoverageContract`/`CheckRuleInputCoverageContract`) participate in the exact same `ignored_violations` / baseline-candidate / unmatched-ignore pipeline as every other contract type, by reusing `ArchitectureContractExecutionContext` unchanged.
- Add `strict_coverage`/`audit_coverage` baseline groups so `baseline generate` and `validate --baseline` cover coverage contracts.
- Preserve full backward compatibility: existing baseline files with no `strict_coverage`/`audit_coverage` keys load and behave exactly as before (new groups default to empty lists, same `ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)` serialization behavior as other groups).

**Non-Goals:**
- No new baseline entry shape, no new schema version, no namespace/glob-pattern baseline entries (per `baseline-generation` spec, entries stay exact `(source_type, forbidden_reference)` pairs).
- No change to how ordinary dependency violations (`strict`, `audit`, etc.) are baselined.
- No CLI flags beyond what `baseline generate`/`validate --baseline` already expose — coverage contracts simply become eligible inputs to the existing flags.
- No dashboard, no automatic policy editing.

## Decisions

### Decision: Treat each coverage finding's "item" as `source_type` and its evidence as `forbidden_reference`
Coverage findings emitted today are `ArchitectureViolation(contract.Name, contract.Id, SourceType: <item>, ForbiddenReference: "uncovered namespace"/"unresolved"/"empty-input", Evidence)`. Looking at `CheckCoverageContract`, the violation already sets `SourceType = entry.Namespace` and `ForbiddenReference = "uncovered namespace"` (the reason string), with evidence carried separately. To reuse `ArchitectureIgnoreMatcher.IsIgnored(sourceType, forbiddenReference, ...)` unchanged, baseline entries key on the same two strings already used to construct the violation: `source_type = <namespace or contract-id-being-referenced>`, `forbidden_reference = "uncovered namespace"` / `"unresolved"` / `"empty-input"`. This requires no change to how violations are constructed — only that `IsIgnored` is consulted with those same two values before a finding is added to the result list.
- **Alternative considered:** introduce a distinct "coverage baseline" entry shape recording namespace/edge/rule semantics explicitly. Rejected — the issue's coverage findings already collapse cleanly onto the existing `(source_type, forbidden_reference)` pair, and a second shape would duplicate the baseline file format and merger/loader logic for no behavioral gain.

### Decision: Add `IgnoredViolations` to `ArchitectureCoverageContract` and wire it through `CreateExecutionContext`
Add `[YamlMember(Alias = "ignored_violations")] public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();` to `ArchitectureCoverageContract`, matching every other contract type. In `CheckCoverageContract`/`CheckRuleInputCoverageContract`, call `CreateExecutionContext(contract, contract.IgnoredViolations)` and replace the raw "always emit" loops with `if (!executionContext.IsIgnored(item, reasonCode)) { emit finding }`, then call `executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations)` before returning. This is the same five-line pattern used by every existing `Check*Contract` method — no new abstraction.
- **Alternative considered:** write a parallel ignore-matching path specific to coverage. Rejected — `ArchitectureContractExecutionContext` already generalizes over contract group/id and is engineered exactly for this; duplicating it violates the "existing abstractions over new abstractions" bias and would create two stale-tracking mechanisms to keep in sync.

### Decision: Add `strict_coverage`/`audit_coverage` to `ArchitectureBaselineContractGroups`, generator, loader, and merger as new cases — no other group touched
- `ArchitectureBaselineContractGroups`: add `StrictCoverage`/`AuditCoverage` properties (`[YamlMember(Alias = "strict_coverage")]` / `"audit_coverage"`), same `List<ArchitectureBaselineContractEntry>` type as all other groups.
- `ArchitectureBaselineGenerator.SetGroupEntries`: add `case "strict_coverage": groups.StrictCoverage = entries; break;` and the audit equivalent. `ArchitectureBaselineGenerator.Generate` already takes candidates from `runner.BaselineCandidates` generically by `ContractGroup` string — no other change needed there once candidates are actually populated for coverage contracts (per the decision above).
- `ArchitectureBaselineLoader.Validate`: add `ValidateGroupEntries(document.Baseline.StrictCoverage, "strict_coverage")` and audit equivalent, matching the existing per-group validation loop.
- `ArchitectureBaselineMerger`: add `groupMerger.MergeGroup(baseline.StrictCoverage, "strict_coverage")` (and audit) in both `Merge` and `MergeAndValidate`; extend `ContractGroupMerger.GetContracts` with `"strict_coverage" => _groups.StrictCoverage...` (and audit) reading from `ArchitectureContractGroups.StrictCoverage`/`AuditCoverage` (already exist per `ArchitectureContractModels.cs:176-179`); extend `GetIgnoredViolations` switch with `ArchitectureCoverageContract c => c.IgnoredViolations`.
- **Alternative considered:** a generic group-name-keyed dictionary instead of named properties, to avoid touching every switch. Rejected — every other group already follows the explicit-property + explicit-switch-case pattern; introducing a dictionary here alone would be an inconsistent, speculative abstraction for two groups when sixteen already exist in the established style.

### Decision: "Stale baseline entry" for coverage reuses `ArchitectureUnmatchedIgnoredViolation` / `unmatched_ignored_violations` unchanged
Because `ArchitectureContractExecutionContext.CollectUnmatchedIgnores` is contract-group/id-agnostic, a `strict_coverage`/`audit_coverage` baseline entry whose `(source_type, forbidden_reference)` pair is no longer produced by `CheckCoverageContract` (namespace now covered, rule reference now resolved) automatically surfaces as an unmatched ignored violation through the exact same `unmatched_ignored_violations` config (`error`/`warn`/`off`) other contract types already use. No coverage-specific stale-detection code path is needed.

## Risks / Trade-offs

- **[Risk]** `CheckCoverageContract`'s `Roots`/`Exclude` filtering happens before the per-namespace ignore check; if `IsIgnored` is inserted at the wrong point in the loop, baseline candidates could be recorded for excluded items. → Mitigation: insert the `IsIgnored` call exactly where the existing "would emit a finding" branch is (after exclusion filtering, after coverage-by-layer checks), mirroring how other `Check*Contract` methods call `IsIgnored` only at the point a violation would otherwise be added.
- **[Risk]** `BuildCoverageSummary`/`BuildNamespaceCoverageSummary` (used for human-readable summaries, not gating) currently duplicates the same uncovered/stale/unknown logic as `CheckCoverageContract` without baseline awareness. If left unfiltered, the summary would show items as "uncovered" that the gate itself treats as accepted baseline debt, confusing users. → Mitigation: scope this change to gating (`Check*Contract`) only, as the issue requires; explicitly note in tasks/docs that summary output is informational and intentionally still shows full uncovered debt (it does not run `validate`'s pass/fail gate). Revisit only if a follow-up issue asks for summary-level baseline awareness.
- **[Risk]** Existing baseline files predate `strict_coverage`/`audit_coverage` keys. → Mitigation: YamlDotNet's `IgnoreUnmatchedProperties()` + new properties defaulting to empty lists guarantees old files deserialize unchanged; `DefaultValuesHandling.OmitNull` ensures newly generated baselines for projects without coverage contracts don't grow spurious empty sections (matches existing per-group omission behavior already relied upon for `strict_asmdef` etc., which are absent entirely rather than empty).

## Migration Plan

No data migration. Existing baseline files remain valid (`version: 1` unchanged — additive optional keys only). Teams adopting coverage baselines run `baseline generate` after upgrading to capture current coverage debt, then opt into `validate --baseline` as before. No rollback concerns beyond reverting the change.

## Open Questions

None outstanding — confirmed via codebase inspection that `ArchitectureContractCatalog` already resolves `strict_coverage`/`audit_coverage` groups and `ArchitectureContractGroups.StrictCoverage`/`AuditCoverage` already exist, so this change only needs to plug coverage contracts into the existing baseline pipeline rather than design a new one.
