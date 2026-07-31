## ADDED Requirements

### Requirement: Expanded contract baselines retain authored and concrete provenance
For a baseline-capable contract expanded from a source set, the identity SHALL retain both the
authored contract provenance and a stable concrete source-instance key. Different expanded sources
or source instances SHALL NOT share one baseline entry solely because their display fields match.

#### Scenario: Two expanded sources have matching display values
- **WHEN** one authored source-set contract expands to two concrete sources that produce otherwise equal display references
- **THEN** each finding SHALL have a distinct canonical baseline identity and baselining one SHALL not suppress the other.

