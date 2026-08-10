## ADDED Requirements

### Requirement: v0.6.1 is the public adoption package line
The 0.6.1 product package line SHALL be the public adoption package line. Its packaged README,
CLI documentation, and release-facing guidance SHALL identify 0.6.1 as that line while continuing
to present the embedded compatibility registry's schema identities as independently versioned
immutable contracts.

Every adoption workaround whose upstream blocker this line fixes SHALL be removable through
documented product behavior, proven against freshly packed candidate artifacts rather than a
source-tree project reference. A workaround SHALL NOT remain documented as the recommended
long-term shape once its blocker is fixed.

#### Scenario: Adopter identifies the current package line
- **WHEN** an adopter reads the packaged README of an installed 0.6.1 package
- **THEN** it identifies 0.6.1 as the public adoption package line and separately identifies the
  embedded immutable schema identities

#### Scenario: A workaround is still required
- **WHEN** a consumer scenario this line is intended to fix still requires a workaround-shaped
  policy or build wrapper when driven from the packed candidate
- **THEN** the release gate records the failure and the line is not authorized for publication
