namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed record ValidateCommandOptions(
    string PolicyPath,
    string Mode,
    string Format,
    IReadOnlyList<string> ContractIds,
    string? ConditionSetName,
    bool TimingsEnabled,
    string? BaselinePath,
    bool ShowHelp,
    bool ShowVersion,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null)
{
    public bool IsFormatExplicit { get; init; }

    public IReadOnlyList<ReportSink> AdditionalSinks { get; init; } = Array.Empty<ReportSink>();

    public string? ReportParseError { get; init; }
}
