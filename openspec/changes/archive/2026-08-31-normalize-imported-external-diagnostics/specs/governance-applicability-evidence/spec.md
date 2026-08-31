## ADDED Requirements

### Requirement: External diagnostic evidence uses shared applicability controls
For every policy-declared external-diagnostic evidence requirement, the system SHALL project its
trusted selection/completeness result through #507 expected applicability membership and produced
records. A required valid zero-result artifact SHALL be evaluable; deliberate optional absence
SHALL be not-applicable; a missing, malformed, filter-mismatched, or wrong-context supplied
artifact SHALL be unassessable with canonical reason/provenance. This projection SHALL NOT create
an imported-diagnostic-specific applicability envelope or independently revalidate #520 trust.

#### Scenario: Wrong revision remains unassessable instead of zero-clean
- **WHEN** a required evidence artifact is structurally valid but rejected for its required source
  revision
- **THEN** the external applicability record is unassessable with canonical external-revision
  provenance and no ordinary selected imported finding is emitted

#### Scenario: Valid selected zero-result evidence is evaluable
- **WHEN** a required current-context artifact is trusted and its selected diagnostic collection is empty
- **THEN** its external applicability record is evaluable and is distinguishable from missing or
  unassessable evidence
