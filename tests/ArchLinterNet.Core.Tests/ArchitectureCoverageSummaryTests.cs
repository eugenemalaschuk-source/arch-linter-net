using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureCoverageSummaryTests
{
    private const string FeatureRoot = "ArchLinterNet.Core.Tests.NamespaceCoverageFixtures.Features";
    private const string RuleInputFixtureRoot = "ArchLinterNet.Core.Tests.RuleInputCoverageFixtures";

    private static ArchitectureAnalysisContext CreateContext(Type fixtureType)
    {
        return new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: new[] { fixtureType.Assembly },
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());
    }

    private static ArchitectureCoverageSummary RequireSummary(ArchitectureCoverageSummary? summary)
    {
        Assert.That(summary, Is.Not.Null);
        return summary!;
    }

    private static ArchitectureCoverageContract CreateNamespaceContract(
        string root = FeatureRoot, IEnumerable<ArchitectureCoverageExclusion>? exclude = null)
    {
        ArchitectureCoverageContract contract = new()
        {
            Name = "namespace-feature-coverage",
            Id = "namespace-feature-coverage",
            Scope = "namespace",
            Reason = "Feature namespaces must be mapped or explicitly excluded.",
            Roots = { new ArchitectureCoverageRoot { Namespace = root } }
        };

        if (exclude != null)
        {
            contract.Exclude.AddRange(exclude);
        }

        return contract;
    }

    private static ArchitectureContractDocument CreateNamespaceDocument()
    {
        ArchitectureContractDocument document = new();
        document.Layers["audio"] = new ArchitectureLayer { Namespace = $"{FeatureRoot}.Audio" };
        document.Layers["feature_api"] = new ArchitectureLayer
        {
            Namespace = $"{FeatureRoot}.*",
            NamespaceSuffix = "Api"
        };
        document.Contracts.StrictLayerTemplates.Add(new ArchitectureLayerTemplateContract
        {
            Name = "billing-template",
            Containers = { $"{FeatureRoot}.Billing" },
            Layers = { new ArchitectureTemplateLayer { Name = "Contracts" } },
            Reason = "Template coverage."
        });

        return document;
    }

    [Test]
    public void BuildCoverageSummary_NamespaceScope_PartiallyCovered_ProducesExpectedCounts()
    {
        ArchitectureContractDocument document = CreateNamespaceDocument();
        ArchitectureCoverageContract contract = CreateNamespaceContract(exclude: new[]
        {
            new ArchitectureCoverageExclusion
            {
                NamespaceSuffix = "Generated",
                Reason = "Generated namespaces are excluded."
            }
        });

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        Assert.That(summary.Scope, Is.EqualTo("namespace"));
        Assert.That(summary.Counts, Is.EqualTo(new ArchitectureCoverageSummaryCounts(
            Covered: 3, Excluded: 1, Uncovered: 2, Stale: 0, Unknown: 0)));

        Assert.That(summary.ExcludedItems.Select(i => i.Item), Is.EqualTo(new[]
        {
            $"{FeatureRoot}.Video.Generated"
        }));
        Assert.That(summary.ExcludedItems.Single().Reason, Is.EqualTo("Generated namespaces are excluded."));

        Assert.That(summary.UncoveredItems.Select(i => i.Item), Is.EqualTo(new[]
        {
            $"{FeatureRoot}.AlphaGap",
            $"{FeatureRoot}.ZetaGap"
        }));
        Assert.That(summary.UncoveredItems.Select(i => i.Evidence), Is.EqualTo(new[]
        {
            $"{FeatureRoot}.AlphaGap.AlphaGapRepresentative",
            $"{FeatureRoot}.ZetaGap.ZetaGapRepresentative"
        }));
    }

    [Test]
    public void BuildCoverageSummary_NamespaceScope_FullyCovered_HasZeroUncoveredAndStale()
    {
        ArchitectureContractDocument document = CreateNamespaceDocument();
        ArchitectureCoverageContract contract = CreateNamespaceContract(root: $"{FeatureRoot}.Audio");

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        Assert.That(summary.Counts, Is.EqualTo(new ArchitectureCoverageSummaryCounts(
            Covered: 1, Excluded: 0, Uncovered: 0, Stale: 0, Unknown: 0)));
        Assert.That(summary.UncoveredItems, Is.Empty);
        Assert.That(summary.ExcludedItems, Is.Empty);
    }

    [Test]
    public void BuildCoverageSummary_NamespaceScope_EmptyRoot_ProducesZeroCountsWithoutError()
    {
        ArchitectureContractDocument document = CreateNamespaceDocument();
        ArchitectureCoverageContract contract = CreateNamespaceContract(root: "Nonexistent.Root.Namespace");

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        Assert.That(summary.Counts, Is.EqualTo(new ArchitectureCoverageSummaryCounts(0, 0, 0, 0, 0)));
        Assert.That(summary.ExcludedItems, Is.Empty);
        Assert.That(summary.UncoveredItems, Is.Empty);
    }

    [Test]
    public void BuildCoverageSummary_ContractNotSelected_ReturnsNull()
    {
        ArchitectureContractDocument document = CreateNamespaceDocument();
        ArchitectureCoverageContract contract = CreateNamespaceContract();

        ArchitectureContractRunner runner = new(
            CreateContext(typeof(ArchitectureCoverageSummaryTests)),
            document,
            selectedContractIds: new HashSet<string>(new[] { "some-other-contract-id" }, StringComparer.OrdinalIgnoreCase));

        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(summary, Is.Null);
    }

    private static ArchitectureContractDocument CreateRuleInputDocument()
    {
        ArchitectureContractDocument document = new();

        document.Layers["audio"] = new ArchitectureLayer { Namespace = $"{RuleInputFixtureRoot}.Audio" };
        document.Layers["video"] = new ArchitectureLayer { Namespace = $"{RuleInputFixtureRoot}.Video" };
        document.Layers["ghost"] = new ArchitectureLayer { Namespace = $"{RuleInputFixtureRoot}.Ghost" };

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "audio-rule",
            Id = "audio-rule",
            Source = "audio",
            Forbidden = { "video" },
            Reason = "Audio must not depend on video."
        });

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "video-to-ghost-rule",
            Id = "video-to-ghost-rule",
            Source = "video",
            Forbidden = { "ghost" },
            Reason = "Video must not depend on ghost."
        });

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "typo-rule",
            Id = "typo-rule",
            Source = "does_not_exist_layer",
            Forbidden = { "audio" },
            Reason = "Placeholder rule with a dangling source layer."
        });

        return document;
    }

    private static ArchitectureCoverageContract CreateRuleInputContract(
        IEnumerable<string> contractIds, IEnumerable<ArchitectureCoverageExclusion>? exclude = null)
    {
        ArchitectureCoverageContract contract = new()
        {
            Name = "rule-input-coverage",
            Id = "rule-input-coverage",
            Scope = "rule_input",
            Reason = "Flag if referenced rules stop matching any code.",
        };

        contract.ContractIds.AddRange(contractIds);

        if (exclude != null)
        {
            contract.Exclude.AddRange(exclude);
        }

        return contract;
    }

    [Test]
    public void BuildCoverageSummary_RuleInputScope_MixedReferences_ClassifiesStaleAndUnknown()
    {
        ArchitectureContractDocument document = CreateRuleInputDocument();
        ArchitectureCoverageContract contract = CreateRuleInputContract(
            new[] { "audio-rule", "video-to-ghost-rule", "typo-rule" });

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        Assert.That(summary.Scope, Is.EqualTo("rule_input"));
        Assert.That(summary.Counts, Is.EqualTo(new ArchitectureCoverageSummaryCounts(
            Covered: 4, Excluded: 0, Uncovered: 0, Stale: 1, Unknown: 1)));

        Assert.That(summary.UncoveredItems, Is.Empty);
        Assert.That(summary.StaleItems.Select(i => (i.Item, i.Evidence)), Is.EquivalentTo(new[]
        {
            ("video-to-ghost-rule", "ghost")
        }));
        Assert.That(summary.UnknownItems.Select(i => (i.Item, i.Evidence)), Is.EquivalentTo(new[]
        {
            ("typo-rule", "does_not_exist_layer")
        }));
    }

    [Test]
    public void BuildCoverageSummary_RuleInputScope_ExcludedContractId_CountsAsExcludedWithReason()
    {
        ArchitectureContractDocument document = CreateRuleInputDocument();
        ArchitectureCoverageContract contract = CreateRuleInputContract(
            new[] { "video-to-ghost-rule", "typo-rule" },
            new[]
            {
                new ArchitectureCoverageExclusion
                {
                    ContractId = "video-to-ghost-rule",
                    Reason = "Ghost layer is intentionally unused for now."
                },
                new ArchitectureCoverageExclusion
                {
                    ContractId = "typo-rule",
                    Reason = "Placeholder rule retained for documentation purposes."
                }
            });

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        Assert.That(summary.Counts, Is.EqualTo(new ArchitectureCoverageSummaryCounts(
            Covered: 0, Excluded: 2, Uncovered: 0, Stale: 0, Unknown: 0)));

        Assert.That(summary.ExcludedItems.Select(i => (i.Item, i.Reason)), Is.EquivalentTo(new[]
        {
            ("video-to-ghost-rule", "Ghost layer is intentionally unused for now."),
            ("typo-rule", "Placeholder rule retained for documentation purposes.")
        }));
    }

    [Test]
    public void BuildCoverageSummary_RuleInputScope_NoContractIds_ProducesZeroCountsWithoutError()
    {
        ArchitectureContractDocument document = CreateRuleInputDocument();
        ArchitectureCoverageContract contract = CreateRuleInputContract(Array.Empty<string>());

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        Assert.That(summary.Counts, Is.EqualTo(new ArchitectureCoverageSummaryCounts(0, 0, 0, 0, 0)));
    }

    [Test]
    public void BuildCoverageSummary_RepeatedRuns_AreDeterministic()
    {
        ArchitectureContractDocument document = CreateNamespaceDocument();
        ArchitectureCoverageContract contract = CreateNamespaceContract(exclude: new[]
        {
            new ArchitectureCoverageExclusion
            {
                NamespaceSuffix = "Generated",
                Reason = "Generated namespaces are excluded."
            }
        });

        ArchitectureContractRunner firstRunner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);
        ArchitectureContractRunner secondRunner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary first = RequireSummary(firstRunner.BuildCoverageSummary(contract));
        ArchitectureCoverageSummary second = RequireSummary(secondRunner.BuildCoverageSummary(contract));

        Assert.That(first.Counts, Is.EqualTo(second.Counts));
        Assert.That(first.ExcludedItems, Is.EqualTo(second.ExcludedItems));
        Assert.That(first.UncoveredItems, Is.EqualTo(second.UncoveredItems));
    }

    [Test]
    public void BuildCoverageSummary_SemanticRoleScope_ReportsGovernedUnclassifiedExcludedAndStaleFacts()
    {
        const string FixtureRoot = "AttributeRoleExtractionTestFixtures";
        ArchitectureContractDocument document = new()
        {
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                    }
                }
            }
        };
        document.Layers["domain"] = new ArchitectureLayer
        {
            Selector = new ArchitectureLayerSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
            }
        };
        document.Layers["stale"] = new ArchitectureLayer
        {
            Selector = new ArchitectureLayerSelector { Role = "NeverDiscovered" }
        };

        ArchitectureCoverageContract contract = new()
        {
            Name = "semantic-role-coverage",
            Id = "semantic-role-coverage",
            Scope = "semantic_role",
            Roots = { new ArchitectureCoverageRoot { Namespace = FixtureRoot } }
        };
        contract.Exclude.Add(new ArchitectureCoverageExclusion
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "Sales" },
            Reason = "Fixture domain is intentionally exempted."
        });

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);

        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        Assert.That(summary.Scope, Is.EqualTo("semantic_role"));
        Assert.That(summary.Counts.Excluded, Is.GreaterThan(0));
        Assert.That(summary.Counts.Uncovered, Is.GreaterThan(0));
        Assert.That(summary.Counts.Stale, Is.EqualTo(1));
        Assert.That(summary.ExcludedItems, Has.Some.Matches<ArchitectureCoverageSummaryExcludedItem>(item =>
            item.Reason == "Fixture domain is intentionally exempted."));
        Assert.That(summary.StaleItems.Single().Item, Does.Contain("NeverDiscovered"));
    }

    [Test]
    public void CheckCoverageContract_SemanticRoleScope_DistinguishesUnclassifiedFact()
    {
        ArchitectureContractDocument document = new()
        {
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer"
                    }
                }
            }
        };
        ArchitectureCoverageContract contract = new()
        {
            Name = "semantic-role-coverage",
            Id = "semantic-role-coverage",
            Scope = "semantic_role",
            Roots = { new ArchitectureCoverageRoot { Namespace = "AttributeRoleExtractionTestFixtures" } }
        };

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);
        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);

        Assert.That(findings, Has.Some.Matches<ArchitectureViolation>(finding =>
            finding.ForbiddenNamespace == "unclassified semantic fact"));
    }

    [Test]
    public void BuildCoverageSummary_SemanticRoleScope_CoversSalesAndInventoryAndReportsSharedKernelAndUnityConventionGaps()
    {
        ArchitectureContractDocument document = new()
        {
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                    }
                }
            }
        };
        document.Layers["domain"] = new ArchitectureLayer
        {
            Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
        };

        ArchitectureCoverageContract contract = new()
        {
            Name = "sample-semantic-coverage",
            Id = "sample-semantic-coverage",
            Scope = "semantic_role",
            Roots = { new ArchitectureCoverageRoot { Namespace = "SemanticCoverageSampleFixtures" } }
        };

        ArchitectureCoverageSummary summary = RequireSummary(
            new ArchitectureContractRunner(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document)
                .BuildCoverageSummary(contract));

        Assert.That(summary.CoveredItems.Select(item => item.Item), Does.Contain(
            "SemanticCoverageSampleFixtures.Sales.Order"));
        Assert.That(summary.CoveredItems.Select(item => item.Item), Does.Contain(
            "SemanticCoverageSampleFixtures.Inventory.StockItem"));
        Assert.That(summary.UncoveredItems.Select(item => item.Item), Does.Contain(
            "SemanticCoverageSampleFixtures.SharedKernel.Clock"));
        Assert.That(summary.UncoveredItems.Select(item => item.Item), Does.Contain(
            "SemanticCoverageSampleFixtures.Unity.Client.ClientBehaviour"));
    }
}
