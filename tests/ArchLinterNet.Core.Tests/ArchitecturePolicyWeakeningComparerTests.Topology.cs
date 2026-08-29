using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitecturePolicyWeakeningComparerTests
{
    [Test]
    public void Compare_AddedReviewedTopologyOutOfScope_IsSemanticWeakening()
    {
        ArchitecturePolicyContextTopology baselineTopology = Topology([]);
        ArchitecturePolicyContextTopology currentTopology = Topology([
            new ArchitecturePolicyContextTopologyOutOfScope(
                "generated",
                new ArchitecturePolicyContextTopologySelector("namespace", "Sample.Generated", "", null, _importedProvenance),
                "Generated code is separately reviewed.",
                _importedProvenance),
        ]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context() with { Topology = baselineTopology },
            Context() with { Topology = currentTopology })).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("topology_out_of_scope_added"));
            Assert.That(finding.Classification, Is.EqualTo("semantic"));
            Assert.That(finding.ControlIdentity, Is.EqualTo("topology:generated"));
            Assert.That(finding.CurrentProvenance, Is.EqualTo(_importedProvenance));
        });
    }

    [Test]
    public void Compare_BroadenedLiteralTopologyNamespaceExclusion_IsSemanticWeakening()
    {
        ArchitecturePolicyContextTopology baselineTopology = Topology([
            new ArchitecturePolicyContextTopologyOutOfScope(
                "generated",
                new ArchitecturePolicyContextTopologySelector("namespace", "Sample.Generated.Proxy", "", null, _importedProvenance),
                "Generated code is separately reviewed.",
                _importedProvenance),
        ]);
        ArchitecturePolicyContextTopology currentTopology = Topology([
            new ArchitecturePolicyContextTopologyOutOfScope(
                "generated",
                new ArchitecturePolicyContextTopologySelector("namespace", "Sample.Generated", "", null, _importedProvenance),
                "Generated code is separately reviewed.",
                _importedProvenance),
        ]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context() with { Topology = baselineTopology },
            Context() with { Topology = currentTopology })).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("topology_out_of_scope_broadened"));
            Assert.That(finding.Classification, Is.EqualTo("semantic"));
        });
    }

    [Test]
    public void Compare_DelimiterBearingContextMetadataChange_RemainsImpactNotProven()
    {
        ArchitecturePolicyContextTopology baselineTopology = Topology([
            new ArchitecturePolicyContextTopologyOutOfScope(
                "generated",
                ContextSelector(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "b,c=d" }),
                "Generated code is separately reviewed.",
                _importedProvenance),
        ]);
        ArchitecturePolicyContextTopology currentTopology = Topology([
            new ArchitecturePolicyContextTopologyOutOfScope(
                "generated",
                ContextSelector(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["a"] = "b",
                    ["c"] = "d",
                }),
                "Generated code is separately reviewed.",
                _importedProvenance),
        ]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context() with { Topology = baselineTopology },
            Context() with { Topology = currentTopology })).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("topology_out_of_scope_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.BaseValues.Single(), Does.Contain("b,c=d"));
            Assert.That(finding.CurrentValues.Single(), Does.Contain("\"c\":\"d\""));
        });
    }

    private static ArchitecturePolicyContextTopology Topology(
        IReadOnlyList<ArchitecturePolicyContextTopologyOutOfScope> outOfScope) => new(
        "exhaustive",
        "type",
        false,
        [new ArchitecturePolicyContextTopologySelector("layer", "application", "", null, _importedProvenance)],
        [new ArchitecturePolicyContextTopologyNode("application", [], _importedProvenance)],
        [],
        outOfScope,
        false,
        _importedProvenance);

    private static ArchitecturePolicyContextTopologySelector ContextSelector(
        IReadOnlyDictionary<string, string> metadata) => new(
        "context",
        string.Empty,
        string.Empty,
        new ArchitecturePolicyContextSelector("context", "DomainLayer", metadata, null),
        _importedProvenance);
}
