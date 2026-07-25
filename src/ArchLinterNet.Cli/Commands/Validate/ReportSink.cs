namespace ArchLinterNet.Cli.Commands.Validate;

internal enum ReportDestinationType
{
    Stderr,
    File
}

internal sealed record ReportSink(
    string Format,
    ReportDestinationType DestinationType,
    string? FilePath = null);
