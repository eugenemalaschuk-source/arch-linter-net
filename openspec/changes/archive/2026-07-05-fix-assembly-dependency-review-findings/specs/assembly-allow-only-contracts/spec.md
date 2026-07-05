## ADDED Requirements

### Requirement: Assembly allow-only rejects transitive depth defensively at check time
The system SHALL reject `dependency_depth: transitive` on an `ArchitectureAssemblyAllowOnlyContract` when the contract is evaluated, even if the contract was constructed programmatically rather than loaded from YAML, with the same actionable error used at policy load time.

#### Scenario: Programmatically constructed transitive contract rejected at check time
- **WHEN** an `ArchitectureAssemblyAllowOnlyContract` with `DependencyDepth` set to `Transitive` is passed directly to the session check method (bypassing the policy loader)
- **THEN** the check throws an actionable error stating that only `direct` is currently supported
