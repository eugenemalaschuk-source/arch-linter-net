using System.Reflection;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;

namespace ArchLinterNet.Core.Tests;

internal sealed class FakeArchitectureAssemblyLoader : IArchitectureAssemblyLoader
{
    private readonly List<Assembly> _loadedAssemblies;

    public FakeArchitectureAssemblyLoader(IEnumerable<Assembly> loadedAssemblies)
    {
        _loadedAssemblies = loadedAssemblies.ToList();
    }

    public bool LoadWasCalled { get; private set; }

    public bool LoadFromWasCalled { get; private set; }

    public IReadOnlyList<Assembly> GetLoadedAssemblies()
    {
        return _loadedAssemblies;
    }

    public Assembly Load(AssemblyName assemblyName)
    {
        LoadWasCalled = true;
        throw new InvalidOperationException($"Fake loader refuses to load '{assemblyName}' by name.");
    }

    public Assembly LoadFrom(string path)
    {
        LoadFromWasCalled = true;
        throw new InvalidOperationException($"Fake loader refuses to load '{path}' from disk.");
    }
}
