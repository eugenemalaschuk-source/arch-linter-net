namespace ArchLinterNet.Cli.Commands.Change.Application;

internal sealed record ChangeSnapshotCommandOptions(
    string PolicyPath,
    string Mode,
    string? ConditionSetName,
    string? BaselinePath,
    string OutputPath,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null);

internal sealed record ChangeReportCommandOptions(
    string BasePath,
    string CurrentPath,
    string Format,
    string? OutputPath,
    bool ShowHelp,
    string? ExecutionContext);
