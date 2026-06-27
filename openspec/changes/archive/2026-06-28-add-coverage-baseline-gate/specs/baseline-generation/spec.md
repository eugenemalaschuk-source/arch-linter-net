## ADDED Requirements

### Requirement: Baseline generation covers coverage contracts

The system SHALL support baseline generation for the `strict_coverage` and `audit_coverage` contract groups, using the same `id` + `ignored_violations` (`source_type` + `forbidden_reference`) entry format as all other supported contract groups.

Coverage findings from `strict_coverage`/`audit_coverage` contracts (uncovered namespace, unresolved rule reference, empty-input rule reference) SHALL be eligible as baseline candidates the same way ordinary dependency violations are, using the `(source_type, forbidden_reference)` pair already produced for each finding.

#### Scenario: Generate baseline captures uncovered namespaces
- **WHEN** user runs baseline generation on a project with namespaces not covered by any layer or layer template, evaluated against a `strict_coverage` or `audit_coverage` contract
- **THEN** each uncovered namespace SHALL appear as an exact `(source_type, forbidden_reference)` entry under the `strict_coverage` or `audit_coverage` group and the corresponding contract ID

#### Scenario: Generate baseline captures unresolved and empty-input rule references
- **WHEN** user runs baseline generation on a project with a `rule_input`-scoped coverage contract that finds unresolved layer references or layer references with no matching code
- **THEN** each finding SHALL appear as an exact `(source_type, forbidden_reference)` entry under the corresponding `strict_coverage` or `audit_coverage` contract ID

#### Scenario: Coverage baseline generation is deterministic
- **WHEN** user runs baseline generation twice against the same unchanged codebase with coverage contracts configured
- **THEN** the `strict_coverage`/`audit_coverage` sections of both output files SHALL be byte-identical

### Requirement: Coverage gate accepts a baseline of existing uncovered areas

The system SHALL accept `ignored_violations` entries on `strict_coverage` and `audit_coverage` contracts, merged in the same way `--baseline` merges entries for other contract groups, so that coverage findings already present in the baseline are suppressed while new uncovered areas are still reported.

This baseline mechanism SHALL apply only to coverage contract findings. It SHALL NOT suppress, hide, or otherwise interact with ordinary dependency-violation findings from `strict`, `audit`, or any other non-coverage contract group.

#### Scenario: Baseline suppresses previously-accepted uncovered namespaces but flags new ones
- **WHEN** user runs `validate --baseline` against a project where some uncovered namespaces are recorded in the `strict_coverage` baseline and a new namespace becomes uncovered
- **THEN** the namespaces present in the baseline SHALL NOT be reported as coverage failures; the new uncovered namespace SHALL still fail validation

#### Scenario: Coverage baseline does not affect ordinary violations
- **WHEN** user runs `validate --baseline` against a project with both a coverage baseline and ordinary dependency violations not present in any baseline
- **THEN** the ordinary dependency violations SHALL still be reported as failures, unaffected by the coverage baseline entries

#### Scenario: Audit-only coverage baseline does not fail the gate
- **WHEN** an `audit_coverage` contract has uncovered areas recorded in its baseline and the corresponding `audit_coverage` contract is configured as non-blocking per existing audit semantics
- **THEN** validation SHALL report the audit coverage findings without failing the gate, consistent with how `audit` contract groups already behave for ordinary violations

### Requirement: Resolved coverage debt is detected as a stale baseline entry

When a `strict_coverage`/`audit_coverage` baseline entry's `(source_type, forbidden_reference)` pair no longer matches any current coverage finding (the namespace became covered, or the rule reference became resolved), the system SHALL report it as an unmatched ignored violation, using the same `unmatched_ignored_violations` configuration (`error`/`warn`/`off`) already applied to other contract groups.

#### Scenario: Resolved uncovered namespace becomes a stale baseline entry
- **WHEN** a namespace recorded in the `strict_coverage` baseline is later covered by a layer or layer template, then validation is run with `unmatched_ignored_violations: error`
- **THEN** the namespace SHALL NOT be reported as a coverage failure, and the stale baseline entry SHALL be reported as an unmatched ignored violation

#### Scenario: Resolved rule-input coverage debt becomes a stale baseline entry
- **WHEN** a `rule_input`-scoped coverage finding recorded in the baseline (unresolved or empty-input rule reference) is later resolved by adding matching code or a valid layer mapping, then validation is run
- **THEN** the resolved finding SHALL NOT be reported, and the stale baseline entry SHALL be reported as an unmatched ignored violation
