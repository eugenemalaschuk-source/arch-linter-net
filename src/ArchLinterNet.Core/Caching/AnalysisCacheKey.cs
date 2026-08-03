using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Caching;

// The reuse-authorization "coarse key" — every dimension a hit must prove compatible before
// per-project build-input eligibility is even considered: tool/cache-format identity, workspace
// binding, effective policy content, requested view/settings, and configuration/TFM/platform/RID.
// See openspec/specs/analysis-cache/spec.md, "Reuse authorization requires more than a fingerprint
// match".
public sealed record AnalysisCacheKey(
    string RepositoryRootDigest,
    string PolicyDigest,
    string ModeSet,
    string? ConditionSetName,
    string ContractIdsDigest,
    string? Configuration,
    string? TargetFramework,
    string? Platform,
    string? RuntimeIdentifier)
{
    public string Digest
    {
        get
        {
            string canonical = string.Join(
                '\n',
                AnalysisCacheEnvelope.SchemaId,
                $"format:{AnalysisCacheEnvelope.FormatVersion}",
                $"tool:{AnalysisCacheEnvelope.ToolVersion}",
                $"repo:{RepositoryRootDigest}",
                $"policy:{PolicyDigest}",
                $"modes:{ModeSet}",
                $"conditionset:{ConditionSetName ?? string.Empty}",
                $"contracts:{ContractIdsDigest}",
                $"configuration:{Configuration ?? string.Empty}",
                $"tfm:{TargetFramework ?? string.Empty}",
                $"platform:{Platform ?? string.Empty}",
                $"rid:{RuntimeIdentifier ?? string.Empty}");
            return HashHex(canonical);
        }
    }

    public static string ComputeContractIdsDigest(IEnumerable<string> contractIds)
    {
        string joined = string.Join(',', contractIds.OrderBy(id => id, StringComparer.Ordinal));
        return HashHex(joined);
    }

    public static string ComputeModeSet(IEnumerable<string> modes)
    {
        return string.Join(',', modes.Select(m => m.ToLowerInvariant()).OrderBy(m => m, StringComparer.Ordinal));
    }

    // Hashed rather than stored verbatim: identity must be portable across checkout roots (see
    // openspec/specs/analysis-build-state-fingerprints/spec.md, "Same repository state in
    // different checkout roots") while still binding a cache entry to one workspace/trust domain.
    public static string ComputeRepositoryRootDigest(string repositoryRoot)
    {
        string normalized = Path.GetFullPath(repositoryRoot)
            .Replace('\\', '/')
            .TrimEnd('/');
        return HashHex(normalized);
    }

    public static string ComputePolicyDigest(IEnumerable<string> policyFilePaths, CancellationToken cancellationToken = default)
    {
        List<(string Path, string Digest)> entries = policyFilePaths
            .Select(path => (
                Path: Path.GetFullPath(path).Replace('\\', '/'),
                Digest: BuildStateCanonicalHasher.ComputeContentDigest(path, cancellationToken)))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToList();

        string canonical = string.Join('\n', entries.Select(entry => $"{entry.Path}:{entry.Digest}"));
        return HashHex(canonical);
    }

    private static string HashHex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
