namespace ArchLinterNet.Cli.Commands.Baseline.Application;

// Reason mapping, preview, and overwrite intent are shared by every writing subcommand, so they are
// declared once here and spread into each command's options record.
internal sealed record BaselineReasonOptions(
    string Reason,
    IReadOnlyList<string> ReasonForContract,
    IReadOnlyList<string> ReasonForFamily);

internal sealed record BaselineWriteOptions(
    bool DryRun,
    bool Force);

internal sealed record BaselineGenerateCommandOptions(
    string PolicyPath,
    string? OutputPath,
    BaselineReasonOptions Reasons,
    string Mode,
    string? ConditionSetName,
    string Format,
    BaselineWriteOptions Write,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null);

internal sealed record BaselineUpdateCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string? OutputPath,
    BaselineReasonOptions Reasons,
    string Mode,
    string? ConditionSetName,
    string Format,
    BaselineWriteOptions Write,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null);

internal sealed record BaselinePruneCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string? OutputPath,
    string Mode,
    string? ConditionSetName,
    string Format,
    BaselineWriteOptions Write,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null);

internal sealed record BaselineDiffCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string Mode,
    string? ConditionSetName,
    string Format,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp)
{
    public bool HasFormatConflict { get; init; }
}

internal sealed record BaselineVerifyCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string Mode,
    string? ConditionSetName,
    string Format,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null)
{
    public bool HasFormatConflict { get; init; }
}

// Deliberately has no Mode/ContractIds — unlike the other baseline subcommands, migrate cannot be
// scoped: a version-2 document cannot preserve version-1 matching semantics for only part of a
// file, so every entry is always classified against the full current candidate set.
internal sealed record BaselineMigrateCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string? OutputPath,
    string? ConditionSetName,
    string Format,
    bool DryRun,
    bool Force,
    bool ShowHelp)
{
    public bool HasFormatConflict { get; init; }
}
