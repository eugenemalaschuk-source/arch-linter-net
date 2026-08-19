using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Analysis;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

internal static class HistoryIngestionFixture
{
    public static HistoryIngestionOutcome Ingest(GitTestRepository repository, string from, string to)
        => Ingest(repository.Path, from, to);

    // Lets a test point ingestion at a directory other than the fixture's own repository root — a
    // linked worktree path, most notably, where git commands still run against the main repository.
    public static HistoryIngestionOutcome Ingest(string repositoryPath, string from, string to)
        => HistoryIngestionService.Default.Ingest(new HistoryIngestionRequest(repositoryPath, from, to));

    public static HistoryIngestionResult Succeed(GitTestRepository repository, string from, string to)
        => Succeed(repository.Path, from, to);

    public static HistoryIngestionResult Succeed(string repositoryPath, string from, string to)
    {
        HistoryIngestionOutcome outcome = Ingest(repositoryPath, from, to);
        Assert.That(outcome.Diagnostic?.KindText, Is.Null, "ingestion was expected to succeed");
        return outcome.Result!;
    }

    public static HistoryDiagnostic Fail(GitTestRepository repository, string from, string to)
        => Fail(repository.Path, from, to);

    public static HistoryDiagnostic Fail(string repositoryPath, string from, string to)
    {
        HistoryIngestionOutcome outcome = Ingest(repositoryPath, from, to);
        Assert.That(outcome.Result, Is.Null, "a fail-closed run must not produce an ingestion result");
        return outcome.Diagnostic!;
    }

    public static LogicalFile File(HistoryIngestionResult result, string canonicalPath)
        => result.LogicalFiles.Single(file => file.CanonicalPath == canonicalPath);
}
