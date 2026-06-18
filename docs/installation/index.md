# Installation

## .NET global tool

```bash
dotnet tool install --global ArchLinterNet.Cli
```

Verify installation:

```bash
dotnet arch-linter-net --help
```

## NuGet package

Add a package reference to your test project:

```bash
dotnet add package ArchLinterNet.Core
```

Or for stricter integration:

```bash
dotnet add package ArchLinterNet.Testing
```

## CI installation

Add to your GitHub Actions workflow:

```yaml
- name: Install ArchLinterNet
  run: dotnet tool install --global ArchLinterNet.Cli

- name: Validate architecture
  run: dotnet arch-linter-net
```

## Requirements

- .NET 10 SDK or later
- Windows, macOS, or Linux
