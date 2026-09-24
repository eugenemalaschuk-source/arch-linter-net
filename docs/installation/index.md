# Installation

ArchLinterNet is distributed on NuGet.org as a .NET tool and reusable packages.

## Requirements

Use the .NET 10 SDK for the CLI, plus the SDKs/workloads required by the code you
analyze. Windows, macOS and Linux are supported. Architecture checks need
compatible compiled outputs or a policy from which the CLI can prepare them.

## Recommended: repository-local .NET tool

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --help
```

Create a manifest only when the repository does not already have one. Commit
`.config/dotnet-tools.json`: it records the exact resolved package version.
CI should restore this manifest, not select a fresh version on every run.

For upgrades, select and review an exact version:

```bash
: "${ARCHLINTERNET_VERSION:?Set the reviewed package version}"
dotnet tool update ArchLinterNet.Cli --version "$ARCHLINTERNET_VERSION"
dotnet tool restore
```

Run the repository's checks before merging the new pin. See [upgrading](../guides/upgrading.md).

## Global .NET tool

For interactive use:

```bash
dotnet tool install --global ArchLinterNet.Cli
arch-linter-net --help
```

A developer's global installation is not the repository's CI version policy.

## Run from source

Inside an ArchLinterNet source checkout, use its actual self-policy:

```bash
dotnet run --project src/ArchLinterNet.Cli -- --help
dotnet run --project src/ArchLinterNet.Cli -- \
  --policy architecture/dependencies.arch.yml --mode strict --ensure-built
```

`architecture/arch.yml` is a recommended consumer filename, not a file supplied
in this source repository. Author that policy in your own repository following
[First policy](../getting-started/first-policy.md).

## Library packages

```bash
dotnet add package ArchLinterNet.Testing
```

Use the [Testing adapter](../usage/test-adapter.md) for test-hosted checks.
`ArchLinterNet.Core` is available for a custom host; Unity `.asmdef` validation
is part of Core, with no separate Unity package. When the CLI analyzes a project
that references these packages, keep the selected package set compatible with
the pinned CLI rather than mixing arbitrary Core assembly versions.

## CI

Use the [minimal required PR workflow](../guides/ci-integration.md).
`--ensure-built` explicitly prepares the selected graph; without it, analysis
does not silently rebuild missing inputs. A supported prepared-receipt consumer
is a distinct [build-reuse path](../usage/timings.md#prepared-receipts).
