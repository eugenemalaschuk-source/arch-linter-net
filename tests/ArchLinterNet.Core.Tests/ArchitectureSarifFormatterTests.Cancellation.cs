using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureSarifFormatterTests
{
    // PR #375 review: the final OrderBy/ThenBy/ToList over the assembled ResultEntry list used to
    // run without any token checks, so cancellation during the sort of a large report was only
    // observed after the whole sort had finished. This proves the sort's per-comparison check is
    // live: the preflight collection cancels the token only after it has been fully enumerated —
    // i.e. after every pre-sort phase (violation mapping, entry serialization, cycle/preflight
    // entry building) already passed with an uncancelled token — so the OperationCanceledException
    // can only have been raised by the sort comparer itself.
    [Test]
    public void FormatResultAsSarif_CancelledAfterEntryMaterialization_ThrowsFromFinalSort()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("z-contract", "z-rule", "Source.Z", "Forbidden.Z", _ref1),
            new("a-contract", "a-rule", "Source.A", "Forbidden.A", _ref2),
        };
        var preflightDiagnostics = new[]
        {
            new BuildStatePreflightDiagnostic(
                "preflight", "preflight-id", BuildStatePreflightState.MissingArtifact,
                new BuildStatePreflightEvidence("src/App.csproj", "App")),
            new BuildStatePreflightDiagnostic(
                "preflight", "preflight-id", BuildStatePreflightState.MissingArtifact,
                new BuildStatePreflightEvidence("src/Other.csproj", "Other")),
        };
        var collection = new CancelOnTerminationCollection<BuildStatePreflightDiagnostic>(preflightDiagnostics);
        using CancellationTokenSource cts = new();
        collection.CancellationTokenSource = cts;

        Assert.Throws<OperationCanceledException>(() => _formatter.FormatResultAsSarif(
            "strict",
            violations,
            Array.Empty<string>(),
            collection,
            Array.Empty<ArchitectureCoverageSummary>(),
            ArchitectureSourceExpansionInventory.Empty,
            "1.2.3",
            subtractiveMatcherParticipation: null,
            cancellationToken: cts.Token));

        Assert.That(collection.FetchedCount, Is.EqualTo(preflightDiagnostics.Length),
            "both preflight entries were materialized before cancellation was observed — the exception must come from the final sort's per-comparison check");
    }

    [Test]
    public void FormatFindingsAsSarif_CancelledAfterEntryMaterialization_ThrowsFromFinalSort()
    {
        ArchitectureFinding[] findings =
        [
            ArchitectureFindingMapper.FromDiagnostic(new CycleDiagnostic("z-contract", "z-rule", "Z -> Z"), "strict"),
            ArchitectureFindingMapper.FromDiagnostic(new CycleDiagnostic("a-contract", "a-rule", "A -> A"), "strict"),
        ];
        var collection = new CancelOnTerminationCollection<ArchitectureFinding>(findings);
        using CancellationTokenSource cts = new();
        collection.CancellationTokenSource = cts;

        Assert.Throws<OperationCanceledException>(() => ArchitectureSarifFormatter.FormatFindingsAsSarif(
            collection,
            "1.2.3",
            cts.Token));

        Assert.That(collection.FetchedCount, Is.EqualTo(findings.Length),
            "both findings were materialized before cancellation was observed — the exception must come from the final sort's per-comparison check");
    }

    private sealed class CancelOnTerminationCollection<T> : IReadOnlyCollection<T>
    {
        private readonly IReadOnlyList<T> _items;

        public CancelOnTerminationCollection(IReadOnlyList<T> items)
        {
            _items = items;
        }

        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public int FetchedCount { get; private set; }

        public int Count => _items.Count;

        public IEnumerator<T> GetEnumerator()
        {
            foreach (T item in _items)
            {
                FetchedCount++;
                yield return item;
            }

            // Fires on the MoveNext() that terminates the enumeration, i.e. only after every
            // phase that consumes this collection has already run to completion.
            CancellationTokenSource?.Cancel();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
