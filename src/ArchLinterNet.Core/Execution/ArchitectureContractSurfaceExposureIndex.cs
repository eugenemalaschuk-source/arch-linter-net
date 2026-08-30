using System.Reflection;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Session-owned cache for recursive contract exposure facts. Type object identity is intentional:
// two load contexts can expose the same assembly/type names while reflection sees different types.
internal sealed class ArchitectureContractSurfaceExposureIndex
{
    private readonly Dictionary<Type, ArchitectureContractSurfaceExposureResult> _roots =
        new(ReferenceEqualityComparer.Instance);

    internal int MaterializationCount { get; private set; }

    internal ArchitectureContractSurfaceExposureResult GetOrMaterialize(Type root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_roots.TryGetValue(root, out ArchitectureContractSurfaceExposureResult? result))
        {
            return result;
        }

        result = ArchitectureContractSurfaceExposureScanner.Scan(root);
        _roots.Add(root, result);
        MaterializationCount++;
        return result;
    }

    internal ArchitectureContractSurfaceExposureResult GetOrMaterialize(IEnumerable<Type> roots)
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
        foreach (Type root in orderedRoots)
        {
            ArchitectureContractSurfaceExposureResult result = GetOrMaterialize(root);
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
                .ToArray()));
    }

    private static string TypeSortKey(Type type)
    {
        try
        {
            return $"{type.Assembly.GetName().Name ?? string.Empty}\u001f{type.FullName ?? type.Name}";
        }
        catch (Exception exception) when (exception is TypeLoadException or FileNotFoundException or FileLoadException)
        {
            return string.Empty;
        }
    }
}
