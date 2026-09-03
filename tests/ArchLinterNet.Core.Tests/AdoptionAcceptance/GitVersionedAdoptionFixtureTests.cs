using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class GitVersionedAdoptionFixtureTests
{
    [Test]
    public void TwoCommitsAreIndependentlyCheckoutableAndCurrentIsNotSharedStateWithBase()
    {
        using GitVersionedAdoptionFixture fixture = GitVersionedAdoptionFixture.Create("modular-consumer");

        string marker = Path.Combine(fixture.Root, "base-current-wiring.marker");
        File.WriteAllText(marker, "base");
        string basePath = fixture.Commit("base");

        File.WriteAllText(marker, "current");
        string currentPath = fixture.Commit("current");

        Assert.That(currentPath, Is.Not.EqualTo(basePath),
            "The base and current commits must be genuinely different revisions.");

        // The working tree is mutable, real state (not two isolated snapshots): reading it back
        // after both commits reflects "current", proving the same fixture root is reused across
        // both revisions rather than each commit producing its own disconnected copy.
        Assert.That(File.ReadAllText(marker), Is.EqualTo("current"));

        string baseBlob = GitShow(fixture.Root, $"{basePath}:base-current-wiring.marker");
        string currentBlob = GitShow(fixture.Root, $"{currentPath}:base-current-wiring.marker");
        Assert.That(baseBlob, Is.EqualTo("base"),
            "The base commit's own tree must still read back its own content independently of the current working tree state.");
        Assert.That(currentBlob, Is.EqualTo("current"));
    }

    private static string GitShow(string workingDirectory, string revisionPath)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        startInfo.ArgumentList.Add("show");
        startInfo.ArgumentList.Add(revisionPath);
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0
            ? output
            : throw new InvalidOperationException($"git show {revisionPath} failed: {error}");
    }
}
