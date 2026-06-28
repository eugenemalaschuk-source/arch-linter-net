using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class DependencyEdgeCoverageValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-dependency-edge-coverage-test-{Guid.NewGuid():N}");
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

    private const string FixtureRoot = "ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures";

    private string AssemblyName => typeof(DependencyEdgeCoverageValidationTests).Assembly.GetName().Name!;

    [Test]
    public void DependencyEdgeCoverage_EmptyBetween_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_coverage:
                - name: dependency-edge-coverage
                  scope: dependency_edge
                  between: []
                  reason: Invalid dependency-edge coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureContractLoader.LoadFromPath(policyPath))!;

        Assert.That(ex.Message, Does.Contain("must declare at least one pair in 'between'"));
    }

    [Test]
    public void DependencyEdgeCoverage_BetweenReferencesUndeclaredLayer_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              source:
                namespace: {FixtureRoot}.Uncovered

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_coverage:
                - name: dependency-edge-coverage
                  scope: dependency_edge
                  between:
                    - [source, does_not_exist_layer]
                  reason: Invalid dependency-edge coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureContractLoader.LoadFromPath(policyPath))!;

        Assert.That(ex.Message, Does.Contain("referencing undeclared layer 'does_not_exist_layer'"));
    }

    [Test]
    public void DependencyEdgeCoverage_WithRoots_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              source:
                namespace: {FixtureRoot}.Uncovered
              target:
                namespace: {FixtureRoot}.UncoveredTarget

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_coverage:
                - name: dependency-edge-coverage
                  scope: dependency_edge
                  between:
                    - [source, target]
                  roots:
                    - namespace: {FixtureRoot}
                  reason: Invalid dependency-edge coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureContractLoader.LoadFromPath(policyPath))!;

        Assert.That(ex.Message, Does.Contain("cannot declare 'roots'"));
    }

    [Test]
    public void DependencyEdgeCoverage_ExclusionWithoutBetween_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              source:
                namespace: {FixtureRoot}.Uncovered
              target:
                namespace: {FixtureRoot}.UncoveredTarget

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_coverage:
                - name: dependency-edge-coverage
                  scope: dependency_edge
                  between:
                    - [source, target]
                  exclude:
                    - reason: Missing between matcher.
                  reason: Invalid dependency-edge coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureContractLoader.LoadFromPath(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Dependency-edge coverage exclusions must declare 'between'"));
    }

    private string BuildPolicy(string coverageGroup, string? analysisCoverage = null)
    {
        string coverageSetting = analysisCoverage is null
            ? string.Empty
            : $"  coverage: {analysisCoverage}{Environment.NewLine}";

        return $"version: 1{Environment.NewLine}" +
               $"name: Test{Environment.NewLine}{Environment.NewLine}" +
               $"layers:{Environment.NewLine}" +
               $"  source:{Environment.NewLine}" +
               $"    namespace: {FixtureRoot}.Uncovered{Environment.NewLine}" +
               $"  target:{Environment.NewLine}" +
               $"    namespace: {FixtureRoot}.UncoveredTarget{Environment.NewLine}{Environment.NewLine}" +
               $"analysis:{Environment.NewLine}" +
               $"  target_assemblies: [{AssemblyName}]{Environment.NewLine}" +
               coverageSetting +
               $"contracts:{Environment.NewLine}" +
               $"  {coverageGroup}:{Environment.NewLine}" +
               $"    - id: dependency-edge-coverage{Environment.NewLine}" +
               $"      name: dependency-edge-coverage{Environment.NewLine}" +
               $"      scope: dependency_edge{Environment.NewLine}" +
               $"      between:{Environment.NewLine}" +
               $"        - [source, target]{Environment.NewLine}" +
               $"      reason: Observed edges must be governed by a declared contract.{Environment.NewLine}";
    }

    [Test]
    public void AuditDependencyEdgeCoverage_ReportsUncoveredWithoutFailingStrictValidation()
    {
        string policyPath = WritePolicy(BuildPolicy("audit_coverage", analysisCoverage: "warn"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.Violations, Is.Empty);
        Assert.That(outcome.CoverageConfig, Is.EqualTo("warn"));
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenNamespace, Is.EqualTo("uncovered dependency edge"));
    }

    [Test]
    public void StrictDependencyEdgeCoverage_DefaultSeverity_FailsOnUncoveredEdge()
    {
        string policyPath = WritePolicy(BuildPolicy("strict_coverage"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenNamespace, Is.EqualTo("uncovered dependency edge"));
    }
}
