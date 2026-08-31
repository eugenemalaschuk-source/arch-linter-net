using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;

namespace ArchLinterNet.Core.Validation;

// Mirrors ValidationRequest minus Mode: everything here is session-level (policy composition,
// project selection, build preparation) and mode-independent, so it can be evaluated for any
// number of modes against one ArchitectureAnalysisSnapshot. See ForMode for producing the
// single-mode ValidationRequest existing lower-level APIs still expect.
public sealed record AnalysisSnapshotRequest
{
    public required string PolicyPath { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    public string? BaselinePath { get; init; }

    public bool IncludeAsmdefContracts { get; init; } = true;

    public bool EnforceUnmatchedIgnoredViolationsPolicy { get; init; }

    public DateOnly? WaiverEvaluationDate { get; init; }

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    // See ValidationRequest.CacheLocation — null leaves the cache completely uninvolved.
    public AnalysisCacheLocation? CacheLocation { get; init; }

    // See ValidationRequest.MaxParallelism.
    public int? MaxParallelism { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;

    // Measurement's exact project-output analysis is read-only and intentionally does not turn
    // absence of an optional build-state receipt into missing metric evidence. This remains
    // internal so ordinary validation snapshots retain their reviewed preflight contract.
    internal bool IsMetricMeasurement { get; init; }

    // Applied after the complete policy has been loaded and validated, before measurement setup
    // chooses project/artifact evidence. This remains internal because ordinary snapshots never
    // select metric definitions.
    internal IReadOnlyCollection<string>? SelectedMetricIds { get; init; }

    public ValidationRequest ForMode(string mode)
    {
        return new ValidationRequest
        {
            PolicyPath = PolicyPath,
            Mode = mode,
            ConditionSetName = ConditionSetName,
            PreprocessorSymbols = PreprocessorSymbols,
            ContractIds = ContractIds,
            BaselinePath = BaselinePath,
            IncludeAsmdefContracts = IncludeAsmdefContracts,
            EnforceUnmatchedIgnoredViolationsPolicy = EnforceUnmatchedIgnoredViolationsPolicy,
            WaiverEvaluationDate = WaiverEvaluationDate,
            PreparationMode = PreparationMode,
            NoRestore = NoRestore,
            RequestedConfiguration = RequestedConfiguration,
            RequestedTargetFramework = RequestedTargetFramework,
            RequestedPlatform = RequestedPlatform,
            RequestedRuntimeIdentifier = RequestedRuntimeIdentifier,
            CacheLocation = CacheLocation,
            MaxParallelism = MaxParallelism,
            CancellationToken = CancellationToken
        };
    }

    public static AnalysisSnapshotRequest FromValidationRequest(ValidationRequest request)
    {
        return new AnalysisSnapshotRequest
        {
            PolicyPath = request.PolicyPath,
            ConditionSetName = request.ConditionSetName,
            PreprocessorSymbols = request.PreprocessorSymbols,
            ContractIds = request.ContractIds,
            BaselinePath = request.BaselinePath,
            IncludeAsmdefContracts = request.IncludeAsmdefContracts,
            EnforceUnmatchedIgnoredViolationsPolicy = request.EnforceUnmatchedIgnoredViolationsPolicy,
            WaiverEvaluationDate = request.WaiverEvaluationDate,
            PreparationMode = request.PreparationMode,
            NoRestore = request.NoRestore,
            RequestedConfiguration = request.RequestedConfiguration,
            RequestedTargetFramework = request.RequestedTargetFramework,
            RequestedPlatform = request.RequestedPlatform,
            RequestedRuntimeIdentifier = request.RequestedRuntimeIdentifier,
            CacheLocation = request.CacheLocation,
            MaxParallelism = request.MaxParallelism,
            CancellationToken = request.CancellationToken
        };
    }
}
