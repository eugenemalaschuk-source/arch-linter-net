using ArchLinterNet.Core.Contracts.Abstractions;

namespace ArchLinterNet.Core.Contracts;

public sealed class ConditionSetResolutionService : IConditionSetResolutionService
{
    public bool TryResolve(
        ArchitectureContractDocument document,
        string? explicitName,
        out IReadOnlyList<string> symbols,
        out string? error)
    {
        return ConditionSetResolver.TryResolve(document, explicitName, out symbols, out error);
    }
}
