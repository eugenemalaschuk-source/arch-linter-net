<p align="center">
  <img src="docs/assets/logo.png" alt="ArchLinterNet" width="420">
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/ArchLinterNet.Cli/"><img alt="NuGet version" src="https://img.shields.io/nuget/v/ArchLinterNet.Cli.svg"></a>
  <a href="https://www.nuget.org/packages/ArchLinterNet.Cli/"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/ArchLinterNet.Cli"></a>
  <a href="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/main-quality.yml"><img alt="Main quality" src="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/main-quality.yml/badge.svg?branch=main"></a>
  <a href="https://raw.githubusercontent.com/eugenemalaschuk-source/arch-linter-net/architecture-health-badge/architecture-health-publication.json"><img alt="Architecture Health" src="https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Feugenemalaschuk-source%2Farch-linter-net%2Farchitecture-health-badge%2Farchitecture-health.json"></a>
  <a href="https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main"><img alt="Sonar Quality Gate" src="https://sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net&metric=alert_status&branch=main"></a>
  <a href="https://app.codecov.io/github/eugenemalaschuk-source/arch-linter-net"><img alt="Test coverage" src="https://codecov.io/github/eugenemalaschuk-source/arch-linter-net/graph/badge.svg?branch=main"></a>
  <a href="https://eugenemalaschuk-source.github.io/arch-linter-net/"><img alt="Documentation" src="https://img.shields.io/badge/docs-GitHub%20Pages-blue"></a>
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-blue.svg"></a>
</p>

YAML-first architecture governance for .NET repositories.

ArchLinterNet turns architecture decisions into executable, reviewable contracts. It governs namespace and assembly boundaries, project/package metadata, semantic roles and contexts, public API surfaces, architecture coverage, migration debt, and CI change gates — with deterministic diagnostics that humans and automation can consume.

**One packed ArchLinterNet CLI covers the complete static architecture-governance cycle:** declare and check policy, prove applicability, validate topology and visible contract surfaces, govern finding and waiver debt, measure budgets, bind repository-local SARIF, compare change, project Architecture Health, and render PR Markdown and the real Health badge. CI invokes and transports the resulting canonical artifacts; it is not a second governance implementation.

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

Create a root policy. This quick start uses the recommended concise path `architecture/arch.yml`; the filename is configurable and has no runtime semantics. New v0.8 policy authoring should prefer `version: 2`, which defaults manual waivers to strict lifecycle governance; existing `version: 1` policies remain supported with compatibility defaults.

```yaml
version: 2
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
- Native declared topology with capture/diff/verify, recursive contract-surface exposure, architecture metrics, absolute/baseline-relative budgets, and repository-local external SARIF evidence.
- Project/solution discovery, explicit build-state preflight, opt-in `--ensure-built`, condition sets, persistent analysis cache, and bounded parallel scanning.
- Strict gates, audit discovery, migration baselines, structured waiver lifecycle, effective policy inventory, policy consistency, policy-context export, and policy-weakening review.
- Architecture change snapshots/reports, history forensics, dependency graphs/path explanation, Architecture Health, deterministic PR Markdown, Health and legacy architecture-policy badge projections, and public API lifecycle commands.
- Human, normalized JSON, SARIF where applicable, repeatable report sinks, timings, analysis profiles, and CI-oriented coverage artifacts.
- CEL-backed `when` predicates at documented closed locations — standard CEL under a safe profile, not an open-ended scripting language.

ArchLinterNet does **not** validate runtime dependency-injection behavior, authorization/security correctness, code ownership, arbitrary semantic data flow, or undocumented custom YAML contract families.

## Documentation

Public product documentation is published through MkDocs and GitHub Pages:

- [Documentation home](https://eugenemalaschuk-source.github.io/arch-linter-net/)
- [Getting started](https://eugenemalaschuk-source.github.io/arch-linter-net/getting-started/)
- [Complete single-tool governance workflow](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/single-tool-workflow/)
- [Installation](https://eugenemalaschuk-source.github.io/arch-linter-net/installation/)
- [CLI reference](https://eugenemalaschuk-source.github.io/arch-linter-net/cli/)
- [Policy format](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/)
- [Structured waivers](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/structured-waivers/)
- [Architecture Health](https://eugenemalaschuk-source.github.io/arch-linter-net/reference/architecture-health/)
- [Contract families](https://eugenemalaschuk-source.github.io/arch-linter-net/contracts/)
- [Coverage contracts](https://eugenemalaschuk-source.github.io/arch-linter-net/contracts/coverage/)
- [Supported capabilities and non-goals](https://eugenemalaschuk-source.github.io/arch-linter-net/policy-format/supported-capabilities/)
- [Real-repository workflow](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/real-repository-workflow/)
- [CI integration](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/ci-integration/)
- [Adopt or upgrade ArchLinterNet](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/upgrading/)
- [Extended governance adoption](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/extended-governance-adoption/)
- [Reference entrypoints](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/reference-entrypoints/)
- [Verify release provenance](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/release-provenance-verification/)
- [AI policy authoring](https://eugenemalaschuk-source.github.io/arch-linter-net/ai/)

The public capability references are checked against runtime/schema/CLI inventories by `make lint-docs`, so a new executable capability cannot silently leave the main documentation matrix stale.

Internal project documentation remains in repository Markdown files such as `docs/internal/`, `openspec/`, `.github/`, and root governance files. It is not part of the published product site.

GitHub Pages is deployed only by the public release workflow. A merge to `main` refreshes quality telemetry and development/dogfood `main.N` packages, but does not deploy MkDocs.

## Local documentation workflow

```bash
make venv        # create Python virtual environment
make docs-serve  # preview MkDocs locally
make docs-build  # build the static documentation site
make fmt-docs    # auto-format markdown documentation
make lint-docs   # strict structure + semantic documentation validation
```

Generated `site/` output is a build artifact and should not be committed.

## Architecture Health badge

Project canonical Architecture Health and canonical policy inventory into a
Shields endpoint payload without rerunning analysis:

```bash
arch-linter-net badge architecture-health \
  --input architecture-health.json \
  --output architecture-health-badge.json
```

The payload headline contains the non-compensating Health category, accumulated
explicit ignore debt, and effective policy-control count. It is not a score,
coverage percentage, test result, or generic workflow status. `UNASSESSABLE · ? ignores · ? rules` explicitly means the required Health/inventory evidence
was not available; it never means zero debt.

`badge architecture-policy` remains available for integrations that need the
older, narrower strict-validation signal:

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

The Main quality badge tracks the latest merged `main` coverage telemetry run. That run refreshes Codecov and SonarCloud for the same merged revision and fails closed if coverage collection, Codecov upload, or the Sonar quality-gate scan cannot complete successfully. The Architecture Health badge is different: a required PR Architecture Coverage job creates the exact ArchLinterNet payload, and a trusted post-merge publisher releases it only after proving the PR head and squash-merged `main` commit have the same Git tree. Missing, stale, or mismatched evidence replaces the stable endpoint with an explicit unassessable badge; it never leaves an old healthy payload represented as current. This publication does not rerun the architecture matrix or deploy MkDocs. SonarCloud also analyzes trusted pull requests, decorates the PR, and evaluates the quality gate on new code before merge:

| Quality signal | Source |
|---|---|
| Build/test | Required pull-request validation runs `make acceptance`-equivalent unit/E2E/packed-artifact gates before merge |
| Test coverage (line %) | PR CI and post-merge `Main Quality Telemetry` collect coverage; the merged-main run uploads Cobertura XML to Codecov so the primary coverage badge follows `main` |
| Architecture Health | Canonical Health, accumulated explicit ignore debt, and effective policy controls from required PR evidence promoted only after exact merged-tree proof; it is not generic CI, a score, or coverage |
| SonarCloud PR quality gate | trusted `pull_request` runs analyze new code, publish a SonarCloud PR result link, and fail CI when the Sonar quality gate fails |
| SonarCloud main quality signals | `Main Quality Telemetry` analyzes the merged revision with OpenCover/TRX/Python coverage and refreshes the `main` quality-gate, maintainability, reliability, and security badges |
| OpenSSF Scorecard | trusted pull requests produce reviewable SARIF; default-branch and scheduled runs publish the supply-chain score to the public Scorecard API and GitHub code scanning |
| Architecture validation | strict ArchLinterNet self-policy check (`architecture/dependencies.arch.yml`), including the reviewed public API snapshots under `architecture/api/`; read-only, never rewrites either |
| Architecture coverage | strict/audit coverage JSON artifacts + Markdown report + sticky PR comment on the required pull-request gate |

See [CI integration](docs/guides/ci-integration.md) for how the PR gate, merged-main test coverage upload, SonarCloud analysis, and the separate architecture coverage gate fit together.

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
