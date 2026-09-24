namespace ArchLinterNet.Cli.Commands.History.Application;

internal enum HistoryReportDestinationType
{
    Stdout,
    Stderr,
    File
}

internal sealed record HistoryReportSink(
    string Format,
    HistoryReportDestinationType DestinationType,
    string? FilePath = null);

internal enum HistoryReportRouteStatus
{
    AllSucceeded,
    PartialOutput,
    OutputFailed,
}

// Stream writes and independent file renames cannot be rolled back as a set. Keep the same
// evidence shape as Validate's ReportCoordinator so a failed history publication is honest about
// what a consumer may already have received.
internal readonly record struct HistoryReportRouteResult(
    HistoryReportRouteStatus Status,
    IReadOnlyList<string> FailedPaths,
    IReadOnlyList<string> CommittedPaths,
    IReadOnlyList<string> StagedPaths,
    IReadOnlyList<string> UncommittedPaths,
    IReadOnlyList<string> ErrorDetails,
    IReadOnlyList<string> DeliveredStreamPaths)
{
    public bool Cancelled { get; init; }
}

// Mirrors the format=destination syntax already shipped on the root `validate` command's
// --report option, restricted to the two formats history analyze supports. Kept local to
// History rather than sharing Validate's ReportSink: the two command families have different
// format sets and no other coupling, so a shared type would be a cross-command abstraction with
// no second justification beyond syntax reuse.
internal static class HistoryReportSinkParser
{
    public static IReadOnlyList<HistoryReportSink> Parse(IReadOnlyList<string>? rawValues)
    {
        if (rawValues is null || rawValues.Count == 0)
        {
            return Array.Empty<HistoryReportSink>();
        }

        List<HistoryReportSink> sinks = new(rawValues.Count);
        HashSet<string> destinations = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in rawValues)
        {
            int eqIndex = raw.IndexOf('=');
            if (eqIndex <= 0 || eqIndex >= raw.Length - 1)
            {
                throw new InvalidOperationException(
                    $"Invalid --report value: '{raw}'. Use format=destination (e.g. json=report.json).");
            }

            string format = raw[..eqIndex];
            string destination = raw[(eqIndex + 1)..];

            if (format is not ("json" or "markdown"))
            {
                throw new InvalidOperationException(
                    $"Invalid format in --report: '{format}'. Use json or markdown.");
            }

            string dedupKey = destination switch
            {
                "stdout" => "stdout",
                "stderr" => "stderr",
                _ => Path.GetFullPath(destination),
            };

            if (!destinations.Add(dedupKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate --report destination: '{destination}'.");
            }

            HistoryReportSink sink = destination switch
            {
                "stdout" => new HistoryReportSink(format, HistoryReportDestinationType.Stdout),
                "stderr" => new HistoryReportSink(format, HistoryReportDestinationType.Stderr),
                _ => new HistoryReportSink(format, HistoryReportDestinationType.File, destination),
            };

            sinks.Add(sink);
        }

        return sinks;
    }
}
