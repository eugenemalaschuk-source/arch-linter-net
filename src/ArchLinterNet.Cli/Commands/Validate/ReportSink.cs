namespace ArchLinterNet.Cli.Commands.Validate;

internal enum ReportDestinationType
{
    Stdout,
    Stderr,
    File
}

internal sealed record ReportSink(
    string Format,
    ReportDestinationType DestinationType,
    string? FilePath = null);
