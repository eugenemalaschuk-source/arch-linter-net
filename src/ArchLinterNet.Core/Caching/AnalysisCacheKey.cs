using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;

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
    string? RuntimeIdentifier,
    // Finding #2: every remaining result-affecting dimension on AnalysisSnapshotRequest/
    // ValidationRequest folded into the key — a run that differs only in one of these must derive
    // a different AnalysisCacheKey.Digest and can never reuse another run's outcome.
    // PreprocessorSymbolsDigest: an order-independent set digest (see
    // ComputePreprocessorSymbolsDigest), empty string when no symbols were requested — symbols
    // change which #if/#else branches conditional-compilation-aware contracts observe.
    string PreprocessorSymbolsDigest = "",
    // BaselineDigest: a content digest (see ComputeBaselineDigest) of the configured baseline
    // file, empty string when no baseline is configured — the baseline's own path is deliberately
    // not part of this (matching PolicyDigest's own "content, never the absolute path" contract),
    // so only a changed baseline's content invalidates reuse, not where it happens to live on disk.
    string BaselineDigest = "",
    bool IncludeAsmdefContracts = true,
    bool EnforceUnmatchedIgnoredViolationsPolicy = false)
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
                $"rid:{RuntimeIdentifier ?? string.Empty}",
                $"symbols:{PreprocessorSymbolsDigest}",
                $"baseline:{BaselineDigest}",
                $"asmdef:{IncludeAsmdefContracts}",
                $"enforceunmatched:{EnforceUnmatchedIgnoredViolationsPolicy}");
            return HashHex(canonical);
        }
    }

    public static string ComputeContractIdsDigest(IEnumerable<string> contractIds)
    {
        string joined = string.Join(',', contractIds.OrderBy(id => id, StringComparer.Ordinal));
        return HashHex(joined);
    }

    // Order-independent, same shape as ComputeContractIdsDigest — two requests naming the same
    // preprocessor symbols in a different order must derive the same digest, but a genuinely
    // different symbol set must not.
    public static string ComputePreprocessorSymbolsDigest(IEnumerable<string>? preprocessorSymbols)
    {
        if (preprocessorSymbols is null)
        {
            return string.Empty;
        }

        string joined = string.Join(',', preprocessorSymbols.OrderBy(symbol => symbol, StringComparer.Ordinal));
        return joined.Length == 0 ? string.Empty : HashHex(joined);
    }

    // Content digest of the configured baseline file — mirrors ComputePolicyDigest's own
    // "content, never the checkout-specific absolute path" contract. Empty string (never hashed)
    // when no baseline is configured, so "no baseline" and "a baseline whose content happens to
    // hash to some value" can never collide.
    public static string ComputeBaselineDigest(string? baselinePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(baselinePath))
        {
            return string.Empty;
        }

        return BuildStateCanonicalHasher.ComputeContentDigest(baselinePath, cancellationToken);
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

    // The snapshot already parsed these decoded-text values. Do not re-read the mutable policy
    // paths merely to construct a key: that could pair a document loaded from state A with a key
    // for state B. Prepare revalidates the identities before lookup and publication.
    internal static string ComputePolicyDigest(
        IEnumerable<ArchitectureLoadedTextIdentity> policyInputs,
        string repositoryRoot)
    {
        List<(string RelativePath, string Digest)> entries = policyInputs
            .Select(input => (
                RelativePath: BuildStateCanonicalHasher.ToRepositoryRelativePath(input.FullPath, repositoryRoot),
                Digest: input.ContentDigest))
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
