# Getting Started

This guide follows the normal adoption path from an existing .NET repository to a blocking architecture gate. It covers the minimal path; the [complete single-tool workflow](../guides/single-tool-workflow.md) continues through topology, structured waivers, metrics, external evidence, Architecture Health, PR Markdown, and the Health badge.

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

New v0.8 policies should prefer `version: 2`, which defaults manual waivers to strict lifecycle governance. Existing `version: 1` policies remain supported with compatibility waiver defaults.

```yaml
version: 2
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

A policy may instead declare explicit projects or target assemblies. See [Policy format](../policy-format/index.md) and [Structured waivers](../policy-format/structured-waivers.md).

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

`--ensure-built` builds the selected project graph once, records and verifies the build receipt, and then validates. It is never implicit. Use `--no-restore` when CI must fail closed instead of restoring.

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

A baseline records reviewed finding debt. A manual policy exception is separate [structured waiver](../policy-format/structured-waivers.md) debt; do not weaken the target architecture merely to make the current repository green.

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

## 8. Continue through the complete governance cycle

The minimal strict gate is only the starting point. The complete v0.8 product path adds, where the repository needs them:

- native declared topology with capture/diff/verify;
- recursive contract-surface and version-isolation governance;
- structured waiver lifecycle and effective policy inventory;
- policy weakening and base/current architecture change evidence;
- measure-first metrics and absolute or baseline-relative budgets;
- current repository/revision/scope binding for repository-local SARIF;
- canonical Architecture Health;
- CLI-rendered PR Markdown and the real Health badge.

Follow the [complete single-tool workflow](../guides/single-tool-workflow.md). Existing v0.7/v1 adopters should use the focused [v0.7 to v0.8 guide](../guides/v07-to-v08-adoption.md).

For a worked adoption against a real repository, continue with [Real-repository workflow](../guides/real-repository-workflow.md).
