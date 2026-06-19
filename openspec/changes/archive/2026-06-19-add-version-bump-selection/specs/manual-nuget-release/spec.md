## MODIFIED Requirements

### Requirement: Manual NuGet release workflow
ArchLinterNet SHALL provide a separate GitHub Actions workflow for official package builds and optional NuGet.org publication that maintainers run through the GitHub Actions UI using `workflow_dispatch`.

#### Scenario: Manual release inputs are required
- **WHEN** a maintainer starts the release workflow manually
- **THEN** the workflow requires a `release_type` choice input (`preview`, `patch`, `minor`, or `major`), exposes an optional `version_override` text input for emergency recovery, and exposes a `publish` boolean input that defaults to `false`
