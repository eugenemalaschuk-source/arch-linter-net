namespace ArchLinterNet.Cli.Commands.Measure.Application;

internal sealed record MeasureCommandOptions(
    string PolicyPath,
    string Format,
    IReadOnlyList<string> MetricIds,
    string? ConditionSetName,
    int? MaxContributors,
    bool AllContributors,
    bool ShowHelp,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null);
