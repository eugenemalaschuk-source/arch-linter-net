## ADDED Requirements

### Requirement: SARIF retains remediation guidance as normalized result metadata
For a SARIF result that embeds a normalized finding, the formatter SHALL retain equivalent remediation-hint metadata beneath the existing namespaced finding property. It SHALL NOT encode remediation guidance as a SARIF executable `fix`.

#### Scenario: SARIF and JSON preserve equivalent hint semantics
- **WHEN** a finding with a remediation hint is rendered in JSON and SARIF
- **THEN** the SARIF namespaced normalized finding contains the same structured remediation category, evidence, seam/direction, caveat, and review semantics as JSON

#### Scenario: Guidance does not claim an automatic edit
- **WHEN** SARIF renders a finding with remediation guidance
- **THEN** the result does not contain a SARIF `fixes` collection for that guidance
