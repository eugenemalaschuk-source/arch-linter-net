## ADDED Requirements

### Requirement: Modular-monolith import example is realistic and executable
The repository SHALL provide a modular-monolith example with one root policy that keeps appropriate shared settings inline and imports focused shared-layer and bounded-context fragments. The example SHALL use documented, executable fields and SHALL be loadable by the production policy loader.

#### Scenario: Modular-monolith example is loaded
- **WHEN** the sample root and its fragments are loaded through `ArchitecturePolicyDocumentLoader`
- **THEN** shared layers, bounded-context definitions, and ordered contracts compose into one valid effective policy

### Requirement: Unity client import example is realistic and executable
The repository SHALL provide a Unity/client example with one root policy that imports focused runtime, editor, and feature fragments while retaining a single execution entry point. The example SHALL use documented, executable fields and SHALL be loadable by the production policy loader.

#### Scenario: Unity client example is loaded
- **WHEN** the sample root and its fragments are loaded through `ArchitecturePolicyDocumentLoader`
- **THEN** runtime, editor, feature, external dependency, and asmdef concerns compose into one valid effective policy

