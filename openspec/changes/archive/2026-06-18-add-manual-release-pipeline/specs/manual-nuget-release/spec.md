## ADDED Requirements

### Requirement: Manual NuGet release workflow
ArchLinterNet SHALL provide a separate GitHub Actions workflow for official package builds and optional NuGet.org publication that maintainers run through the GitHub Actions UI using `workflow_dispatch`.

#### Scenario: Manual release inputs are required
- **WHEN** a maintainer starts the release workflow manually
- **THEN** the workflow requires a `version` input and exposes a `publish` boolean input that defaults to `false`

#### Scenario: Manual release starts from GitHub UI
- **WHEN** a maintainer releases preview packages
- **THEN** the maintainer starts the release from the GitHub Actions UI instead of publishing from a local machine

#### Scenario: Manual release validates version input
- **WHEN** the release workflow starts
- **THEN** it rejects empty or obviously invalid package version input before building packages

### Requirement: Manual release package build
The release workflow SHALL restore, run the repository acceptance gate, build, pack, and upload versioned NuGet package artifacts using the explicit version input.

#### Scenario: Dry-run release builds artifacts
- **WHEN** the release workflow runs with `publish=false`
- **THEN** it restores packages, builds in Release configuration with the explicit version, runs `make acceptance`, packs versioned `.nupkg` artifacts, uploads them to the workflow run, and publishes nothing

#### Scenario: Package version comes from workflow input
- **WHEN** the release workflow packs packages
- **THEN** it passes the explicit version input as `PackageVersion` for the package artifacts

#### Scenario: Expected package projects are packed
- **WHEN** the release workflow packs packages
- **THEN** it creates package artifacts for `ArchLinterNet.Core`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, and `ArchLinterNet.Unity`

### Requirement: Controlled NuGet.org publication
The release workflow SHALL publish packages to NuGet.org only when the manual run explicitly requests publication.

#### Scenario: Publication requires publish input
- **WHEN** the release workflow runs with `publish=true`
- **THEN** it publishes package artifacts to `https://api.nuget.org/v3/index.json`

#### Scenario: Publication uses trusted publishing
- **WHEN** the release workflow publishes packages
- **THEN** it publishes through NuGet.org Trusted Publishing without a classic NuGet API key

#### Scenario: Publication is rerun-safe
- **WHEN** the release workflow publishes packages
- **THEN** it uses `--skip-duplicate` for NuGet push operations

### Requirement: Documentation publication
The release workflow SHALL publish the documentation site to GitHub Pages when public package publication is requested.

#### Scenario: Documentation deploys with public publication
- **WHEN** the release workflow runs with `publish=true` and package publication succeeds
- **THEN** it builds the documentation site and deploys it to GitHub Pages

#### Scenario: Documentation does not deploy during dry-run
- **WHEN** the release workflow runs with `publish=false`
- **THEN** it does not deploy documentation to GitHub Pages
