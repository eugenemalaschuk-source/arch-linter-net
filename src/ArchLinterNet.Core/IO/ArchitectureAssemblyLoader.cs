using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

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
        private readonly Dictionary<string, ArchitectureLoadedAssemblyArtifact> _loadedArtifactsByPath =
            new(StringComparer.OrdinalIgnoreCase);
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

        public IReadOnlyCollection<ArchitectureLoadedAssemblyArtifact> LoadedAssemblyArtifacts
        {
            get
            {
                lock (_loadedAssemblyPathsGate)
                {
                    return _loadedArtifactsByPath.Values.ToArray();
                }
            }
        }

        // Cache lookup happens before the contract executor reaches metadata that would normally
        // trigger AssemblyLoadContext.Load. Materialize the available local reference closure now
        // so both a miss and a later lookup compare the same complete scoped artifact set. Missing
        // framework/external references remain CLR-resolved on demand and are not treated as a
        // probing-path artifact owned by this scope.
        public bool MaterializeProbingPathReferences(
            IEnumerable<Assembly> rootAssemblies,
            int maximumAdditionalArtifactCount,
            long maximumAdditionalArtifactBytes,
            CancellationToken cancellationToken)
        {
            Queue<Assembly> pending = new(rootAssemblies);
            HashSet<string> visited = new(StringComparer.Ordinal);
            int additionalArtifactCount = 0;
            long additionalArtifactBytes = 0;

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assembly assembly = pending.Dequeue();
                string identity = assembly.FullName ?? assembly.GetName().Name ?? string.Empty;
                if (!visited.Add(identity))
                {
                    continue;
                }

                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    cancellationToken.ThrowIfCancellationRequested();
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

                    string fullCandidate = Path.GetFullPath(candidate);
                    if (IsLoaded(fullCandidate))
                    {
                        continue;
                    }

                    try
                    {
                        long candidateBytes = GetArtifactSize(fullCandidate);
                        if (additionalArtifactCount >= maximumAdditionalArtifactCount
                            || candidateBytes > maximumAdditionalArtifactBytes - additionalArtifactBytes)
                        {
                            return false;
                        }

                        pending.Enqueue(LoadAssemblyFromStream(fullCandidate));
                        additionalArtifactCount++;
                        additionalArtifactBytes += candidateBytes;
                    }
                    catch (Exception ex) when (ex is BadImageFormatException or IOException
                        or UnauthorizedAccessException)
                    {
                        // Cache authorization needs a complete byte inventory. A local reference
                        // that cannot be captured is not evidence that can safely authorize a
                        // hit; normal evaluation still retains its historical lazy resolution.
                        return false;
                    }
                }
            }

            return true;
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
            string assemblyContentDigest = ComputeStreamDigest(assemblyStream);
            string pdbPath = Path.ChangeExtension(fullPath, ".pdb");
            string pdbContentDigest = "missing";
            Assembly assembly;
            if (!File.Exists(pdbPath))
            {
                assembly = LoadFromStream(assemblyStream);
            }
            else
            {
                using FileStream pdbStream = File.OpenRead(pdbPath);
                pdbContentDigest = ComputeStreamDigest(pdbStream);
                assembly = LoadFromStream(assemblyStream, pdbStream);
            }

            lock (_loadedAssemblyPathsGate)
            {
                if (_loadedAssembliesByPath.TryGetValue(fullPath, out Assembly? existing))
                {
                    return existing;
                }

                _loadedAssembliesByPath.Add(fullPath, assembly);
                _loadedArtifactsByPath.Add(fullPath, new ArchitectureLoadedAssemblyArtifact(
                    fullPath,
                    assemblyContentDigest,
                    pdbContentDigest));
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

        private bool IsLoaded(string fullPath)
        {
            lock (_loadedAssemblyPathsGate)
            {
                return _loadedAssembliesByPath.ContainsKey(fullPath);
            }
        }

        private static long GetArtifactSize(string assemblyPath)
        {
            long assemblyBytes = new FileInfo(assemblyPath).Length;
            string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            return checked(assemblyBytes + (File.Exists(pdbPath) ? new FileInfo(pdbPath).Length : 0));
        }

        private static string ComputeStreamDigest(Stream stream)
        {
            long position = stream.Position;
            try
            {
                return Convert.ToHexStringLower(SHA256.HashData(stream));
            }
            finally
            {
                stream.Position = position;
            }
        }
    }
}
