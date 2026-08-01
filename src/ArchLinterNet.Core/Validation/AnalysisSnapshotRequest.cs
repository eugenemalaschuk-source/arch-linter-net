using ArchLinterNet.Core.BuildState;

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

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;

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
            PreparationMode = PreparationMode,
            NoRestore = NoRestore,
            RequestedConfiguration = RequestedConfiguration,
            RequestedTargetFramework = RequestedTargetFramework,
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
            PreparationMode = request.PreparationMode,
            NoRestore = request.NoRestore,
            RequestedConfiguration = request.RequestedConfiguration,
            RequestedTargetFramework = request.RequestedTargetFramework,
            CancellationToken = request.CancellationToken
        };
    }
}
