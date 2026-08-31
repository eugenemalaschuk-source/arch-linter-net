using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureReferenceGraph
{
    private readonly Dictionary<Type, ReferenceScan> _referencedTypesByType = new();

    public IReadOnlyList<Type> GetReferencedTypes(Type type)
    {
        TryGetReferencedTypes(type, out IReadOnlyList<Type> referenced);
        return referenced;
    }

    // Validation callers retain the existing best-effort list through GetReferencedTypes. Metric
    // projections additionally need to know whether that list is a complete direct-reference
    // universe, so that a scanner degradation cannot be mistaken for a trusted zero.
    internal bool TryGetReferencedTypes(Type type, out IReadOnlyList<Type> referenced)
    {
        if (!_referencedTypesByType.TryGetValue(type, out ReferenceScan? cached))
        {
            bool isComplete = ArchitectureReferenceScanner.TryGetReferencedTypes(type, out List<Type> scanned);
            cached = new ReferenceScan(scanned, isComplete);
            _referencedTypesByType[type] = cached;
        }

        referenced = cached.ReferencedTypes;
        return cached.IsComplete;
    }

    public IEnumerable<(Type referenced, List<Type> path)> GetTransitiveReferencedTypes(
        Type type,
        Func<Type, bool>? traversePredicate = null)
    {
        HashSet<Type> visited = new();
        Queue<(Type current, List<Type> path)> queue = new();

        List<Type> initialPath = new() { type };
        queue.Enqueue((type, initialPath));
        visited.Add(type);

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            foreach (Type directRef in GetReferencedTypes(current))
            {
                if (visited.Contains(directRef))
                {
                    continue;
                }

                visited.Add(directRef);
                List<Type> refPath = new(path) { directRef };
                yield return (directRef, refPath);

                if (traversePredicate == null || traversePredicate(directRef))
                {
                    queue.Enqueue((directRef, refPath));
                }
            }
        }
    }

    private sealed record ReferenceScan(IReadOnlyList<Type> ReferencedTypes, bool IsComplete);
}
