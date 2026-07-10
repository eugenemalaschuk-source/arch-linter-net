using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SelfArchitecturePolicyTests
{
    [Test]
    public void RepositoryPolicy_ValidatesOwnInternalBoundaries()
    {
        string repoRoot = FindRepoRoot();

        ArchitectureValidationResult result = ArchitectureAssertions
            .FromRepositoryRoot(repoRoot)
            .ValidateStrict();

        result.ShouldPass();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && dir.GetFiles("ArchLinterNet.slnx").Length == 0)
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }
}
