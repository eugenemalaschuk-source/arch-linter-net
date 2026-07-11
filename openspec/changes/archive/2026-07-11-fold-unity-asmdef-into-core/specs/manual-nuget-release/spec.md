## MODIFIED Requirements

### Requirement: Expected package projects are packed
The manual release workflow SHALL create package artifacts for `ArchLinterNet.Core`, `ArchLinterNet.Cli`, and `ArchLinterNet.Testing`. Unity asmdef validation is included in `ArchLinterNet.Core`, so a separate Unity package is not part of the release set.

#### Scenario: Release package set is built
- **WHEN** the release workflow packs the product
- **THEN** Core, CLI, and Testing package artifacts are created
- **AND** the package set contains no separate Unity artifact
