using ArchLinterNet.Core.Change;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureChangeReportsTests
{
    private static readonly string[] _knownDebtIdentities = { "known" };
    private static readonly string[] _resolvedFindingIdentities = { "resolved" };
    private static readonly string[] _frozenDebtIdentities = { "frozen-debt" };
    private static readonly string[] _newFindingIdentities = { "new" };
    private static readonly string[] _addedEntryIdentities =
    {
        "coverage|namespace|uncovered|Acme.New",
        "namespace:Acme.New->Acme.B",
        "Acme.New",
        "Order|bounded_context|Sales",
    };
    private static readonly string[] _removedEntryIdentities =
    {
        "namespace:Acme.A->Acme.B",
        "Acme.Legacy",
    };
    private static readonly string[] _sortedNamespaceIdentities = { "Alpha", "Zeta" };

    [Test]
    public void Compare_SeparatesNewSurfacesFindingsAndExistingDebtInDeterministicOrder()
    {
        ArchitectureChangeSnapshot baseline = Snapshot(
            new ArchitectureChangeEntry("namespace", "Acme.Legacy", "Acme.Legacy"),
            new ArchitectureChangeEntry("dependency_edge", "namespace:Acme.A->Acme.B", "Acme.A -> Acme.B"),
            findings: new[]
            {
                new ArchitectureChangeFinding("known", "dependency", "known finding"),
                new ArchitectureChangeFinding("resolved", "dependency", "resolved finding"),
            },
            debt: _knownDebtIdentities);
        ArchitectureChangeSnapshot current = Snapshot(
            new ArchitectureChangeEntry("semantic_context", "Order|bounded_context|Sales", "Order: bounded_context=Sales"),
            new ArchitectureChangeEntry("namespace", "Acme.New", "Acme.New"),
            new ArchitectureChangeEntry("coverage_blind_spot", "coverage|namespace|uncovered|Acme.New", "uncovered namespace: Acme.New"),
            new ArchitectureChangeEntry("dependency_edge", "namespace:Acme.New->Acme.B", "Acme.New -> Acme.B"),
            findings: new[]
            {
                new ArchitectureChangeFinding("new", "dependency", "new finding"),
                new ArchitectureChangeFinding("known", "dependency", "known finding"),
            },
            debt: _knownDebtIdentities);

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current);

        Assert.Multiple(() =>
        {
            Assert.That(report.Added.Select(static entry => entry.Identity), Is.EqualTo(_addedEntryIdentities));
            Assert.That(report.Removed.Select(static entry => entry.Identity), Is.EqualTo(_removedEntryIdentities));
            Assert.That(report.NewFindings.Select(static finding => finding.Identity), Is.EqualTo(_newFindingIdentities));
            Assert.That(report.ExistingFindings.Select(static finding => finding.Identity), Is.EqualTo(_knownDebtIdentities));
            Assert.That(report.ResolvedFindings.Select(static finding => finding.Identity), Is.EqualTo(_resolvedFindingIdentities));
            Assert.That(report.BaselineDebt, Is.EqualTo(_knownDebtIdentities));
        });
    }

    [Test]
    public void SerializeAndDeserialize_RetainsOrderedVersionedSnapshot()
    {
        ArchitectureChangeSnapshot snapshot = Snapshot(
            new ArchitectureChangeEntry("namespace", "Zeta", "Zeta"),
            new ArchitectureChangeEntry("namespace", "Alpha", "Alpha"),
            findings: new[] { new ArchitectureChangeFinding("z", "dependency", "z") });

        ArchitectureChangeSnapshot restored = ArchitectureChangeReports.DeserializeSnapshot(
            ArchitectureChangeReports.SerializeSnapshot(snapshot));

        Assert.Multiple(() =>
        {
            Assert.That(restored.SchemaVersion, Is.EqualTo(ArchitectureChangeSnapshot.CurrentSchemaVersion));
            Assert.That(restored.Entries.Select(static entry => entry.Identity), Is.EqualTo(_sortedNamespaceIdentities));
            Assert.That(ArchitectureChangeReports.FormatJson(ArchitectureChangeReports.Compare(restored, restored)),
                Does.Contain("resolved_findings"));
        });
    }

    [Test]
    public void Compare_ReportsBaseOnlyFindingsAsResolvedWithoutOverlappingBuckets()
    {
        ArchitectureChangeSnapshot baseline = Snapshot(
            findings: new[]
            {
                new ArchitectureChangeFinding("z-resolved", "dependency", "z"),
                new ArchitectureChangeFinding("a-existing", "dependency", "a"),
            });
        ArchitectureChangeSnapshot current = Snapshot(
            findings: new[]
            {
                new ArchitectureChangeFinding("a-existing", "dependency", "a"),
                new ArchitectureChangeFinding("new", "dependency", "new"),
            });

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current);

        Assert.Multiple(() =>
        {
            Assert.That(report.NewFindings.Select(static finding => finding.Identity), Is.EqualTo(["new"]));
            Assert.That(report.ExistingFindings.Select(static finding => finding.Identity), Is.EqualTo(["a-existing"]));
            Assert.That(report.ResolvedFindings.Select(static finding => finding.Identity), Is.EqualTo(["z-resolved"]));
            Assert.That(report.NewFindings.Concat(report.ExistingFindings).Concat(report.ResolvedFindings)
                .Select(static finding => finding.Identity).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));
        });

        Assert.That(ArchitectureChangeReports.FormatHuman(report), Does.Contain("Resolved findings: 1"));
    }

    [Test]
    public void Compare_RejectsIncompatibleModesAndInvalidInput()
    {
        ArchitectureChangeSnapshot strict = Snapshot();
        ArchitectureChangeSnapshot audit = strict with { Mode = "audit" };

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitectureChangeReports.Compare(strict, audit), Throws.ArgumentException);
            Assert.That(() => ArchitectureChangeReports.DeserializeSnapshot("{}"), Throws.ArgumentException);
        });
    }

    [Test]
    public void Compare_RejectsSnapshotsFromDifferentConditionSets()
    {
        ArchitectureChangeSnapshot baseline = Snapshot(conditionSetName: "ci");
        ArchitectureChangeSnapshot current = Snapshot(conditionSetName: "developer");

        Assert.That(() => ArchitectureChangeReports.Compare(baseline, current), Throws.ArgumentException);
    }

    [TestCase("{\"snapshot_kind\":\"architecture-change-snapshot\",\"schema_version\":2,\"mode\":\"strict\",\"condition_set_name\":\"\",\"findings\":[],\"baseline_debt\":[]}")]
    [TestCase("{\"snapshot_kind\":\"architecture-change-snapshot\",\"schema_version\":2,\"mode\":\"strict\",\"condition_set_name\":\"\",\"entries\":[],\"baseline_debt\":[]}")]
    [TestCase("{\"snapshot_kind\":\"architecture-change-snapshot\",\"schema_version\":2,\"mode\":\"strict\",\"condition_set_name\":\"\",\"entries\":[],\"findings\":[]}")]
    public void DeserializeSnapshot_RejectsTruncatedAuthoritativeArtifact(string json)
    {
        Assert.That(() => ArchitectureChangeReports.DeserializeSnapshot(json), Throws.ArgumentException);
    }

    [Test]
    public void Compare_BaseBaselineDebtMakesCurrentFindingExisting()
    {
        ArchitectureChangeSnapshot baseline = Snapshot(debt: _frozenDebtIdentities);
        ArchitectureChangeSnapshot current = Snapshot(
            findings: new[] { new ArchitectureChangeFinding("frozen-debt", "dependency", "known debt") });

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current);

        Assert.Multiple(() =>
        {
            Assert.That(report.NewFindings, Is.Empty);
            Assert.That(report.ExistingFindings.Select(static finding => finding.Identity), Is.EqualTo(_frozenDebtIdentities));
        });
    }

    private static ArchitectureChangeSnapshot Snapshot(
        ArchitectureChangeEntry? first = null,
        ArchitectureChangeEntry? second = null,
        ArchitectureChangeEntry? third = null,
        ArchitectureChangeEntry? fourth = null,
        IReadOnlyList<ArchitectureChangeFinding>? findings = null,
        IReadOnlyList<string>? debt = null,
        string conditionSetName = "")
    {
        return new ArchitectureChangeSnapshot(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            "strict",
            conditionSetName,
            new[] { first, second, third, fourth }.OfType<ArchitectureChangeEntry>().ToArray(),
            findings ?? Array.Empty<ArchitectureChangeFinding>(),
            debt ?? Array.Empty<string>());
    }
}
