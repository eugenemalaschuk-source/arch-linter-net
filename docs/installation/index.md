# Installation

ArchLinterNet is distributed through NuGet.org as a .NET tool and as reusable .NET packages.

## Requirements

- .NET 10 SDK or later.
- Windows, macOS, or Linux.
- For architecture validation, either current build outputs or an analysis configuration that can discover/build the selected projects.

## Recommended: repository-local .NET tool

A local tool manifest makes the resolved CLI version part of the repository rather than a developer-machine convention.

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
dotnet tool restore
dotnet arch-linter-net --help
```

Commit `.config/dotnet-tools.json`. When upgrading:

```bash
dotnet tool update ArchLinterNet.Cli
dotnet tool restore
```

Review the manifest diff and run the repository acceptance gate before merging the upgrade.

Documentation intentionally does not hard-code the current product package version. The local manifest is the version authority for a repository.

## Global .NET tool

For interactive use:

```bash
dotnet tool install --global ArchLinterNet.Cli
arch-linter-net --help
```

A global install follows the package source and version resolution selected by the caller, so do not treat a developer's global tool as an implicit CI version policy.

## Run from source

Inside this repository:

```bash
dotnet run --project src/ArchLinterNet.Cli -- --help

dotnet run --project src/ArchLinterNet.Cli -- \
  --policy architecture/arch.yml \
  --mode strict
```

## Library packages

Use the testing adapter when architecture validation should run from a test project:

```bash
dotnet add package ArchLinterNet.Testing
```

Use the core package when building a custom host or using the core application APIs:

```bash
dotnet add package ArchLinterNet.Core
```

Unity `.asmdef` validation is part of `ArchLinterNet.Core`; no separate Unity package is required.

## CI

For a repository-local tool:

```yaml
- name: Restore local tools
  run: dotnet tool restore

- name: Validate architecture
  run: dotnet arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

`--ensure-built` is opt-in. Without it, ArchLinterNet validates the available project/assembly state and fails closed on missing, stale, or ambiguous inputs instead of silently rebuilding.

The Apple Silicon self-dogfood defect previously tracked in issue #639 is fixed on current `main` by #648. Current documentation describes the fixed `--ensure-built` behavior; release-qualified historical reproduction belongs in the provenance workflow, not in evergreen install commands.

See [Getting Started](../getting-started/index.md), [CI integration](../guides/ci-integration.md), and [Adopt or Upgrade ArchLinterNet](../guides/upgrading.md).
