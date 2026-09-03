# architecture-policy-badge-cli Specification

## Purpose
Provide native CLI output for the strict architecture-policy badge and for the
Markdown report posted to pull requests.

## Requirements

### Requirement: Native architecture-policy badge command
The CLI SHALL expose `badge architecture-policy`, which SHALL read a completed
strict validation JSON result and write a Shields endpoint JSON object to stdout with
`schemaVersion`, `label`, `message`, and `color` fields. The label SHALL be
`architecture policy`; a passing strict result SHALL use `passing` and
`brightgreen`, and a strict-policy failure SHALL use `failing` and `red`.

#### Scenario: Strict policy passes
- **WHEN** a user runs `arch-linter-net badge architecture-policy --input strict.json`
  for a strict result with no findings
- **THEN** stdout is a Shields endpoint JSON object with message `passing` and
  color `brightgreen`
- **AND** the command exits 0

#### Scenario: Strict policy fails
- **WHEN** a user runs the command for a strict result with findings
- **THEN** stdout is a Shields endpoint JSON object with message `failing` and
  color `red`
- **AND** the command exits 1

#### Scenario: Validation cannot execute
- **WHEN** the strict-result input cannot be read or parsed
- **THEN** stdout is a Shields endpoint JSON object with message `unavailable`
  and color `red`
- **AND** the command exits 2

### Requirement: Native architecture-coverage report command
The CLI SHALL expose `coverage report`, which SHALL read strict CLI JSON and
render the deterministic architecture-coverage Markdown report to stdout or an
explicit output path. It SHALL support changed-files classification,
`--diff-status failed`, and `--max-failure-diagnostics` for the compact
PR-comment form.

#### Scenario: Full report renders from strict JSON
- **WHEN** a user runs `arch-linter-net coverage report --input
  architecture-strict.json`
- **THEN** the command renders the architecture-coverage status, failed-rule
  section when applicable, and covered/excluded/uncovered/stale/unknown totals

#### Scenario: Compact PR report preserves bounded diagnostics
- **WHEN** a user supplies `--max-failure-diagnostics 3`
- **THEN** each failed rule contains at most three representative diagnostics
- **AND** the report identifies omitted diagnostics

#### Scenario: Failed changed-files diff is explicit
- **WHEN** a user supplies `--diff-status failed`
- **THEN** the New-code coverage section reports the diff as unavailable
- **AND** it does not report zero changed first-party files

### Requirement: Native Architecture Health badge command
The CLI SHALL expose `badge architecture-health`, which SHALL read one
canonical `architecture-health/v1` JSON document and write a deterministic
Shields endpoint JSON object to stdout with `schemaVersion`, `label`,
`message`, and `color` fields. The command SHALL use the canonical top-level
Health category without recalculating it, and SHALL use the canonical selected
policy-inventory receipt's `ignore_debt.total` and `effective_rule_count`
without parsing policy YAML, findings, or waiver records.

For assessable input, the label SHALL be `architecture` and the message SHALL
contain all three facts in the form `<HEALTH> · <ignores> ignores · <rules>
rules`. The color SHALL be selected deterministically from the typed Health
category in one central badge projection mapping. `healthy`, `debt`,
`degrading`, and `failing` SHALL remain visibly distinct. An unassessable or
incomplete input SHALL produce `UNASSESSABLE · ? ignores · ? rules` and a
non-green unavailable color; it SHALL never invent zero counts or a healthy
state. The command SHALL not run architecture analysis or modify its input.
Its exit code SHALL preserve the canonical Health gate category: `pass` exits
0, `fail` exits 1, and `unassessable` or invalid badge input exits 2.

#### Scenario: Healthy canonical evidence produces a complete headline
- **WHEN** a canonical Health document reports `health=healthy`, zero explicit
  ignore debt, and 42 effective rules
- **THEN** the command writes a deterministic `architecture` badge whose
  message contains `HEALTHY`, `0 ignores`, and `42 rules`
- **AND** it exits 0 without evaluating policy or assemblies

#### Scenario: Reviewed waiver debt remains visible in a passing Health badge
- **WHEN** a canonical Health document reports `health=debt` and its selected
  policy inventory reports seven explicit ignores and 42 effective rules
- **THEN** the command writes a badge whose message contains `DEBT`, `7
  ignores`, and `42 rules`
- **AND** it does not infer the count from finding, source-set, or waiver-record fan-out

#### Scenario: Degrading and failing Health preserve the canonical category
- **WHEN** a canonical Health document reports either `degrading` or `failing`
  with a complete policy inventory
- **THEN** the command writes the corresponding upper-case Health category and
  canonical ignore/rule counts
- **AND** the two states do not share the healthy color

#### Scenario: Unassessable or incomplete evidence cannot fabricate counts
- **WHEN** the Health document, its selected policy-inventory receipt, or the
  required inventory counters are missing, malformed, unsupported, or
  unassessable
- **THEN** the command writes the explicit unassessable badge with unknown
  ignore and rule counts
- **AND** it exits 2 without emitting a healthy-looking or zero-count payload

#### Scenario: Equivalent input has equivalent output
- **WHEN** the command receives equivalent canonical Health and inventory
  evidence more than once
- **THEN** every generated badge JSON payload is byte-for-byte equivalent

### Requirement: Architecture Health badge accepts only complete canonical evidence
An assessable `badge architecture-health` input SHALL contain the complete
canonical report-evidence envelope: `schema_version` 2,
`kind=architecture-health-report-evidence`, and inner `gate` and `health`
values equal to the top-level canonical Health state. Every validation outcome
used as report evidence SHALL contain a complete supported policy-inventory
receipt; incomplete, unsupported, inventory-less, or inconsistent outcomes
SHALL make the badge unassessable. The command SHALL not silently discard such
outcomes to produce an assessable badge.

#### Scenario: Production-shaped canonical evidence is projected
- **WHEN** the command receives a Health document with a version-2 canonical
  report-evidence envelope whose inner state and complete inventory receipts
  agree with the top-level state
- **THEN** it projects the canonical Health, ignore debt, and effective rule
  count into the deterministic ready badge

#### Scenario: Unsupported evidence envelope is rejected
- **WHEN** the report-evidence schema version or kind is absent or unsupported
- **THEN** the command emits the explicit unassessable badge
- **AND** it exits 2 without inventing a ready state

#### Scenario: Inner state disagreement is rejected
- **WHEN** the report-evidence gate or Health differs from the top-level
  canonical state
- **THEN** the command emits the explicit unassessable badge
- **AND** it does not project the inventory counters

#### Scenario: Inventory-less outcome is rejected
- **WHEN** a validation outcome lacks a complete supported policy-inventory
  receipt
- **THEN** the command emits the explicit unassessable badge
- **AND** it does not ignore that outcome to produce a colored badge
