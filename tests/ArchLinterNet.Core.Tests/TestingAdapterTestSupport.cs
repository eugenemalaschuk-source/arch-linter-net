using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Shared temporary repository and baseline policy helpers for TestingAdapter fixtures.
/// This support type intentionally contains no tests.
/// </summary>
public abstract class TestingAdapterTestSupport
{
    protected string TempDirectory { get; private set; } = null!;

    [SetUp]
    public void SetUpTestingAdapterFixture()
    {
        TempDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(TempDirectory);
    }

    [TearDown]
    public void TearDownTestingAdapterFixture()
    {
        if (Directory.Exists(TempDirectory))
        {
            Directory.Delete(TempDirectory, true);
        }
    }

    protected string WriteSelfForbiddenPolicy()
    {
        string contractDir = Path.Combine(TempDirectory, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Builder Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict:
    - id: self-forbidden
      name: core-must-not-depend-on-itself
      source: core
      forbidden: [core]
    - id: harmless
      name: harmless-rule
      source: core
      forbidden: []
");
        return contractPath;
    }
}
