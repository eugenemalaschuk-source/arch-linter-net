<p align="center">
  <img src="docs/assets/logo.png" alt="ArchLinterNet" width="420">
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/ArchLinterNet.Cli/"><img alt="NuGet version" src="https://img.shields.io/nuget/v/ArchLinterNet.Cli.svg"></a>
  <a href="https://www.nuget.org/packages/ArchLinterNet.Cli/"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/ArchLinterNet.Cli"></a>
  <a href="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/main-quality.yml"><img alt="Main quality" src="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/main-quality.yml/badge.svg?branch=main"></a>
  <a href="https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net"><img alt="Sonar Quality Gate" src="https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=alert_status"></a>
  <a href="https://app.codecov.io/github/eugenemalaschuk-source/arch-linter-net"><img alt="Test coverage" src="https://codecov.io/github/eugenemalaschuk-source/arch-linter-net/graph/badge.svg?branch=main"></a>
  <a href="https://eugenemalaschuk-source.github.io/arch-linter-net/"><img alt="Documentation" src="https://img.shields.io/badge/docs-GitHub%20Pages-blue"></a>
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-blue.svg"></a>
</p>

YAML-first architecture governance for .NET repositories.

ArchLinterNet turns architecture decisions into executable, reviewable contracts. It governs namespace and assembly boundaries, project/package metadata, semantic roles and contexts, public API surfaces, architecture coverage, migration debt, and CI change gates — with deterministic diagnostics that humans and automation can consume.

The goal is not just to lint dependencies. ArchLinterNet makes architecture rules explicit and safe to evolve as normal repository code.

## Why ArchLinterNet?

Architecture rules often start as diagrams, ADRs, review comments, handwritten test helpers, or tribal knowledge. That works for a while, but the rules quickly become hard to discover, hard to reuse across repositories, and hard for humans or AI agents to review consistently.

ArchLinterNet uses a repository-owned YAML policy file as the source of truth:

```text
architecture/arch.yml (recommended; any selected filename works)
        ↓
ArchLinterNet CLI / test adapter
        ↓
strict or audit architecture validation
        ↓
human diagnostics + JSON/SARIF/CI artifacts
```

Use it when you want architecture rules to be declarative, reviewable, CI-friendly, and independent from one-off test code.

## Quick start

Create a root policy. This quick start uses the recommended concise path `architecture/arch.yml`; the filename is configurable and has no runtime semantics:

```yaml
version: 1
name: Example Architecture Contract

layers:
  application:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
  infrastructure:
    namespace: MyApp.Infrastructure

analysis:
  target_assemblies:
    - MyApp.Application
    - MyApp.Domain
    - MyApp.Infrastructure

contracts:
  strict:
    - id: application-not-infrastructure
      name: application-must-not-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      reason: Application code must depend on abstractions, not concrete infrastructure.

  strict_layers:
    - id: clean-architecture-layering
      name: clean-architecture-layering
      layers: [infrastructure, application, domain]
      reason: Dependencies must point inward toward the domain.
```

Run from this repository during development:

```bash
dotnet run --project src/ArchLinterNet.Cli -- --policy architecture/arch.yml --mode strict
```

Or install the CLI as a local .NET tool:

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli --version <version>
dotnet tool run arch-linter-net -- --policy architecture/arch.yml --mode strict
```

For CI, pin the tool version in `.config/dotnet-tools.json` and run `dotnet tool restore` before invoking the CLI.

## Core ideas

### Repository-owned policy

The architecture policy lives with the code and is reviewed through the same pull request process.

### Deterministic contracts

Contracts are evaluated from explicit policy, project/assembly facts, semantic classification, and reviewed snapshots rather than hidden hosted configuration.

### Strict and audit modes

Strict mode is blocking. Audit mode produces diagnostics without converting known architecture debt into an implicit pass.

### Architecture coverage

Coverage reports whether first-party code is actually governed by configured architecture rules, so a green validation result cannot hide an accidentally unclassified area.

### Baselines and change gates

Reviewed baselines make existing debt explicit. The change gate can block newly introduced architecture debt without silently weakening the target architecture.

### Public API governance

Reviewed public API snapshots protect intentional compatibility surfaces independently from general CLR visibility.

### Human and automation outputs

The same analysis can produce readable diagnostics plus machine-oriented JSON/SARIF artifacts for CI and tooling.

## Documentation

The public documentation is published at <https://eugenemalaschuk-source.github.io/arch-linter-net/>.

Start with:

- [Getting started](https://eugenemalaschuk-source.github.io/arch-linter-net/getting-started/)
- [CLI reference](https://eugenemalaschuk-source.github.io/arch-linter-net/cli/)
- [Policy format](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/)
- [Contract reference](https://eugenemalaschuk-source.github.io/arch-linter-net/contracts/)
- [CI integration](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/ci-integration/)

GitHub Pages is deployed only by the public release workflow. Merges to `main` refresh quality telemetry and internal `main.N` packages but do not publish MkDocs documentation.

## Packages

ArchLinterNet is shipped as four packages:

- `ArchLinterNet.Cli` — installable .NET tool and primary user entrypoint.
- `ArchLinterNet.Core` — reusable validation engine and policy model.
- `ArchLinterNet.Testing` — NUnit adapter for architecture checks in test projects.
- `ArchLinterNet.CEL` — reusable CEL-compatible expression engine used by semantic policy expressions.

Stable/preview public releases are published to NuGet.org. Internal `main.N` development builds are published to GitHub Packages for dogfooding and are not public releases.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, validation commands, and pull request expectations.

## License

MIT. See [LICENSE](LICENSE).
