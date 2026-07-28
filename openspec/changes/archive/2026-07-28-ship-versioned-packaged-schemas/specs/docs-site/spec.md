## ADDED Requirements

### Requirement: Packaged schema reference
The public documentation SHALL list every 0.5.1 packaged schema logical id and version, distinguish root, fragment, baseline, and baseline identity contracts, and show offline list/print commands and immutable release-qualified editor references.

#### Scenario: Adopter configures an editor for a release
- **WHEN** an adopter follows the schema reference without cloning the repository
- **THEN** the documentation identifies the exact packaged schema to print and an immutable release-qualified `$id` for the selected document role
