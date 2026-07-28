## ADDED Requirements

### Requirement: Packaged schema registry is an executable release contract
The 0.5.1 `adoption-stabilization/v1` compatibility envelope SHALL be represented by an immutable packaged schema registry. Unversioned web schema URLs MAY remain convenience aliases, but SHALL NOT be the source of truth for an installed release contract.

#### Scenario: Later source alias changes
- **WHEN** the repository default branch changes an unversioned schema alias after a 0.5.1 package is installed
- **THEN** the installed tool continues to list and print the same release-qualified 0.5.1 schema set and digests
