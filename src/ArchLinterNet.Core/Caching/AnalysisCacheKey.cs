using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Caching;

// The reuse-authorization "coarse key" — every dimension a hit must prove compatible before
// per-project build-input eligibility is even considered: tool/cache-format identity, effective
// policy content, requested mode, view/settings, and configuration/TFM/platform/RID. See
// openspec/specs/analysis-cache/spec.md, "Reuse authorization requires more than a fingerprint
// match", and openspec/specs/analysis-build-state-fingerprints/spec.md's "Cache manifest identity
// is portable and trust bounded" requirement.
//
// Portable by construction: every input hashed into PortableDigest is either checkout-independent
// (schema/format/tool identity, mode, condition set, contract-id set, configuration/TFM/platform/
// RID) or repository-relative content (PolicyDigest, hashed from paths relative to repositoryRoot,
// never an absolute checkout path). No absolute path or host-specific prefix is ever hashed into
// it — two checkouts of equivalent repository content at different absolute locations produce the
// same PortableDigest, matching "Same repository state in different checkout roots".
//
// WorkspaceDigest is a separate, explicit control, not folded into the portable digest: it binds
// an entry to the repository-relative set of discovered project paths that produced it, so two
// otherwise policy-identical repositories (e.g. two unrelated checkouts that happen to author the
// same policy content) do not casually share cache entries just because their policy/contract
// digests collide. It stays portable (repository-relative paths, no absolute prefix) while adding
// a workspace/trust boundary as its own control, per "add trust-domain authorization as a separate
// control" rather than "treating fingerprint equality alone as cache authorization".
public sealed record AnalysisCacheKey(
    string PolicyDigest,
    string Mode,
    string? ConditionSetName,
    string ContractIdsDigest,
    string WorkspaceDigest,
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
                $"policy:{PolicyDigest}",
                $"mode:{Mode.ToLowerInvariant()}",
                $"conditionset:{ConditionSetName ?? string.Empty}",
                $"contracts:{ContractIdsDigest}",
                $"workspace:{WorkspaceDigest}",
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

    // One key per mode (see openspec/specs/analysis-cache/spec.md, "One reuse-authorization unit
    // per requested mode") — a combined "strict,audit" request populates/looks up one
    // AnalysisCacheKey/AnalysisCacheEntryV1 per mode, never a single entry whose stored Outcome can
    // only ever reflect one of them.
    public static string NormalizeMode(string mode) => mode.ToLowerInvariant();

    // Repository-relative content digest: hashes each policy file's own content, joined with its
    // path relative to repositoryRoot (via BuildStateCanonicalHasher.ToRepositoryRelativePath) —
    // never the absolute path — so equivalent policy content produces the same digest regardless of
    // checkout location.
    public static string ComputePolicyDigest(
        IEnumerable<string> policyFilePaths, string repositoryRoot, CancellationToken cancellationToken = default)
    {
        List<(string RelativePath, string Digest)> entries = policyFilePaths
            .Select(path => (
                RelativePath: BuildStateCanonicalHasher.ToRepositoryRelativePath(path, repositoryRoot),
                Digest: BuildStateCanonicalHasher.ComputeContentDigest(path, cancellationToken)))
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ToList();

        string canonical = string.Join('\n', entries.Select(entry => $"{entry.RelativePath}:{entry.Digest}"));
        return HashHex(canonical);
    }

    // The separate workspace/trust-domain control (see the type-level remarks above): a repository-
    // relative, ordinally sorted digest of the discovered project paths that this run's policy
    // resolved, distinguishing which workspace produced an entry without depending on any absolute
    // checkout path.
    public static string ComputeWorkspaceDigest(IEnumerable<string> discoveredProjectPaths, string repositoryRoot)
    {
        IEnumerable<string> relativePaths = discoveredProjectPaths
            .Select(path => BuildStateCanonicalHasher.ToRepositoryRelativePath(path, repositoryRoot))
            .OrderBy(path => path, StringComparer.Ordinal);
        return HashHex(string.Join('\n', relativePaths));
    }

    private static string HashHex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
