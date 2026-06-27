using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureAnalysisSession(ArchitectureAnalysisContext context)
{
    public ArchitectureAnalysisContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

    public ArchitectureTypeIndex TypeIndex { get; } = new(context.TargetAssemblies);

    public ArchitectureReferenceGraph ReferenceGraph { get; } = new();

    public ArchitectureCoverageInventory BuildCoverageInventory(
        ArchitectureContractDocument document,
        ProjectDiscoveryResult? projectDiscovery = null)
    {
        return ArchitectureCoverageInventory.Build(document, this, projectDiscovery);
    }
}
