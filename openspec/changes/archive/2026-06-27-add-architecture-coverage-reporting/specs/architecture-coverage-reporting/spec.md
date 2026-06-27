## ADDED Requirements

### Requirement: Coverage summary computation
For every coverage contract that is selected and run, the system SHALL compute a deterministic coverage summary containing counts for `covered`, `excluded`, `uncovered`, `stale`, and `unknown`, derived from the existing coverage inventory and coverage findings without altering coverage validation behavior.

#### Scenario: Namespace scope summary counts
- **WHEN** a `scope: namespace` coverage contract runs against a repository with namespaces matching a declared layer, namespaces matching an `exclude` rule, and namespaces matching neither
- **THEN** the summary reports the matched-layer namespaces under `covered`, the excluded namespaces under `excluded`, and the unmatched namespaces under `uncovered`, with `stale` and `unknown` at zero

#### Scenario: Rule-input scope summary counts
- **WHEN** a `scope: rule_input` coverage contract runs with one referenced contract whose layer no longer matches any code and one referenced contract whose layer name does not exist
- **THEN** the summary reports the first under `stale` and the second under `unknown`, with `covered`, `excluded`, and `uncovered` reflecting any remaining referenced contracts that resolve cleanly or are excluded

#### Scenario: Empty repository
- **WHEN** a coverage contract runs against a repository with no namespaces or no referenced rule contracts in scope
- **THEN** the summary reports all counts as zero for that contract, with no error

#### Scenario: Fully covered repository
- **WHEN** every namespace under a `scope: namespace` contract's roots matches a declared layer, namespace-glob layer, or expanded layer template
- **THEN** `uncovered`, `stale`, and `unknown` are zero and `covered` equals the total in-scope namespace count

#### Scenario: Reserved scopes are not summarized
- **WHEN** a policy is loaded
- **THEN** the summary never contains entries for `scope: project`, `scope: assembly`, or `scope: dependency_edge`, because the loader rejects those scopes before any contract instance exists to summarize

### Requirement: Exclusion reasons surfaced in summary
The coverage summary SHALL include the `reason` text for every excluded item, sourced from the coverage contract's `exclude` entries.

#### Scenario: Namespace exclusion reason included
- **WHEN** a namespace matches an `exclude` rule with `reason: "Generated code is excluded from manual architecture coverage."`
- **THEN** the summary's excluded-items list includes that namespace paired with that exact reason text

#### Scenario: Rule-input exclusion reason included
- **WHEN** a referenced contract ID matches an `exclude` entry with a `contract_id` and `reason`
- **THEN** the summary's excluded-items list includes that contract ID paired with that reason text

### Requirement: Uncovered evidence in summary
The coverage summary SHALL include identifying evidence for each uncovered, stale, or unknown item so a reviewer can locate it without re-running validation.

#### Scenario: Uncovered namespace evidence
- **WHEN** a namespace is reported as uncovered
- **THEN** the summary's uncovered-items list includes that namespace and a representative type name from that namespace

#### Scenario: Stale or unknown rule-input evidence
- **WHEN** a referenced contract is reported as stale or unknown
- **THEN** the summary includes the referenced contract ID and the layer name that triggered the finding

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
- **THEN** its entry includes `contract`, `contract_id`, `scope`, a `counts` object with `covered`/`excluded`/`uncovered`/`stale`/`unknown` integer fields, an `excluded_items` array of `{item, reason}` objects, and an `uncovered_items` array of `{item, evidence}` objects (empty arrays when there are no such items)

#### Scenario: Partially covered repository in JSON
- **WHEN** a repository has a mix of covered, excluded, and uncovered namespaces
- **THEN** the JSON `coverage_summary` entry's counts reflect that mix exactly, and `excluded_items`/`uncovered_items` list every corresponding item
