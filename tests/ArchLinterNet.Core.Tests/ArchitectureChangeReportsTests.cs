using ArchLinterNet.Core.Change;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureChangeReportsTests
{
    [Test]
    public void Compare_SeparatesNewSurfacesFindingsAndExistingDebtInDeterministicOrder()
    {
        ArchitectureChangeSnapshot baseline = Snapshot(
            new ArchitectureChangeEntry("namespace", "Acme.Legacy", "Acme.Legacy"),
            new ArchitectureChangeEntry("dependency_edge", "namespace:Acme.A->Acme.B", "Acme.A -> Acme.B"),
            findings: new[] { new ArchitectureChangeFinding("known", "dependency", "known finding") },
            debt: new[] { "known" });
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
            debt: new[] { "known" });

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current);

        Assert.Multiple(() =>
        {
            Assert.That(report.Added.Select(static entry => entry.Identity), Is.EqualTo(new[]
            {
                "coverage|namespace|uncovered|Acme.New",
                "namespace:Acme.New->Acme.B",
                "Acme.New",
                "Order|bounded_context|Sales",
            }));
            Assert.That(report.Removed.Select(static entry => entry.Identity), Is.EqualTo(new[]
            {
                "namespace:Acme.A->Acme.B",
                "Acme.Legacy",
            }));
            Assert.That(report.NewFindings.Select(static finding => finding.Identity), Is.EqualTo(new[] { "new" }));
            Assert.That(report.ExistingFindings.Select(static finding => finding.Identity), Is.EqualTo(new[] { "known" }));
            Assert.That(report.BaselineDebt, Is.EqualTo(new[] { "known" }));
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
            Assert.That(restored.Entries.Select(static entry => entry.Identity), Is.EqualTo(new[] { "Alpha", "Zeta" }));
            Assert.That(ArchitectureChangeReports.FormatJson(ArchitectureChangeReports.Compare(restored, restored)), Does.Contain("new_findings"));
        });
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

    private static ArchitectureChangeSnapshot Snapshot(
        ArchitectureChangeEntry? first = null,
        ArchitectureChangeEntry? second = null,
        ArchitectureChangeEntry? third = null,
        ArchitectureChangeEntry? fourth = null,
        IReadOnlyList<ArchitectureChangeFinding>? findings = null,
        IReadOnlyList<string>? debt = null)
    {
        return new ArchitectureChangeSnapshot(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            "strict",
            new[] { first, second, third, fourth }.OfType<ArchitectureChangeEntry>().ToArray(),
            findings ?? Array.Empty<ArchitectureChangeFinding>(),
            debt ?? Array.Empty<string>());
    }
}
