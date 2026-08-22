## ADDED Requirements

### Requirement: Checkpoint B preserves the complete candidate subject inventory
Checkpoint B platform records and final release evidence SHALL retain and
compare the complete canonical candidate package-subject inventory, including
the explicit primary-package and symbol-package pair for each package ID. They
SHALL reject a record or artifact whose manifest schema, source commit, version,
paired inventory, file identity, size, or digest differs from the candidate
manifest used by the release workflow.

#### Scenario: Platform evidence omits a symbol package
- **WHEN** a platform record reports primary packages but omits a manifest
  symbol subject
- **THEN** Checkpoint B evidence aggregation fails and publication is not
  authorized

#### Scenario: Candidate bytes are modified after packing
- **WHEN** a package or paired symbol file changes after the canonical manifest
  is created
- **THEN** downstream candidate verification fails before release evidence can
  authorize publication
