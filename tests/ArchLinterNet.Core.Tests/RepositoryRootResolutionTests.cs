using ArchLinterNet.Core;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class RepositoryRootResolutionTests
{
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
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void Validator_ResolvesRootFromArchitectureSubdirectory()
    {
        string repoDir = Path.Combine(_tempDir, "myrepo");
        string archDir = Path.Combine(repoDir, "architecture");
        Directory.CreateDirectory(archDir);

        string contractPath = Path.Combine(archDir, "dependencies.arch.yml");
        File.WriteAllText(contractPath, @"
version: 1
name: Root Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
");

        bool result = ArchitectureValidator.Validate(contractPath);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Validator_ResolvesRootFromRepoRoot()
    {
        string repoDir = Path.Combine(_tempDir, "myrepo");
        Directory.CreateDirectory(repoDir);

        string contractPath = Path.Combine(repoDir, "dependencies.arch.yml");
        File.WriteAllText(contractPath, @"
version: 1
name: Root Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
");

        bool result = ArchitectureValidator.Validate(contractPath);

        Assert.That(result, Is.True);
    }
}
