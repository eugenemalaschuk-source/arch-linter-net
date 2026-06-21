# Test Adapter

Use `ArchLinterNet.Testing` when architecture validation should run from a .NET test project instead of only from the CLI.

## Install

```bash
dotnet add package ArchLinterNet.Testing
```

## NUnit example

```csharp
using ArchLinterNet.Testing;
using NUnit.Framework;

[TestFixture]
public sealed class ArchitectureTests
{
    [Test]
    public void StrictArchitectureContractsMustPass()
    {
        ArchitectureAssertions
            .FromPolicy("architecture/dependencies.arch.yml")
            .ValidateStrict()
            .ShouldPass();
    }

    [Test]
    public void AuditArchitectureContractsMustPassWhenTeamMakesThemBlocking()
    {
        ArchitectureAssertions
            .FromPolicy("architecture/dependencies.arch.yml")
            .ValidateAudit()
            .ShouldPass();
    }
}
```

## When to use tests vs CLI

Use the CLI when you want a simple CI step, JSON artifacts, baseline generation, or contract selection from command-line options.

Use the test adapter when architecture validation belongs in the repository's normal test suite and should be visible as test results.

## Keep adapters thin

The policy file should remain the source of truth. Test projects should load the policy, execute strict or audit validation, and fail with diagnostics. Avoid duplicating architecture rules in C# test helper code.

## Parity note

The CLI is the primary execution surface for user workflows such as baseline generation, JSON output, timings, and condition-set selection. If a test adapter lacks a CLI capability, prefer the CLI for that workflow or add a separate backlog task for API parity.
