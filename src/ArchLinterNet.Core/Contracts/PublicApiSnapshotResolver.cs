using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

// Resolves every contract's `api_snapshot` at policy load time.
//
// Failures split into two categories on purpose:
//
// * A *path* that is absolute or escapes the policy boundary is a configuration error and throws.
//   No workflow can repair it, and it must never be silently accepted.
// * A snapshot that is missing, unparsable, or owned by another contract is recorded on the contract
//   as ApiSnapshotError instead of throwing. Throwing here would deadlock the advertised bootstrap:
//   a policy declaring `api_snapshot: architecture/api/module-api.txt` could never run the very
//   `public-api capture` that creates that file, because loading the policy would fail first.
//   Validation still fails loudly — PublicApiSurfaceChecker turns the recorded error into a
//   violation — so a broken snapshot is never mistaken for "this contract declares nothing".
internal static class PublicApiSnapshotResolver
{
    public static void Resolve(
        ArchitectureContractDocument document,
        string policyPath,
        IArchitectureFileSystem fileSystem)
    {
        string boundary = ResolveBoundary(policyPath);

        foreach (ArchitecturePublicApiSurfaceContract contract in document.Contracts.StrictPublicApiSurface
                     .Concat(document.Contracts.AuditPublicApiSurface))
        {
            contract.ApiSnapshotError = null;
            contract.ResolvedSnapshotEntries = Array.Empty<PublicApiSnapshotEntry>();

            if (string.IsNullOrWhiteSpace(contract.ApiSnapshot))
            {
                continue;
            }

            // Throws for an unsafe path; that is a configuration error, not a bootstrap state.
            string resolvedPath = ResolveSnapshotPath(
                boundary, contract.ApiSnapshot!, $"Public API surface contract '{contract.Name}'");

            contract.ResolvedSnapshotPath = resolvedPath;

            if (!fileSystem.FileExists(resolvedPath))
            {
                contract.ApiSnapshotError =
                    $"references a public API snapshot '{contract.ApiSnapshot}' that does not exist " +
                    $"(resolved to '{resolvedPath}'). Run 'arch-linter-net public-api capture " +
                    $"--contract {contract.Id ?? contract.Name} --output {contract.ApiSnapshot}' to create it.";
                continue;
            }

            try
            {
                PublicApiSnapshotDocument snapshot =
                    PublicApiSnapshotFormat.Parse(fileSystem.ReadAllText(resolvedPath), contract.ApiSnapshot!);
                contract.ApiSnapshotError = ValidateOwnership(snapshot, contract);
                if (contract.ApiSnapshotError == null)
                {
                    contract.ResolvedSnapshotEntries = snapshot.Entries;
                }
            }
            catch (InvalidOperationException exception)
            {
                contract.ApiSnapshotError = exception.Message;
            }
        }
    }

    // A snapshot is a per-contract artifact. Without these checks, contract A's file could be
    // attached to contract B — or overwritten by B's update — with no ownership error at all, and a
    // wrong `@assembly` header would validate against assemblies the contract never declared.
    public static string? ValidateOwnership(
        PublicApiSnapshotDocument snapshot,
        ArchitecturePublicApiSurfaceContract contract,
        string? authoredPath = null)
    {
        string path = authoredPath ?? contract.ApiSnapshot ?? "<snapshot>";
        string expectedContractId = contract.Id ?? contract.Name;

        if (snapshot.ContractId.Length == 0)
        {
            return $"has a public API snapshot '{path}' with no '@contract' directive. " +
                $"Recapture it for contract '{expectedContractId}'.";
        }

        if (!string.Equals(snapshot.ContractId, expectedContractId, StringComparison.OrdinalIgnoreCase))
        {
            return $"has a public API snapshot '{path}' captured for contract " +
                $"'{snapshot.ContractId}', but it is attached to contract '{expectedContractId}'. " +
                "A snapshot belongs to exactly one contract.";
        }

        HashSet<string> declaredAssemblies = new(contract.Assemblies, StringComparer.Ordinal);
        List<string> foreignAssemblies = snapshot.Entries
            .Select(entry => entry.AssemblyName)
            .Distinct(StringComparer.Ordinal)
            .Where(assembly => assembly.Length > 0 && !declaredAssemblies.Contains(assembly))
            .OrderBy(assembly => assembly, StringComparer.Ordinal)
            .ToList();

        if (foreignAssemblies.Count > 0)
        {
            return $"has a public API snapshot '{path}' describing assemblies the contract does " +
                $"not declare: {string.Join(", ", foreignAssemblies)}. Declared assemblies are " +
                $"{string.Join(", ", contract.Assemblies)}.";
        }

        return null;
    }

    // Mirrors ArchitecturePolicyPathResolver.ResolveRoot: the boundary is the policy's directory,
    // or its parent when the policy lives in an `architecture/` folder. That is what lets a policy
    // at architecture/dependencies.arch.yml reference architecture/api/module-api.txt exactly as
    // the repository lays it out.
    public static string ResolveBoundary(string policyPath)
    {
        string fullPath = Path.GetFullPath(policyPath);
        string policyDirectory = Path.GetDirectoryName(fullPath) ?? fullPath;
        return string.Equals(Path.GetFileName(policyDirectory), "architecture", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(policyDirectory) ?? policyDirectory
            : policyDirectory;
    }

    // Repository-local means: relative, non-rooted, and still inside the boundary once normalized.
    // Returns the absolute path so callers can read or write it.
    public static string ResolveSnapshotPath(string boundary, string snapshotPath, string subjectDescription)
    {
        if (Path.IsPathRooted(snapshotPath))
        {
            throw new InvalidOperationException(
                $"{subjectDescription} declares an absolute public API snapshot path '{snapshotPath}'. " +
                "Snapshot paths must be relative and stay inside the policy boundary so a policy " +
                "cannot read or write reviewed API state from outside the repository.");
        }

        string platformPath = snapshotPath.Replace('/', Path.DirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(boundary, platformPath));
        string relative = Path.GetRelativePath(boundary, candidate);

        bool escapes = Path.IsPathRooted(relative)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

        if (escapes)
        {
            throw new InvalidOperationException(
                $"{subjectDescription} declares a public API snapshot path '{snapshotPath}' that resolves " +
                $"outside the policy boundary '{boundary}'. Snapshot paths must stay repository-local.");
        }

        return candidate;
    }
}
