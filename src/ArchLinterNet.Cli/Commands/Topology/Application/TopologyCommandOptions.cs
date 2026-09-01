namespace ArchLinterNet.Cli.Commands.Topology.Application;

internal sealed record TopologyCaptureCommandOptions(
    string PolicyPath,
    string SubjectKind,
    string Format,
    string? OutputPath,
    string? ConditionSetName,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null,
    int? MaxParallelism = null)
{
    public bool HasFormatConflict { get; init; }
}

internal sealed record TopologyDiffCommandOptions(
    string PolicyPath,
    string Mode,
    string Format,
    string? OutputPath,
    string? ConditionSetName,
    string? BaselinePath,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null,
    int? MaxParallelism = null)
{
    public bool HasFormatConflict { get; init; }
}

internal sealed record TopologyVerifyCommandOptions(
    string PolicyPath,
    string Mode,
    string Format,
    string? OutputPath,
    string? ConditionSetName,
    string? BaselinePath,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null,
    int? MaxParallelism = null)
{
    public bool HasFormatConflict { get; init; }
}
