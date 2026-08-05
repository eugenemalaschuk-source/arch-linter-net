using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution.Abstractions;

public sealed record ArchitectureRunnerSetup(string RepositoryRoot, IArchitectureContractRunner Runner)
{
    public int AssemblyLoads { get; init; }
}

public interface IArchitectureRunnerSetupService
{
    ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath = null,
        ValidationTiming? timing = null);

    ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath,
        ValidationTiming? timing,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // This method IS the cancellation-aware overload; the token is already observed via the
        // ThrowIfCancellationRequested calls bracketing this call, not by forwarding it further.
        ArchitectureContractDocument document = LoadDocument(policyPath, baselinePath, timing); // NOSONAR: see comment above
        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }

    ArchitectureRunnerSetup BuildRunner( // NOSONAR: each parameter maps to a distinct configuration concern; a parameter object would wrap disparate optional axes without reducing call-site cognitive load
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null,
        CancellationToken cancellationToken = default,
        int? maxParallelism = null);

    // The post-ensure-built pass must build a runner from fresh artifacts, not by delegating to
    // ordinary same-simple-name assembly resolution.
    ArchitectureRunnerSetup BuildRunnerForPostBuild(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null,
        CancellationToken cancellationToken = default,
        int? maxParallelism = null);

    // Planning is intentionally metadata-only. Implementations that cannot prove an independent
    // artifact selection leave the plan incomplete; callers then fail closed for cache reuse.
    ArchitectureRunnerPreparation PrepareRunner(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Metadata-only runner preparation is not implemented by this setup service.");
    }

    ArchitectureRunnerSetup MaterializePreparedRunner(
        ArchitectureContractDocument document,
        ArchitectureRunnerPreparation preparation,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null,
        CancellationToken cancellationToken = default,
        int? maxParallelism = null)
    {
        throw new NotSupportedException("Prepared runner materialization is not implemented by this setup service.");
    }
}
