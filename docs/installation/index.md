# Installation

ArchLinterNet is distributed as .NET packages and a .NET tool.

Package publication happens through NuGet.org. Until a package is published, use the development-from-source command shown below.

## Requirements

- .NET 10 SDK or later.
- Windows, macOS, or Linux.
- A built repository or configured assembly search paths for the assemblies you want to validate.

## Development from source

From this repository:

```bash
dotnet run --project src/ArchLinterNet.Cli -- --help

dotnet run --project src/ArchLinterNet.Cli -- \
  --policy architecture/dependencies.arch.yml \
  --mode strict
```

## .NET global tool

After the CLI package is available on NuGet.org:

```bash
dotnet tool install --global ArchLinterNet.Cli
arch-linter-net --help
```

For reproducible repository/CI use, prefer a local tool manifest below. A global
install follows the package source selected by the caller and should not be used
as an implicit repository version policy.

Run validation:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode strict
```

## .NET local tool

For repository-pinned usage, create or update a tool manifest:

```bash
dotnet new tool-manifest

dotnet tool install ArchLinterNet.Cli

dotnet tool restore

dotnet arch-linter-net --policy architecture/dependencies.arch.yml --mode strict
```

The install records the exact resolved package version in
`.config/dotnet-tools.json`. Review and commit that manifest. When upgrading,
run `dotnet tool update ArchLinterNet.Cli`, review the manifest diff, and run the
repository acceptance gate before merging it.

Local tools are recommended for CI because the selected tool version is pinned
in the repository rather than in evergreen documentation.

## NuGet packages for test integration

Use the testing package when architecture validation should run from a test project:

```bash
dotnet add package ArchLinterNet.Testing
```

Use the core package when building a custom host or calling the asmdef-only validation API:

```bash
dotnet add package ArchLinterNet.Core
```

Unity `.asmdef` validation is part of `ArchLinterNet.Core`; no separate Unity package is required:

```csharp
using ArchLinterNet.Core.Asmdef;

bool passed = AsmdefValidator.Validate(
    "architecture/dependencies.arch.yml",
    out var violations);
```

## CI installation

A minimal GitHub Actions step for a published global tool:

```yaml
- name: Install ArchLinterNet
  run: dotnet tool install --global ArchLinterNet.Cli

- name: Validate architecture
  run: arch-linter-net --mode strict
```

For local tools, prefer:

```yaml
- name: Restore local tools
  run: dotnet tool restore

- name: Validate architecture
  run: dotnet arch-linter-net --mode strict
```

See [CI integration](../guides/ci-integration.md) for strict + audit workflows.
For greenfield adoption, upgrades, and prepared/offline environments, see
[Adopt or Upgrade ArchLinterNet](../guides/upgrading.md).

## NuGet.org links

NuGet package metadata should expose only public product links:

- project URL: the GitHub Pages documentation site;
- repository URL: the GitHub repository;
- package README: the concise product README;
- license expression: the repository license.

See [NuGet package metadata](../reference/nuget-metadata.md) for the expected link model.
