namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed record PublicApiCaptureCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? OutputPath,
    string? ConditionSetName,
    string Format,
    bool Force,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false);

internal sealed record PublicApiDiffCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? SnapshotPath,
    string? ConditionSetName,
    string Format,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false);

internal sealed record PublicApiUpdateCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? SnapshotPath,
    string? ConditionSetName,
    string Format,
    bool DryRun,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false);

internal sealed record PublicApiMigrateCommandOptions(
    string PolicyPath,
    string? ContractId,
    string? OutputPath,
    string? ConditionSetName,
    string Format,
    bool AcceptDrift,
    bool Force,
    bool DryRun,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false);
