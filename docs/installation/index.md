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

Local tools are recommended for CI because the tool version is pinned in the repository.

## NuGet packages for test integration

Use the testing package when architecture validation should run from a test project:

```bash
dotnet add package ArchLinterNet.Testing
```

Use the core package only when building a custom host or adapter:

```bash
dotnet add package ArchLinterNet.Core
```

Unity-specific `.asmdef` validation lives behind the optional Unity package:

```bash
dotnet add package ArchLinterNet.Unity
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

## NuGet.org links

NuGet package metadata should expose only public product links:

- project URL: the GitHub Pages documentation site;
- repository URL: the GitHub repository;
- package README: the concise product README;
- license expression: the repository license.

See [NuGet package metadata](../reference/nuget-metadata.md) for the expected link model.
