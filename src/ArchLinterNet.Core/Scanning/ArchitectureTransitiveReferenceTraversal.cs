namespace ArchLinterNet.Core.Scanning;

// Namespace validation often rejects most reached types, so constructing a full path for every
// BFS node wastes O(sum of path lengths) allocations. Keep predecessor links and materialize only
// the paths the caller retains as violation evidence.
internal static class ArchitectureTransitiveReferenceTraversal
{
    internal static IEnumerable<ArchitectureTransitiveReference> Enumerate(
        Type source,
        Func<Type, IEnumerable<Type>> getDirectReferences,
        Func<Type, bool>? traversePredicate)
    {
        HashSet<Type> visited = new() { source };
        Dictionary<Type, Type> predecessorByType = new();
        Queue<Type> queue = new();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            Type current = queue.Dequeue();
            foreach (Type directReference in getDirectReferences(current))
            {
                if (!visited.Add(directReference))
                {
                    continue;
                }

                predecessorByType.Add(directReference, current);
                yield return new ArchitectureTransitiveReference(source, directReference, predecessorByType);

                if (traversePredicate == null || traversePredicate(directReference))
                {
                    queue.Enqueue(directReference);
                }
            }
        }
    }
}

internal readonly struct ArchitectureTransitiveReference(
    Type source,
    Type referenced,
    IReadOnlyDictionary<Type, Type> predecessorByType)
{
    internal Type Referenced { get; } = referenced;

    internal List<Type> BuildPath()
    {
        List<Type> path = new() { Referenced };
        Type current = Referenced;
        while (!ReferenceEquals(current, source))
        {
            current = predecessorByType[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
