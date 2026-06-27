using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureReferenceGraph
{
    private readonly Dictionary<Type, IReadOnlyList<Type>> _referencedTypesByType = new();

    public IReadOnlyList<Type> GetReferencedTypes(Type type)
    {
        if (_referencedTypesByType.TryGetValue(type, out IReadOnlyList<Type>? cached))
        {
            return cached;
        }

        IReadOnlyList<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();
        _referencedTypesByType[type] = referenced;
        return referenced;
    }
}
