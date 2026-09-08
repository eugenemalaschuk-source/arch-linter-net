using ArchLinterNet.Core.IO;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class RepositoryLocalRegularFileReaderTests
{
    private SarifEvidenceTestRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new SarifEvidenceTestRepository();
    }

    [TearDown]
    public void TearDown()
    {
        _repository.Dispose();
    }

    [Test]
    public void OpenRepositoryLocalRegularFile_ContainedRegularFile_ReturnsContents()
    {
        _repository.AddUtf8File("reports/scan.sarif", "contained");

        using Stream stream = ArchitectureFileSystem.Real.OpenRepositoryLocalRegularFile(
            _repository.Root,
            "reports/scan.sarif");
        using var reader = new StreamReader(stream);

        Assert.That(reader.ReadToEnd(), Is.EqualTo("contained"));
    }

    [Test]
    public void OpenRepositoryLocalRegularFile_ParentSegment_IsRejected()
    {
        Assert.That(
            () => ArchitectureFileSystem.Real.OpenRepositoryLocalRegularFile(_repository.Root, "../outside.sarif"),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void OpenRepositoryLocalRegularFile_Directory_IsRejected()
    {
        Directory.CreateDirectory(_repository.GetPath("reports"));

        Assert.That(
            () => ArchitectureFileSystem.Real.OpenRepositoryLocalRegularFile(_repository.Root, "reports"),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void OpenRepositoryLocalRegularFile_FinalSymlinkEscape_IsRejectedWhenSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The Windows CI account cannot be assumed to have symlink creation rights.");
        }

        using var outside = new SarifEvidenceTestRepository();
        string outsidePath = outside.AddUtf8File("outside.sarif", "outside");
        File.CreateSymbolicLink(_repository.GetPath("scan.sarif"), outsidePath);

        Assert.That(
            () => ArchitectureFileSystem.Real.OpenRepositoryLocalRegularFile(_repository.Root, "scan.sarif"),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void OpenRepositoryLocalRegularFile_AncestorSymlinkEscape_IsRejectedWhenSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The Windows CI account cannot be assumed to have symlink creation rights.");
        }

        using var outside = new SarifEvidenceTestRepository();
        outside.AddUtf8File("scan.sarif", "outside");
        Directory.CreateSymbolicLink(_repository.GetPath("linked"), outside.Root);

        Assert.That(
            () => ArchitectureFileSystem.Real.OpenRepositoryLocalRegularFile(_repository.Root, "linked/scan.sarif"),
            Throws.TypeOf<InvalidDataException>());
    }
}
