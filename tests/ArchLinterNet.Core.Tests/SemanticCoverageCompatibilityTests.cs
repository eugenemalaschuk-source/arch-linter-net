using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SemanticCoverageCompatibilityTests
{
    [Test]
    public void ExcludedItem_PreservesPositionalRecordApi()
    {
        var item = new ArchitectureCoverageSummaryExcludedItem("item", "reason");
        var changed = item with { Evidence = "evidence" };
        (string subject, string reason) = changed;

        Assert.That((subject, reason, changed.Evidence), Is.EqualTo(("item", "reason", "evidence")));
    }
}
