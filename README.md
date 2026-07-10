<p align="center">
  <img src="docs/assets/logo.png" alt="ArchLinterNet" width="420">
</p>

[![CI](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml/badge.svg)](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/ArchLinterNet.Cli)](https://www.nuget.org/packages/ArchLinterNet.Cli/)
[![Test coverage](https://codecov.io/github/eugenemalaschuk-source/arch-linter-net/graph/badge.svg)](https://app.codecov.io/github/eugenemalaschuk-source/arch-linter-net)
[![Sonar Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=alert_status&branch=main)](https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main)
[![Sonar Maintainability](https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=sqale_rating&branch=main)](https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main)
[![Sonar Reliability](https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=reliability_rating&branch=main)](https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main)
[![Sonar Security](https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=security_rating&branch=main)](https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main)

The CI badge tracks one workflow that runs all quality signals in the same `validate` job, so a green badge means all of them passed. SonarCloud also analyzes trusted pull requests, decorates the PR, and evaluates the quality gate on new code rather than forcing the entire historical codebase to be clean before the PR can merge:

| Quality signal | Source |
|---|---|
| Build/test | `make acceptance` (lint + all tests) |
| Test coverage (line %) | CI runs `make test-coverage`, uploads Cobertura XML to Codecov, and the badge above updates dynamically from Codecov |
| SonarCloud PR quality gate | trusted `pull_request` runs analyze new code, publish a SonarCloud PR result link, and fail CI when the Sonar quality gate fails |
| SonarCloud main quality signals | the Sonar badges above track the `main` branch project status for quality gate, maintainability, reliability, and security |
| Architecture validation | strict ArchLinterNet self-policy check (`architecture/dependencies.arch.yml`) |
| Architecture coverage | strict/audit coverage JSON artifacts + Markdown report + sticky PR comment |

See [CI integration](docs/guides/ci-integration.md#test-coverage-with-codecov-and-sonarcloud) for how test coverage upload, SonarCloud PR analysis, the dynamic badge, and the architecture coverage gate fit together.

YAML-first architecture governance for .NET repositories.

ArchLinterNet turns architectural decisions into executable contracts: layered boundaries, protected internal APIs, dependency policies, baseline-aware adoption, architecture coverage, and CI-ready diagnostics. It helps teams catch architecture drift in pull requests before it becomes hidden design debt.

The goal is not just to lint dependencies. ArchLinterNet makes architecture rules explicit, reviewable, enforceable, and safe to evolve as a normal part of development.

> Status: early preview. The YAML policy surface is being stabilized for the first public `0.x` NuGet and .NET tool releases.

## Why ArchLinterNet?

Architecture rules often start as diagrams, ADRs, review comments, handwritten test helpers, or tribal knowledge. That works for a while, but the rules quickly become hard to discover, hard to reuse across repositories, and hard for humans or AI agents to review consistently.

ArchLinterNet uses a repository-owned YAML policy file as the source of truth:

```text
architecture/dependencies.arch.yml
        ↓
ArchLinterNet CLI / test adapter
        ↓
strict or audit architecture validation
        ↓
human diagnostics + CI artifacts
```

Use it when you want architecture rules to be declarative, reviewable, CI-friendly, and independent from one-off test code.

## Quick start

Create `architecture/dependencies.arch.yml`:

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
dotnet run --project src/ArchLinterNet.Cli -- --policy architecture/dependencies.arch.yml --mode strict
```

After installing the .NET tool from NuGet.org:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode strict
```

## Main capabilities

ArchLinterNet focuses on static architecture guardrails:

- YAML policy loading and schema-backed contract authoring.
- Namespace/layer dependency contracts and allow-only contracts.
- Ordered layer contracts and reusable layer templates.
- Dependency cycle, acyclic sibling, independence, and assembly independence checks.
- Directional assembly dependency and assembly allow-only checks.
- Protected surface contracts for importer restrictions.
- External dependency leakage checks and external allow-only whitelist checks for vendor/framework namespaces.
- Method-body forbidden API checks.
- Unity `.asmdef` dependency validation.
- Coverage contracts for unmapped first-party namespaces, projects, and assemblies.
- Project/solution discovery when assemblies are not hand-listed.
- Policy-consistency diagnostics for contradictory or unreachable policy definitions.
- Strict gates, audit diagnostics, JSON output, timings, and migration baselines.

ArchLinterNet does **not** validate runtime dependency injection behavior, authorization/security correctness, code ownership, semantic data flow, or arbitrary custom YAML fields outside the documented schema.

## Documentation

Public product documentation is published through MkDocs and GitHub Pages:

- [Documentation home](https://eugenemalaschuk-source.github.io/arch-linter-net/)
- [Getting started](https://eugenemalaschuk-source.github.io/arch-linter-net/getting-started/)
- [Installation](https://eugenemalaschuk-source.github.io/arch-linter-net/installation/)
- [CLI usage](https://eugenemalaschuk-source.github.io/arch-linter-net/cli/)
- [Policy format](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/)
- [Contract families](https://eugenemalaschuk-source.github.io/arch-linter-net/contracts/)
- [Coverage contracts](https://eugenemalaschuk-source.github.io/arch-linter-net/contracts/coverage/)
- [CI integration](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/ci-integration/)
- [AI policy authoring](https://eugenemalaschuk-source.github.io/arch-linter-net/ai/)
- [Supported capabilities and non-goals](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/supported-capabilities/)

Internal project documentation remains in repository Markdown files such as `docs/internal/`, `openspec/`, `.github/`, and root governance files. It is not part of the published product site.

## Local documentation workflow

```bash
make venv        # create Python virtual environment
make docs-serve  # preview MkDocs locally
make docs-build  # build the static documentation site
make fmt-docs    # auto-format markdown documentation
make lint-docs   # strict documentation validation
```

Generated `site/` output is a build artifact and should not be committed.

## NuGet and repository links

NuGet packages should expose only public user-facing links:

- project/documentation URL: the GitHub Pages MkDocs site;
- repository URL: this GitHub repository;
- package README: this concise product README;
- license: repository license expression.

NuGet metadata must not point users to internal backlog governance, OpenSpec archives, or maintenance-agent instructions as product documentation.

## License

MIT.
