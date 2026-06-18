# ArchLinterNet

Declarative architecture contracts and dependency linting for .NET repositories.

ArchLinterNet is a small, CI-friendly architecture governance tool for teams that want their architecture boundaries to live in a repository policy file instead of being scattered across hardcoded test rules.

It is inspired by tools such as Import Linter, ArchUnit, ArchUnitNET, NetArchTest, jQAssistant, and NDepend, but it is an independent project and is not affiliated with those tools.

> Status: early extraction / preview.
>
> The intended first stable target is a `0.x` NuGet and .NET tool release with a stable YAML policy format.

---

## Documentation

The user-facing documentation is maintained as a [MkDocs](https://www.mkdocs.org/) site
under `docs/`. To build and view locally:

```bash
make venv        # create Python virtual environment (one-time setup)
make docs-serve  # start local preview at http://127.0.0.1:8000
make docs-build  # build static site to site/
```

See the [Getting Started](https://eugenemalaschuk-source.github.io/arch-linter-net/getting-started/)
guide for a quick walkthrough.

---

## Why ArchLinterNet?

Many .NET architecture testing tools are excellent when you want to express rules directly in C# tests:

```csharp
Types().That().ResideInNamespace("MyApp.Application")
    .Should().NotDependOnAny("MyApp.Infrastructure");
```

That works well for many projects, but it has a few drawbacks:

- architecture policy becomes code, not a simple repository contract;
- rules are harder for humans and AI agents to inspect quickly;
- multiple repositories tend to duplicate slightly different test helpers;
- strict rules, audit rules, migration baselines, and CI output need extra infrastructure;
- Unity projects may also need assembly definition (`.asmdef`) boundary checks.

ArchLinterNet focuses on a different workflow:

```text
architecture/dependencies.arch.yml
        ↓
ArchLinterNet CLI / test adapter
        ↓
strict or audit architecture validation
        ↓
human-readable diagnostics + CI artifacts
```

The YAML policy file is the source of truth. Test projects, CLI wrappers, and CI steps are execution adapters.

---

## Goals

ArchLinterNet aims to be:

- **Declarative** — architecture boundaries are described in YAML.
- **.NET-native** — designed for C#/.NET repositories.
- **CI-first** — usable from GitHub Actions, local scripts, and build pipelines.
- **Test-adapter friendly** — can also be run from NUnit, xUnit, or MSTest.
- **Migration-aware** — supports strict gates, audit diagnostics, and frozen-debt baselines.
- **Unity-compatible** — optional support for Unity `.asmdef` dependency boundaries.
- **Small and practical** — not a full enterprise static analysis platform.

---

## Non-goals

ArchLinterNet is not intended to replace:

- full static analysis platforms;
- security analyzers;
- code quality suites;
- dependency visualization products;
- IDE architecture diagram tools.

It is intentionally focused on executable architecture contracts.

---

## Main features

Initial / planned feature set:

- YAML policy loading.
- Target assembly resolution.
- Namespace and assembly dependency checks.
- Ordered layer contracts.
- Allow-only whitelist contracts.
- Dependency cycle detection.
- Independence contracts between layers/modules.
- Strict and audit rule groups.
- Frozen-debt `ignored_violations` baseline.
- Method-body forbidden API checks.
- Optional Unity `.asmdef` validation.
- Human-readable diagnostics.
- JSON output for CI artifacts.
- CLI and test-runner integration.

---

## Package layout

Suggested package split:

```text
ArchLinterNet.Core
ArchLinterNet.Cli
ArchLinterNet.Testing
ArchLinterNet.Unity
```

### `ArchLinterNet.Core`

Core model, YAML loading, assembly resolution, dependency scanning, contract execution, and diagnostics.

### `ArchLinterNet.Cli`

A .NET global/local tool for local and CI validation.

```bash
# Run via dotnet run (development):
dotnet run --project src/ArchLinterNet.Cli -- --policy architecture/dependencies.arch.yml --mode strict

# Run via local tool (after dotnet tool restore):
dotnet arch-linter-net --policy architecture/dependencies.arch.yml --mode audit --format json

# Shortcut flags:
dotnet arch-linter-net --policy architecture/dependencies.arch.yml --strict --json
```

### `ArchLinterNet.Testing`

Thin helpers for using ArchLinterNet from NUnit tests.

```csharp
using NUnit.Framework;
using ArchLinterNet.Testing;

[Test]
public void ArchitectureStrictContractsMustPass()
{
    ArchitectureAssertions
        .FromPolicy("architecture/dependencies.arch.yml")
        .ValidateStrict()
        .ShouldPass();
}
```

### `ArchLinterNet.Unity`

Optional Unity-specific contracts, especially `.asmdef` dependency validation.

---

## Quick start

### 1. Add a policy file

Create:

```text
architecture/dependencies.arch.yml
```

Example:

```yaml
version: 1
name: Example Architecture Contract

layers:
  app:
    namespace: MyCompany.App

  domain:
    namespace: MyCompany.Domain

  infrastructure:
    namespace: MyCompany.Infrastructure

  ui:
    namespace: MyCompany.Ui

analysis:
  target_assemblies:
    - MyCompany.App
    - MyCompany.Domain
    - MyCompany.Infrastructure
    - MyCompany.Ui

contracts:
  strict:
    - name: app-must-not-depend-on-infrastructure
      source: app
      forbidden: [infrastructure]
      reason: Application code must depend on abstractions, not concrete infrastructure.

    - name: domain-must-not-depend-on-ui
      source: domain
      forbidden: [ui]
      reason: Domain code must stay UI-independent.

  strict_layers:
    - name: clean-architecture-layering
      layers:
        - ui
        - app
        - domain
      reason: Dependencies must point inward from UI to application to domain.

  strict_allow_only:
    - name: domain-allowed-dependencies
      source: domain
      allowed: []
      reason: Domain must remain a pure leaf layer.

  strict_cycles:
    - name: main-layer-cycles
      layers:
        - ui
        - app
        - domain
        - infrastructure
      reason: Main architecture layers must not form dependency cycles.

  audit:
    - name: audit-ui-direct-domain-coupling
      source: ui
      forbidden: [domain]
      reason: Discover places where UI bypasses application use cases.

  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
```

### 2. Run strict validation

```bash
dotnet run --project src/ArchLinterNet.Cli -- --mode strict
```

Or if installed as a local tool:

```bash
dotnet arch-linter-net --mode strict
```

Strict validation should be used as the no-new-debt gate in CI. Exit code 0 means all contracts pass. Exit code 1 means violations were found.

### 3. Run audit validation

```bash
dotnet run --project src/ArchLinterNet.Cli -- --mode audit
```

Audit validation is useful during decomposition and migration work. It can expose known debt without blocking every pull request.

---

## Running against samples

A sample clean architecture policy is available at `samples/BasicCleanArchitecture/`:

```bash
# Strict validation (will report missing target assemblies - the sample
# assemblies don't exist, demonstrating configuration diagnostics)
dotnet run --project src/ArchLinterNet.Cli -- \
    --policy samples/BasicCleanArchitecture/architecture/dependencies.arch.yml \
    --mode strict

# Audit validation
dotnet run --project src/ArchLinterNet.Cli -- \
    --policy samples/BasicCleanArchitecture/architecture/dependencies.arch.yml \
    --mode audit
```

Sample NUnit usage:
```csharp
using ArchLinterNet.Testing;

[Test]
public void Architecture_Strict_Contracts_Must_Pass()
{
    ArchitectureAssertions
        .FromPolicy("samples/BasicCleanArchitecture/architecture/dependencies.arch.yml")
        .ValidateStrict()
        .ShouldPass();
}
```

---

## Policy model

### Layers

Layers map short names to namespace roots:

```yaml
layers:
  application:
    namespace: MyCompany.Application

  domain_models:
    namespace: MyCompany.Domain
    namespace_suffix: Models
```

A layer can represent:

- a whole namespace tree;
- a namespace suffix convention;
- a pure contract/model slice;
- a runtime slice;
- a module boundary;
- a third-party namespace boundary.

### Strict contracts

Strict contracts are blocking rules. They should be green in normal CI.

```yaml
contracts:
  strict:
    - name: application-must-not-depend-on-ui
      source: application
      forbidden: [ui]
      reason: Application must remain UI-independent.
```

### Audit contracts

Audit contracts are diagnostic rules. They are useful for future-state architecture, migrations, and debt discovery.

```yaml
contracts:
  audit:
    - name: audit-legacy-runtime-coupling
      source: application
      forbidden: [legacy_runtime]
      reason: Discover application coupling to legacy runtime code.
```

### Allow-only contracts

Allow-only contracts are whitelist-style rules. They are stronger than blacklist rules.

```yaml
contracts:
  strict_allow_only:
    - name: domain-models-allowed-dependencies
      source: domain_models
      allowed: []
      reason: Domain models must not depend on other first-party layers.
```

### Layer contracts

Layer contracts model ordered architecture layers.

```yaml
contracts:
  strict_layers:
    - name: application-internal-layering
      layers:
        - presenters
        - interfaces
        - models
      reason: Dependencies must point inward and never back outward.
```

### Cycle contracts

Cycle contracts detect directed dependency cycles between selected layers.

```yaml
contracts:
  strict_cycles:
    - name: module-cycles
      layers:
        - sales
        - billing
        - inventory
      reason: Business modules must not form dependency cycles.
```

### Ignored violations

Ignored violations are a frozen-debt baseline. They should be narrow, explicit, and issue-linked.

```yaml
ignored_violations:
  - source_type: MyCompany.Application.Legacy.LegacyUseCase
    forbidden_reference: MyCompany.Infrastructure.Legacy.LegacyGateway
    reason: Existing debt tracked in #123. Remove after migration.
```

Ignored violations are not intended to hide new debt.

---

## CLI usage

```
arch-linter-net [options]

Options:
  -p, --policy <path>   Path to YAML contract file
                        (default: architecture/dependencies.arch.yml)
  -m, --mode <mode>     Validation mode: strict or audit (default: strict)
      --strict          Shortcut for --mode strict
      --audit           Shortcut for --mode audit
  -f, --format <fmt>    Output format: human or json (default: human)
      --json            Shortcut for --format json
  -h, --help            Show help message
  -v, --version         Show version

Exit codes:
  0   All contracts passed
  1   One or more contracts failed
  2   Runtime error (invalid arguments, file not found, etc.)
```

## Test adapter API

Use from NUnit tests:

```csharp
[Test]
public void ArchitectureStrictContractsMustPass()
{
    ArchitectureAssertions
        .FromPolicy("architecture/dependencies.arch.yml")
        .ValidateStrict()
        .ShouldPass();
}

[Test]
public void ArchitectureAuditContractsMustPass()
{
    ArchitectureAssertions
        .FromPolicy("architecture/dependencies.arch.yml")
        .ValidateAudit()
        .ShouldPass();
}
```

## CI example

GitHub Actions:

```yaml
name: Architecture

on:
  pull_request:
  push:
    branches: [main]

jobs:
  strict:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet tool restore
      - run: dotnet arch-linter-net --mode strict --format json

  audit:
    runs-on: ubuntu-latest
    continue-on-error: true  # audit diagnostics should not block CI
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet tool restore
      - run: dotnet arch-linter-net --mode audit --format json
```

Using Make:

```makefile
.PHONY: lint-architecture audit-architecture

lint-architecture:
	dotnet run --project src/ArchLinterNet.Cli -- --mode strict

audit-architecture:
	dotnet run --project src/ArchLinterNet.Cli -- --mode audit
```

---

## Unity usage

Unity support should remain optional.

Typical Unity-specific contracts include:

- runtime assemblies must not reference editor assemblies;
- pure core assemblies must not reference Unity runtime assemblies;
- feature assemblies must not reference unrelated feature assemblies;
- `.asmdef` references must follow the same direction as namespace/assembly policy.

Example target shape:

```yaml
contracts:
  strict_asmdef:
    - name: runtime-must-not-reference-editor-assemblies
      source_assemblies:
        - Runtime
      forbidden_editor_refs: true
      reason: Runtime assemblies must not depend on UnityEditor-only code.
```

---

## Output formats

```bash
dotnet arch-linter-net --mode strict --format human
dotnet arch-linter-net --mode strict --format json
dotnet arch-linter-net --mode audit --format json
```

### Human output

For local development and readable CI logs. Example:

```
- [web-must-not-depend-on-infrastructure] MyApp.Web -> MyApp.Infrastructure: MyApp.Web.Services.LegacyService
- [inward-layering] MyApp.Infrastructure -> MyApp.Web: MyApp.Infrastructure.Data.WebContext
```

### JSON output

Single JSON object for CI artifacts, dashboards, and downstream automation:

```json
{
  "passed": false,
  "mode": "strict",
  "violations": [
    {
      "contract": "web-must-not-depend-on-infrastructure",
      "source": "MyApp.Web",
      "forbidden_namespace": "MyApp.Infrastructure",
      "forbidden_references": ["MyApp.Web.Services.LegacyService"]
    }
  ],
  "cycles": ["web -> infrastructure -> web"]
}
```

---

## Roadmap

### `0.1.0-preview`

- Extract core library.
- Publish initial NuGet package.
- Publish initial CLI as a .NET tool.
- Support YAML policy loading.
- Support strict/audit dependency contracts.
- Support allow-only contracts.
- Support layer contracts.
- Support cycle contracts.
- Support text and JSON output.

### `0.2.0-preview`

- Add YAML schema validation.
- Improve diagnostics and source location hints.
- Add test framework adapters.
- Add sample repositories.
- Add GitHub Actions example.
- Add Source Link and symbol packages.

### `0.3.0-preview`

- Add method-body forbidden API contracts.
- Add richer ignored-violation matching.
- Add performance smoke tests for larger solutions.
- Add baseline generation helpers.

### `0.4.0-preview`

- Add optional Unity package.
- Add `.asmdef` dependency validation.
- Add Unity sample project.

### `1.0.0`

- Stabilize YAML policy schema v1.
- Stabilize CLI contract.
- Stabilize package layout.
- Document migration from in-repo architecture test helpers.

---

## Repository structure

Suggested initial structure:

```text
.
├── src
│   ├── ArchLinterNet.Core
│   ├── ArchLinterNet.Cli
│   ├── ArchLinterNet.Testing
│   └── ArchLinterNet.Unity
├── samples
│   ├── BasicCleanArchitecture
│   └── UnityAsmdefBoundaries
├── tests
│   ├── ArchLinterNet.Core.Tests
│   ├── ArchLinterNet.Cli.Tests
│   └── ArchLinterNet.Unity.Tests
├── docs
│   ├── policy-schema.md
│   ├── contracts.md
│   ├── cli.md
│   └── migration-guide.md
├── architecture
│   └── dependencies.arch.yml
├── README.md
└── LICENSE
```

---

## Design principles

- The YAML policy is the source of truth.
- Execution adapters must stay thin.
- Strict rules block new accidental architecture debt.
- Audit rules expose future-state violations without stopping every change.
- Baselines must be narrow, documented, and temporary.
- Diagnostics should be useful for humans and machines.
- The core engine should not depend on Unity.
- Unity support should live behind optional packages.
- The tool should remain small enough to understand and maintain.

---

## License

Recommended license: MIT.

---

## Project status

This repository is intended to host the extraction of an existing internal architecture validation engine into a reusable public .NET tool.

The first milestone is not to become a full architecture analysis suite. The first milestone is to provide a practical, declarative, CI-friendly architecture linter for real .NET repositories.
