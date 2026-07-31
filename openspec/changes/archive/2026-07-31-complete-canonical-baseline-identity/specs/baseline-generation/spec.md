## ADDED Requirements

### Requirement: Baseline identity is complete for every baseline-capable registered family
The system SHALL derive every version-2 baseline candidate from the canonical semantic identity
declared for its registered finding family. A candidate SHALL contain the authored contract ID,
concrete source-instance key when expansion applies, all applicable source/target assembly, type,
member, package, framework, API, configuration, and target-framework dimensions, plus a
deterministic non-line-based occurrence discriminator. Display text, reasons, paths, line numbers,
timings, report destinations, and rendering state SHALL NOT participate.

#### Scenario: A qualified family produces distinct exact candidates
- **WHEN** a baseline-capable family emits two otherwise similarly rendered findings with different semantic source, target, source-instance, or occurrence dimensions
- **THEN** baseline generation SHALL emit distinct structured entries and each entry SHALL suppress only its exact finding.

### Requirement: Requalified structured identities require review
The system SHALL never reinterpret a previously emitted structured baseline identity as broadly
matching after a required canonical dimension is introduced. It SHALL classify a proven one-to-one
predecessor/successor difference as `changed`, an unresolvable entry as `stale`, and multiple
possible successors as `ambiguous`; only `matched` SHALL suppress a live finding.

#### Scenario: An old under-qualified structured entry has one successor
- **WHEN** a version-2 baseline entry corresponds to exactly one live finding under legacy display projection but differs in its canonical structured identity
- **THEN** comparison SHALL report the entry as `changed`, SHALL not suppress the finding, and SHALL preserve its review metadata only in the explicit update path.

