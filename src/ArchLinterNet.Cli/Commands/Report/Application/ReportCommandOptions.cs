namespace ArchLinterNet.Cli.Commands.Report.Application;

internal sealed record PrReportCommandOptions(
    string HealthPath,
    string ChangePath,
    string? OutputPath,
    int MaxDetails,
    bool ShowHelp,
    string? RepositoryUrl = null,
    string? HeadSha = null,
    string? ArtifactUrl = null);
