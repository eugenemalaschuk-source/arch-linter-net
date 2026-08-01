using ArchLinterNet.Core.BuildState;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #375 follow-up: BuildStateCanonicalHasher previously accepted no CancellationToken at
// all, so BuildStatePreflightEvaluator.CheckReceiptFreshness (and
// BuildStatePreparationService.WriteReceiptsForCurrentArtifacts, which re-hashes to write a
// receipt) could not be interrupted mid-hash — only before/after the whole call. These tests
// prove the token is now honored before any file is touched.
[TestFixture]
public sealed class BuildStateCanonicalHasherCancellationTests
{
    [Test]
    public void ComputeContentDigest_PreCancelledToken_ThrowsWithoutReadingTheFile()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // A path that does not exist: if the token check did not run before File.ReadAllBytes,
        // this would surface as FileNotFoundException instead of OperationCanceledException.
        string missingPath = Path.Combine(Path.GetTempPath(), $"arch-linter-missing-{Guid.NewGuid():N}.dll");

        Assert.Throws<OperationCanceledException>(() =>
            BuildStateCanonicalHasher.ComputeContentDigest(missingPath, cts.Token));
    }

    [Test]
    public void ComputeBuildInputFingerprint_PreCancelledToken_ThrowsBeforeCompletingTheFingerprint()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-hasher-cancel-{Guid.NewGuid():N}");
        string projectDirectory = Path.Combine(directory, "src", "Fixture");
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, "Fixture.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(projectDirectory, "Class1.cs"), "namespace Fixture; public class C {}");

        try
        {
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                BuildStateCanonicalHasher.ComputeBuildInputFingerprint(
                    Path.GetRelativePath(directory, projectPath).Replace(Path.DirectorySeparatorChar, '/'),
                    directory,
                    cts.Token));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ComputeBuildInputFingerprint_TokenNotCancelled_ComputesDeterministicFingerprintAsBefore()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-hasher-cancel-{Guid.NewGuid():N}");
        string projectDirectory = Path.Combine(directory, "src", "Fixture");
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, "Fixture.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(projectDirectory, "Class1.cs"), "namespace Fixture; public class C {}");

        try
        {
            string relativeProjectPath =
                Path.GetRelativePath(directory, projectPath).Replace(Path.DirectorySeparatorChar, '/');

            string first = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(
                relativeProjectPath, directory, CancellationToken.None);
            string second = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(relativeProjectPath, directory);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
