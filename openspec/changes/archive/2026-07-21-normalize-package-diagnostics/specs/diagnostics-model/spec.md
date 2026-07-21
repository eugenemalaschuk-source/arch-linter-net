## ADDED Requirements

### Requirement: Every diagnostic subtype is recognized at every concrete-subtype dispatch point

Formatting adapters that read shared display fields (`SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`)
off a diagnostic by pattern-matching on its concrete `ArchitectureDiagnostic` subtype SHALL include every
subtype that declares those fields. Introducing a new diagnostic subtype without extending every such
dispatch point SHALL NOT silently degrade to an empty/generic value in any adapter.

#### Scenario: A subtype present in one adapter is present in all
- **WHEN** a diagnostic subtype is recognized by the SARIF formatter's field-extraction switch
- **THEN** the same subtype is also recognized by the Human/JSON formatter's equivalent switches, so no
  adapter alone determines whether a finding's evidence is visible
