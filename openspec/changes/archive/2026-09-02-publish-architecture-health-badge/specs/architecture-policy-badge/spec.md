## MODIFIED Requirements

### Requirement: Dynamic strict self-policy badge
The repository SHALL expose one primary ArchLinterNet-specific README
Architecture Health badge sourced from a stable public Shields endpoint JSON
payload. The badge SHALL communicate canonical Architecture Health, accumulated
explicit ignore debt, and effective policy-control count; it SHALL not represent
generic workflow success, test coverage, architecture-coverage percentage,
SonarCloud, or Codecov status.

#### Scenario: Primary README badge uses the canonical public payload
- **WHEN** a reader views the README after a verified `main` publication
- **THEN** its Architecture Health image resolves through the stable public
  endpoint payload rather than `ci.yml` or another workflow-status image
- **AND** the message includes canonical Health, explicit ignore debt, and
  effective rule count

#### Scenario: Default branch strict self-policy passes
- **WHEN** a pull request with successful required architecture validation is
  squash-merged and its validated tree matches `main`
- **THEN** the primary README badge renders the promoted canonical Architecture
  Health payload for that merged tree
- **AND** it does not use a generic strict-policy workflow conclusion as its source

#### Scenario: Strict self-policy fails
- **WHEN** canonical Architecture Health for the promotable pull-request tree
  is failing
- **THEN** the stable primary badge renders the failing canonical Health payload
- **AND** it does not present a passing architecture state because another
  workflow or generic quality signal succeeded

#### Scenario: Generic quality remains separate
- **WHEN** a reader views the README badge block
- **THEN** Main quality, SonarCloud, and Codecov remain separately named
  generic-quality signals
- **AND** none of them is labeled or described as Architecture Health

## REMOVED Requirements

### Requirement: Architecture-policy badge remains publication-free
**Reason**: A workflow-status badge cannot truthfully represent the canonical
Architecture Health, waiver debt, and effective-control inventory after PR CI
became the authoritative validation path.

**Migration**: Use the trusted stable Architecture Health endpoint for the
primary README signal; retain `badge architecture-policy` only for integrations
that explicitly need the legacy strict-validation projection.
