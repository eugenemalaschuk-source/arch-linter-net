namespace ArchLinterNet.Cli.Commands.Validate.Application;

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
    string? TargetFramework = null,
    string? Platform = null,
    string? RuntimeIdentifier = null)
{
    public bool IsFormatExplicit { get; init; }

    public IReadOnlyList<ReportSink> AdditionalSinks { get; init; } = Array.Empty<ReportSink>();

    public string? ReportParseError { get; init; }

    // null = --profile not requested (no behavior change). Otherwise "stdout", "stderr", or a file
    // path, independent of --timings/--report. See openspec/specs/analysis-profile/spec.md.
    public string? ProfileDestination { get; init; }

    // null = --cache not requested (persistent cache disabled, no behavior change). Otherwise
    // "auto" or an explicit path. See openspec/specs/analysis-cache/spec.md.
    public string? CacheDestination { get; init; }

    // null = --max-parallelism not requested (resolves to the default degree). See
    // openspec/specs/bounded-parallel-scanning/spec.md.
    public int? MaxParallelism { get; init; }

    // null uses the current UTC calendar date once for the whole validation. Supplying an ISO
    // date makes expiry-boundary runs reproducible in CI and tests.
    public string? WaiverEvaluationDate { get; init; }
}
