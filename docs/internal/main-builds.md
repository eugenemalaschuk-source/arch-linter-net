# Installable `main.N` builds

ArchLinterNet publishes an installable NuGet development build for every successful `main` package workflow run. These builds exist for dogfooding in real consumer repositories before a public release; they are not release candidates and are never published to NuGet.org.

## Version authority

`Directory.Build.props` contains the single explicit development release line used by installable main builds:

```xml
<ArchLinterDevelopmentVersion>0.8.0</ArchLinterDevelopmentVersion>
```

The `Main NuGet Builds` workflow combines that value with its monotonic GitHub Actions run number:

```text
0.8.0-main.421
0.8.0-main.422
```

CI must not infer whether the next release is patch/minor/major from the latest git tag. After a stable public release, advance `ArchLinterDevelopmentVersion` to the next intended development line in a normal reviewed PR. The manual public release workflow remains tag/scenario driven and explicitly overrides the package/assembly version.

`ArchLinterDevelopmentVersion` is intentionally decoupled from the ordinary source-tree `VersionPrefix`/`VersionSuffix`. Changing the next main-build release line therefore cannot silently change product output, serialized version evidence, or byte-golden tests in normal development builds. `main-packages.yml` supplies the exact `main.N` `Version`/`PackageVersion` explicitly when it builds and packs.

## Post-merge topology

A merge to protected `main` starts two independent workflows:

```text
main
├─ Main Quality Telemetry
│  ├─ Linux unit coverage shards
│  ├─ Python tooling coverage
│  ├─ SonarCloud main analysis + quality-gate observation
│  └─ Codecov main upload
└─ Main NuGet Builds
   ├─ restore + Release build
   ├─ pack CEL/Core/Cli/Testing
   ├─ verify package-manifest identity against the exact source SHA
   ├─ publish four primary .nupkg files to GitHub Packages
   ├─ install/restore the exact published version on a clean runner
   └─ retain five complete sets and prune safely stale partial records
```

The full lint/architecture/cross-platform/E2E/packed-artifact/package-validation/CodeQL merge gate remains on the up-to-date pull request. `main` does not rerun that matrix merely because the accepted tree was merged.

Package publication does not depend on SonarCloud/Codecov success, and quality telemetry does not depend on package-registry success. Within quality telemetry, SonarCloud and Codecov are attempted independently after the coverage evidence is available so an outage in one external service does not suppress the other refresh attempt.

The Sonar scanner still waits for the merged-main Quality Gate and records its result. An explicit processed `FAILED` gate is emitted as a workflow warning and remains visible through SonarCloud and its badge, but it does not redefine the post-merge telemetry refresh as failed: the required up-to-date PR checks remain the merge authority. Authentication, configuration, scanner, upload, processing, timeout, and unrecognized-result failures remain fail-closed and make `Main Quality Telemetry` red.

## Producer authentication and package visibility

No new repository secret is required for `arch-linter-net`.

The workflow uses the built-in `GITHUB_TOKEN` with job-local `packages: write` permission to publish and to manage package retention. The publish step configures credentials only on the ephemeral runner. The consumer-smoke step keeps its temporary `NuGet.Config` credential-free and supplies GitHub Packages credentials through the step-local `NuGetPackageSourceCredentials_github` environment variable. No credential is committed, uploaded as an artifact, or printed by the source diagnostic.

Package visibility is not an authorization boundary for `main.N` publication. The workflow accepts the existing GitHub Package identity visibility and does not attempt to require or change it. This allows the development versions to share the same package IDs as the existing public ArchLinterNet packages while remaining clearly identified by their `-main.N` prerelease version.

`RepositoryUrl` already links every ArchLinterNet package to this repository, which gives the publishing repository package administration needed by the retention workflow.

## Published-package availability proof

A successful package upload is not by itself treated as proof that a consumer can restore the build. Before retention starts, a clean runner performs a bounded eventual-consistency smoke against GitHub Packages:

1. creates a temporary `NuGet.Config` with `<clear />` and exactly two sources: NuGet.org for external dependencies and the repository GitHub Packages feed for ArchLinterNet packages;
2. lists those effective sources without credentials for diagnostics;
3. installs `ArchLinterNet.Cli` at the exact `main.N` version into an empty tool path using that config;
4. confirms the installed tool manifest reports that exact package version and executes the packaged entrypoint;
5. creates an empty `net10.0` consumer project;
6. restores `ArchLinterNet.Testing` at the exact version using the same explicit config;
7. verifies `project.assets.json` resolved the exact matching `ArchLinterNet.Testing`, `ArchLinterNet.Core`, and `ArchLinterNet.CEL` versions.

The smoke never relies on user-, machine-, repository-, or temporary-project-location NuGet source discovery. The final bounded retry increases restore verbosity so a persistent failure shows which configured feeds NuGet actually queried, while credentials remain outside the config and logs.

The four-package build is declared consumable only after this proof succeeds. Registry/authentication/dependency convergence failures remain red, and cleanup does not run after a failed availability smoke.

## Retention semantics

A retained build is a version present under all four package IDs:

- `ArchLinterNet.CEL`;
- `ArchLinterNet.Core`;
- `ArchLinterNet.Cli`;
- `ArchLinterNet.Testing`.

Only versions matching `major.minor.patch-main.N` participate in retention. Stable versions and other prerelease families such as `rc.*` are never selected for deletion.

The cleanup keeps the newest five complete sets. Complete versions outside that window are deleted from all four package identities. The current workflow's version is never selected for deletion even when workflow runs finish out of order.

Partial/orphan versions are handled separately. A partial version is eligible for cleanup only when all of these are true:

- its parsed `major.minor.patch-main.N` identity is older than the current successfully published complete build;
- every package-version record that exists for that partial version has a GitHub creation timestamp older than the explicit cleanup cutoff;
- the current workflow has already passed the exact-version consumer restore smoke.

The workflow currently uses a one-hour grace window. This leaves failed partial publication visible long enough for diagnosis and prevents a cleanup run from deleting a fresh package record belonging to an overlapping/in-flight publication. Newer partial versions, fresh partial records, records without trustworthy age metadata, stable versions, and unrelated prerelease families are protected. Near-concurrent deletion is idempotent: an already-deleted package-version record returning `404` is tolerated.

The generated `.snupkg` files remain part of the local package manifest/integrity check. GitHub Packages receives only the four primary `.nupkg` packages (`dotnet nuget push --no-symbols`); public-release symbol publication remains owned by `release-nuget.yml`/NuGet.org.

## Use from another GitHub Actions repository

Consumer repositories should authenticate their GitHub Packages NuGet source with their own `GITHUB_TOKEN`; do not create a shared `GITHUB_PACKAGES_PAT` repository secret for this workflow. If a package identity or repository access policy requires explicit Actions access, grant the consumer repository **Read** under **Package settings → Manage Actions access** once. Main-build publication itself does not depend on package visibility being private.

Use source mapping when GitHub Packages and NuGet.org are both configured so only `ArchLinterNet.*` is resolved from the GitHub feed.

Pin the exact build in the consumer's local-tool manifest, for example:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "archlinternet.cli": {
      "version": "0.8.0-main.421",
      "commands": ["arch-linter-net"]
    }
  }
}
```

The consumer CI should then execute its normal architecture/acceptance pipeline against that exact build. Main builds are deliberately not auto-promoted and this repository does not auto-open consumer upgrade PRs.

## Local developer consumption

GitHub's NuGet registry requires authenticated NuGet access. For local installation, use a developer-owned PAT classic with `read:packages` in the developer's user-level NuGet credentials. Never commit that token or a credential-bearing `NuGet.Config`.

## Public release boundary

A successfully dogfooded `main.N` build is evidence that the corresponding `main` source state works in a consumer, but it is not the public release artifact.

`release-nuget.yml` still creates a fresh immutable public candidate, executes Checkpoint B, freezes and verifies package/symbol subjects, creates GitHub provenance attestations, and only then may publish to NuGet.org and create the GitHub Release. No `main.N` package is promoted or renamed into a stable release.

MkDocs/GitHub Pages publication is not part of either ordinary `main` workflow. Public docs are deployed only by `release-nuget.yml` for a real public release with `publish: true`.
