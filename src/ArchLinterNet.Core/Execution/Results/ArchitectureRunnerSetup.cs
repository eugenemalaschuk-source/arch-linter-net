using ArchLinterNet.Core.Execution.Abstractions;

namespace ArchLinterNet.Core.Execution.Results;

/// <summary>
/// The materialized runner and its repository context.
/// </summary>
public sealed record ArchitectureRunnerSetup(string RepositoryRoot, IArchitectureContractRunner Runner)
{
    public int AssemblyLoads { get; init; }
}
