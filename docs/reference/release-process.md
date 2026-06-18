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
  restore, Release build, and Release test steps only.
- `.github/workflows/release-nuget.yml` is the only workflow that builds
  official versioned package artifacts and can publish to NuGet.org.

The CI workflow must not call `dotnet pack`, read `NUGET_API_KEY`, publish
packages, create tags, or create GitHub Releases.

## NuGet.org Setup

Before the first public publication:

1. Create or obtain a NuGet.org API key that can publish these package IDs:
   `ArchLinterNet.Core`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, and
   `ArchLinterNet.Unity`.
2. Store the key as the repository secret `NUGET_API_KEY`.
3. Do not commit the key or expose it to pull request CI.

NuGet.org is the primary public package storage for preview consumption.
GitHub Packages is not part of the initial preview release path.

## Manual Release Steps

Initial preview releases use two explicit manual workflow runs.

### Step 1: Dry-Run Package Build

Run the `Release NuGet packages` workflow manually with:

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

After the dry-run artifacts are checked, rerun the same workflow with the same
version and publication enabled:

```text
version = 0.1.0-preview.1
publish = true
```

Expected result:

- restore, build, and tests pass again in Release configuration;
- versioned `.nupkg` artifacts are produced again;
- package artifacts are uploaded to the workflow run;
- packages are pushed to NuGet.org using `NUGET_API_KEY`;
- duplicate pushes are skipped to make accidental reruns non-destructive.

After publication, record the published package IDs and versions in the related
issue or pull request notes.

## Future Automation

Tag-triggered publication and GitHub Release automation are out of scope for the
initial preview process. They can be introduced later after the manual NuGet.org
flow is validated.
