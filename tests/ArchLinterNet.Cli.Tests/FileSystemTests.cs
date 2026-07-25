using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
[NonParallelizable]
public sealed class FileSystemTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "arch-linter-net-fs-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }

    [Test]
    public void WriteAllTextToTemp_WritesToTempPathAndReturnsIt()
    {
        var fs = new FileSystem();
        string filePath = Path.Combine(_tempRoot, "report.json");
        string content = "{\"passed\":true}";

        string tempPath = fs.WriteAllTextToTemp(filePath, content);

        Assert.That(tempPath, Does.EndWith(".tmp"));
        Assert.That(Path.IsPathRooted(tempPath), Is.True);
        Assert.That(File.Exists(tempPath), Is.True);
        Assert.That(File.ReadAllText(tempPath), Is.EqualTo(content));
    }

    [Test]
    public void WriteAllTextToTemp_CreatesMissingDirectory()
    {
        var fs = new FileSystem();
        string missingDir = Path.Combine(_tempRoot, "nested", "deep");
        string filePath = Path.Combine(missingDir, "output.json");
        string content = "hello";

        Assert.That(Directory.Exists(missingDir), Is.False);

        string tempPath = fs.WriteAllTextToTemp(filePath, content);

        Assert.That(Directory.Exists(missingDir), Is.True);
        Assert.That(File.Exists(tempPath), Is.True);
    }

    [Test]
    public void WriteAllTextToTemp_UsesUniqueTempName()
    {
        var fs = new FileSystem();
        string filePath = Path.Combine(_tempRoot, "results.json");

        string temp1 = fs.WriteAllTextToTemp(filePath, "first");
        string temp2 = fs.WriteAllTextToTemp(filePath, "second");

        Assert.That(temp1, Is.Not.EqualTo(temp2));
    }

    [Test]
    public void CanWriteToDirectory_ExistingWritableDirectory_ReturnsTrue()
    {
        var fs = new FileSystem();
        string filePath = Path.Combine(_tempRoot, "results.json");

        Assert.That(fs.CanWriteToDirectory(filePath), Is.True);
    }

    [Test]
    public void CanWriteToDirectory_NonExistentDirectoryCreatable_ReturnsTrue()
    {
        var fs = new FileSystem();
        string dir = Path.Combine(_tempRoot, "will-be-created");
        string filePath = Path.Combine(dir, "results.json");

        Assert.That(Directory.Exists(dir), Is.False);
        Assert.That(fs.CanWriteToDirectory(filePath), Is.True);
        Assert.That(Directory.Exists(dir), Is.True);
    }

    [Test]
    public void CanWriteToDirectory_FileNameOnly_UsesCurrentDirectory()
    {
        var fs = new FileSystem();

        Assert.That(fs.CanWriteToDirectory("somefile.json"), Is.True);
    }

    [Test]
    public void FileExists_DelegatesToFileSystem()
    {
        var fs = new FileSystem();
        string path = Path.Combine(_tempRoot, "existing.txt");
        File.WriteAllText(path, "hello");

        Assert.That(fs.FileExists(path), Is.True);
        Assert.That(fs.FileExists(Path.Combine(_tempRoot, "nonexistent.txt")), Is.False);
    }

    [Test]
    public void WriteAllText_DelegatesToFileSystem()
    {
        var fs = new FileSystem();
        string path = Path.Combine(_tempRoot, "written.txt");
        string content = "test content";

        fs.WriteAllText(path, content);

        Assert.That(File.ReadAllText(path), Is.EqualTo(content));
    }

    [Test]
    public void RenameTempToTarget_DelegatesToFileSystem()
    {
        var fs = new FileSystem();
        string tempPath = Path.Combine(_tempRoot, "source.tmp");
        string targetPath = Path.Combine(_tempRoot, "target.json");
        File.WriteAllText(tempPath, "data");

        fs.RenameTempToTarget(tempPath, targetPath);

        Assert.That(File.Exists(tempPath), Is.False);
        Assert.That(File.Exists(targetPath), Is.True);
        Assert.That(File.ReadAllText(targetPath), Is.EqualTo("data"));
    }

    [Test]
    public void DeleteFile_DelegatesToFileSystem()
    {
        var fs = new FileSystem();
        string path = Path.Combine(_tempRoot, "to-delete.txt");
        File.WriteAllText(path, "delete me");

        fs.DeleteFile(path);

        Assert.That(File.Exists(path), Is.False);
    }
}
