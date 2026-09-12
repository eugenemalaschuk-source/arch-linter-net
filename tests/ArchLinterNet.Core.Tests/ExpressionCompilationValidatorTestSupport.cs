using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Shared temporary-policy lifecycle for expression compilation fixtures.
/// This type intentionally contains no tests.
/// </summary>
public abstract class ExpressionCompilationValidatorTestSupport
{
    protected string TempDirectory { get; private set; } = null!;

    protected static string AssemblyName => typeof(ExpressionCompilationValidatorTests).Assembly.GetName().Name!;

    [SetUp]
    public void SetUpExpressionCompilationFixture()
    {
        TempDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-expression-compilation-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(TempDirectory);
    }

    [TearDown]
    public void TearDownExpressionCompilationFixture()
    {
        if (Directory.Exists(TempDirectory))
        {
            Directory.Delete(TempDirectory, true);
        }
    }

    protected string WritePolicy(string yaml, string fileName = "dependencies.arch.yml")
    {
        string path = Path.Combine(TempDirectory, fileName);
        File.WriteAllText(path, yaml);
        return path;
    }
}
