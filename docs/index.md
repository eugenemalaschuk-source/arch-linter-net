# ArchLinterNet

Declarative architecture contracts and dependency linting for .NET repositories.

ArchLinterNet is a small, CI-friendly architecture governance tool for teams that want
their architecture boundaries to live in a repository policy file instead of being
scattered across hardcoded test rules.

## Why ArchLinterNet?

Architecture testing tools often express rules directly in C#:

```csharp
Types().That().ResideInNamespace("MyApp.Application")
    .Should().NotDependOnAny("MyApp.Infrastructure");
```

That works, but has drawbacks:

- Architecture policy becomes code, not a simple repository contract
- Rules are harder for humans and AI agents to inspect quickly
- Multiple repositories duplicate slightly different test helpers
- Strict rules, audit rules, migration baselines, and CI output need extra infrastructure

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

The YAML policy file is the source of truth. Test projects, CLI wrappers, and CI steps
are execution adapters.

## Features

- YAML policy loading and validation
- Target assembly resolution (loaded assemblies, `Assembly.Load`, probe paths)
- Namespace and assembly dependency checks
- Ordered layer contracts
- Allow-only whitelist contracts
- Dependency cycle detection
- Independence contracts between layers/modules
- Strict and audit rule groups
- Frozen-debt `ignored_violations` baseline
- Method-body forbidden API checks
- Optional Unity `.asmdef` validation
- Human-readable diagnostics
- JSON output for CI artifacts
- CLI and test-runner integration

## Package layout

```
src/
  ArchLinterNet.Core/     — model, YAML loading, assembly resolution
  ArchLinterNet.Cli/      — .NET global/local tool CLI
  ArchLinterNet.Testing/  — test framework adapters
  ArchLinterNet.Unity/    — Unity .asmdef validation (optional)
tests/
  ArchLinterNet.Core.Tests/
  ArchLinterNet.Cli.Tests/
  ArchLinterNet.Unity.Tests/
```
