# Installable `main.N` builds

ArchLinterNet publishes an internal installable NuGet build for every successful `main` package workflow run. These builds exist for dogfooding in real consumer repositories before a public release; they are not release candidates and are never published to NuGet.org.

## Version authority

`Directory.Build.props` contains the single explicit development release line:

```xml
<ArchLinterDevelopmentVersion>0.8.0</ArchLinterDevelopmentVersion>
```

The `Main NuGet Builds` workflow combines that value with its monotonic GitHub Actions run number:

```text
0.8.0-main.421
0.8.0-main.422
```

CI must not infer whether the next release is patch/minor/major from the latest git tag. After a stable public release, advance `ArchLinterDevelopmentVersion` to the next intended development line in a normal reviewed PR. The manual public release workflow remains tag/scenario driven and explicitly overrides the package/assembly version.

Ordinary local source builds use the separate `dev` suffix (`0.8.0-dev`).

## Post-merge topology

A merge to protected `main` starts two independent workflows:

```text
main
├─ Main Quality Telemetry
│  ├─ Linux unit coverage shards
│  ├─ Python tooling coverage
│  ├─ SonarCloud main analysis
│  └─ Codecov main upload
└─ Main NuGet Builds
   ├─ restore + Release build
   ├─ pack CEL/Core/Cli/Testing
   ├─ verify package-manifest identity against the exact source SHA
   ├─ publish four primary .nupkg files to GitHub Packages
   └─ retain the latest five complete main-build sets
```

The full lint/architecture/cross-platform/E2E/packed-artifact/package-validation/CodeQL merge gate remains on the up-to-date pull request. `main` does not rerun that matrix merely because the accepted tree was merged.

Package publication does not depend on SonarCloud/Codecov success, and quality telemetry does not depend on package-registry success.

## Producer authentication

No new repository secret is required for `arch-linter-net`.

The workflow uses the built-in `GITHUB_TOKEN` with job-local `packages: write` permission to publish and to manage package retention. The token is added only to the ephemeral runner's NuGet source; no credential is committed to the repository.

The first GitHub Packages publication is private by default. `RepositoryUrl` already links every ArchLinterNet package to this repository, which gives the publishing repository package administration needed by the retention workflow.

## Retention semantics

A retained build is a version present under all four package IDs:

- `ArchLinterNet.CEL`;
- `ArchLinterNet.Core`;
- `ArchLinterNet.Cli`;
- `ArchLinterNet.Testing`.

Only versions matching `major.minor.patch-main.N` participate in retention. Stable versions and other prerelease families such as `rc.*` are never selected for deletion.

The cleanup keeps the newest five complete sets. A partially published `main.N` is not counted and is intentionally left visible for diagnosis rather than being silently deleted. Cleanup starts only after the current four-package publication succeeds. Near-concurrent cleanup is safe: deletion of an already-removed version is tolerated, and the current workflow's version is never selected for deletion even if runs finish out of order.

The generated `.snupkg` files remain part of the local package manifest/integrity check. GitHub Packages receives only the four primary `.nupkg` packages (`dotnet nuget push --no-symbols`); public-release symbol publication remains owned by `release-nuget.yml`/NuGet.org.

## Use from another GitHub Actions repository

For a private main-build package, grant the consumer repository **Read** access under each package's **Package settings → Manage Actions access** once. Then that consumer repository can authenticate its GitHub Packages NuGet source with its own `GITHUB_TOKEN`; do not create a shared `GITHUB_PACKAGES_PAT` repository secret for this workflow.

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
