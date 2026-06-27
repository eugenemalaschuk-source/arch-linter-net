namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureAnalysisSession(ArchitectureAnalysisContext context)
{
    public ArchitectureAnalysisContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

    public ArchitectureTypeIndex TypeIndex { get; } = new(context.TargetAssemblies);

    public ArchitectureReferenceGraph ReferenceGraph { get; } = new();
}
