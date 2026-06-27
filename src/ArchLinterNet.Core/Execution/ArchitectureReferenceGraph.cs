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
}
