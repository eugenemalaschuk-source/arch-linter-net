using System.Security.Cryptography;
using System.Text;

namespace ArchLinterNet.Core.Caching;

// Computes the canonical content digest for an AnalysisCacheEntryV1, independent of JSON
// serialization/property ordering (which System.Text.Json does not guarantee to be a stable
// hashing surface across versions) — built the same way BuildStateCanonicalHasher/
// EvaluatedBuildInputManifestCollector canonicalize their own digests: an explicit ordinal join.
internal static class AnalysisCacheContentDigest
{
    public static string Compute(AnalysisCacheEntryV1 entry)
    {
        IEnumerable<string> manifestLines = entry.ProjectManifests
            .OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal)
            .Select(manifest => $"{manifest.ProjectPath}|{manifest.ManifestDigest}|{manifest.Eligibility}");

        string canonical = string.Join(
            '\n',
            AnalysisCacheEnvelope.SchemaId,
            $"format:{entry.FormatVersion}",
            $"key:{entry.KeyDigest}",
            $"tool:{entry.ToolVersion}",
            $"created:{entry.CreatedAtUtc:O}",
            $"status:{entry.CompletionStatus}",
            $"manifests:{string.Join(';', manifestLines)}",
            $"facts:{FormatFacts(entry.Facts)}");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string FormatFacts(AnalysisCacheFactsV1 facts) => string.Join('|', new object[]
    {
        facts.Passed,
        facts.ViolationCount,
        facts.CoverageFindingCount,
        facts.CycleCount,
        facts.UnmatchedIgnoredViolationCount,
        facts.PolicyConsistencyFindingCount,
        facts.ClassificationConflictCount,
        facts.ClassificationMetadataFailureCount,
        facts.DiscoveredProjectCount,
        facts.RetainedAssemblyCount,
        facts.SelectedAssemblyCount,
    });
}
