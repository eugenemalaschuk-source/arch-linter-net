# Release Process Documentation Specification

## Purpose
Documents the manual release procedure for maintainers.

## Requirements

### Requirement: Manual release procedure documentation
ArchLinterNet SHALL document the initial preview package release process using the manual NuGet release workflow.

#### Scenario: Dry-run procedure is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains how to run the manual release workflow from the GitHub Actions UI with an explicit preview version and `publish=false`

#### Scenario: Public publication procedure is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains how to rerun the manual release workflow from the GitHub Actions UI with the same explicit preview version and `publish=true`

#### Scenario: NuGet.org trusted publishing setup is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains the required NuGet.org trusted publishing policy fields and states that classic API keys are not used for automated publishing

#### Scenario: Publication recordkeeping is documented
- **WHEN** packages are published publicly
- **THEN** the documentation instructs maintainers to record published package IDs, versions, and GitHub Pages deployment URL in issue or PR notes
