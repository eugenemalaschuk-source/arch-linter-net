## MODIFIED Requirements

### Requirement: Packaged schema reference
The public documentation SHALL list every 0.5.1 packaged schema logical id and version, distinguish root, fragment, baseline, and baseline identity contracts, and show offline list/print commands and immutable release-qualified editor references. Release-facing documentation for the 0.6.1 product package line SHALL state that those schema identifiers are independently versioned immutable compatibility identities, not package-version URLs, and SHALL identify 0.6.1 as the current public adoption package line.

#### Scenario: Adopter configures an editor for a release
- **WHEN** an adopter follows the schema reference without cloning the repository
- **THEN** the documentation identifies the exact packaged schema to print and an immutable release-qualified `$id` for the selected document role

#### Scenario: Adopter distinguishes product and schema versions
- **WHEN** an adopter reads the 0.6.1 release-facing schema guidance
- **THEN** it explains that most supported `$schema` identities remain `https://archlinternet.dev/schema/0.5.1/...`, that policy root/fragment advanced to `0.6.1`, and that no `schema/0.6.0` identity is shipped
