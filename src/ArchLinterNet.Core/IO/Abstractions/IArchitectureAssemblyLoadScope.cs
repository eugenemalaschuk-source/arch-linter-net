using System.Reflection;

namespace ArchLinterNet.Core.IO.Abstractions;

// An explicitly owned loading context lets a post-build snapshot inspect the exact artifacts
// produced by that build without reusing an assembly with the same simple name in the default
// AppDomain. A single scope is shared by all targets in one snapshot so project references retain
// normal type identity within that snapshot.
public interface IArchitectureAssemblyLoadScope : IDisposable
{
    Assembly LoadFrom(string path);
}
