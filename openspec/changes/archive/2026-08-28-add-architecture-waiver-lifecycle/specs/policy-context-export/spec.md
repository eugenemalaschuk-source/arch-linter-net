## ADDED Requirements

### Requirement: Effective policy context exports typed waiver declarations
The effective policy-context export SHALL include supported waiver-lifecycle
profile metadata and typed declared waiver identity, target, remediation
metadata, and composed-policy provenance. It SHALL remain a static policy
projection and SHALL NOT claim active, stale, or expired runtime state without
analysis evidence.

#### Scenario: Imported structured waiver retains provenance
- **WHEN** a root policy imports a fragment containing a structured waiver
- **THEN** exported context identifies the waiver and its originating fragment
  provenance without loading assemblies or evaluating findings
