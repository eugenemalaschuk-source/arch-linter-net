# Unmatched Ignore Alerting Specification

## Purpose
Detects ignore entries that no longer match any actual violation and surfaces them as a diagnostic with contract and ignore-entry context.

## Requirements

### Requirement: Detect unmatched ignored violations

The system SHALL detect `ignored_violations` entries that match no current dependency violation. An entry is unmatched if no (source_type, forbidden_reference) pair in the analyzed codebase satisfies both the source_type and forbidden_reference patterns simultaneously.

#### Scenario: Exact-match ignore becomes stale
- **WHEN** an `ignored_violations` entry has `source_type: "MyApp.Services.LegacyProcessor"` and `forbidden_reference: "MyApp.Infrastructure.LegacyDb"`, AND the code no longer contains any dependency from `MyApp.Services.LegacyProcessor` to `MyApp.Infrastructure.LegacyDb`
- **THEN** the system SHALL report this entry as an unmatched ignored violation

#### Scenario: Wildcard ignore becomes stale
- **WHEN** an `ignored_violations` entry has `source_type: "MyApp.Legacy.*"` and `forbidden_reference: "*"`, AND no type matching `MyApp.Legacy.*` references any forbidden type
- **THEN** the system SHALL report this entry as an unmatched ignored violation

#### Scenario: Valid ignore is not reported
- **WHEN** an `ignored_violations` entry has patterns that match at least one actual (source_type, forbidden_reference) pair in the codebase
- **THEN** the system SHALL NOT report this entry as unmatched

#### Scenario: Overlapping ignores are all tracked
- **WHEN** multiple `ignored_violations` entries match the same (source_type, forbidden_reference) pair (e.g., a broad `*` entry and a specific one)
- **THEN** the system SHALL report only those entries whose patterns match no actual violation; entries matching at least one violation SHALL NOT be reported as unmatched

### Requirement: Diagnostic includes contract context and ignore entry details

Unmatched ignored violation diagnostics SHALL include: contract name, contract ID (if present), ignore entry index within the contract, source_type pattern, forbidden_reference pattern, and the reason from the original ignore entry.

#### Scenario: Full diagnostic output
- **WHEN** a contract named `"domain-no-infra"` with ID `"domain"` has an unmatched `ignored_violations` entry at index 2 with `source_type: "MyApp.Legacy.*"`, `forbidden_reference: "MyApp.Infrastructure.*"`, and `reason: "Tracked in #1234"`
- **THEN** the diagnostic SHALL include all of: contract name `"domain-no-infra"`, contract ID `"domain"`, ignore index 2, source_type pattern `"MyApp.Legacy.*"`, forbidden_reference pattern `"MyApp.Infrastructure.*"`, and reason `"Tracked in #1234"`

### Requirement: Configurable severity

The system SHALL support three severity levels for unmatched ignore detection, configured via `analysis.unmatched_ignored_violations` in the policy file.

#### Scenario: Error severity fails validation
- **WHEN** `analysis.unmatched_ignored_violations` is set to `"error"` (or not set, as it is the default), AND at least one unmatched ignored violation is detected
- **THEN** the system SHALL report the unmatched entries AND SHALL exit with a non-zero exit code

#### Scenario: Warn severity does not fail
- **WHEN** `analysis.unmatched_ignored_violations` is set to `"warn"`, AND at least one unmatched ignored violation is detected
- **THEN** the system SHALL report the unmatched entries BUT SHALL NOT change the exit code based on them alone

#### Scenario: Off severity skips detection
- **WHEN** `analysis.unmatched_ignored_violations` is set to `"off"`
- **THEN** the system SHALL NOT perform unmatched ignore detection, SHALL NOT report unmatched entries, and SHALL NOT show them in any output

### Requirement: Separate output from violations

Unmatched ignored violation diagnostics SHALL be reported in a separate section in human-readable output and a separate JSON field, distinct from regular dependency violations and cycle reports.

#### Scenario: Human output has distinct section
- **WHEN** there are both violations and unmatched ignores
- **THEN** the human-readable output SHALL show violations first, then cycles, then a separate `"Unmatched ignored violations:"` section

#### Scenario: JSON output has distinct field
- **WHEN** the output format is JSON
- **THEN** the JSON payload SHALL include an `"unmatched_ignored_violations"` array at the top level, separate from `"violations"` and `"cycles"`

### Requirement: Detection works in strict and audit modes

Unmatched ignored violation detection SHALL run in both `strict` and `audit` CLI modes. The severity behavior is determined by the config, not by the mode.

#### Scenario: Strict mode detection
- **WHEN** CLI mode is `"strict"` AND `analysis.unmatched_ignored_violations` is `"error"`
- **THEN** unmatched ignores in strict contracts SHALL be detected and reported

#### Scenario: Audit mode detection
- **WHEN** CLI mode is `"audit"` AND `analysis.unmatched_ignored_violations` is `"error"`
- **THEN** unmatched ignores in audit contracts SHALL be detected and reported

### Requirement: Valid ignored violations continue to suppress

The introduction of unmatched detection SHALL NOT change existing ignore matching behavior. All existing `ignored_violations` entries that match actual violations SHALL continue to suppress those violations.

#### Scenario: Existing ignore still suppresses
- **WHEN** a contract has an `ignored_violations` entry that matches a current violation
- **THEN** the violation SHALL be suppressed (not appear in output) AND the ignore entry SHALL NOT appear in unmatched diagnostics
