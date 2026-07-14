using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Proves the cel-must-not-depend-on-* contract family fires on an intentional reverse dependency.
/// Uses ArchLinterNet.Testing as a stand-in for "a CEL assembly that references Core",
/// since Testing is a real in-process assembly that directly references Core.
/// </summary>
[TestFixture]
public sealed class CelBoundaryArchitectureTests
{
    private string _tempDir = null!;

    private static readonly string _testingName =
        typeof(ArchLinterNet.Testing.ArchitectureAssertions).Assembly.GetName().Name!;

    private static readonly string _coreName =
        typeof(ArchLinterNet.Core.Contracts.ArchitectureContractDocument).Assembly.GetName().Name!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cel-boundary-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    [Test]
    public void CelMustNotDependOnCore_ReverseReference_ProducesViolation()
    {
        // ArchLinterNet.Testing maps to the "cel" layer here because it is a real in-process
        // assembly that directly references ArchLinterNet.Core — exactly what the
        // cel-must-not-depend-on-core contract is designed to catch.
        string policyPath = WritePolicy($"""
            version: 1
            name: CEL boundary test
            layers:
              cel:
                namespace: ArchLinterNet.Testing
              core:
                namespace: ArchLinterNet.Core
            analysis:
              target_assemblies:
                - {_testingName}
                - {_coreName}
            contracts:
              strict:
                - name: cel-must-not-depend-on-core
                  id: cel-must-not-depend-on-core
                  source: cel
                  forbidden: [core]
                  reason: CEL must not depend on Core.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
        });

        Assert.That(outcome.Passed, Is.False,
            "A reverse-dependency from the CEL layer to Core must fail strict validation.");
        Assert.That(outcome.Violations, Is.Not.Empty,
            "At least one violation must be reported for the cel-must-not-depend-on-core contract.");
        Assert.That(
            outcome.Violations.All(v => v.ContractId == "cel-must-not-depend-on-core"),
            Is.True,
            "All violations must be attributed to the cel-must-not-depend-on-core contract.");
    }

    [Test]
    public void CelMustNotDependOnCore_NoReference_Passes()
    {
        // ArchLinterNet.Core itself does not reference ArchLinterNet.Testing,
        // so mapping Core to "cel" and Testing to "core" yields a clean result.
        string policyPath = WritePolicy($"""
            version: 1
            name: CEL boundary clean test
            layers:
              cel:
                namespace: ArchLinterNet.Core
              core:
                namespace: ArchLinterNet.Testing
            analysis:
              target_assemblies:
                - {_coreName}
                - {_testingName}
            contracts:
              strict:
                - name: cel-must-not-depend-on-core
                  id: cel-must-not-depend-on-core
                  source: cel
                  forbidden: [core]
                  reason: CEL must not depend on Core.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
        });

        Assert.That(outcome.Violations.Where(v => v.ContractId == "cel-must-not-depend-on-core"), Is.Empty,
            "When CEL does not reference Core the contract must produce no violations.");
    }
}
