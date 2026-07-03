using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblyIndependenceStrictAuditTests
{
    private string _tempDir = null!;
    private string _testingAssemblyName = null!;
    private string _coreAssemblyName = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-assembly-independence-strict-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // ArchLinterNet.Testing directly references ArchLinterNet.Core, so this pair is a real
        // direct-reference violation regardless of which mode evaluates it.
        _testingAssemblyName = typeof(ArchLinterNet.Testing.ArchitectureAssertions).Assembly.GetName().Name!;
        _coreAssemblyName = typeof(ArchitectureContractDocument).Assembly.GetName().Name!;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    [Test]
    public void StrictAssemblyIndependence_DirectReference_FailsValidation()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{_testingAssemblyName}, {_coreAssemblyName}]
            contracts:
              strict_assembly_independence:
                - name: assembly-independence
                  assemblies: [{_testingAssemblyName}, {_coreAssemblyName}]
                  reason: Test.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations, Has.Count.EqualTo(1));
    }

    [Test]
    public void AuditAssemblyIndependence_DirectReference_ReportsWithoutFailingStrictValidation()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{_testingAssemblyName}, {_coreAssemblyName}]
            contracts:
              audit_assembly_independence:
                - name: assembly-independence
                  assemblies: [{_testingAssemblyName}, {_coreAssemblyName}]
                  reason: Test.
            """);

        ValidationOutcome strictOutcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
        });

        ValidationOutcome auditOutcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit",
        });

        Assert.That(strictOutcome.Passed, Is.True,
            "An audit_assembly_independence contract must not be evaluated (and therefore cannot fail) under strict mode.");
        Assert.That(strictOutcome.Violations, Is.Empty);

        Assert.That(auditOutcome.Violations, Has.Count.EqualTo(1),
            "The same contract must be evaluated and its violation reported under audit mode.");
    }
}
