# Getting Started

This guide shows the shortest path from a .NET repository to an executable architecture policy.

## 1. Install or run the tool

During ArchLinterNet development, run the CLI from source:

```bash
dotnet run --project src/ArchLinterNet.Cli -- --policy architecture/arch.yml --mode strict
```

After the .NET tool is installed from NuGet.org, run:

```bash
arch-linter-net --policy architecture/arch.yml --mode strict
```

See [Installation](../installation/index.md) for global tool, local tool, and package usage.

## 2. Create a policy

Create one root policy. This guide uses the recommended concise path
`architecture/arch.yml`, but the selected filename is configurable and has no
runtime semantics. Start with a small rule that maps to real namespaces and
passes or fails for a known reason.

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
      layers:
        - infrastructure
        - application
        - domain
      reason: Dependencies must point inward toward the domain.
```

See [First policy](first-policy.md) for a walkthrough.

## 3. Choose strict or audit

Use **strict** contracts for boundaries that should block CI today.

Use **audit** contracts for migration discovery, future-state architecture, and known debt that should be visible before it becomes a gate.

```bash
arch-linter-net --mode strict
arch-linter-net --mode audit
```

## 4. Add CI

A common CI setup runs strict validation as a blocking step and audit validation as a non-blocking artifact:

```yaml
- name: Validate architecture (strict)
  run: arch-linter-net --mode strict

- name: Architecture audit report
  if: always()
  continue-on-error: true
  run: arch-linter-net --mode audit --json > architecture-audit.json
```

See [CI integration](../guides/ci-integration.md) for full workflows.

## 5. Handle existing debt

When adopting ArchLinterNet in an existing repository, generate a baseline for current violations instead of weakening the rule:

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output architecture/baseline.arch.yml \
  --reason "Initial migration baseline"
```

Then validate with:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --baseline architecture/baseline.arch.yml --mode strict
```

See [Migration baselines](../guides/migration-baselines.md) for the lifecycle.

## Next steps

- [Policy format](../policy-format/index.md)
- [Contract families](../contracts/index.md)
- [Supported capabilities and non-goals](../policy-format/supported-capabilities.md)
- [AI policy authoring](../ai/index.md)
