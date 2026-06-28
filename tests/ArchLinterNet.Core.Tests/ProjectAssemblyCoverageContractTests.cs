using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ProjectAssemblyCoverageContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-project-assembly-coverage-{Guid.NewGuid():N}");
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

    private static readonly System.Reflection.Assembly _coreAssembly = typeof(ArchitectureContractDocument).Assembly;
    private static readonly System.Reflection.Assembly _testingAssembly = typeof(ArchLinterNet.Testing.ArchitectureAssertions).Assembly;

    private static ArchitectureContractDocument CreateDocument(ArchitectureCoverageContract contract, ProjectDiscoveryResult? discovery = null)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { _coreAssembly.GetName().Name!, _testingAssembly.GetName().Name! },
                Solution = discovery != null ? "fixture.slnx" : string.Empty,
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictCoverage = new List<ArchitectureCoverageContract> { contract },
            },
        };
    }

    private ArchitectureContractRunner CreateRunner(ArchitectureCoverageContract contract, ProjectDiscoveryResult? discovery = null)
    {
        ArchitectureContractDocument document = CreateDocument(contract, discovery);
        ArchitectureAnalysisContext context = new(
            _tempDir,
            new[] { _coreAssembly, _testingAssembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);

        return new ArchitectureContractRunner(context, document);
    }

    [Test]
    public void AssemblyCoverage_CoreAssemblyMatchesLayer_IsCovered()
    {
        ArchitectureCoverageContract contract = new()
        {
            Id = "assembly-coverage",
            Name = "assembly-coverage",
            Scope = "assembly",
            Reason = "Every first-party assembly must be mapped or excluded.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings.Select(f => f.SourceType), Does.Not.Contain(_coreAssembly.GetName().Name));
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary!.Counts.Covered, Is.EqualTo(1));
    }

    [Test]
    public void AssemblyCoverage_TestingAssemblyMatchesNoLayer_IsUncoveredWithPathAndRepresentativeTypeEvidence()
    {
        ArchitectureCoverageContract contract = new()
        {
            Id = "assembly-coverage",
            Name = "assembly-coverage",
            Scope = "assembly",
            Reason = "Every first-party assembly must be mapped or excluded.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        ArchitectureViolation finding = findings.Single(f => f.SourceType == _testingAssembly.GetName().Name);
        Assert.That(finding.ForbiddenNamespace, Is.EqualTo("uncovered assembly"));
        Assert.That(summary!.Counts.Uncovered, Is.EqualTo(1));

        // The assembly's identity (name) is the finding's SourceType; the finding's
        // ForbiddenReferences and the summary evidence must additionally carry the
        // assembly's file path (when available) and a representative type, per
        // architecture-coverage-reporting's "Uncovered evidence in summary" requirement.
        Assert.That(_testingAssembly.Location, Is.Not.Empty);
        Assert.That(finding.ForbiddenReferences, Contains.Item(_testingAssembly.Location));
        Assert.That(finding.ForbiddenReferences, Contains.Item("ArchLinterNet.Testing.ArchitectureAssertions"));

        string evidence = summary.UncoveredItems.Single().Evidence;
        Assert.That(evidence, Does.Contain(_testingAssembly.Location));
    }

    [Test]
    public void AssemblyCoverage_ExcludedAssembly_ProducesNoFindingAndIsCountedExcluded()
    {
        ArchitectureCoverageContract contract = new()
        {
            Id = "assembly-coverage",
            Name = "assembly-coverage",
            Scope = "assembly",
            Reason = "Every first-party assembly must be mapped or excluded.",
            Exclude = new List<ArchitectureCoverageExclusion>
            {
                new() { Assembly = _testingAssembly.GetName().Name!, Reason = "Test-only helper assembly." },
            },
        };

        ArchitectureContractRunner runner = CreateRunner(contract);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Excluded, Is.EqualTo(1));
        Assert.That(summary.Counts.Covered, Is.EqualTo(1));
        Assert.That(summary.ExcludedItems.Single().Reason, Is.EqualTo("Test-only helper assembly."));
    }

    private static ProjectDiscoveryResult CreateDiscovery(params ArchitectureDiscoveredProject[] projects)
    {
        return new ProjectDiscoveryResult(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = projects
        };
    }

    [Test]
    public void ProjectCoverage_DiscoveredProjectResolvesToCoveredAssembly_IsCovered()
    {
        ProjectDiscoveryResult discovery = CreateDiscovery(
            new ArchitectureDiscoveredProject("src/Core/Core.csproj", _coreAssembly.GetName().Name!, new[] { "net10.0" }));

        ArchitectureCoverageContract contract = new()
        {
            Id = "project-coverage",
            Name = "project-coverage",
            Scope = "project",
            Reason = "Every discovered project must be mapped or excluded.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract, discovery);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Covered, Is.EqualTo(1));
    }

    [Test]
    public void ProjectCoverage_DiscoveredProjectResolvesToUncoveredAssembly_IsUncoveredWithAssemblyNameAndRepresentativeTypeEvidence()
    {
        string assemblyName = _testingAssembly.GetName().Name!;
        ProjectDiscoveryResult discovery = CreateDiscovery(
            new ArchitectureDiscoveredProject("src/Testing/Testing.csproj", assemblyName, new[] { "net10.0" }));

        ArchitectureCoverageContract contract = new()
        {
            Id = "project-coverage",
            Name = "project-coverage",
            Scope = "project",
            Reason = "Every discovered project must be mapped or excluded.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract, discovery);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        ArchitectureViolation finding = findings.Single();
        Assert.That(finding.SourceType, Is.EqualTo("src/Testing/Testing.csproj"));
        Assert.That(finding.ForbiddenNamespace, Is.EqualTo("uncovered project"));
        Assert.That(summary!.Counts.Uncovered, Is.EqualTo(1));

        // Project path is the finding's SourceType; the discovered assembly name must
        // additionally appear in the finding/summary evidence alongside a representative
        // type, per project-coverage-contracts' "uncovered project" evidence requirement.
        Assert.That(finding.ForbiddenReferences, Contains.Item(assemblyName));

        string evidence = summary.UncoveredItems.Single().Evidence;
        Assert.That(evidence, Does.Contain(assemblyName));
    }

    [Test]
    public void ProjectCoverage_ExcludedProject_ProducesNoFindingAndIsCountedExcluded()
    {
        ProjectDiscoveryResult discovery = CreateDiscovery(
            new ArchitectureDiscoveredProject("samples/Demo/Demo.csproj", _testingAssembly.GetName().Name!, new[] { "net10.0" }));

        ArchitectureCoverageContract contract = new()
        {
            Id = "project-coverage",
            Name = "project-coverage",
            Scope = "project",
            Reason = "Every discovered project must be mapped or excluded.",
            Exclude = new List<ArchitectureCoverageExclusion>
            {
                new() { Project = "samples/Demo/Demo.csproj", Reason = "Sample project excluded from coverage." },
            },
        };

        ArchitectureContractRunner runner = CreateRunner(contract, discovery);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Excluded, Is.EqualTo(1));
        Assert.That(summary.ExcludedItems.Single().Reason, Is.EqualTo("Sample project excluded from coverage."));
    }

    [Test]
    public void ProjectCoverage_ExclusionWithDifferentCase_DoesNotMatch()
    {
        ProjectDiscoveryResult discovery = CreateDiscovery(
            new ArchitectureDiscoveredProject("samples/Demo/Demo.csproj", _testingAssembly.GetName().Name!, new[] { "net10.0" }));

        ArchitectureCoverageContract contract = new()
        {
            Id = "project-coverage",
            Name = "project-coverage",
            Scope = "project",
            Reason = "Every discovered project must be mapped or excluded.",
            Exclude = new List<ArchitectureCoverageExclusion>
            {
                // Differs only by case from the discovered project's path/file name. Project
                // exclusions match by exact (ordinal) string equality, like assembly exclusions,
                // not case-insensitively, so this exclusion must NOT suppress the finding.
                new() { Project = "SAMPLES/DEMO/DEMO.CSPROJ", Reason = "Wrong case." },
            },
        };

        ArchitectureContractRunner runner = CreateRunner(contract, discovery);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings.Single().SourceType, Is.EqualTo("samples/Demo/Demo.csproj"));
        Assert.That(summary!.Counts.Excluded, Is.EqualTo(0));
        Assert.That(summary.Counts.Uncovered, Is.EqualTo(1));
    }

    [Test]
    public void ProjectCoverage_DiscoveredProjectWithNoResolvedAssembly_IsUnknown()
    {
        ProjectDiscoveryResult discovery = CreateDiscovery(
            new ArchitectureDiscoveredProject("src/Ghost/Ghost.csproj", "Ghost.Assembly.Not.Resolved", new[] { "net10.0" }));

        ArchitectureCoverageContract contract = new()
        {
            Id = "project-coverage",
            Name = "project-coverage",
            Scope = "project",
            Reason = "Every discovered project must be mapped or excluded.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract, discovery);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings.Single().SourceType, Is.EqualTo("src/Ghost/Ghost.csproj"));
        Assert.That(findings.Single().ForbiddenNamespace, Is.EqualTo("unresolved project"));
        Assert.That(summary!.Counts.Unknown, Is.EqualTo(1));
        Assert.That(summary.Counts.Uncovered, Is.EqualTo(0));
        Assert.That(summary.UnknownItems.Single().Item, Is.EqualTo("src/Ghost/Ghost.csproj"));
    }

    [Test]
    public void AssemblyCoverage_BaselinedFinding_IsSuppressedAsIgnoredViolation()
    {
        ArchitectureCoverageContract contract = new()
        {
            Id = "assembly-coverage",
            Name = "assembly-coverage",
            Scope = "assembly",
            Reason = "Every first-party assembly must be mapped or excluded.",
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = _testingAssembly.GetName().Name!,
                    ForbiddenReference = "uncovered assembly",
                    Reason = "Accepted as baseline debt."
                },
            },
        };

        ArchitectureContractRunner runner = CreateRunner(contract);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(runner.UnmatchedIgnoredViolations, Is.Empty);
    }
}
