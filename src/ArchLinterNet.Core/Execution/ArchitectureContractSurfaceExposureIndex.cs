using System.Collections.ObjectModel;
using System.Reflection;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Session-owned cache for recursive contract exposure facts. Type object identity is intentional:
// two load contexts can expose the same assembly/type names while reflection sees different types.
// The normalized visible-surface shape is part of the key, because one root can legitimately have
// different evidence for exported and internal-visible contract semantics.
internal sealed class ArchitectureContractSurfaceExposureIndex
{
    private readonly Dictionary<Type, Dictionary<ArchitectureContractSurfaceShape, ArchitectureContractSurfaceExposureResult>> _roots =
        new(ReferenceEqualityComparer.Instance);

    internal int MaterializationCount { get; private set; }

    internal ArchitectureContractSurfaceExposureResult GetOrMaterialize(Type root)
    {
        return GetOrMaterialize(root, ArchitectureContractSurfaceShape.Exported);
    }

    internal ArchitectureContractSurfaceExposureResult GetOrMaterialize(
        Type root,
        ArchitectureContractSurfaceShape surfaceShape)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_roots.TryGetValue(root, out Dictionary<ArchitectureContractSurfaceShape, ArchitectureContractSurfaceExposureResult>? results) &&
            results.TryGetValue(surfaceShape, out ArchitectureContractSurfaceExposureResult? result))
        {
            return result;
        }

        result = ArchitectureContractSurfaceExposureScanner.Scan(root, surfaceShape);
        results ??= new Dictionary<ArchitectureContractSurfaceShape, ArchitectureContractSurfaceExposureResult>();
        results.Add(surfaceShape, result);
        _roots[root] = results;
        MaterializationCount++;
        return result;
    }

    internal ArchitectureContractSurfaceExposureResult GetOrMaterialize(IEnumerable<Type> roots)
    {
        return GetOrMaterialize(roots, ArchitectureContractSurfaceShape.Exported);
    }

    internal ArchitectureContractSurfaceExposureResult GetOrMaterialize(
        IEnumerable<Type> roots,
        ArchitectureContractSurfaceShape surfaceShape)
    {
        ArgumentNullException.ThrowIfNull(roots);

        HashSet<Type> distinctRoots = new(ReferenceEqualityComparer.Instance);
        foreach (Type? root in roots)
        {
            if (root == null)
            {
                throw new ArgumentException("A contract exposure root cannot be null.", nameof(roots));
            }

            distinctRoots.Add(root);
        }

        List<Type> orderedRoots = distinctRoots.OrderBy(TypeSortKey, StringComparer.Ordinal).ToList();

        List<ArchitectureContractExposure> exposures = new();
        List<ArchitectureContractExposureIncompleteEvidence> incomplete = new();
        HashSet<ArchitectureContractExposure> exposureSet = new();
        HashSet<ArchitectureContractExposureIncompleteEvidence> incompleteSet = new();
        Dictionary<ArchitectureContractExposureTarget, Type> referencedTypes = new();
        foreach (Type root in orderedRoots)
        {
            ArchitectureContractSurfaceExposureResult result = GetOrMaterialize(root, surfaceShape);
            foreach (ArchitectureContractExposure exposure in result.Exposures)
            {
                if (exposureSet.Add(exposure))
                {
                    exposures.Add(exposure);
                }
            }

            foreach (ArchitectureContractExposureIncompleteEvidence evidence in result.IncompleteEvidence)
            {
                if (incompleteSet.Add(evidence))
                {
                    incomplete.Add(evidence);
                }
            }

            foreach ((ArchitectureContractExposureTarget target, Type type) in result.ReferencedTypes)
            {
                referencedTypes.TryAdd(target, type);
            }
        }

        return new ArchitectureContractSurfaceExposureResult(
            Array.AsReadOnly(exposures
                .OrderBy(item => item.DeclaringType.AssemblyName, StringComparer.Ordinal)
                .ThenBy(item => item.DeclaringType.FullTypeName, StringComparer.Ordinal)
                .ThenBy(item => item.Path.CanonicalKey, StringComparer.Ordinal)
                .ThenBy(item => item.ReferencedType.AssemblyName, StringComparer.Ordinal)
                .ThenBy(item => item.ReferencedType.FullTypeName, StringComparer.Ordinal)
                .ToArray()),
            Array.AsReadOnly(incomplete
                .OrderBy(item => item.DeclaringType.AssemblyName, StringComparer.Ordinal)
                .ThenBy(item => item.DeclaringType.FullTypeName, StringComparer.Ordinal)
                .ThenBy(item => item.Path.CanonicalKey, StringComparer.Ordinal)
                .ThenBy(item => item.Reason, StringComparer.Ordinal)
                .ToArray()))
        {
            ReferencedTypes = new ReadOnlyDictionary<ArchitectureContractExposureTarget, Type>(
                referencedTypes
                    .OrderBy(item => item.Key.AssemblyName, StringComparer.Ordinal)
                    .ThenBy(item => item.Key.FullTypeName, StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.Value)),
        };
    }

    private static string TypeSortKey(Type type)
    {
        try
        {
            return $"{type.Assembly.FullName ?? string.Empty}\u001f{type.FullName ?? type.Name}";
        }
        catch (Exception exception) when (exception is TypeLoadException or FileNotFoundException or FileLoadException)
        {
            return string.Empty;
        }
    }
}
