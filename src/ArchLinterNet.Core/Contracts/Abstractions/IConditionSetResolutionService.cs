using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Abstractions;

public interface IConditionSetResolutionService
{
    bool TryResolve(
        ArchitectureContractDocument document,
        string? explicitName,
        out IReadOnlyList<string> symbols,
        out string? error);
}
