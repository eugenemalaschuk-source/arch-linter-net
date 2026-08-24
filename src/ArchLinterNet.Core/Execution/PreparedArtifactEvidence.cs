using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

// Builds the metadata-only evidence that authorizes lazy runner materialization. It intentionally
// reads PE metadata and bytes without loading an Assembly, so an ensure-built graph build can
// replace selected outputs before this evidence is refreshed.
internal static class PreparedArtifactEvidence
{
    internal static (IReadOnlyList<string> Paths, bool Complete) BuildMetadataReferenceClosure(
        IReadOnlyList<string> roots,
        ProjectDiscoveryResult discovery,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> candidates = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in roots.Concat(discovery.ResolvedAssemblyPaths.Values).Where(File.Exists))
        {
            candidates[Path.GetFileNameWithoutExtension(path)] = Path.GetFullPath(path);
        }

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
        {
            foreach (string path in trustedPlatformAssemblies.Split(
                         Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Where(File.Exists))
            {
                candidates.TryAdd(Path.GetFileNameWithoutExtension(path), Path.GetFullPath(path));
            }
        }

        Queue<string> pending = new(roots.Select(Path.GetFullPath));
        HashSet<string> closure = new(StringComparer.OrdinalIgnoreCase);
        // A project-only metadata contract has no exact PE/PDB root inventory. Do not make it
        // reusable merely because its reference walk is vacuously empty.
        bool complete = roots.Count > 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = pending.Dequeue();
            if (!closure.Add(path))
            {
                continue;
            }

            try
            {
                using FileStream stream = File.OpenRead(path);
                using PEReader reader = new(stream, PEStreamOptions.LeaveOpen);
                if (!reader.HasMetadata)
                {
                    complete = false;
                    continue;
                }

                MetadataReader metadata = reader.GetMetadataReader();
                foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
                {
                    string name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                    if (string.IsNullOrWhiteSpace(name) || !candidates.TryGetValue(name, out string? referencePath))
                    {
                        complete = false;
                        continue;
                    }

                    pending.Enqueue(referencePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
            {
                complete = false;
            }
        }

        return (closure.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(), complete);
    }

    internal static IReadOnlyDictionary<string, string> CaptureDigests(
        IReadOnlyList<string> artifactPaths,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> digests = new(StringComparer.OrdinalIgnoreCase);
        foreach (string artifactPath in artifactPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Add(artifactPath);
            Add(Path.ChangeExtension(artifactPath, ".pdb"));
            Add(BuildReceiptStore.ReceiptPathFor(artifactPath));
        }

        return digests;

        void Add(string path)
        {
            string fullPath = Path.GetFullPath(path);
            digests[fullPath] = File.Exists(fullPath)
                ? BuildStateCanonicalHasher.ComputeContentDigest(fullPath, cancellationToken)
                : "missing";
        }
    }
}
