# ArchLinterNet

[![CI](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml/badge.svg)](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml)

The CI badge covers build, test, and the architecture coverage quality gate — see [CI integration](docs/guides/ci-integration.md#baseline-debt-semantics-in-the-coverage-gate) for how the coverage gate and PR comment work.

Declarative architecture contracts and dependency linting for .NET repositories.

ArchLinterNet helps teams keep architecture boundaries executable: define a YAML policy, run it locally or in CI, and catch dependency drift before it becomes hidden design debt.

> Status: early preview. The YAML policy surface is being stabilized for the first public `0.x` NuGet and .NET tool releases.

## Why ArchLinterNet?

Many .NET projects enforce architecture with handwritten test helpers or tribal knowledge. That works, but policy quickly becomes hard to discover, hard to reuse across repositories, and hard for humans or AI agents to review.

ArchLinterNet uses a repository-owned policy file as the source of truth:

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
- Dependency cycle, acyclic sibling, and independence checks.
- Protected surface contracts for importer restrictions.
- External dependency leakage checks for vendor/framework namespaces.
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
