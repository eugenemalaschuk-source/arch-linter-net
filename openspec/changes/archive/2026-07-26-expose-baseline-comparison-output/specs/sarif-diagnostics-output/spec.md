## ADDED Requirements

### Requirement: Baseline comparison commands render SARIF results

The CLI SHALL support SARIF output for `baseline diff`, `baseline verify`, and
`baseline migrate`. Each baseline comparison entry SHALL produce a deterministic
SARIF result with the stable contract ID as its rule ID and the entry's lifecycle
status and canonical identity fields in result properties.

#### Scenario: Diff SARIF exposes a new entry
- **WHEN** `baseline diff` is run with SARIF output and finds a new entry
- **THEN** the SARIF result contains `baseline_status: "new"` and the entry's
  structured identity properties without requiring consumers to parse its message

#### Scenario: Verify SARIF exposes stale and ambiguous entries
- **WHEN** `baseline verify` is run with SARIF output and finds stale or ambiguous
  entries
- **THEN** it emits a result for each entry with `baseline_status` equal to
  `"stale"` or `"ambiguous"` respectively

#### Scenario: Migration SARIF exposes migration statuses
- **WHEN** `baseline migrate --dry-run` is run with SARIF output
- **THEN** its results expose each migrated entry's matched, stale, or ambiguous
  status as a structured SARIF property

#### Scenario: Baseline comparison SARIF is deterministic
- **WHEN** the same comparison command is rendered as SARIF twice with unchanged
  inputs
- **THEN** both SARIF documents are byte-identical
