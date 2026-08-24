# Getting Started

This guide follows the normal adoption path from an existing .NET repository to a blocking architecture gate.

## 1. Install the CLI

For repository and CI use, prefer a local .NET tool manifest:

```bash
dotnet new tool-manifest
dotnet tool install ArchLinterNet.Cli
dotnet tool restore
```

Then invoke the pinned repository tool with:

```bash
dotnet arch-linter-net --help
```

A global install is also supported for interactive use. See [Installation](../installation/index.md).

## 2. Create a minimal root policy

`architecture/arch.yml` is the recommended concise convention in these guides. The filename itself has no runtime semantics. The CLI's compatibility default remains `architecture/dependencies.arch.yml`, so pass `--policy` when using another path.

```yaml
version: 1
name: My Architecture Contract

layers:
  application:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
  infrastructure:
    namespace: MyApp.Infrastructure

analysis:
  solution: MyApp.sln

contracts:
  strict:
    - id: application-not-infrastructure
      name: application-must-not-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      reason: Application code depends on abstractions, not concrete infrastructure.

  strict_layers:
    - id: clean-architecture-layering
      name: clean-architecture-layering
      layers: [infrastructure, application, domain]
      reason: Dependencies point inward.
```

A policy may instead declare explicit projects or target assemblies. See [Policy format](../policy-format/index.md).

## 3. Check the policy before analyzing code

```bash
dotnet arch-linter-net policy check \
  --policy architecture/arch.yml
```

`policy check` validates syntax, imports, composition, contract references, and static configuration. Architecture checks that need project, assembly, or source facts are reported as deferred; a successful policy check is not an architecture-compliance result.

## 4. Produce trustworthy build inputs

ArchLinterNet validates compiled architecture facts. You can build normally:

```bash
dotnet build MyApp.sln
dotnet arch-linter-net --policy architecture/arch.yml --mode strict
```

or opt in to the build-state workflow:

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built
```

`--ensure-built` builds the selected project graph once, records/verifies the build receipt, and then validates. It is never implicit. Use `--no-restore` when CI must fail closed instead of restoring.

## 5. Make strict validation the CI gate

A minimal job is:

```yaml
- name: Restore local tools
  run: dotnet tool restore

- name: Validate architecture
  run: dotnet arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

Use audit contracts for discovery and future-state governance that should be visible without blocking merges.

For JSON/SARIF reports, additional sinks, caching, and build selectors, see [CLI reference](../cli/index.md) and [CI integration](../guides/ci-integration.md).

## 6. Baseline existing debt instead of weakening policy

If a strict rule describes the desired boundary but the repository already violates it, capture the current violations:

```bash
dotnet arch-linter-net baseline generate \
  --config architecture/arch.yml \
  --output architecture/baseline.arch.yml \
  --reason "Initial migration baseline"
```

Then validate with the baseline:

```bash
dotnet arch-linter-net \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode strict
```

Review baseline entries like code. Use `baseline update`, `prune`, `diff`, `verify`, and `migrate` for its lifecycle. See [Migration baselines](../guides/migration-baselines.md).

## 7. Add architecture coverage

A dependency rule can pass while new architecture falls outside every rule. Coverage contracts detect that drift.

Implemented coverage scopes are:

- `namespace`
- `project`
- `assembly`
- `dependency_edge`
- `rule_input`
- `semantic_role`

Example:

```yaml
contracts:
  strict_coverage:
    - id: first-party-namespace-coverage
      name: first-party-namespace-coverage
      scope: namespace
      roots:
        - namespace: MyApp
      reason: Every first-party namespace must be mapped or explicitly excluded.
```

See [Coverage contracts](../contracts/coverage.md).

## 8. Add advanced governance only where needed

Common next steps are:

- [project/package/framework governance](../contracts/package-dependencies.md);
- [public API surface governance](../contracts/public-api-surface.md);
- [semantic classification](../policy-format/semantic-classification.md), contextual rules, and [port boundaries](../contracts/port-boundary.md);
- reusable `source_sets` for repeated project/assembly/layer selectors;
- `policy context` + `policy weakening` for policy-change review;
- `change snapshot` + `change report` and `gate` for CI change governance;
- `graph`, `explain`, and `history analyze` for investigation.

For a worked adoption against a real repository, continue with [Real-repository workflow](../guides/real-repository-workflow.md).
