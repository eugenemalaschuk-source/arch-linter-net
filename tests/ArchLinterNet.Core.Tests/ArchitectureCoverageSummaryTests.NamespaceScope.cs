using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Namespace-scope coverage tests split out of ArchitectureCoverageSummaryTests.cs to stay
// under the repository's 800-line file-size lint threshold. Shared fixtures/helpers
// (CreateNamespaceContract, CreateNamespaceDocument, FeatureRoot, CreateContext,
// RequireSummary) live in the main partial-class file and are used here as-is.
public sealed partial class ArchitectureCoverageSummaryTests
{
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
    public void BuildCoverageSummary_NamespaceScope_NamespaceIncludedThenExcludedByLayer_ClassifiedAsExcludedNotUncovered()
    {
        // Regression for PR #384 review: issue #356 requires coverage to distinguish "not
        // included" from "included then excluded". AlphaGap is matched by "alpha_excluded"'s
        // namespace pattern, then subtracted by that layer's own exclude entry - it must land in
        // ExcludedItems (with a reason naming the layer/pattern), not ordinary UncoveredItems.
        // ZetaGap, which no declared layer's pattern ever matches at all, stays genuinely
        // Uncovered ("not included") - the two must not be conflated.
        ArchitectureContractDocument document = CreateNamespaceDocument();
        document.Layers["alpha_excluded"] = new ArchitectureLayer
        {
            Namespace = $"{FeatureRoot}.AlphaGap",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = $"{FeatureRoot}.AlphaGap" }
            }
        };
        ArchitectureCoverageContract contract = CreateNamespaceContract();

        ArchitectureContractRunner runner = new(CreateContext(typeof(ArchitectureCoverageSummaryTests)), document);
        ArchitectureCoverageSummary summary = RequireSummary(runner.BuildCoverageSummary(contract));

        ArchitectureCoverageSummaryExcludedItem excludedAlpha = summary.ExcludedItems
            .Single(i => i.Item == $"{FeatureRoot}.AlphaGap");
        Assert.Multiple(() =>
        {
            Assert.That(excludedAlpha.Reason, Does.Contain("alpha_excluded"));
            Assert.That(excludedAlpha.Reason, Does.Contain("layer"));
            Assert.That(summary.UncoveredItems.Select(i => i.Item), Does.Contain($"{FeatureRoot}.ZetaGap"));
            Assert.That(summary.UncoveredItems.Select(i => i.Item), Does.Not.Contain($"{FeatureRoot}.AlphaGap"));
        });
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
            selectedContractIds: new HashSet<string>(_someOtherContractId, StringComparer.OrdinalIgnoreCase));

        ArchitectureCoverageSummary? summary = runner.BuildCoverageSummary(contract);

        Assert.That(summary, Is.Null);
    }
}
