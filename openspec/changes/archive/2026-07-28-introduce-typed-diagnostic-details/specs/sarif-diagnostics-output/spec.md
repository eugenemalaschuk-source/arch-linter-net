## ADDED Requirements

### Requirement: SARIF retains normalized typed finding details
SARIF results SHALL retain the normalized finding envelope and exact typed details in a namespaced result property while using standard SARIF locations for physical source locations.

#### Scenario: Package evidence is parity-preserved
- **WHEN** a package finding is emitted in JSON and SARIF
- **THEN** both outputs contain equivalent project, package, condition, target-framework, and provenance evidence

#### Scenario: Physical source location remains standard SARIF
- **WHEN** a normalized finding has a physical source location
- **THEN** SARIF emits it as a physical location in addition to the typed details property
