using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineLoaderTests
{
    private readonly ArchitectureBaselineLoadingService _service = new(ArchitectureFileSystem.Real);

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public void LoadFromPath_MissingFile_ThrowsFileNotFound()
    {
        string missingPath = Path.Combine(_tempDir, "nonexistent.yml");
        Assert.Throws<FileNotFoundException>(() =>
            _service.LoadFromPath(missingPath));
    }

    [Test]
    public void LoadFromPath_InvalidVersion_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 999
baseline:
  strict: []
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("version"));
    }

    [Test]
    public void LoadFromPath_EmptyId_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 1
baseline:
  strict:
    - id: ''
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: test
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("id"));
    }

    [Test]
    public void LoadFromPath_EmptySourceType_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 1
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: ''
          forbidden_reference: Bad.Type
          reason: test
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("source_type"));
    }

    [Test]
    public void LoadFromPath_EmptyForbiddenReference_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 1
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: ''
          reason: test
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("forbidden_reference"));
    }
}
