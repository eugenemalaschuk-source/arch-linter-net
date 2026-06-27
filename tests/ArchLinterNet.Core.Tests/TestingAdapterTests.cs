using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class TestingAdapterTests
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
    public void ValidateStrict_CleanPolicy_Passes()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Clean Test
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

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void ShouldPass_WhenFailed_ThrowsInvalidOperationException()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Clean Test
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

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.DoesNotThrow(() => result.ShouldPass());
    }

    [Test]
    public void FromRepositoryRoot_LoadsPolicyFromArchitectureDir()
    {
        string repoDir = Path.Combine(_tempDir, "myrepo");
        string archDir = Path.Combine(repoDir, "architecture");
        Directory.CreateDirectory(archDir);

        string policyPath = Path.Combine(archDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, @"
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

        var result = ArchitectureAssertions.FromRepositoryRoot(repoDir).ValidateStrict();

        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void UnknownConditionSet_ThrowsInvalidOperation()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
  condition_sets:
    runtime: []
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureAssertions
                .FromPolicy(contractPath)
                .WithConditionSet("nonexistent")
                .ValidateStrict());

        Assert.That(ex!.Message, Does.Contain("Unknown condition set"));
        Assert.That(ex.Message, Does.Contain("nonexistent"));
        Assert.That(ex.Message, Does.Contain("runtime"));
    }

    [Test]
    public void ValidateStrict_IndependenceConflict_Fails()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Independence Conflict Test
layers:
  core:
    namespace: ArchLinterNet.Core
  contracts:
    namespace: ArchLinterNet.Core.Contracts
analysis:
  target_assemblies:
    - ArchLinterNet.Core
  policy_consistency: error
contracts:
  strict_independence:
    - name: core-contracts-independent
      layers: [core, contracts]
  strict_allow_only:
    - name: core-allows-contracts
      source: core
      allowed: [contracts]
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.False);
    }
}
