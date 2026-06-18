## ADDED Requirements

### Requirement: Manual release procedure documentation
ArchLinterNet SHALL document the initial preview package release process using the manual NuGet release workflow.

#### Scenario: Dry-run procedure is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains how to run the manual release workflow from the GitHub Actions UI with an explicit preview version and `publish=false`

#### Scenario: Public publication procedure is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains how to rerun the manual release workflow from the GitHub Actions UI with the same explicit preview version and `publish=true`

#### Scenario: NuGet.org setup is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains that `NUGET_API_KEY` must be configured as a repository secret before publication and must not be committed

#### Scenario: Publication recordkeeping is documented
- **WHEN** packages are published publicly
- **THEN** the documentation instructs maintainers to record published package IDs, versions, and GitHub Pages deployment URL in issue or PR notes
