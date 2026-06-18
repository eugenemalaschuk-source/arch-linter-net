# Release Process

## Versioning

ArchLinterNet follows [Semantic Versioning 2.0](https://semver.org/).

Pre-1.0 preview releases use explicit versions such as `0.1.0-preview.1`.
The manual release workflow receives the package version as an input and passes
it to MSBuild when building official package artifacts. Do not update
`Directory.Build.props` just to run a preview release.

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
2. Enable GitHub Pages for the repository and use GitHub Actions as the Pages
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
**Run workflow**, and enter:

```text
version = 0.1.0-preview.1
publish = false
```

Expected result:

- restore, build, and tests pass in Release configuration;
- versioned `.nupkg` artifacts are created for the publishable projects;
- package artifacts are uploaded to the workflow run;
- nothing is published to NuGet.org.

Download or inspect the uploaded artifacts before publication.

### Step 2: Public Publication

After the dry-run artifacts are checked in the GitHub workflow run, rerun the
same workflow from the GitHub Actions UI with the same version and publication
enabled:

```text
version = 0.1.0-preview.1
publish = true
```

Expected result:

- restore, build, and tests pass again in Release configuration;
- versioned `.nupkg` artifacts are produced again;
- package artifacts are uploaded to the workflow run;
- packages are pushed to NuGet.org using trusted publishing;
- duplicate pushes are skipped to make accidental reruns non-destructive;
- documentation is built and deployed to GitHub Pages through GitHub Actions.

After publication, record the published package IDs, versions, and GitHub Pages
deployment URL in the related issue or pull request notes.

## Future Automation

Tag-triggered publication and GitHub Release automation are out of scope for the
initial preview process. Workflow artifacts are the dry-run inspection and audit
trail.
