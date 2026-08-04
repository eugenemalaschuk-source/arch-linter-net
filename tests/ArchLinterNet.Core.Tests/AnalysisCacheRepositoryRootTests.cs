using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Review finding #2: ValidateCommandHandler.Cache.cs and ArchitectureValidationBuilder used to
// derive "repository root" via Path.GetDirectoryName(policyPath). For the normal self-policy path
// `<repo>/architecture/dependencies.arch.yml`, that derivation yields `<repo>/architecture` — wrong
// by one directory segment, since ArchitectureRepositoryRootResolver.ResolveFrom strips a
// conventional "architecture/" policy subfolder and DiscoveredProjectPaths are resolved against the
// real repository root, not the policy's own directory.
//
// ValidationOutcome now carries the authoritative RepositoryRoot the pipeline itself resolved (see
// ArchitectureAnalysisSnapshot's constructor and EvaluateCore/BuildBlockedOutcome), so CLI/Testing
// cache population and lookup consume that value directly instead of re-deriving it. This test
// proves the fix at the seam that actually matters: for a policy at
// `<repo>/architecture/dependencies.arch.yml`, ValidationOutcome.RepositoryRoot equals `<repo>`,
// never `<repo>/architecture`.
[TestFixture]
public sealed class AnalysisCacheRepositoryRootTests
{
    private string _repositoryRoot = null!;
    private string _policyPath = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-repo-root-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_repositoryRoot, "architecture"));
        _policyPath = Path.Combine(_repositoryRoot, "architecture", "dependencies.arch.yml");
        File.WriteAllText(_policyPath, """
            version: 1
            name: Test

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution

            analysis:
              target_assemblies: [ArchLinterNet.Core]
            """);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repositoryRoot))
        {
            Directory.Delete(_repositoryRoot, recursive: true);
        }
    }

    [Test]
    public void ValidationOutcome_RepositoryRoot_IsRepoRootNotArchitectureSubfolder()
    {
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        ValidationRequest request = new()
        {
            PolicyPath = _policyPath,
            Mode = "strict",
        };

        ValidationOutcome outcome = engine.Validate(request);

        // The bug this finding named: Path.GetDirectoryName(policyPath) would produce
        // "<repo>/architecture" here, one segment too deep.
        string wrongLegacyDerivation = Path.GetDirectoryName(Path.GetFullPath(_policyPath))!;
        string normalizedActual = Path.GetFullPath(outcome.RepositoryRoot).TrimEnd(Path.DirectorySeparatorChar);
        string normalizedExpected = Path.GetFullPath(_repositoryRoot).TrimEnd(Path.DirectorySeparatorChar);
        string normalizedWrong = Path.GetFullPath(wrongLegacyDerivation).TrimEnd(Path.DirectorySeparatorChar);

        Assert.That(normalizedActual, Is.Not.EqualTo(normalizedWrong).IgnoreCase);
        Assert.That(normalizedActual, Is.EqualTo(normalizedExpected).IgnoreCase);
    }

    [Test]
    public void ValidationOutcome_RepositoryRoot_IsPopulatedForCombinedModeSnapshotToo()
    {
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        AnalysisSnapshotRequest request = new() { PolicyPath = _policyPath };

        using ArchitectureAnalysisSnapshot snapshot = engine.CreateSnapshot(request);
        ValidationOutcome strict = snapshot.Evaluate("strict");
        ValidationOutcome audit = snapshot.Evaluate("audit");

        Assert.That(strict.RepositoryRoot, Is.Not.Empty);
        Assert.That(strict.RepositoryRoot, Is.EqualTo(audit.RepositoryRoot));
        Assert.That(strict.RepositoryRoot, Is.EqualTo(snapshot.RepositoryRoot));
    }
}
