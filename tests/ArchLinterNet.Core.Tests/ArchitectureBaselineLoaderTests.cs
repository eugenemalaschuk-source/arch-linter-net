using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.IO;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

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

    [Test]
    public void MergeAndValidate_ProjectMetadataGroup_AppliesIgnoredViolations()
    {
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract>
                {
                    new()
                    {
                        Name = "project-metadata",
                        Id = "project-metadata",
                        Projects = new List<string> { "src/MyApp/MyApp.csproj" },
                        RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Nullable"] = "enable"
                        }
                    }
                }
            }
        };

        ArchitectureBaselineDocument baseline = new()
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "project-metadata",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "src/MyApp/MyApp.csproj",
                                ForbiddenReference = "friend_assembly:MyApp.Tools",
                                Reason = "known debt"
                            }
                        }
                    }
                }
            }
        };

        ArchitectureBaselineLoadingService.MergeAndValidate(policy, baseline);

        Assert.That(policy.Contracts.StrictProjectMetadata[0].IgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(policy.Contracts.StrictProjectMetadata[0].IgnoredViolations[0].ForbiddenReference,
            Is.EqualTo("friend_assembly:MyApp.Tools"));
    }
}
