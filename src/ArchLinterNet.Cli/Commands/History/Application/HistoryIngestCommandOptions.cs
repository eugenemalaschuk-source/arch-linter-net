namespace ArchLinterNet.Cli.Commands.History.Application;

internal sealed record HistoryIngestCommandOptions(
    string Repository,
    string From,
    string To,
    string Format,
    bool ShowHelp,
    string? PolicyPath = null,
    bool RequestDotNetEnrichment = false,
    IReadOnlyList<HistoryReportSink>? ReportSinks = null,
    string? ReportParseError = null,
    bool TimingsEnabled = false);
