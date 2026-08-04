using System.Reflection;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.IO.Abstractions;

// An explicitly owned loading context lets a post-build snapshot inspect the exact artifacts
// produced by that build without reusing an assembly with the same simple name in the default
// AppDomain. A single scope is shared by all targets in one snapshot so project references retain
// normal type identity within that snapshot.
public interface IArchitectureAssemblyLoadScope : IDisposable
{
    Assembly LoadFrom(string path);
}

// Internal capability deliberately separate from the public load-scope abstraction: callers only
// need LoadFrom/Dispose, while Core's cache authorization needs the physical identity of stream-
// loaded assemblies. Keeping this implementation detail non-public avoids expanding the public
// loading API merely to transport snapshot-local cache evidence.
internal interface IArchitectureAssemblyLoadScopeArtifactInventory
{
    IReadOnlyCollection<ArchitectureLoadedAssemblyArtifact> LoadedAssemblyArtifacts { get; }

    bool MaterializeProbingPathReferences(
        IEnumerable<Assembly> rootAssemblies,
        int maximumAdditionalArtifactCount,
        long maximumAdditionalArtifactBytes,
        CancellationToken cancellationToken);
}
