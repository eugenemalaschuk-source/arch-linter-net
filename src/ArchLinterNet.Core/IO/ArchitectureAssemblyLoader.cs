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

    private sealed class IsolatedAssemblyLoadScope : AssemblyLoadContext, IArchitectureAssemblyLoadScope,
        IArchitectureAssemblyLoadScopeArtifactInventory
    {
        private readonly IReadOnlyList<string> _probingPaths;
        private readonly IReadOnlyDictionary<string, string> _exactAssemblyPaths;
        private readonly HashSet<string> _loadedAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Assembly> _loadedAssembliesByPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _loadedAssemblyPathsGate = new();

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

        public IReadOnlyCollection<string> LoadedAssemblyPaths
        {
            get
            {
                lock (_loadedAssemblyPathsGate)
                {
                    return _loadedAssemblyPaths.ToArray();
                }
            }
        }

        // Cache lookup happens before the contract executor reaches metadata that would normally
        // trigger AssemblyLoadContext.Load. Materialize the available local reference closure now
        // so both a miss and a later lookup compare the same complete scoped artifact set. Missing
        // framework/external references remain CLR-resolved on demand and are not treated as a
        // probing-path artifact owned by this scope.
        public void MaterializeProbingPathReferences(IEnumerable<Assembly> rootAssemblies)
        {
            Queue<Assembly> pending = new(rootAssemblies);
            HashSet<string> visited = new(StringComparer.Ordinal);

            while (pending.Count > 0)
            {
                Assembly assembly = pending.Dequeue();
                string identity = assembly.FullName ?? assembly.GetName().Name ?? string.Empty;
                if (!visited.Add(identity))
                {
                    continue;
                }

                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    string? simpleName = reference.Name;
                    if (string.IsNullOrWhiteSpace(simpleName))
                    {
                        continue;
                    }

                    string? candidate = ResolveScopedReferencePath(simpleName);
                    if (candidate is null)
                    {
                        continue;
                    }

                    try
                    {
                        pending.Enqueue(LoadAssemblyFromStream(candidate));
                    }
                    catch (Exception ex) when (ex is BadImageFormatException or IOException
                        or UnauthorizedAccessException)
                    {
                        // This eagerly captured closure is cache evidence, not a new analysis
                        // resolution path. Preserve the pre-existing lazy-load behavior for an
                        // unreadable optional reference; if analysis consumes it later, its normal
                        // resolution will still surface the original error.
                    }
                }
            }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? simpleName = assemblyName.Name;
            if (string.IsNullOrWhiteSpace(simpleName))
            {
                return null;
            }

            string? candidate = ResolveScopedReferencePath(simpleName);

            return candidate == null ? null : LoadAssemblyFromStream(candidate);
        }

        public void Dispose()
        {
            Unload();
        }

        private Assembly LoadAssemblyFromStream(string path)
        {
            string fullPath = Path.GetFullPath(path);
            lock (_loadedAssemblyPathsGate)
            {
                if (_loadedAssembliesByPath.TryGetValue(fullPath, out Assembly? existing))
                {
                    return existing;
                }
            }

            using FileStream assemblyStream = File.OpenRead(fullPath);
            string pdbPath = Path.ChangeExtension(fullPath, ".pdb");
            Assembly assembly;
            if (!File.Exists(pdbPath))
            {
                assembly = LoadFromStream(assemblyStream);
            }
            else
            {
                using FileStream pdbStream = File.OpenRead(pdbPath);
                assembly = LoadFromStream(assemblyStream, pdbStream);
            }

            lock (_loadedAssemblyPathsGate)
            {
                if (_loadedAssembliesByPath.TryGetValue(fullPath, out Assembly? existing))
                {
                    return existing;
                }

                _loadedAssembliesByPath.Add(fullPath, assembly);
                _loadedAssemblyPaths.Add(fullPath);
            }

            return assembly;
        }

        private string? ResolveScopedReferencePath(string simpleName)
        {
            return _exactAssemblyPaths.TryGetValue(simpleName, out string? exactPath)
                ? exactPath
                : _probingPaths
                    .Select(path => Path.Combine(path, $"{simpleName}.dll"))
                    .FirstOrDefault(File.Exists);
        }
    }
}
