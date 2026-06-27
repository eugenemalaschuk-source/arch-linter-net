using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureAnalysisSession(ArchitectureAnalysisContext context)
{
    private ArchitectureCoverageInventory? _cachedCoverageInventory;
    private ArchitectureContractDocument? _cachedCoverageInventoryDocument;

    public ArchitectureAnalysisContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

    public ArchitectureTypeIndex TypeIndex { get; } = new(context.TargetAssemblies);

    public ArchitectureReferenceGraph ReferenceGraph { get; } = new();

    // Cached per session so multiple future coverage contract handlers share one inventory instead of
    // each rebuilding it; an explicit projectDiscovery override bypasses the cache (test-only substitution).
    public ArchitectureCoverageInventory BuildCoverageInventory(
        ArchitectureContractDocument document,
        ProjectDiscoveryResult? projectDiscovery = null)
    {
        if (projectDiscovery != null)
        {
            return ArchitectureCoverageInventory.Build(document, this, projectDiscovery);
        }

        if (_cachedCoverageInventory != null && ReferenceEquals(_cachedCoverageInventoryDocument, document))
        {
            return _cachedCoverageInventory;
        }

        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(document, this, Context.ProjectDiscovery);
        _cachedCoverageInventory = inventory;
        _cachedCoverageInventoryDocument = document;
        return inventory;
    }
}
