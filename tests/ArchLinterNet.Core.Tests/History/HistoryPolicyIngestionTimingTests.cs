using System.Globalization;
using ArchLinterNet.Core.History;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryPolicyIngestionTimingTests
{
    [Test]
    public void RecordsEnrichmentTimingOnlyWhenEnrichmentIsRequested()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Widget.cs", "namespace Example; public class Widget { }\n");
        string revision = repository.Commit("history timing fixture");

        HistoryIngestionTiming notRequestedTiming = new();
        HistoryIngestionOutcome notRequested = HistoryPolicyIngestionService.Ingest(
            new HistoryIngestionRequest(repository.Path, revision, revision),
            policyPath: null,
            timing: notRequestedTiming);

        HistoryIngestionTiming requestedTiming = new();
        HistoryIngestionOutcome requested = HistoryPolicyIngestionService.Ingest(
            new HistoryIngestionRequest(repository.Path, revision, revision, requestDotNetEnrichment: true),
            policyPath: null,
            timing: requestedTiming);

        Assert.Multiple(() =>
        {
            Assert.That(notRequested.Result, Is.Not.Null);
            Assert.That(requested.Result, Is.Not.Null);
            Assert.That(ReadEnrichmentTiming(notRequestedTiming), Is.EqualTo("n/a"));
            Assert.That(double.TryParse(
                    ReadEnrichmentTiming(requestedTiming),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _),
                Is.True,
                "Requested enrichment must produce a numeric timing even when facts are unavailable.");
        });
    }

    private static string ReadEnrichmentTiming(HistoryIngestionTiming timing) =>
        timing.Format()
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(part => part.StartsWith("enrichment=", StringComparison.Ordinal))
            .Split('=', 2)[1];
}
