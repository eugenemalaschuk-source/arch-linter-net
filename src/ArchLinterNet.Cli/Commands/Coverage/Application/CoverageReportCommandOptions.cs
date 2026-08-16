namespace ArchLinterNet.Cli.Commands.Coverage.Application;

internal sealed record CoverageReportCommandOptions(
    string InputPath,
    string? ChangedFilesPath,
    string RepositoryRoot,
    string? OutputPath,
    int? MaxFailureDiagnostics,
    string DiffStatus,
    bool ShowHelp);
