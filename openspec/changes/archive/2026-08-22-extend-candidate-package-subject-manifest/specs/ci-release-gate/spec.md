## MODIFIED Requirements

### Requirement: Release pipeline publishes validated immutable candidates
The manual release workflow SHALL calculate version, build final metadata, pack
and manifest the complete paired primary-package and symbol-package candidate
set once, validate the downloaded candidate set through Checkpoint B, and
publish or attach only the same manifest-selected digest-verified subjects. It
SHALL use exactly one primary-package NuGet push per pair, verify the adjacent
manifest-selected symbol package before the push, and SHALL not independently
double-push or silently omit a symbol subject. It SHALL install the pinned
OpenSpec CLI and run `openspec validate --all --strict` before evidence
aggregation.

#### Scenario: Strict OpenSpec validation fails
- **WHEN** the strict OpenSpec gate fails or its pinned executable is unavailable
- **THEN** evidence aggregation and publication do not run

#### Scenario: Publication selects an unexpected package file
- **WHEN** the publication or GitHub Release attachment path selects a package
  or symbol file outside the verified manifest inventory
- **THEN** the workflow fails before publication or attachment
