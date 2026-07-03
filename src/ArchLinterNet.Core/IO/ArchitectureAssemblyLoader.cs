using System.Reflection;

namespace ArchLinterNet.Core.IO;

public sealed class ArchitectureAssemblyLoader : IArchitectureAssemblyLoader
{
    public static readonly ArchitectureAssemblyLoader Real = new();

    public IReadOnlyList<Assembly> GetLoadedAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies();
    }

    public Assembly Load(AssemblyName assemblyName)
    {
        return Assembly.Load(assemblyName);
    }

    public Assembly LoadFrom(string path)
    {
        return Assembly.LoadFrom(path);
    }
}
