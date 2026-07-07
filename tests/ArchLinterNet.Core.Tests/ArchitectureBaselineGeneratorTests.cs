using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineGeneratorTests
{
    private readonly ArchitectureBaselineGenerator _generator = new();

    [Test]
    public void Generate_EmptyCandidates_ProducesEmptyBaseline()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = Array.Empty<ArchitectureBaselineCandidate>();
        var baseline = _generator.Generate(policy, candidates);

        Assert.That(baseline.Version, Is.EqualTo(1));
        Assert.That(baseline.Baseline.Strict, Is.Empty);
        Assert.That(baseline.Baseline.Audit, Is.Empty);
        Assert.That(baseline.Baseline.StrictLayers, Is.Empty);
        Assert.That(baseline.Baseline.StrictCycles, Is.Empty);
        Assert.That(baseline.Baseline.StrictAcyclicSiblings, Is.Empty);
        Assert.That(baseline.Baseline.StrictMethodBody, Is.Empty);
        Assert.That(baseline.Baseline.StrictProtected, Is.Empty);
        Assert.That(baseline.Baseline.StrictExternal, Is.Empty);
        Assert.That(baseline.Baseline.StrictIndependence, Is.Empty);
        Assert.That(baseline.Baseline.StrictAllowOnly, Is.Empty);
        Assert.That(baseline.Baseline.StrictProjectMetadata, Is.Empty);
    }

    [Test]
    public void Generate_SingleViolation_ProducesOneEntry()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "my-rule", "MyApp.Service", "Legacy.Provider")
        };

        var baseline = _generator.Generate(policy, candidates);

        Assert.That(baseline.Baseline.Strict, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.Strict[0].Id, Is.EqualTo("my-rule"));
        Assert.That(baseline.Baseline.Strict[0].IgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.Strict[0].IgnoredViolations[0].SourceType, Is.EqualTo("MyApp.Service"));
        Assert.That(baseline.Baseline.Strict[0].IgnoredViolations[0].ForbiddenReference,
            Is.EqualTo("Legacy.Provider"));
    }

    [Test]
    public void Generate_DeterministicOutput_IdenticalBetweenRuns()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "z-rule", "ZType", "AForbidden"),
            new("strict", "a-rule", "AType", "ZForbidden"),
            new("strict", "a-rule", "AType", "AForbidden"),
            new("strict", "z-rule", "AType", "ZForbidden")
        };

        var baseline1 = _generator.Generate(policy, candidates);
        var baseline2 = _generator.Generate(policy, candidates);

        string yaml1 = _generator.Serialize(baseline1);
        string yaml2 = _generator.Serialize(baseline2);

        Assert.That(yaml1, Is.EqualTo(yaml2));
    }

    [Test]
    public void Generate_DeduplicatesIdenticalCandidates()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "my-rule", "MyApp.Service", "Legacy.Provider"),
            new("strict", "my-rule", "MyApp.Service", "Legacy.Provider")
        };

        var baseline = _generator.Generate(policy, candidates);

        Assert.That(baseline.Baseline.Strict, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.Strict[0].IgnoredViolations, Has.Count.EqualTo(1));
    }

    [Test]
    public void Generate_ReasonOverride_AppearsInAllEntries()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "rule-a", "Src.A", "Ref.A"),
            new("strict_cycles", "rule-b", "Src.B", "Ref.B")
        };

        var baseline = _generator.Generate(policy, candidates, reason: "custom reason");

        Assert.That(baseline.Baseline.Strict[0].IgnoredViolations[0].Reason, Is.EqualTo("custom reason"));
        Assert.That(baseline.Baseline.StrictCycles[0].IgnoredViolations[0].Reason, Is.EqualTo("custom reason"));
    }

    [Test]
    public void Generate_CycleAndAcyclicSiblingCandidates_PlacedInCorrectGroups()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict_cycles", "no-cycles", "A.B", "C.D"),
            new("strict_acyclic_siblings", "no-sibling-cycles", "E.F", "G.H")
        };

        var baseline = _generator.Generate(policy, candidates);

        Assert.That(baseline.Baseline.StrictCycles, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictCycles[0].Id, Is.EqualTo("no-cycles"));
        Assert.That(baseline.Baseline.StrictCycles[0].IgnoredViolations[0].SourceType, Is.EqualTo("A.B"));
        Assert.That(baseline.Baseline.StrictAcyclicSiblings, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictAcyclicSiblings[0].Id, Is.EqualTo("no-sibling-cycles"));
        Assert.That(baseline.Baseline.StrictAcyclicSiblings[0].IgnoredViolations[0].SourceType, Is.EqualTo("E.F"));
    }

    [Test]
    public void Generate_MultipleContractTypes_EachHasCorrectEntries()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "dep-rule", "Src.Dep", "Ref.Dep"),
            new("strict_layers", "layer-rule", "Src.Layer", "Ref.Layer"),
            new("strict_cycles", "cycle-rule", "Src.Cycle", "Ref.Cycle"),
            new("strict_acyclic_siblings", "acyc-rule", "Src.Acyc", "Ref.Acyc"),
            new("strict_method_body", "method-rule", "Src.Method", "Ref.Method"),
            new("strict_independence", "indep-rule", "Src.Indep", "Ref.Indep"),
            new("strict_protected", "prot-rule", "Src.Prot", "Ref.Prot"),
            new("strict_external", "ext-rule", "Src.Ext", "Ref.Ext"),
            new("strict_allow_only", "allow-rule", "Src.Allow", "Ref.Allow"),
            new("strict_project_metadata", "project-rule", "src/MyApp/MyApp.csproj", "friend_assembly:MyApp.Tools")
        };

        var baseline = _generator.Generate(policy, candidates);

        Assert.That(baseline.Baseline.Strict, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictLayers, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictCycles, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictAcyclicSiblings, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictMethodBody, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictIndependence, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictProtected, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictExternal, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictAllowOnly, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictProjectMetadata, Has.Count.EqualTo(1));
    }

    [Test]
    public void Generate_NullContractIdCandidates_Skipped()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", null, "Src.Type", "Ref.Type")
        };

        var baseline = _generator.Generate(policy, candidates);

        Assert.That(baseline.Baseline.Strict, Is.Empty);
    }

    [Test]
    public void Serialize_EmptyDocument_ProducesValidYaml()
    {
        var doc = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups()
        };

        string yaml = _generator.Serialize(doc);

        Assert.That(yaml, Does.Contain("version: 1"));
        Assert.That(yaml, Does.Contain("baseline:"));
    }
}
