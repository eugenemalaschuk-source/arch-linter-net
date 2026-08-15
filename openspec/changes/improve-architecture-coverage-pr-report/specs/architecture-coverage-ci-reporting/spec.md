## ADDED Requirements

### Requirement: Failed PR reports identify the failed rules
When strict architecture validation fails, the Architecture Coverage PR comment SHALL render a
deterministic, compact "Failed rules" section before aggregate coverage tables. It SHALL group
diagnostics by stable contract ID (falling back to a named diagnostic category only when no
contract is available), show the rule name, and include concise representative evidence such as
source, forbidden reference, coverage item, or source/policy location.

#### Scenario: Contract violations are visible in the PR comment
- **WHEN** strict JSON contains failed findings for one or more contracts
- **THEN** the PR comment lists every distinct failed contract ID and name before the aggregate tables
- **AND** each listed contract includes concise representative diagnostic details

#### Scenario: Repeated diagnostics are bounded without hiding their count
- **WHEN** a failed contract has more diagnostics than the report's representative limit
- **THEN** the PR comment renders deterministic representative diagnostics for that contract
- **AND** it states how many additional diagnostics were omitted

#### Scenario: Coverage-summary failures have a useful fallback
- **WHEN** strict JSON fails and a coverage summary has uncovered, stale, unknown, or excluded items but no structured coverage findings
- **THEN** the PR comment lists those items under the corresponding coverage contract

### Requirement: A detailed failure report is retained as a CI artifact
The Architecture Coverage workflow SHALL upload a detailed Markdown report containing all failed
diagnostics. The compact PR comment SHALL link reviewers to the artifacts of its current workflow
run, where the detailed report and `architecture-strict.json` are available.

#### Scenario: Reviewer follows the detailed-report link
- **WHEN** a PR Architecture Coverage workflow posts or updates its comment
- **THEN** the comment links to the artifacts page for that workflow run
- **AND** the artifacts include the detailed Architecture coverage Markdown report and the strict JSON diagnostics

#### Scenario: Detailed report does not hide repeated diagnostics
- **WHEN** a failed rule has more diagnostics than the PR comment's representative limit
- **THEN** the detailed Markdown artifact renders every diagnostic for that rule

### Requirement: Failure counts are visible in the aggregate report
The aggregate Architecture coverage table in both detailed and compact reports SHALL include the
count of unique failed rules and the total count of failed diagnostics. Passing reports SHALL show
both counts as zero and SHALL omit the Failed rules detail section.

#### Scenario: Failed report distinguishes rules from diagnostics
- **WHEN** multiple diagnostics belong to one failed contract
- **THEN** the aggregate table reports one failed rule and the total number of its diagnostics

#### Scenario: Passing report remains concise
- **WHEN** strict JSON reports `passed: true`
- **THEN** the report contains no Failed rules section
- **AND** the aggregate table reports zero failed rules and diagnostics
