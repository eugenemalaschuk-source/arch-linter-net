using System.Reflection;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// The immutable result of one public API reflection pass. Entries and ExportedTypes are the two
// views of the same traversal: selectors match the stored Type instances, while contract-specific
// filtering continues to operate on the stored entries.
internal sealed record ArchitecturePublicApiSurfaceMaterialization(
    IReadOnlyList<ArchitectureExportedApiEntry> Entries,
    IReadOnlyList<Type> ExportedTypes);

// Session-local cache for public API surfaces. Assembly identity is deliberately reference-based:
// the resolved Assembly object, rather than a name/path that could alias a different load context,
// defines the surface that reflection observed. No state escapes the owning analysis session.
internal sealed class ArchitecturePublicApiSurfaceIndex
{
    private readonly Dictionary<Assembly, ArchitecturePublicApiSurfaceMaterialization> _surfaces =
        new(ReferenceEqualityComparer.Instance);

    internal int MaterializationCount { get; private set; }

    internal ArchitecturePublicApiSurfaceMaterialization GetOrMaterialize(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (_surfaces.TryGetValue(assembly, out ArchitecturePublicApiSurfaceMaterialization? surface))
        {
            return surface;
        }

        (IReadOnlyList<ArchitectureExportedApiEntry> entries, IReadOnlyList<Type> exportedTypes) =
            ArchitecturePublicApiSurfaceScanner.MaterializeExportedSurface(assembly);
        surface = new ArchitecturePublicApiSurfaceMaterialization(entries, exportedTypes);
        _surfaces.Add(assembly, surface);
        MaterializationCount++;
        return surface;
    }
}
