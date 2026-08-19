using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryIngestionTextWriterTests
{
    [Test]
    public void SummarizesRangeCommitsAndLogicalFiles()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Old.cs", "one\ntwo\n");
        string first = repository.Commit("first #5");
        repository.Move("src/Old.cs", "src/New.cs");
        string second = repository.Commit("rename");

        string text = HistoryIngestionTextWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));
        string[] lines = text.Split('\n');

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Is.EqualTo("object format: sha1"));
            Assert.That(lines[1], Does.StartWith($"from: {first} -> {first}"));
            Assert.That(lines[2], Does.StartWith($"to: {second} -> {second}"));
            Assert.That(lines[3], Is.EqualTo("commits: 1 (excluded merges: 0)"));
            Assert.That(lines[4], Is.EqualTo("rename candidates: 1"));
            Assert.That(lines[5], Is.EqualTo("logical files: 1"));
            Assert.That(lines[6], Is.EqualTo("  src/New.cs (aliases: src/Old.cs): commits=1 additions=0 deletions=0 churn=0"));
            Assert.That(text, Does.EndWith("\n"));
        });
    }

    [Test]
    public void OmitsTheAliasParenthesesWhenALogicalFileHasNoAliases()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second");

        string text = HistoryIngestionTextWriter.Write(HistoryIngestionFixture.Succeed(repository, first, second));

        Assert.That(text, Does.Contain("  a.txt: commits=1 additions=1 deletions=0 churn=1"));
        Assert.That(text, Does.Not.Contain("aliases"));
    }
}
