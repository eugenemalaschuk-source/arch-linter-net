# Release Process

## Versioning

ArchLinterNet follows [Semantic Versioning 2.0](https://semver.org/).

Pre-1.0 preview releases use versions such as `0.1.0-preview.1`.
The manual release workflow calculates the package version automatically from
git tags based on the selected release scenario (`preview`, `patch`, `minor`,
or `major`). Do not update `Directory.Build.props` just to run a release.

### Version Calculation Rules

The workflow detects the latest SemVer-compatible git tag (format
`vX.Y.Z` or `vX.Y.Z-preview.N`) and calculates the next version:

| Latest tag | Release type | Calculated version |
|---|---|---|
| `v0.1.1-preview.2` | `preview` | `0.1.1-preview.3` |
| `v0.1.0` | `preview` | `0.1.1-preview.1` |
| `v0.1.1-preview.2` | `patch` | `0.1.1` |
| `v0.1.0` | `patch` | `0.1.1` |
| `v0.1.0` | `minor` | `0.2.0` |
| `v0.1.0` | `major` | `1.0.0` |

- `preview` increments the preview number within the current preview train,
  or starts a new preview train from the next patch when the latest tag is
  stable.
- `patch` finalizes a preview train (drops the prerelease suffix) or
  increments the patch version from the latest stable tag.
- `minor` and `major` always produce stable versions from the base version,
  ignoring any prerelease suffix.
- Tags use `v` prefix; package versions are always output without `v`.

An explicit `version_override` input bypasses tag detection and can be used
for the first release (when no tags exist yet) or for emergency recovery.

## Workflow Separation

Pull request CI and package publication are intentionally separate:

- `.github/workflows/ci.yml` validates pull requests and pushes to `main` with
  `make restore` followed by the full `make acceptance` gate.
- `.github/workflows/release-nuget.yml` is the only workflow that builds
  official versioned package artifacts, can publish to NuGet.org, and deploys
  documentation to GitHub Pages when publication is enabled.

The CI workflow must not call `dotnet pack`, request publishing identity tokens,
publish packages, create tags, or create GitHub Releases.

Local `make pack` is only for developer inspection. Official preview package
publication and GitHub Pages deployment are performed by the manual GitHub
Actions workflow.

## NuGet.org Trusted Publishing Setup

Before the first public publication:

1. Configure a NuGet.org trusted publishing policy with these fields:
   - package owner: `eugene.malaschuk`
   - repository owner: `eugenemalaschuk-source`
   - repository: `arch-linter-net`
   - workflow file: `release-nuget.yml`
   - environment: empty
1. Enable GitHub Pages for the repository and use GitHub Actions as the Pages
   source.

Classic NuGet API keys are not used for automated publishing in this workflow.

NuGet.org is the only package publication target for preview consumption.
GitHub Packages is not used as package storage or as a mirror in the initial
release pipeline.

## Manual Release Steps

Initial preview releases use two explicit manual workflow runs from the GitHub
Actions UI. Do not publish packages from a local machine.

### Step 1: Dry-Run Package Build

In GitHub, open **Actions**, select `Release NuGet packages and docs`, choose
**Run workflow**, and select:

- **release_type**: preview (or the appropriate scenario for your release)
- **publish**: false
- **version_override**: leave empty for normal releases

The workflow automatically detects the latest git tag and calculates the
package version. The calculated version is printed in the workflow log.

Expected result:

- restore, explicit Release build, and `make acceptance` pass;
- versioned `.nupkg` artifacts are created for the publishable projects;
- package artifacts are uploaded to the workflow run;
- nothing is published to NuGet.org.

Download or inspect the uploaded artifacts before publication.

### Step 2: Public Publication

After the dry-run artifacts are checked in the GitHub workflow run, rerun the
same workflow from the GitHub Actions UI with the same settings and publication
enabled:

- **release_type**: same scenario as dry-run
- **publish**: true
- **version_override**: leave empty

Expected result:

- restore, explicit Release build, and `make acceptance` pass again;
- versioned `.nupkg` artifacts are produced again;
- package artifacts are uploaded to the workflow run;
- packages are pushed to NuGet.org using trusted publishing;
- duplicate pushes are skipped to make accidental reruns non-destructive;
- documentation is built and deployed to GitHub Pages through GitHub Actions.

After publication, record the published package IDs, versions, and GitHub Pages
deployment URL in the related issue or pull request notes.

### Emergency Version Override

If the automatic version calculation cannot find a valid base tag (e.g.,
first release with no tags), use `version_override` to specify the exact
package version (e.g., `0.1.0-preview.1`). The override bypasses tag
detection and version calculation entirely. Override values are still
validated against the NuGet version format before proceeding.

## Future Automation

Tag-triggered publication and GitHub Release automation are out of scope for the
initial preview process. Workflow artifacts are the dry-run inspection and audit
trail.
