## 1. Core summary model

- [x] 1.1 Add `ArchitectureCoverageSummary` record (and nested `ArchitectureCoverageSummaryCounts`, excluded/uncovered item records) under `src/ArchLinterNet.Core/Reporting/`
- [x] 1.2 Add `BuildCoverageSummary(ArchitectureCoverageContract contract)` to `ArchitectureContractRunner.Coverage.cs` for `scope: namespace`, reusing `ArchitectureCoverageInventory`, the same root/exclude matching helpers, and the existing `CheckCoverageContract` findings to derive covered/excluded/uncovered counts plus excluded-item reasons and uncovered-item evidence
- [x] 1.3 Extend the same builder for `scope: rule_input`, mapping `"empty-input"` findings to `stale`, `"unresolved"` findings to `unknown`, and excluded `contract_id`s to `excluded` with reason text
- [x] 1.4 Wire summary computation into the existing coverage contract execution path (wherever `CheckCoverageContract` is currently invoked) so a summary is produced for every selected coverage contract, independent of `analysis.coverage` severity

## 2. Reporting / formatting

- [x] 2.1 Add `FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary>)` to `ArchitectureDiagnosticFormatter`, ordered by contract id/name (ordinal), rendering one line per contract with counts plus indented excluded/uncovered sub-lists
- [x] 2.2 Add `coverage_summary` to the payload built in `FormatResultForCiArtifacts`, serialized with snake_case keys per the design's JSON shape (`counts`, `excluded_items: [{item, reason}]`, `uncovered_items: [{item, evidence}]`)
- [x] 2.3 Ensure existing `coverage_findings`, `violations`, and `cycles` JSON shapes are unchanged (regression-guard via existing tests)

## 3. CLI wiring

- [x] 3.1 Update `src/ArchLinterNet.Cli/Program.cs` validate command to pass coverage summaries into both the human-output and JSON-output code paths
- [x] 3.2 Confirm `--format human` prints a `Coverage summary:` section after `Coverage findings:` when summaries are non-empty, and prints nothing extra when there are no coverage contracts

## 4. Tests

- [x] 4.1 Core unit tests: empty repository (zero counts, no error) for both namespace and rule_input scopes
- [x] 4.2 Core unit tests: fully covered namespace scope (covered == total, others zero)
- [x] 4.3 Core unit tests: partially covered namespace scope (mixed covered/excluded/uncovered with correct reasons and evidence)
- [x] 4.4 Core unit tests: rule_input scope stale (`empty-input`) and unknown (`unresolved`) classification, including excluded contract IDs
- [x] 4.5 CLI integration tests: `--format json` includes `coverage_summary` with correct shape, sibling to `coverage_findings`
- [x] 4.6 CLI integration tests: `--format human` deterministic output across repeated runs (same input -> byte-identical summary section)
- [x] 4.7 CLI integration tests: audit-only mode (`analysis.coverage: warn` or `off`) still emits the summary in both output formats while exit code reflects only configured severity

## 5. Documentation

- [x] 5.1 Update `docs/contracts/coverage.md` with a "Coverage summary" section: counts semantics, the stale/unknown mapping for rule_input, exclusion reason surfacing, and the explicit note that `project`/`assembly`/`dependency_edge` scopes are not summarized because they are rejected at load time
- [x] 5.2 Update `docs/usage/output-formats.md` with the `coverage_summary` JSON shape example
- [x] 5.3 Update `docs/cli/index.md` if the `validate` command reference needs a mention of summary output
- [x] 5.4 Update `mkdocs.yml` nav only if a new page is added (otherwise confirm no nav change needed since existing pages are edited in place)
- [x] 5.5 Update `docs/reference/yaml-schema.md` only if needed for clarity (no YAML schema changes are expected, since this change is reporting-only)

## 6. Validation

- [x] 6.1 Run full test suite (`dotnet test`) and confirm no regressions
- [ ] 6.2 Run `openspec validate --all` after archiving
