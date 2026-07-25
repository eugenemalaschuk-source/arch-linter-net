using System.Reflection;
using System.Runtime.Loader;
using ArchLinterNet.Core.IO.Abstractions;

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

    public IArchitectureAssemblyLoadScope CreateIsolatedLoadScope(
        IReadOnlyList<string> probingPaths,
        IReadOnlyDictionary<string, string> exactAssemblyPaths)
    {
        return new IsolatedAssemblyLoadScope(probingPaths, exactAssemblyPaths);
    }

    private sealed class IsolatedAssemblyLoadScope : AssemblyLoadContext, IArchitectureAssemblyLoadScope
    {
        private readonly IReadOnlyList<string> _probingPaths;
        private readonly IReadOnlyDictionary<string, string> _exactAssemblyPaths;

        public IsolatedAssemblyLoadScope(
            IReadOnlyList<string> probingPaths,
            IReadOnlyDictionary<string, string> exactAssemblyPaths)
            : base(isCollectible: true)
        {
            _probingPaths = probingPaths;
            _exactAssemblyPaths = exactAssemblyPaths;
        }

        public Assembly LoadFrom(string path)
        {
            return LoadAssemblyFromStream(path);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? simpleName = assemblyName.Name;
            if (string.IsNullOrWhiteSpace(simpleName))
            {
                return null;
            }

            string? candidate = _exactAssemblyPaths.TryGetValue(simpleName, out string? exactPath)
                ? exactPath
                : _probingPaths
                    .Select(path => Path.Combine(path, $"{simpleName}.dll"))
                    .FirstOrDefault(File.Exists);

            return candidate == null ? null : LoadAssemblyFromStream(candidate);
        }

        public void Dispose()
        {
            Unload();
        }

        private Assembly LoadAssemblyFromStream(string path)
        {
            using FileStream assemblyStream = File.OpenRead(path);
            string pdbPath = Path.ChangeExtension(path, ".pdb");
            if (!File.Exists(pdbPath))
            {
                return LoadFromStream(assemblyStream);
            }

            using FileStream pdbStream = File.OpenRead(pdbPath);
            return LoadFromStream(assemblyStream, pdbStream);
        }
    }
}
