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

    private const string FeatureRoot = "ArchLinterNet.Core.Tests.NamespaceCoverageFixtures.Features";

    private string WriteNamespaceCoveragePolicy(string coverageGroup, string? analysisCoverage = null)
    {
        string assemblyName = typeof(CoverageContractReservedTests).Assembly.GetName().Name!;
        string coverageSetting = analysisCoverage is null
            ? string.Empty
            : $"  coverage: {analysisCoverage}{Environment.NewLine}";

        return WritePolicy(
            $"version: 1{Environment.NewLine}" +
            $"name: Test{Environment.NewLine}{Environment.NewLine}" +
            $"layers:{Environment.NewLine}" +
            $"  audio:{Environment.NewLine}" +
            $"    namespace: {FeatureRoot}.Audio{Environment.NewLine}" +
            $"  feature_api:{Environment.NewLine}" +
            $"    namespace: {FeatureRoot}.*{Environment.NewLine}" +
            $"    namespace_suffix: Api{Environment.NewLine}{Environment.NewLine}" +
            $"analysis:{Environment.NewLine}" +
            $"  target_assemblies: [{assemblyName}]{Environment.NewLine}" +
            coverageSetting +
            $"contracts:{Environment.NewLine}" +
            $"  strict_layer_templates:{Environment.NewLine}" +
            $"    - name: billing-template{Environment.NewLine}" +
            $"      containers: [{FeatureRoot}.Billing]{Environment.NewLine}" +
            $"      layers:{Environment.NewLine}" +
            $"        - name: Contracts{Environment.NewLine}" +
            $"      reason: Template coverage.{Environment.NewLine}" +
            $"  {coverageGroup}:{Environment.NewLine}" +
            $"    - id: namespace-feature-coverage{Environment.NewLine}" +
            $"      name: namespace-feature-coverage{Environment.NewLine}" +
            $"      scope: namespace{Environment.NewLine}" +
            $"      roots:{Environment.NewLine}" +
            $"        - namespace: {FeatureRoot}{Environment.NewLine}" +
            $"      exclude:{Environment.NewLine}" +
            $"        - namespace_suffix: Generated{Environment.NewLine}" +
            $"          reason: Generated namespaces are excluded from manual architecture coverage.{Environment.NewLine}" +
            $"      reason: Feature namespaces must be mapped or explicitly excluded.{Environment.NewLine}");
    }

    [Test]
    public void StrictNamespaceCoverage_DefaultSeverity_FailsAndReportsFindings()
    {
        string policyPath = WriteNamespaceCoveragePolicy("strict_coverage");

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.CoverageConfig, Is.EqualTo("error"));
        Assert.That(outcome.CoverageFindings.Select(f => f.SourceType), Is.EqualTo(new[]
        {
            $"{FeatureRoot}.AlphaGap",
            $"{FeatureRoot}.ZetaGap"
        }));
        Assert.That(outcome.CoverageFindings.All(f => f.ContractId == "namespace-feature-coverage"), Is.True);
    }

    [Test]
    public void AuditNamespaceCoverage_WarnSeverity_ReportsWithoutFailing()
    {
        string policyPath = WriteNamespaceCoveragePolicy("audit_coverage", analysisCoverage: "warn");

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.CoverageConfig, Is.EqualTo("warn"));
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(2));
    }

    [Test]
    public void NamespaceCoverage_OffSeverity_SuppressesFindings()
    {
        string policyPath = WriteNamespaceCoveragePolicy("strict_coverage", analysisCoverage: "off");

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.CoverageConfig, Is.EqualTo("off"));
        Assert.That(outcome.CoverageFindings, Is.Empty);
    }

    [Test]
    public void UnsupportedCoverageScope_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: dependency-edge-coverage
                  scope: dependency_edge
                  between:
                    - [a, b]
                  reason: Reserved for a later issue.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain(
            "Only coverage contracts with scope 'namespace', 'rule_input', 'project', or 'assembly' are implemented"));
    }

    [Test]
    public void NamespaceCoverageRoot_WithoutNamespace_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: namespace-coverage
                  scope: namespace
                  roots:
                    - include: ["src/**/*.cs"]
                  reason: Invalid namespace coverage root.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("without a non-empty namespace"));
    }

    [Test]
    public void NamespaceCoverageRoot_WithIncludeExcludeFields_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: namespace-coverage
                  scope: namespace
                  roots:
                    - namespace: ArchLinterNet.Core
                      include: ["src/**/*.cs"]
                  reason: Invalid namespace coverage root.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("include/exclude discovery fields"));
    }

    [Test]
    public void NamespaceCoverageExclusion_WithoutReason_ThrowsActionableError()
    {
        string assemblyName = typeof(CoverageContractReservedTests).Assembly.GetName().Name!;
        string policyPath = WritePolicy(
            $"version: 1{Environment.NewLine}" +
            $"name: Test{Environment.NewLine}{Environment.NewLine}" +
            $"analysis:{Environment.NewLine}" +
            $"  target_assemblies: [{assemblyName}]{Environment.NewLine}" +
            $"contracts:{Environment.NewLine}" +
            $"  strict_coverage:{Environment.NewLine}" +
            $"    - name: namespace-coverage{Environment.NewLine}" +
            $"      scope: namespace{Environment.NewLine}" +
            $"      roots:{Environment.NewLine}" +
            $"        - namespace: {FeatureRoot}{Environment.NewLine}" +
            $"      exclude:{Environment.NewLine}" +
            $"        - namespace_suffix: Generated{Environment.NewLine}" +
            $"      reason: Invalid exclusion.{Environment.NewLine}");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("without a non-empty reason"));
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
    public void ProjectCoverage_WithoutSolutionOrProjectsConfigured_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: project-coverage
                  scope: project
                  reason: Every discovered project must be mapped or excluded.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("requires 'analysis.solution' or 'analysis.projects'"));
    }

    [Test]
    public void AssemblyCoverage_WithoutSolutionConfigured_DoesNotThrow()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: assembly-coverage
                  scope: assembly
                  reason: Every first-party assembly must be mapped or excluded.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.CoverageFindings.All(f => f.ForbiddenNamespace == "uncovered assembly"), Is.True);
    }

    [Test]
    public void ProjectCoverageExclusion_WithoutProjectMatcher_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]
              projects: ["src/ArchLinterNet.Core/ArchLinterNet.Core.csproj"]

            contracts:
              strict_coverage:
                - name: project-coverage
                  scope: project
                  exclude:
                    - reason: Missing matcher.
                  reason: Every discovered project must be mapped or excluded.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("'project' matcher"));
    }

    [Test]
    public void AssemblyCoverageExclusion_WithoutReason_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: assembly-coverage
                  scope: assembly
                  exclude:
                    - assembly: SomeAssembly
                  reason: Every first-party assembly must be mapped or excluded.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("without a non-empty reason"));
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
