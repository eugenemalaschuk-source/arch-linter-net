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

The CLI is the primary execution surface for user workflows such as baseline generation, JSON output, timings, and condition-set selection. The Testing API shares the policy, normalized finding, baseline, cache, profile, bounded-parallelism, and cancellation semantics that it exposes; callers retain explicit snapshot ownership through `CreateSnapshot()`.

Use `WithBaseline(path).VerifyBaseline()` for a read-only CI gate. Keep
baseline/API capture and update actions in a reviewed local workflow; test code
must not automatically approve or rewrite them. See
[Adopt or Upgrade ArchLinterNet](../guides/upgrading.md#solution-shapes).

For the combined new-debt gate, keep the same explicit baseline and optionally
provide policy contexts exported from independently prepared base/current states:

```csharp
ArchitectureDebtGateOutcome gate = ArchitectureAssertions
    .FromPolicy("architecture/dependencies.arch.yml")
    .WithBaseline("architecture/baseline.arch.yml")
    .WithPolicyWeakeningContexts("base-policy-context.json", "current-policy-context.json")
    .EvaluateDebtGate();

Assert.That(gate.Passed, Is.True);
```

`gate.PersistentDebt` exposes exact lifecycle entries and `gate.PolicyWeakening`
exposes the independent change-time records. A policy warning or
`impact_not_proven` record is not a fake baseline entry; only error-severity
weakening contributes to the final gate decision.
