namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed record PublicApiCaptureCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? OutputPath,
    string? ConditionSetName,
    string Format,
    bool Force,
    bool ShowHelp);

internal sealed record PublicApiDiffCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? SnapshotPath,
    string? ConditionSetName,
    string Format,
    bool ShowHelp);

internal sealed record PublicApiUpdateCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? SnapshotPath,
    string? ConditionSetName,
    string Format,
    bool DryRun,
    bool ShowHelp);

internal sealed record PublicApiMigrateCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? OutputPath,
    string? ConditionSetName,
    string Format,
    bool AcceptDrift,
    bool DryRun,
    bool ShowHelp);
