using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;

namespace ArchLinterNet.Core.Validation;

public sealed record ValidationRequest
{
    public required string PolicyPath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    public string? BaselinePath { get; init; }

    public bool IncludeAsmdefContracts { get; init; } = true;

    public bool EnforceUnmatchedIgnoredViolationsPolicy { get; init; }

    // Ordinary (default): never restores/builds. EnsureBuilt: explicit opt-in that builds the
    // graph once and verifies it via a build receipt. See BuildPreparationMode.
    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    // Fails closed offline with a `restore-required` diagnostic instead of allowing restore.
    // Composes with PreparationMode: valid with Ordinary (checked, never built) and with
    // EnsureBuilt (checked, then passed through to the build invocation).
    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    // Null (the default) leaves analysis-cache/v1 completely uninvolved — every mode always runs
    // the full pipeline, exactly as before this option existed. Non-null enables both the
    // cache-hit short-circuit inside ArchitectureAnalysisSnapshot.Evaluate and (via the caller's own
    // post-run population call) writing a verified entry after a completed run.
    public AnalysisCacheLocation? CacheLocation { get; init; }

    // Null (the default) resolves to max(1, min(Environment.ProcessorCount, 4)) — bounded parallel
    // scanning applies automatically. A positive override is validated and honored; `1` is a
    // first-class supported sequential mode. See openspec/specs/bounded-parallel-scanning/spec.md.
    public int? MaxParallelism { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}
