using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePrReportProjectorTests
{
    [Test]
    public void Project_ClassifiesEveryNonHealthyDimensionAndPreservesCanonicalReason()
    {
        ArchitectureHealthReason debtReason = new("metadata_incomplete", "waiver_lifecycle")
        {
            Family = "waiver",
            ControlIdentity = "waiver:one",
            PolicyIdentity = "policy.yml:rules[0]",
            EvidenceIdentity = "waiver-one",
        };
        ArchitectureHealthReason degradingReason = new("stale_metadata", "waiver_lifecycle");
        ArchitectureHealthReason failReason = new("violations", "current_evaluation")
        {
            ControlIdentity = "dependencies:one",
        };
        ArchitectureHealthReason unassessableReason = new("missing_input", "applicability")
        {
            EvidenceIdentity = "applicability:one",
        };
        ArchitectureHealthSummary summary = new(
            ArchitectureHealthSummary.CurrentSchemaId,
            ArchitectureHealthGate.Pass,
            ArchitectureHealthState.Degrading,
            [
                new("healthy", ArchitectureHealthDimensionState.Pass, [new("ignored", "health")]),
                new("optional", ArchitectureHealthDimensionState.NotConfigured, [new("ignored", "optional")]),
                new("out_of_scope", ArchitectureHealthDimensionState.NotApplicable, [new("ignored", "scope")]),
                new("waiver_debt", ArchitectureHealthDimensionState.Debt, [debtReason]),
                new("waiver_metadata", ArchitectureHealthDimensionState.Degrading, [degradingReason]),
                new("evaluation", ArchitectureHealthDimensionState.Fail, [failReason]),
                new("applicability", ArchitectureHealthDimensionState.Unassessable, [unassessableReason]),
            ]);

        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.Project(
            new ArchitecturePrReportInput(summary, null, Change()));

        Assert.Multiple(() =>
        {
            Assert.That(projection.Headline.Gate, Is.EqualTo(ArchitectureHealthGate.Pass));
            Assert.That(projection.Headline.Health, Is.EqualTo(ArchitectureHealthState.Degrading));
            Assert.That(projection.Headline.DimensionExplanations.Select(item => item.Dimension), Is.EqualTo(
                ["applicability", "evaluation", "waiver_debt", "waiver_metadata"]));
            Assert.That(projection.Headline.DimensionExplanations.Where(item => item.IsBlocking)
                .Select(item => item.Dimension), Is.EqualTo(["applicability", "evaluation"]));

            ArchitecturePrReportDimensionExplanation preserved = projection.Headline.DimensionExplanations
                .Single(item => item.Dimension == "waiver_debt");
            Assert.That(preserved.State, Is.EqualTo(ArchitectureHealthDimensionState.Debt));
            Assert.That(preserved.Code, Is.EqualTo("metadata_incomplete"));
            Assert.That(preserved.Source, Is.EqualTo("waiver_lifecycle"));
            Assert.That(preserved.Family, Is.EqualTo("waiver"));
            Assert.That(preserved.ControlIdentity, Is.EqualTo("waiver:one"));
            Assert.That(preserved.PolicyIdentity, Is.EqualTo("policy.yml:rules[0]"));
            Assert.That(preserved.EvidenceIdentity, Is.EqualTo("waiver-one"));
        });
    }

    [Test]
    public void Project_DoesNotRecomputeGateOrHealthFromDimensionExplanations()
    {
        ArchitectureHealthSummary summary = new(
            ArchitectureHealthSummary.CurrentSchemaId,
            ArchitectureHealthGate.Pass,
            ArchitectureHealthState.Healthy,
            [new("dimension", ArchitectureHealthDimensionState.Fail, [new("canonical", "source")])]);

        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.Project(
            new ArchitecturePrReportInput(summary, null, Change()));

        Assert.Multiple(() =>
        {
            Assert.That(projection.Headline.Gate, Is.EqualTo(ArchitectureHealthGate.Pass));
            Assert.That(projection.Headline.Health, Is.EqualTo(ArchitectureHealthState.Healthy));
            Assert.That(projection.Headline.DimensionExplanations.Single().IsBlocking, Is.True);
        });
    }

    [Test]
    public void Project_StoresSafeNavigationContextAndDerivesCommitBoundSourceUrl()
    {
        string sha = new('a', 40);
        var context = new ArchitecturePrReportNavigationContext(
            "https://github.com/example/repository/",
            sha,
            "https://github.com/example/repository/actions/runs/123");
        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.Project(
            new ArchitecturePrReportInput(Summary(), null, Change()), context);

        Assert.Multiple(() =>
        {
            Assert.That(projection.NavigationContext, Is.Not.Null);
            Assert.That(projection.NavigationContext!.ArtifactUrl,
                Is.EqualTo("https://github.com/example/repository/actions/runs/123"));
            Assert.That(projection.NavigationContext.GetSourceUrl("src/Some File.cs"),
                Is.EqualTo($"https://github.com/example/repository/blob/{sha}/src/Some%20File.cs"));
            Assert.That(projection.NavigationContext.GetSourceUrl("../secrets.txt"), Is.Null);
        });
    }

    [Test]
    public void Project_OmitsUnsafeNavigationContextWithoutChangingCanonicalSummary()
    {
        ArchitectureHealthSummary summary = Summary();
        var context = new ArchitecturePrReportNavigationContext(
            "javascript:alert(1)",
            new string('b', 40),
            "https://evil.example/report");

        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.Project(
            new ArchitecturePrReportInput(summary, null, Change()), context);

        Assert.Multiple(() =>
        {
            Assert.That(projection.NavigationContext, Is.Null);
            Assert.That(projection.Headline.Gate, Is.EqualTo(summary.Gate));
            Assert.That(projection.Headline.Health, Is.EqualTo(summary.Health));
        });
    }

    private static ArchitectureHealthSummary Summary() => new(
        ArchitectureHealthSummary.CurrentSchemaId,
        ArchitectureHealthGate.Pass,
        ArchitectureHealthState.Healthy,
        [new("dimension", ArchitectureHealthDimensionState.Pass, [])]);

    private static ArchitecturePrReportChange Change() => new(
        new ArchitecturePrReportExecutionContext("run", "ci"),
        "strict",
        [],
        [],
        [],
        [],
        [],
        []);
}
