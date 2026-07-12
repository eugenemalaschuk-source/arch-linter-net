using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ContextualContractTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Baseline generation/comparison tests for the context_dependencies/context_allow_only families,
// per tasks.md 5.2, mirroring ArchitectureBaselineGeneratorTests/ArchitectureBaselineIntegrationTests
// for the existing dependency/allow_only families. See design.md Decision 8.
[TestFixture]
public sealed class ContextualContractBaselineTests
{
    private static readonly Assembly _fixturesAssembly = typeof(SalesOrder).Assembly;
    private static readonly ArchitectureBaselineGenerator _generator = new();
    private static readonly ArchitectureBaselineLoadingService _loadingService = new();

    private static ArchitectureClassificationConfiguration Classification()
    {
        return new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                }
            }
        };
    }

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext("/tmp", new[] { _fixturesAssembly }, Array.Empty<string>(), Array.Empty<string>());
    }

    private static ArchitectureContextDependencyContract CrossDomainDependencyContract()
    {
        return new ArchitectureContextDependencyContract
        {
            Id = "sales-no-inventory",
            Name = "sales-must-not-depend-on-inventory",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } }
            }
        };
    }

    private static ArchitectureContextAllowOnlyContract SalesAllowOnlyContract()
    {
        return new ArchitectureContextAllowOnlyContract
        {
            Id = "sales-allow-only",
            Name = "sales-may-depend-only-on-own-context",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Allowed = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } }
            }
        };
    }

    [Test]
    public void Runner_ContextDependencyContract_CollectsBaselineCandidates()
    {
        ArchitectureContextDependencyContract contract = CrossDomainDependencyContract();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = Classification(),
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { _fixturesAssembly.GetName().Name! } },
            Contracts = new ArchitectureContractGroups { StrictContextDependencies = new List<ArchitectureContextDependencyContract> { contract } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.Session.CheckContextDependencyContract(contract);

        Assert.That(runner.BaselineCandidates, Is.Not.Empty);
    }

    [Test]
    public void Generator_FromContextDependencyRunnerCandidates_ProducesEntryInCorrectGroup()
    {
        ArchitectureContextDependencyContract contract = CrossDomainDependencyContract();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = Classification(),
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { _fixturesAssembly.GetName().Name! } },
            Contracts = new ArchitectureContractGroups { StrictContextDependencies = new List<ArchitectureContextDependencyContract> { contract } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.Session.CheckContextDependencyContract(contract);

        ArchitectureBaselineDocument baseline = _generator.Generate(document, runner.BaselineCandidates, "test baseline");

        Assert.That(baseline.Baseline.StrictContextDependencies, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictContextDependencies[0].Id, Is.EqualTo("sales-no-inventory"));
        Assert.That(baseline.Baseline.StrictContextDependencies[0].IgnoredViolations, Is.Not.Empty);
        Assert.That(baseline.Baseline.StrictContextDependencies[0].IgnoredViolations[0].SourceType,
            Is.EqualTo(typeof(SalesCheckout).FullName));
    }

    [Test]
    public void Generator_FromContextAllowOnlyRunnerCandidates_ProducesEntryInCorrectGroup()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = Classification(),
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { _fixturesAssembly.GetName().Name! } },
            Contracts = new ArchitectureContractGroups { StrictContextAllowOnly = new List<ArchitectureContextAllowOnlyContract> { contract } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.Session.CheckContextAllowOnlyContract(contract);

        ArchitectureBaselineDocument baseline = _generator.Generate(document, runner.BaselineCandidates, "test baseline");

        Assert.That(baseline.Baseline.StrictContextAllowOnly, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictContextAllowOnly[0].Id, Is.EqualTo("sales-allow-only"));
        Assert.That(baseline.Baseline.StrictContextAllowOnly[0].IgnoredViolations, Is.Not.Empty);
    }

    [Test]
    public void FullFlow_GenerateMergeValidate_ContextDependency_NewViolationsStillFail()
    {
        ArchitectureContextDependencyContract contract = CrossDomainDependencyContract();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = Classification(),
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { _fixturesAssembly.GetName().Name! } },
            Contracts = new ArchitectureContractGroups { StrictContextDependencies = new List<ArchitectureContextDependencyContract> { contract } }
        };

        var context = CreateContext();
        var generateRunner = new ArchitectureContractRunner(context, document);
        generateRunner.Session.CheckContextDependencyContract(contract);

        ArchitectureBaselineDocument baseline = _generator.Generate(document, generateRunner.BaselineCandidates, "auto-baseline");
        string yaml = _generator.Serialize(baseline);

        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string baselinePath = Path.Combine(tempDir, "baseline.yml");
            File.WriteAllText(baselinePath, yaml);

            var loadedBaseline = _loadingService.LoadFromPath(baselinePath);
            ArchitectureBaselineLoadingService.MergeAndValidate(document, loadedBaseline);

            var finalRunner = new ArchitectureContractRunner(context, document);
            List<ArchLinterNet.Core.Model.ArchitectureViolation> violations =
                finalRunner.Session.CheckContextDependencyContract(document.Contracts.StrictContextDependencies[0]);

            // Every violation the generator captured is now present in ignored_violations, so a
            // re-run with the merged baseline must report no violations for the same condition.
            Assert.That(violations, Is.Empty,
                "Baselined contextual violations must be suppressed after merge, mirroring the existing dependency family's baseline behavior.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public void Baseline_ContextDependencyGroupName_RoundTripsThroughGetGroupAndSetGroup()
    {
        var groups = new ArchitectureBaselineContractGroups();
        var entry = new ArchitectureBaselineContractEntry
        {
            Id = "sales-no-inventory",
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = "A", ForbiddenReference = "B", Reason = "r" }
            }
        };

        groups.SetGroup("strict_context_dependencies", new List<ArchitectureBaselineContractEntry> { entry });
        groups.SetGroup("audit_context_allow_only", new List<ArchitectureBaselineContractEntry> { entry });

        Assert.That(groups.GetGroup("strict_context_dependencies"), Has.Count.EqualTo(1));
        Assert.That(groups.GetGroup("audit_context_allow_only"), Has.Count.EqualTo(1));
        Assert.That(ArchitectureBaselineContractGroups.GroupNames, Does.Contain("strict_context_dependencies"));
        Assert.That(ArchitectureBaselineContractGroups.GroupNames, Does.Contain("audit_context_dependencies"));
        Assert.That(ArchitectureBaselineContractGroups.GroupNames, Does.Contain("strict_context_allow_only"));
        Assert.That(ArchitectureBaselineContractGroups.GroupNames, Does.Contain("audit_context_allow_only"));
    }
}
