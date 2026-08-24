<p align="center">
  <img src="docs/assets/logo.png" alt="ArchLinterNet" width="420">
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/ArchLinterNet.Cli/"><img alt="NuGet version" src="https://img.shields.io/nuget/v/ArchLinterNet.Cli.svg"></a>
  <a href="https://www.nuget.org/packages/ArchLinterNet.Cli/"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/ArchLinterNet.Cli"></a>
  <a href="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml"><img alt="Architecture policy" src="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml/badge.svg?branch=main"></a>
  <a href="https://app.codecov.io/github/eugenemalaschuk-source/arch-linter-net"><img alt="Test coverage" src="https://codecov.io/github/eugenemalaschuk-source/arch-linter-net/graph/badge.svg"></a>
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

After installing the .NET tool from NuGet.org:

```bash
arch-linter-net --policy architecture/arch.yml --mode strict
```

For repository/CI adoption, prefer a local tool manifest and run `dotnet arch-linter-net ...`; see [Installation](https://eugenemalaschuk-source.github.io/arch-linter-net/installation/).

## Main capabilities

ArchLinterNet focuses on static architecture governance:

- YAML root policies, deterministic local imports, packaged schemas, and reusable bounded source sets.
- Namespace/layer dependency, allow-only, ordered-layer, protected-surface, cycle, independence, assembly, and module-container governance.
- External dependency, NuGet package, framework-reference, project-metadata, method-body, and Unity `.asmdef` rules.
- Type placement, source-layout conventions, attribute usage, inheritance, interface implementation, composition boundaries, and public API surface snapshots.
- Semantic classification from implemented code facts, selector-backed layers, contextual dependency/allow-only rules, and semantic port/ACL boundaries.
- Coverage contracts for `namespace`, `project`, `assembly`, `dependency_edge`, `rule_input`, and `semantic_role` inventory.
- Project/solution discovery, explicit build-state preflight, opt-in `--ensure-built`, condition sets, persistent analysis cache, and bounded parallel scanning.
- Strict gates, audit discovery, migration baselines, policy consistency, policy-context export, and policy-weakening review.
- Architecture change snapshots/reports, history forensics, dependency graphs/path explanation, architecture-policy badge projection, and public API lifecycle commands.
- Human, normalized JSON, SARIF where applicable, repeatable report sinks, timings, analysis profiles, and CI-oriented coverage artifacts.
- CEL-backed `when` predicates at documented closed locations — standard CEL under a safe profile, not an open-ended scripting language.

ArchLinterNet does **not** validate runtime dependency-injection behavior, authorization/security correctness, code ownership, arbitrary semantic data flow, or undocumented custom YAML contract families.

## Documentation

Public product documentation is published through MkDocs and GitHub Pages:

- [Documentation home](https://eugenemalaschuk-source.github.io/arch-linter-net/)
- [Getting started](https://eugenemalaschuk-source.github.io/arch-linter-net/getting-started/)
- [Installation](https://eugenemalaschuk-source.github.io/arch-linter-net/installation/)
- [CLI reference](https://eugenemalaschuk-source.github.io/arch-linter-net/cli/)
- [Policy format](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/)
- [Contract families](https://eugenemalaschuk-source.github.io/arch-linter-net/contracts/)
- [Coverage contracts](https://eugenemalaschuk-source.github.io/arch-linter-net/contracts/coverage/)
- [Supported capabilities and non-goals](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/supported-capabilities/)
- [Real-repository workflow](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/real-repository-workflow/)
- [CI integration](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/ci-integration/)
- [Adopt or upgrade ArchLinterNet](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/upgrading/)
- [Reference entrypoints](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/reference-entrypoints/)
- [Verify release provenance](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/release-provenance-verification/)
- [AI policy authoring](https://eugenemalaschuk-source.github.io/arch-linter-net/ai/)

The public capability references are checked against runtime/schema/CLI inventories by `make lint-docs`, so a new executable capability cannot silently leave the main documentation matrix stale.

Internal project documentation remains in repository Markdown files such as `docs/internal/`, `openspec/`, `.github/`, and root governance files. It is not part of the published product site.

## Local documentation workflow

```bash
make venv        # create Python virtual environment
make docs-serve  # preview MkDocs locally
make docs-build  # build the static documentation site
make fmt-docs    # auto-format markdown documentation
make lint-docs   # strict structure + semantic documentation validation
```

Generated `site/` output is a build artifact and should not be committed.

## Architecture-policy badge

Project an existing strict JSON result to a Shields endpoint payload without rerunning analysis:

```bash
arch-linter-net badge architecture-policy --input architecture-strict.json
```

## Project health and assurance

<details>
<summary>Security, maintainability, and supply-chain status</summary>

<p>
  <a href="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/codeql.yml/badge.svg"></a>
  <a href="https://scorecard.dev/viewer/?uri=github.com/eugenemalaschuk-source/arch-linter-net"><img alt="OpenSSF Scorecard" src="https://api.scorecard.dev/projects/github.com/eugenemalaschuk-source/arch-linter-net/badge"></a>
  <a href="https://www.bestpractices.dev/en/projects/13572/passing"><img alt="OpenSSF Best Practices" src="https://www.bestpractices.dev/projects/13572/badge"></a>
  <a href="https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main"><img alt="Sonar Quality Gate" src="https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=alert_status&branch=main"></a>
  <a href="https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main"><img alt="Sonar Maintainability" src="https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=sqale_rating&branch=main"></a>
  <a href="https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main"><img alt="Sonar Reliability" src="https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=reliability_rating&branch=main"></a>
  <a href="https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main"><img alt="Sonar Security" src="https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=security_rating&branch=main"></a>
</p>

The CI badge tracks the central CI workflow. The Architecture policy badge is a separate dynamic status for the latest `main` run of strict ArchLinterNet self-policy validation; it does not claim test coverage or architecture coverage. SonarCloud also analyzes trusted pull requests, decorates the PR, and evaluates the quality gate on new code rather than forcing the entire historical codebase to be clean before the PR can merge:

| Quality signal | Source |
|---|---|
| Build/test | `make acceptance` (lint + all tests) |
| Test coverage (line %) | CI runs `make test-coverage`, uploads Cobertura XML to Codecov, and the primary coverage badge updates dynamically from Codecov |
| Architecture policy | The Architecture policy badge tracks the latest `main` run of `make lint-architecture`; it proves the repository's strict, read-only self-policy passed, not a coverage percentage |
| SonarCloud PR quality gate | trusted `pull_request` runs analyze new code, publish a SonarCloud PR result link, and fail CI when the Sonar quality gate fails |
| SonarCloud main quality signals | the Sonar badges track the `main` branch project status for quality gate, maintainability, reliability, and security |
| OpenSSF Scorecard | trusted pull requests produce reviewable SARIF; default-branch and scheduled runs publish the supply-chain score to the public Scorecard API and GitHub code scanning |
| Architecture validation | strict ArchLinterNet self-policy check (`architecture/dependencies.arch.yml`), including the reviewed public API snapshots under `architecture/api/`; read-only, never rewrites either |
| Architecture coverage | strict/audit coverage JSON artifacts + Markdown report + sticky PR comment |

See [CI integration](docs/guides/ci-integration.md#architecture-policy-badge) for how the strict-policy badge, test coverage upload, SonarCloud PR analysis, and the separate architecture coverage gate fit together.

</details>

## NuGet and repository links

NuGet packages should expose only public user-facing links:

- project/documentation URL: the GitHub Pages MkDocs site;
- repository URL: this GitHub repository;
- package README: this concise product README;
- license: repository license expression.

NuGet metadata must not point users to internal backlog governance, OpenSpec archives, or maintenance-agent instructions as product documentation.

## Security

Report suspected vulnerabilities privately through GitHub Private Vulnerability Reporting. Do not disclose unresolved vulnerabilities in public issues, pull requests, or discussions. See the [security policy](SECURITY.md) for supported preview releases, reporting guidance, and disclosure expectations.

## License

MIT.
