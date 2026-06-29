using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
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
                - name: not-yet-implemented-scope-coverage
                  scope: not_yet_implemented
                  reason: Reserved for a later issue.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain(
            "Only coverage contracts with scope 'namespace', 'rule_input', 'project', 'assembly', or 'dependency_edge' are implemented"));
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

    [Test]
    public void AuditAssemblyCoverage_WarnSeverity_ReportsWithoutFailing()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]
              coverage: warn

            contracts:
              audit_coverage:
                - name: assembly-coverage
                  scope: assembly
                  reason: Every first-party assembly must be mapped or excluded.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.CoverageConfig, Is.EqualTo("warn"));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenNamespace, Is.EqualTo("uncovered assembly"));
    }

    [Test]
    public void StrictMode_WithOnlyAuditProjectCoverageDeclared_DoesNotFailStrictGate()
    {
        string projectDir = Path.Combine(_tempDir, "src", "Fixture");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]
              projects: ["src/Fixture/Fixture.csproj"]

            contracts:
              audit_coverage:
                - name: project-coverage
                  scope: project
                  reason: Every discovered project must be mapped or excluded.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.CoverageFindings, Is.Empty);
    }

    private string WriteUnresolvableProjectFixture()
    {
        // A project with a declared target framework but no build output anywhere under bin/ —
        // project discovery will diagnose "missing project build output" and never resolve this
        // project to a target assembly. With no analysis.target_assemblies set, this previously
        // made IArchitectureRunnerSetupService.BuildRunner throw before any coverage contract ran.
        string projectDir = Path.Combine(_tempDir, "src", "Unresolvable");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "Unresolvable.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        return "src/Unresolvable/Unresolvable.csproj";
    }

    [Test]
    public void StrictProjectCoverage_AllProjectsUnresolved_ReportsUnresolvedProjectInsteadOfThrowing()
    {
        string relativeProjectPath = WriteUnresolvableProjectFixture();
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              projects: ["{relativeProjectPath}"]

            contracts:
              strict_coverage:
                - name: project-coverage
                  scope: project
                  reason: Every discovered project must be mapped or excluded.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        ArchitectureViolation finding = outcome.CoverageFindings.Single();
        Assert.That(finding.ForbiddenNamespace, Is.EqualTo("unresolved project"));
        Assert.That(finding.SourceType, Is.EqualTo(relativeProjectPath));

        ArchitectureCoverageSummary summary = outcome.CoverageSummaries.Single();
        Assert.That(summary.Counts.Unknown, Is.EqualTo(1));
        Assert.That(summary.Counts.Uncovered, Is.EqualTo(0));
    }

    [Test]
    public void AuditProjectCoverage_AllProjectsUnresolved_CoverageEngineStillRunsAndReportsUnknown()
    {
        string relativeProjectPath = WriteUnresolvableProjectFixture();
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              projects: ["{relativeProjectPath}"]
              coverage: warn

            contracts:
              audit_coverage:
                - name: project-coverage
                  scope: project
                  reason: Every discovered project must be mapped or excluded.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit"
        });

        // The missing-build-output discovery diagnostic is itself a "<configuration>" violation
        // (ArchitectureContractRunner.CheckConfiguration), surfaced regardless of mode or
        // analysis.coverage — that's pre-existing, orthogonal behavior, not asserted here. What
        // this test proves is the actual regression: IArchitectureRunnerSetupService.BuildRunner no
        // longer throws before the coverage engine runs, so the contract still reaches
        // CheckProjectCoverageContract and classifies the unresolved project as unknown.
        Assert.That(outcome.CoverageConfig, Is.EqualTo("warn"));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenNamespace, Is.EqualTo("unresolved project"));
        Assert.That(outcome.CoverageSummaries.Single().Counts.Unknown, Is.EqualTo(1));
    }

    [Test]
    public void StrictMode_WithOnlyAuditProjectCoverageDeclaredAndUnresolvedProjects_StillThrows()
    {
        // The unresolved-project bypass in IArchitectureRunnerSetupService.BuildRunner is mode/selection
        // aware: an audit_coverage-only scope: project contract can never run in strict mode (the
        // executor only runs contracts ContractsFor("strict", ...) selects), so it must not relax
        // the no-resolved-assemblies hard-fail for a strict-mode run.
        string relativeProjectPath = WriteUnresolvableProjectFixture();
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              projects: ["{relativeProjectPath}"]

            contracts:
              audit_coverage:
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

        Assert.That(ex.Message, Does.Contain("Architecture YAML must define analysis.target_assemblies"));
    }

    [Test]
    public void StrictMode_WithUnselectedProjectCoverageContractAndUnresolvedProjects_StillThrows()
    {
        // Same mode-awareness guarantee, but exercised through --contract selection instead of
        // mode: when a project-scope coverage contract exists but isn't among the selected
        // contract IDs for this run, it can't run either, so the bypass must not apply.
        string relativeProjectPath = WriteUnresolvableProjectFixture();
        string assemblyName = typeof(CoverageContractReservedTests).Assembly.GetName().Name!;
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              core:
                namespace: {assemblyName}

            analysis:
              projects: ["{relativeProjectPath}"]

            contracts:
              strict:
                - id: unrelated-rule
                  name: unrelated-rule
                  source: core
                  forbidden: []
                  reason: Placeholder selected contract.
              strict_coverage:
                - id: project-coverage
                  name: project-coverage
                  scope: project
                  reason: Every discovered project must be mapped or excluded.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict",
                ContractIds = new List<string> { "unrelated-rule" }
            }))!;

        Assert.That(ex.Message, Does.Contain("Architecture YAML must define analysis.target_assemblies"));
    }
}
