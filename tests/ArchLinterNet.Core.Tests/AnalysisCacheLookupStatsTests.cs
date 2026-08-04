using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AnalysisCacheLookupStatsTests
{
    [Test]
    public void RecordLookup_MissingEntry_IsMissButNotRejectReason()
    {
        AnalysisCacheLookupStats stats = new();

        stats.RecordLookup(AnalysisCacheLookupResult.Miss(AnalysisCacheRejectReason.Missing));

        Assert.Multiple(() =>
        {
            Assert.That(stats.Lookups, Is.EqualTo(1));
            Assert.That(stats.Misses, Is.EqualTo(1));
            Assert.That(stats.Rejects, Is.EqualTo(0));
            Assert.That(stats.RejectReasonCounts, Is.Empty);
        });
    }

    [Test]
    public void RecordLookup_IneligibleProjectUnits_AreIncludedInSnapshot()
    {
        AnalysisCacheLookupStats stats = new();

        stats.RecordLookup(
            AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IneligibleBuildInput),
            ineligibleUnitCount: 3);

        Assert.That(stats.Snapshot().IneligibleUnitCount, Is.EqualTo(3));
    }
}
