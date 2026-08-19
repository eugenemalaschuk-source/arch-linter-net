namespace ArchLinterNet.Cli.Commands.Change.Application;

internal sealed record ChangeSnapshotCommandOptions(
    string PolicyPath,
    string Mode,
    string? ConditionSetName,
    string? BaselinePath,
    string OutputPath,
    bool ShowHelp);

internal sealed record ChangeReportCommandOptions(
    string BasePath,
    string CurrentPath,
    string Format,
    string? OutputPath,
    bool ShowHelp);
