using ArchLinterNet.Core.History;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryRepositoryLayoutTests
{
    [Test]
    public void Sha256RepositoriesResolveTheSixtyFourCharacterHexDigestLength()
    {
        using GitTestRepository repository = GitTestRepository.CreateWithObjectFormat("sha256");
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);

        Assert.That(result.ObjectFormatName, Is.EqualTo("sha256"));
        Assert.That(result.ResolvedTo, Has.Length.EqualTo(64));
    }

    [Test]
    public void AnUnsupportedDeclaredObjectFormatFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        string configPath = Path.Combine(repository.Path, ".git", "config");
        File.AppendAllText(configPath, "\n[extensions]\n\tobjectformat = sha3\n");

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, "HEAD");

        Assert.That(diagnostic.KindText, Is.EqualTo("unsupported_object_format"));
    }

    [Test]
    public void ABareRepositoryIsDiscoveredAsItsOwnGitDirectory()
    {
        string barePath = Path.Combine(Path.GetTempPath(), "arch-linter-history-bare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(barePath);
        try
        {
            RunGit(barePath, "init", "-q", "-b", "main", "--bare");
            using GitTestRepository worktree = GitTestRepository.Create();
            worktree.Write("a.txt", "one\n");
            string first = worktree.Commit("first");
            worktree.Write("a.txt", "one\ntwo\n");
            string second = worktree.Commit("second");
            RunGit(worktree.Path, "push", "-q", barePath, "main");

            HistoryIngestionResult result = HistoryIngestionFixture.Succeed(barePath, first, second);

            Assert.That(result.Commits, Has.Count.EqualTo(1));
        }
        finally
        {
            GitTestRepository.DeleteDirectoryRecursively(barePath);
        }
    }

    [Test]
    public void NoRepositoryAnywhereAboveTheStartDirectoryFailsClosed()
    {
        string plainDirectory = Path.Combine(Path.GetTempPath(), "arch-linter-history-no-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(plainDirectory);
        try
        {
            HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(plainDirectory, "HEAD", "HEAD");

            Assert.That(diagnostic.KindText, Is.EqualTo("repository_not_found"));
        }
        finally
        {
            Directory.Delete(plainDirectory, recursive: true);
        }
    }

    [Test]
    public void ConfigCommentsAndUnrelatedSectionsAreSkippedWhileFindingObjectFormat()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        string configPath = Path.Combine(repository.Path, ".git", "config");
        File.AppendAllText(configPath, "\n# a comment\n; another comment\n[core]\n\tobjectformat = bogus\n[Extensions]\n\tOBJECTFORMAT = SHA1\n");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, "HEAD");

        Assert.That(result.ObjectFormatName, Is.EqualTo("sha1"));
    }

    [Test]
    public void ARepositoryWithNoConfigFileDefaultsToSha1()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        File.Delete(Path.Combine(repository.Path, ".git", "config"));

        Assert.That(HistoryIngestionFixture.Succeed(repository, first, "HEAD").ObjectFormatName, Is.EqualTo("sha1"));
    }

    [Test]
    public void AGitFileWhoseTargetDoesNotExistFailsClosed()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "arch-linter-history-bad-gitfile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        try
        {
            File.WriteAllText(Path.Combine(workingDirectory, ".git"), "gitdir: ./does-not-exist\n");

            Assert.That(HistoryIngestionFixture.Fail(workingDirectory, "HEAD", "HEAD").KindText, Is.EqualTo("repository_not_found"));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        System.Diagnostics.ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }
    }
}
