using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PolicyConsistencyPipelineTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-pc-test-{Guid.NewGuid():N}");
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

    private const string DuplicateIdPolicyTemplate = """
        version: 1
        name: Test

        layers:
          core:
            namespace: ArchLinterNet.Core
          contracts:
            namespace: ArchLinterNet.Core.Contracts

        analysis:
          target_assemblies:
            - ArchLinterNet.Core
          {0}

        contracts:
          strict:
            - id: dup-id
              name: core-no-forbidden
              source: core
              forbidden: []
              reason: First.
          audit:
            - id: dup-id
              name: contracts-no-forbidden
              source: contracts
              forbidden: []
              reason: Second, duplicate ID.
        """;

    [Test]
    public void DefaultSeverity_FailsValidation()
    {
        string policyPath = WritePolicy(string.Format(DuplicateIdPolicyTemplate, string.Empty));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.PolicyConsistencyFindings, Is.Not.Empty);
        Assert.That(outcome.PolicyConsistencyConfig, Is.EqualTo("error"));
    }

    [Test]
    public void WarnSeverity_ReportsWithoutFailing()
    {
        string policyPath = WritePolicy(
            string.Format(DuplicateIdPolicyTemplate, "policy_consistency: warn"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.PolicyConsistencyFindings, Is.Not.Empty);
    }

    [Test]
    public void OffSeverity_SuppressesFindingsAndDoesNotAffectPassed()
    {
        string policyPath = WritePolicy(
            string.Format(DuplicateIdPolicyTemplate, "policy_consistency: off"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.PolicyConsistencyFindings, Is.Empty);
    }

    [Test]
    public void InvalidSeverityValue_Throws()
    {
        string policyPath = WritePolicy(
            string.Format(DuplicateIdPolicyTemplate, "policy_consistency: nonsense"));

        Assert.Throws<InvalidOperationException>(() => ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        }));
    }

    [Test]
    public void GreenPolicy_PassesUnaffected()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            layers:
              core:
                namespace: ArchLinterNet.Core

            analysis:
              target_assemblies:
                - ArchLinterNet.Core

            contracts:
              strict:
                - id: core-no-forbidden
                  name: core-has-no-forbidden-dependencies
                  source: core
                  forbidden: []
                  reason: No contradictions.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.PolicyConsistencyFindings, Is.Empty);
    }
}
