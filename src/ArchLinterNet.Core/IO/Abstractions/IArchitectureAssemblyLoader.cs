using System.Reflection;

namespace ArchLinterNet.Core.IO;

public interface IArchitectureAssemblyLoader
{
    IReadOnlyList<Assembly> GetLoadedAssemblies();

    Assembly Load(AssemblyName assemblyName);

    Assembly LoadFrom(string path);
}
