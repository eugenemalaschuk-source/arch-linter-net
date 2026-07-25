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

    ArchitectureRunnerSetup BuildRunner( // NOSONAR: each parameter maps to a distinct configuration concern; a parameter object would wrap disparate optional axes without reducing call-site cognitive load
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null);

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
        string? mode = null);
}
