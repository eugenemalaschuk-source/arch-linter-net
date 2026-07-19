# Violation Reporting Specification

## Purpose
Formats violations for human-readable terminal output and for machine-readable CI JSON artifacts.
## Requirements
### Requirement: Format violations for human-readable output
The system SHALL format violation lists as sorted bullet lines with format `- {SourceType} -> {ForbiddenNamespace}: {refs}`.

#### Scenario: Multiple violations
- **WHEN** a contract produces 3 violations
- **THEN** the formatted output contains 3 bullet lines sorted by `SourceType` then `ForbiddenNamespace`

#### Scenario: Empty violations
- **WHEN** a contract produces 0 violations
- **THEN** the formatted output is an empty string

### Requirement: Format violations for CI JSON artifacts
The system SHALL format violations as a JSON object with `kind = "architecture_violations"`, `contract` name, and `violations` array containing `source`, `forbidden_namespace`, and `forbidden_references`.

#### Scenario: JSON output structure
- **WHEN** a contract named `"my-contract"` produces 2 violations
- **THEN** the JSON contains `{"kind":"architecture_violations","contract":"my-contract","violations":[...]}`

### Requirement: Format cycles for human-readable output
The system SHALL format cycle lists as sorted bullet lines with format `- {cycle_path}`.

#### Scenario: Multiple cycles
- **WHEN** cycle detection finds 2 cycles
- **THEN** the formatted output contains 2 bullet lines sorted alphabetically

### Requirement: Format cycles for CI JSON artifacts
The system SHALL format cycles as a JSON object with `kind = "architecture_cycles"`, `contract` name, and `cycles` array of path strings.

#### Scenario: JSON cycle output
- **WHEN** a contract produces 1 cycle
- **THEN** the JSON contains `{"kind":"architecture_cycles","contract":"...","cycles":["A -> B -> A"]}`

### Requirement: Human output includes contract ID
The human-readable violation formatter SHALL prefix each violation line with the contract ID in square brackets when available.

#### Scenario: Violation with explicit ID
- **WHEN** a contract with `id: my-rule` produces a violation
- **THEN** the human output format is `[my-rule] [My Contract] SourceType -> ForbiddenNamespace: refs`

#### Scenario: Violation with fallback ID
- **WHEN** a contract has no explicit `id` and produces a violation
- **THEN** the human output includes `[<normalized-name>]` prefix using the fallback ID

### Requirement: JSON output includes contract_id
The JSON formatter SHALL include a `contract_id` field alongside the existing `contract` field.

#### Scenario: JSON violation with ID
- **WHEN** a contract with `id: my-rule` produces a violation
- **THEN** the JSON violation object contains `"contract_id": "my-rule"`

#### Scenario: JSON cycle with ID
- **WHEN** a cycle contract with `id: cycle-check` detects a cycle
- **THEN** the JSON cycle object contains `"contract_id": "cycle-check"`

### Requirement: Format violations and cycles as SARIF
The system SHALL format violations and cycles as a SARIF 2.1.0 `run`, with each violation or cycle rendered as a `result` referencing a `rule` keyed by contract ID.

#### Scenario: SARIF violation result
- **WHEN** a contract named `"my-contract"` with `id: my-rule` produces a violation
- **THEN** the SARIF output contains a result with `ruleId == "my-rule"` and a `message.text` describing the violation

#### Scenario: SARIF cycle result
- **WHEN** a cycle contract with `id: cycle-check` detects a cycle
- **THEN** the SARIF output contains a result with `ruleId == "cycle-check"` and a `message.text` containing the cycle path

### Requirement: Existing human and JSON formatting are unaffected
Adding SARIF formatting SHALL NOT change the output of `FormatViolationsForHumans`, `FormatCyclesForHumans`, or `FormatResultForCiArtifacts`.

#### Scenario: Human and JSON output unchanged
- **WHEN** the same violations and cycles are formatted for human and JSON output before and after the SARIF formatter is added
- **THEN** the human and JSON output is byte-identical

### Requirement: Port-boundary diagnostics preserve seam evidence
Every port-boundary finding SHALL identify the source and target types, their
resolved role/metadata, evidence kind, expected seam, actual forbidden edge or
binding mismatch, and a safe remediation hint. Human and JSON output SHALL
preserve this information in deterministic form.

#### Scenario: JSON output distinguishes a direct edge from a binding mismatch
- **WHEN** JSON output contains both a forbidden direct reference and an
  adapter-to-port mismatch
- **THEN** each finding SHALL identify its evidence kind and expected seam so
  an AI consumer can distinguish the remediation

### Requirement: Policy loading failures have format-aware output
The CLI SHALL format typed policy import and validation failures according to
the selected output format. Human output SHALL include policy source and root
context; JSON output SHALL emit an `architecture_policy_error` object with
`policy_location`, `related_policy_locations`, and `import_chain`; SARIF output
SHALL emit a result with policy physical and related locations when available.

#### Scenario: JSON policy validation failure
- **WHEN** the CLI is invoked with JSON output and an imported effective-policy
  value fails validation
- **THEN** stdout contains an `architecture_policy_error` object with the
  fragment policy location and ordered import chain

#### Scenario: SARIF policy import failure
- **WHEN** the CLI is invoked with SARIF output and a typed import failure has
  a source location
- **THEN** stdout contains a SARIF result whose physical location identifies
  that policy source

### Requirement: Policy exception locations use established machine schemas
The CLI SHALL serialize policy-exception JSON locations with the same
snake_case fields as ordinary CI diagnostics. SARIF policy exceptions SHALL
include typed related policy locations in addition to the primary location.

#### Scenario: Conflict has two policy declarations
- **WHEN** a typed policy conflict has root and fragment locations
- **THEN** JSON uses normalized location fields and SARIF contains the fragment
  as a related physical location

### Requirement: Policy exception location metadata matches CI diagnostics
The CLI SHALL include source ordinal and per-location import chain in every policy-exception JSON location using the established snake_case schema.

#### Scenario: Imported policy exception
- **WHEN** an imported policy exception is rendered as JSON
- **THEN** its location includes source_ordinal and import_chain

### Requirement: Policy location JSON has one optional-field shape
The system SHALL omit optional policy location fields when values are absent in both ordinary and exception JSON output.

#### Scenario: Root policy exception has no import metadata
- **WHEN** a root policy exception has no contract or import fields
- **THEN** its JSON location omits those fields rather than serializing null

### Requirement: Root and raw policy failures use format-aware typed output
CLI JSON and SARIF output SHALL recognize typed root parsing and raw composed
YAML validation failures as policy diagnostics. These failures SHALL not fall
through to the generic runtime-error output path.

#### Scenario: Malformed root renders as JSON policy error
- **WHEN** JSON validation loads a malformed selected root
- **THEN** stdout contains an `architecture_policy_error` with a root-role
  `policy_location`

#### Scenario: Imported raw failure renders as SARIF policy error
- **WHEN** SARIF validation loads an imported raw YAML value rejected before
  deserialization
- **THEN** the SARIF result identifies the fragment policy location

### Requirement: Policy-exception SARIF contains complete declaration evidence

The CLI SHALL emit every available primary and related policy definition in the
SARIF result's `relatedLocations` when a typed policy exception has source
locations. Each related location SHALL contain a portable policy source URI,
the authored source region, and a message identifying the policy role and YAML
path. The locations SHALL follow composition encounter order; for a conflict
this preserves original-then-conflicting order.

#### Scenario: Root definition conflicts with an imported fragment

- **WHEN** a typed policy composition conflict identifies a root declaration
  and a conflicting imported-fragment declaration
- **THEN** the SARIF result `locations` identifies the primary declaration
- **AND THEN** `relatedLocations` contains the root declaration followed by the
  fragment declaration
- **AND THEN** each related-location message identifies its authored YAML path
  and each physical location contains a portable policy source URI

### Requirement: Violations expose CEL expression participation additively
When a context-dependency, context-allow-only, or layout-convention violation involves a selector that declares `when`, the human-readable and CI JSON output SHALL include the expression's source text, its YAML location, and its result (`matched`, `not_matched`, or `evaluation_failed`), in addition to all existing fields for that diagnostic kind. Diagnostics with no `when`-bearing selector SHALL omit this data entirely rather than emitting empty or null placeholders.

#### Scenario: JSON violation includes expression participation
- **WHEN** a context-dependency contract's `forbidden[*].when` predicate evaluates `true` and produces a violation
- **THEN** the JSON output for that violation includes a `when_expression` object with the expression's source text, its YAML path, and `"result": "matched"`

#### Scenario: Human output includes the expression text
- **WHEN** the same violation is rendered as human-readable output
- **THEN** the output line includes the evaluated `when` expression's source text alongside the existing violation description

#### Scenario: Non-CEL violation is unaffected
- **WHEN** a context-dependency or layout-convention violation involves no `when`-bearing selector
- **THEN** its human-readable and JSON output are identical to the output produced before this change

#### Scenario: Layout-convention violation includes expression participation
- **WHEN** a `strict_layout_conventions[*].files_matching.when` predicate narrows which files are checked and a violation results
- **THEN** the JSON output for that violation includes the same `when_expression` shape used for context-dependency/context-allow-only violations

