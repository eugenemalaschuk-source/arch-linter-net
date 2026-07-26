using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PublicApiSnapshotFormatTests
{
    private static PublicApiSnapshotDocument Document(params PublicApiSnapshotEntry[] entries)
    {
        return new PublicApiSnapshotDocument(PublicApiSnapshotFormat.CurrentVersion, "module-api", entries);
    }

    [Test]
    public void Serialize_SameSurfaceTwice_IsByteIdentical()
    {
        PublicApiSnapshotDocument document = Document(
            new PublicApiSnapshotEntry("B", "class B.Second"),
            new PublicApiSnapshotEntry("A", "class A.First"));

        Assert.That(PublicApiSnapshotFormat.Serialize(document), Is.EqualTo(PublicApiSnapshotFormat.Serialize(document)));
    }

    [Test]
    public void Serialize_OrdersAssembliesAndSignaturesOrdinallyAndCollapsesDuplicates()
    {
        string serialized = PublicApiSnapshotFormat.Serialize(Document(
            new PublicApiSnapshotEntry("B", "class B.Zeta"),
            new PublicApiSnapshotEntry("A", "class A.Beta"),
            new PublicApiSnapshotEntry("A", "class A.Alpha"),
            new PublicApiSnapshotEntry("A", "class A.Alpha")));

        string[] lines = serialized.Split('\n');
        int firstEntry = Array.IndexOf(lines, "@assembly A");

        Assert.That(lines[firstEntry..(firstEntry + 4)], Is.EqualTo(new[]
        {
            "@assembly A", "class A.Alpha", "class A.Beta", "@assembly B",
        }));
    }

    [Test]
    public void Serialize_UsesLineFeedEndingsAndTrailingNewline()
    {
        string serialized = PublicApiSnapshotFormat.Serialize(Document(new PublicApiSnapshotEntry("A", "class A.Alpha")));

        Assert.Multiple(() =>
        {
            Assert.That(serialized, Does.Not.Contain("\r"));
            Assert.That(serialized, Does.EndWith("\n"));
        });
    }

    [Test]
    public void Serialize_ContainsNoEnvironmentSpecificData()
    {
        string serialized = PublicApiSnapshotFormat.Serialize(Document(new PublicApiSnapshotEntry("A", "class A.Alpha")));

        Assert.Multiple(() =>
        {
            Assert.That(serialized, Does.Not.Contain(Environment.MachineName));
            Assert.That(serialized, Does.Not.Contain(DateTime.UtcNow.Year.ToString()));
            Assert.That(serialized, Does.Not.Contain(Path.DirectorySeparatorChar == '\\' ? "C:\\" : "/Users"));
        });
    }

    [Test]
    public void Parse_RoundTripsSerializedDocument()
    {
        PublicApiSnapshotDocument original = Document(
            new PublicApiSnapshotEntry("A", "class A.Alpha"),
            new PublicApiSnapshotEntry("B", "method B.Beta.Do(System.Int32): System.Void"));

        PublicApiSnapshotDocument parsed = PublicApiSnapshotFormat.Parse(
            PublicApiSnapshotFormat.Serialize(original), "snapshot.txt");

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Version, Is.EqualTo(PublicApiSnapshotFormat.CurrentVersion));
            Assert.That(parsed.ContractId, Is.EqualTo("module-api"));
            Assert.That(parsed.Entries, Is.EqualTo(original.Entries));
        });
    }

    [Test]
    public void Parse_AcceptsCarriageReturnLineFeedInput()
    {
        string content = PublicApiSnapshotFormat.Serialize(Document(new PublicApiSnapshotEntry("A", "class A.Alpha")))
            .Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.That(PublicApiSnapshotFormat.Parse(content, "snapshot.txt").Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void Parse_UnsupportedVersion_Throws()
    {
        string content = $"@format {PublicApiSnapshotFormat.FormatIdentifier}\n@version 2\n";

        Assert.That(
            () => PublicApiSnapshotFormat.Parse(content, "snapshot.txt"),
            Throws.InvalidOperationException.With.Message.Contains("unsupported snapshot version '2'"));
    }

    [Test]
    public void Parse_UnknownDirective_Throws()
    {
        string content = $"@format {PublicApiSnapshotFormat.FormatIdentifier}\n@version 1\n@future value\n";

        Assert.That(
            () => PublicApiSnapshotFormat.Parse(content, "snapshot.txt"),
            Throws.InvalidOperationException.With.Message.Contains("unknown directive '@future'"));
    }

    [Test]
    public void Parse_EntryBeforeAssemblyDirective_Throws()
    {
        string content = $"@format {PublicApiSnapshotFormat.FormatIdentifier}\n@version 1\nclass A.Alpha\n";

        Assert.That(
            () => PublicApiSnapshotFormat.Parse(content, "snapshot.txt"),
            Throws.InvalidOperationException.With.Message.Contains("before any '@assembly' directive"));
    }

    [Test]
    public void Parse_MissingFormatDirective_Throws()
    {
        Assert.That(
            () => PublicApiSnapshotFormat.Parse("@version 1\n", "snapshot.txt"),
            Throws.InvalidOperationException.With.Message.Contains("missing '@format"));
    }

    [Test]
    public void Parse_LineExceedingMaximumLength_Throws()
    {
        string longLine = new('x', PublicApiSnapshotFormat.MaxLineLength + 1);
        string content = $"@format {PublicApiSnapshotFormat.FormatIdentifier}\n@version 1\n@assembly A\n{longLine}\n";

        Assert.That(
            () => PublicApiSnapshotFormat.Parse(content, "snapshot.txt"),
            Throws.InvalidOperationException.With.Message.Contains("maximum supported length"));
    }

    [Test]
    public void Parse_IgnoresCommentAndBlankLines()
    {
        string content = $"# header\n\n@format {PublicApiSnapshotFormat.FormatIdentifier}\n@version 1\n@assembly A\n\n# note\nclass A.Alpha\n";

        Assert.That(PublicApiSnapshotFormat.Parse(content, "snapshot.txt").Entries, Has.Count.EqualTo(1));
    }
}
