using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class DependencyEdgeCoverageContractTests
{
    private static readonly Assembly _testAssembly = typeof(DependencyEdgeCoverageContractTests).Assembly;

    private const string NsPrefix = "ArchLinterNet.Core.Tests.DependencyEdgeCoverageFixtures";

    private static ArchitectureContractRunner CreateRunner(
        ArchitectureCoverageContract coverageContract,
        Dictionary<string, ArchitectureLayer> layers,
        List<ArchitectureDependencyContract>? dependencyContracts = null,
        List<ArchitectureLayerContract>? layerContracts = null,
        List<ArchitectureIndependenceContract>? independenceContracts = null,
        List<ArchitectureLayerTemplateContract>? layerTemplateContracts = null,
        List<ArchitectureCoverageContract>? extraCoverageContracts = null)
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Test",
            Layers = layers,
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { _testAssembly.GetName().Name! },
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = dependencyContracts ?? new List<ArchitectureDependencyContract>(),
                StrictLayers = layerContracts ?? new List<ArchitectureLayerContract>(),
                StrictIndependence = independenceContracts ?? new List<ArchitectureIndependenceContract>(),
                StrictLayerTemplates = layerTemplateContracts ?? new List<ArchitectureLayerTemplateContract>(),
                StrictCoverage = new List<ArchitectureCoverageContract> { coverageContract }
                    .Concat(extraCoverageContracts ?? new List<ArchitectureCoverageContract>())
                    .ToList(),
            },
        };

        ArchitectureAnalysisContext context = new(
            AppContext.BaseDirectory,
            new[] { _testAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        return new ArchitectureContractRunner(context, document);
    }

    [Test]
    public void DependencyEdgeCoverage_PairGovernedByLayerContract_IsCovered()
    {
        string source = $"{NsPrefix}.LayerGoverned";
        string target = $"{NsPrefix}.LayerGovernedTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureContractRunner runner = CreateRunner(
            contract,
            layers,
            layerContracts: new List<ArchitectureLayerContract>
            {
                new() { Name = "chain", Layers = new List<string> { "source", "target" }, Reason = "fixture" },
            });

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Covered, Is.EqualTo(1));
    }

    [Test]
    public void DependencyEdgeCoverage_PairGovernedByIndependenceContract_IsCovered()
    {
        string source = $"{NsPrefix}.IndependenceGoverned";
        string target = $"{NsPrefix}.IndependenceGovernedTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureContractRunner runner = CreateRunner(
            contract,
            layers,
            independenceContracts: new List<ArchitectureIndependenceContract>
            {
                new() { Name = "independent", Layers = new List<string> { "source", "target" }, Reason = "fixture" },
            });

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Covered, Is.EqualTo(1));
    }

    [Test]
    public void DependencyEdgeCoverage_PairGovernedByDependencyContract_IsCovered()
    {
        string source = $"{NsPrefix}.DependencyGoverned";
        string target = $"{NsPrefix}.DependencyGovernedTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureContractRunner runner = CreateRunner(
            contract,
            layers,
            dependencyContracts: new List<ArchitectureDependencyContract>
            {
                new() { Name = "forbidden", Source = "source", Forbidden = new List<string> { "target" }, Reason = "fixture" },
            });

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Covered, Is.EqualTo(1));
    }

    [Test]
    public void DependencyEdgeCoverage_PairGovernedByExpandedLayerTemplate_IsCovered()
    {
        string source = $"{NsPrefix}.LayerGoverned";
        string target = $"{NsPrefix}.LayerGovernedTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureContractRunner runner = CreateRunner(
            contract,
            layers,
            layerTemplateContracts: new List<ArchitectureLayerTemplateContract>
            {
                new()
                {
                    Name = "fixture-template",
                    Containers = new List<string> { NsPrefix },
                    Layers = new List<ArchitectureTemplateLayer>
                    {
                        new() { Name = "LayerGoverned" },
                        new() { Name = "LayerGovernedTarget" },
                    },
                    Reason = "fixture",
                },
            });

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Covered, Is.EqualTo(1));
    }

    [Test]
    public void DependencyEdgeCoverage_OverlappingDeclaredLayers_MatchesSpecificPairLayerNotFirstOverallMatch()
    {
        // "narrow" matches the source namespace exactly; "broad" matches it too via a wildcard.
        // Declaring "narrow" first in document.Layers exercises the bug where edge-to-layer
        // resolution collapsed to whichever declared layer happened to match first overall,
        // instead of checking the edge against the SPECIFIC layer named in `between`.
        string source = $"{NsPrefix}.Uncovered";
        string target = $"{NsPrefix}.UncoveredTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["narrow"] = new ArchitectureLayer { Namespace = source },
            ["broad"] = new ArchitectureLayer { Namespace = NsPrefix },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "broad", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract, layers);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings.Single().SourceType, Is.EqualTo($"{source} -> {target}"));
        Assert.That(summary!.Counts.Uncovered, Is.EqualTo(1));
    }

    [Test]
    public void DependencyEdgeCoverage_PairNotGovernedByAnyContract_IsUncoveredWithEvidence()
    {
        string source = $"{NsPrefix}.Uncovered";
        string target = $"{NsPrefix}.UncoveredTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract, layers);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        ArchitectureViolation finding = findings.Single();
        Assert.That(finding.SourceType, Is.EqualTo($"{source} -> {target}"));
        Assert.That(finding.ForbiddenNamespace, Is.EqualTo("uncovered dependency edge"));
        Assert.That(finding.ForbiddenReferences, Contains.Item($"{source}.UncoveredSourceType"));
        Assert.That(summary!.Counts.Uncovered, Is.EqualTo(1));
        Assert.That(summary.UncoveredItems.Single().Item, Is.EqualTo($"{source} -> {target}"));
    }

    [Test]
    public void DependencyEdgeCoverage_ExcludedPair_ProducesNoFindingAndIsCountedExcluded()
    {
        string source = $"{NsPrefix}.Excluded";
        string target = $"{NsPrefix}.ExcludedTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
            Exclude = new List<ArchitectureCoverageExclusion>
            {
                new() { Between = new List<string> { "source", "target" }, Reason = "Known legacy edge, tracked separately." },
            },
        };

        ArchitectureContractRunner runner = CreateRunner(contract, layers);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Excluded, Is.EqualTo(1));
        Assert.That(summary.ExcludedItems.Single().Reason, Is.EqualTo("Known legacy edge, tracked separately."));
    }

    [Test]
    public void DependencyEdgeCoverage_PairNotDeclaredInBetween_ProducesNoFindingAndIsNotCounted()
    {
        string source = $"{NsPrefix}.Uncovered";
        string target = $"{NsPrefix}.UncoveredTarget";
        string unrelated = $"{NsPrefix}.LayerGovernedTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
            ["unrelated"] = new ArchitectureLayer { Namespace = unrelated },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "unrelated" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureContractRunner runner = CreateRunner(contract, layers);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);
        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(summary!.Counts.Covered, Is.EqualTo(0));
        Assert.That(summary.Counts.Uncovered, Is.EqualTo(0));
        Assert.That(summary.Counts.Excluded, Is.EqualTo(0));
    }

    [Test]
    public void DependencyEdgeCoverage_MixedWithNamespaceCoverageContract_BothScopesClassifyIndependently()
    {
        string source = $"{NsPrefix}.Uncovered";
        string target = $"{NsPrefix}.UncoveredTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract edgeContract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
        };

        ArchitectureCoverageContract namespaceContract = new()
        {
            Id = "namespace-coverage",
            Name = "namespace-coverage",
            Scope = "namespace",
            Roots = new List<ArchitectureCoverageRoot> { new() { Namespace = NsPrefix } },
            Reason = "Every fixture namespace must be mapped or excluded.",
        };

        ArchitectureContractRunner runner = CreateRunner(
            edgeContract, layers, extraCoverageContracts: new List<ArchitectureCoverageContract> { namespaceContract });

        List<ArchitectureViolation> edgeFindings = runner.CheckCoverageContract(edgeContract);
        ArchitectureCoverageSummary? edgeSummary = runner.BuildCoverageSummary(edgeContract);

        List<ArchitectureViolation> namespaceFindings = runner.CheckCoverageContract(namespaceContract);
        ArchitectureCoverageSummary? namespaceSummary = runner.BuildCoverageSummary(namespaceContract);

        Assert.That(edgeFindings.Single().ForbiddenNamespace, Is.EqualTo("uncovered dependency edge"));
        Assert.That(edgeSummary!.Counts.Uncovered, Is.EqualTo(1));

        // "source"/"target" are declared layers (covering the edge contract's own namespaces),
        // so a different fixture namespace not declared as any layer demonstrates that
        // namespace coverage still independently finds its own uncovered units.
        Assert.That(namespaceFindings, Has.Some.Matches<ArchitectureViolation>(
            f => f.SourceType == $"{NsPrefix}.DependencyGoverned" && f.ForbiddenNamespace == "uncovered namespace"));
        Assert.That(namespaceSummary!.Counts.Uncovered, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void DependencyEdgeCoverage_BaselinedFinding_IsSuppressedAsIgnoredViolation()
    {
        string source = $"{NsPrefix}.Uncovered";
        string target = $"{NsPrefix}.UncoveredTarget";

        Dictionary<string, ArchitectureLayer> layers = new()
        {
            ["source"] = new ArchitectureLayer { Namespace = source },
            ["target"] = new ArchitectureLayer { Namespace = target },
        };

        ArchitectureCoverageContract contract = new()
        {
            Id = "dependency-edge-coverage",
            Name = "dependency-edge-coverage",
            Scope = "dependency_edge",
            Between = new List<List<string>> { new() { "source", "target" } },
            Reason = "Observed edges must be governed by a declared contract.",
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = $"{source} -> {target}",
                    ForbiddenReference = "uncovered dependency edge",
                    Reason = "Accepted as baseline debt."
                },
            },
        };

        ArchitectureContractRunner runner = CreateRunner(contract, layers);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(runner.UnmatchedIgnoredViolations, Is.Empty);
    }
}
