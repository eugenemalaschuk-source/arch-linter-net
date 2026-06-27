using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CoverageContractReservedTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-coverage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
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

    private const string CoveragePolicyTemplate = """
        version: 1
        name: Test

        layers:
          core:
            namespace: ArchLinterNet.Core

        analysis:
          target_assemblies: [ArchLinterNet.Core]

        contracts:
          {0}:
            - name: domain-namespace-coverage
              scope: namespace
              roots:
                - namespace: ArchLinterNet.Core
              reason: coverage test
        """;

    [Test]
    public void StrictCoverageContract_ThrowsInsteadOfBeingSilentlyDropped()
    {
        string policyPath = WritePolicy(string.Format(CoveragePolicyTemplate, "strict_coverage"));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("coverage contract"));
    }

    [Test]
    public void AuditCoverageContract_ThrowsInsteadOfBeingSilentlyDropped()
    {
        string policyPath = WritePolicy(string.Format(CoveragePolicyTemplate, "audit_coverage"));

        Assert.Throws<InvalidOperationException>(() => ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit"
        }));
    }

    [Test]
    public void PolicyWithoutCoverageContracts_IsUnaffected()
    {
        const string Yaml = """
            version: 1
            name: Test

            layers:
              core:
                namespace: ArchLinterNet.Core

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts: {}
            """;
        string policyPath = WritePolicy(Yaml);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
    }

    [Test]
    public void InvalidCoverageSeverityValue_ThrowsEvenWithoutCoverageContracts()
    {
        const string Yaml = """
            version: 1
            name: Test

            layers:
              core:
                namespace: ArchLinterNet.Core

            analysis:
              target_assemblies: [ArchLinterNet.Core]
              coverage: nonsense

            contracts: {}
            """;
        string policyPath = WritePolicy(Yaml);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("analysis.coverage"));
    }
}
