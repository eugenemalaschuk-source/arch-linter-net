# architecture-coverage-reporting Specification

## Purpose
TBD - created by archiving change add-architecture-coverage-reporting. Update Purpose after archive.
## Requirements
### Requirement: Coverage summary computation
For every coverage contract that is selected and run, the system SHALL compute a deterministic coverage summary containing counts for `covered`, `excluded`, `uncovered`, `stale`, and `unknown`, derived from the existing coverage inventory and coverage findings without altering coverage validation behavior.

#### Scenario: Namespace scope summary counts
- **WHEN** a `scope: namespace` coverage contract runs against a repository with namespaces matching a declared layer, namespaces matching an `exclude` rule, and namespaces matching neither
- **THEN** the summary reports the matched-layer namespaces under `covered`, the excluded namespaces under `excluded`, and the unmatched namespaces under `uncovered`, with `stale` and `unknown` at zero

#### Scenario: Rule-input scope summary counts
- **WHEN** a `scope: rule_input` coverage contract runs with one referenced contract whose layer no longer matches any code and one referenced contract whose layer name does not exist
- **THEN** the summary reports the first under `stale` and the second under `unknown`, with `covered`, `excluded`, and `uncovered` reflecting any remaining referenced contracts that resolve cleanly or are excluded

#### Scenario: Project scope summary counts
- **WHEN** a `scope: project` coverage contract runs against a repository with discovered projects matching a declared layer, projects matching an `exclude` rule, projects matching neither, and a project whose assembly could not be resolved
- **THEN** the summary reports the matched-layer projects under `covered`, the excluded projects under `excluded`, the unmatched projects under `uncovered`, and the unresolved project under `unknown`, with `stale` at zero

#### Scenario: Assembly scope summary counts
- **WHEN** a `scope: assembly` coverage contract runs against a repository with resolved assemblies matching a declared layer, assemblies matching an `exclude` rule, and assemblies matching neither
- **THEN** the summary reports the matched-layer assemblies under `covered`, the excluded assemblies under `excluded`, and the unmatched assemblies under `uncovered`, with `stale` and `unknown` at zero

#### Scenario: Empty repository
- **WHEN** a coverage contract runs against a repository with no namespaces, no discovered projects/assemblies, or no referenced rule contracts in scope
- **THEN** the summary reports all counts as zero for that contract, with no error

#### Scenario: Fully covered repository
- **WHEN** every namespace under a `scope: namespace` contract's roots, or every discovered project under a `scope: project` contract, or every resolved assembly under a `scope: assembly` contract, matches a declared layer, namespace-glob layer, or expanded layer template
- **THEN** `uncovered`, `stale`, and `unknown` are zero and `covered` equals the total in-scope unit count

#### Scenario: Reserved scopes are not summarized
- **WHEN** a policy is loaded
- **THEN** the summary never contains entries for `scope: dependency_edge`, because the loader rejects that scope before any contract instance exists to summarize

### Requirement: Exclusion reasons surfaced in summary
The coverage summary SHALL include the `reason` text for every excluded item, sourced from the coverage contract's `exclude` entries.

#### Scenario: Namespace exclusion reason included
- **WHEN** a namespace matches an `exclude` rule with `reason: "Generated code is excluded from manual architecture coverage."`
- **THEN** the summary's excluded-items list includes that namespace paired with that exact reason text

#### Scenario: Rule-input exclusion reason included
- **WHEN** a referenced contract ID matches an `exclude` entry with a `contract_id` and `reason`
- **THEN** the summary's excluded-items list includes that contract ID paired with that reason text

### Requirement: Uncovered evidence in summary
The coverage summary SHALL include identifying evidence for each uncovered, stale, or unknown item so a reviewer can locate it without re-running validation, and SHALL keep uncovered, stale, and unknown evidence in separate lists so the three statuses are never ambiguous to a reader or downstream tooling.

#### Scenario: Uncovered namespace evidence
- **WHEN** a namespace is reported as uncovered
- **THEN** the summary's uncovered-items list includes that namespace and a representative type name from that namespace

#### Scenario: Stale or unknown rule-input evidence is bucketed separately
- **WHEN** a referenced contract is reported as stale or unknown
- **THEN** the summary includes the referenced contract ID and the layer name that triggered the finding, placed in a stale-items list or an unknown-items list respectively — never in the uncovered-items list, and never combined into one undifferentiated list

### Requirement: Coverage contract selection respected in summary
The coverage summary SHALL only include an entry for a coverage contract that was actually selected to run for the current `validate` invocation (per `--contract` filtering); it SHALL NOT include a zero-count placeholder entry for a coverage contract that was not selected.

#### Scenario: Contract filter excludes unselected coverage contract from summary
- **WHEN** `validate --contract <id>` selects only non-coverage contracts
- **THEN** `coverage_summary` contains no entry for any coverage contract in the policy, in both human and JSON output — not an entry with all counts at zero

#### Scenario: Contract filter includes selected coverage contract
- **WHEN** `validate --contract <id>` selects a coverage contract's own ID
- **THEN** that coverage contract's summary entry is still computed and reported normally

### Requirement: Deterministic human-readable coverage summary output
The CLI SHALL render the coverage summary as deterministic, stably ordered human-readable text suitable for PR review, in a `validate` invocation using human output.

#### Scenario: Stable ordering across runs
- **WHEN** the same policy and codebase are validated twice in human output mode
- **THEN** the coverage summary section is byte-for-byte identical between the two runs, with contracts ordered by contract ID (or name when no ID is set) using ordinal comparison, and excluded/uncovered sub-items ordered ordinally

#### Scenario: Audit-only mode still reports summary
- **WHEN** `analysis.coverage` is set to `warn` or `off`
- **THEN** the coverage summary is still computed and rendered in human output, independent of whether coverage findings affect the run's pass/fail result

### Requirement: Machine-readable JSON coverage summary output
The CLI SHALL render the coverage summary as a top-level `coverage_summary` array in JSON output, additive to and independent of the existing `coverage_findings` array, with stable snake_case keys.

#### Scenario: JSON output includes summary alongside existing fields
- **WHEN** `validate` runs with `--format json`
- **THEN** the JSON payload includes `coverage_summary` as a top-level array sibling to `violations`, `cycles`, and `coverage_findings`, and the existing fields are unchanged in shape

#### Scenario: JSON summary entry shape
- **WHEN** a coverage contract appears in `coverage_summary`
- **THEN** its entry includes `contract`, `contract_id`, `scope`, a `counts` object with `covered`/`excluded`/`uncovered`/`stale`/`unknown` integer fields, an `excluded_items` array of `{item, reason}` objects, and `uncovered_items`, `stale_items`, and `unknown_items` arrays of `{item, evidence}` objects, kept as three distinct arrays rather than merged into one (each empty when there are no such items for that contract's scope)

#### Scenario: Partially covered repository in JSON
- **WHEN** a repository has a mix of covered, excluded, and uncovered namespaces
- **THEN** the JSON `coverage_summary` entry's counts reflect that mix exactly, and `excluded_items`/`uncovered_items` list every corresponding item

