# release-publication-authorization Specification

## Purpose

Defines exact reviewed publication authority for stable and preview package
targets while preserving candidate binding and keeping version overrides
non-authoritative.

## Requirements

### Requirement: Publication authority can target an exact reviewed preview
The release authorization system SHALL allow a reviewed publication
declaration to target an exact `X.Y.Z-preview.N` package version in addition
to an exact stable `X.Y.Z` version. Declaration selection SHALL use exact
candidate-manifest version equality and SHALL remain bound to the immutable
candidate source commit, manifest digest, declaration identity/digest, and
resolved required-item states.

#### Scenario: Exact preview candidate is authorized by its reviewed declaration
- **WHEN** the immutable candidate version is `0.9.0-preview.1`
- **AND** exactly one reviewed declaration targets `0.9.0-preview.1`
- **AND** all required publication evidence and required-item states pass
- **THEN** publication authorization uses that declaration for that candidate

#### Scenario: Preview authority does not authorize stable
- **WHEN** a reviewed declaration targets `0.9.0-preview.1`
- **AND** the immutable candidate version is `0.9.0`
- **THEN** the preview declaration is not selected
- **AND** publication fails closed unless a separate exact stable declaration exists

#### Scenario: Preview authority does not authorize another preview
- **WHEN** a reviewed declaration targets `0.9.0-preview.1`
- **AND** the immutable candidate version is `0.9.0-preview.2`
- **THEN** publication fails closed unless a separate exact declaration targets
  `0.9.0-preview.2`

#### Scenario: Arbitrary prerelease labels remain unsupported
- **WHEN** a publication candidate uses an unmapped prerelease shape such as
  `0.9.0-rc.1`
- **THEN** the release-scope target is rejected
- **AND** no stable or preview declaration is treated as authority

### Requirement: Version override does not create publication authority
The release workflow MAY use an exact version override when tag-based
calculation cannot express the intended release target, including the first
preview of a new minor line. The override SHALL NOT select, synthesize, or
weaken release authority. Publication SHALL still require exactly one reviewed
declaration whose target equals the immutable candidate version.

#### Scenario: First next-minor preview uses an exact override safely
- **WHEN** the latest stable tag is `v0.8.2`
- **AND** the maintainer requests `0.9.0-preview.1` via version override
- **THEN** the candidate version may be `0.9.0-preview.1`
- **AND** publication proceeds only if the exact `0.9.0-preview.1`
  declaration and all normal publication evidence pass
