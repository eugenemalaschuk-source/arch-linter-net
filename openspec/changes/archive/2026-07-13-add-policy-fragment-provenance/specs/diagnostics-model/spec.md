## ADDED Requirements

### Requirement: Diagnostics carry typed policy-origin metadata
Every architecture diagnostic type SHALL be able to carry one optional typed policy
location and an ordered collection of related typed policy locations as common
diagnostic metadata. A policy location SHALL use named fields for portable source path,
root/fragment role, YAML path, source ordinal, and contract family/ID where applicable;
it SHALL NOT use a generic nullable property bag or expose host-absolute paths.

#### Scenario: Contract violation retains owning fragment
- **WHEN** a violation is produced by a contract loaded from a fragment
- **THEN** the mapped typed diagnostic carries that contract's fragment location without adding family-specific provenance fields to its payload

#### Scenario: Policy consistency conflict retains both owners
- **WHEN** a policy-consistency finding involves contracts or layers from two sources
- **THEN** its primary and related policy locations identify the involved source nodes in deterministic composed order

#### Scenario: Ignored violation retains nested YAML path
- **WHEN** an unmatched ignored violation originated in an imported contract
- **THEN** its diagnostic location points to the corresponding `ignored_violations` entry in that fragment

### Requirement: Human and CI JSON formatters expose policy origin additively
Human-readable diagnostics SHALL append a compact portable policy location when one is
available. CI JSON diagnostics SHALL add `policy_location` and, when non-empty,
`related_policy_locations` objects while preserving all existing fields, diagnostic
kinds, ordering, and compatibility for diagnostics without policy provenance.

#### Scenario: Human fragment diagnostic is actionable
- **WHEN** a diagnostic owns a fragment location
- **THEN** human output includes the fragment path and YAML path while preserving the existing diagnostic message

#### Scenario: CI JSON location is structured and portable
- **WHEN** the same diagnostic is rendered as CI JSON on different machines
- **THEN** `policy_location` contains the same `/`-separated source path, role, YAML path, source ordinal, and contract metadata on each machine

#### Scenario: Existing consumer ignores additive metadata
- **WHEN** a consumer reads only fields that existed before policy provenance
- **THEN** those fields retain their previous names and values
