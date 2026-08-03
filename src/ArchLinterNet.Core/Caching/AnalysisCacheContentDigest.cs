using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
            $"mode:{entry.Mode}",
            $"tool:{entry.ToolVersion}",
            $"created:{entry.CreatedAtUtc:O}",
            $"status:{entry.CompletionStatus}",
            $"manifests:{string.Join(';', manifestLines)}",
            $"outcome:{FormatOutcome(entry.Outcome)}");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    // The reusable outcome carries deeply nested findings (violations with a closed-set polymorphic
    // Payload, baseline identities, policy locations, ...). Hand-canonicalizing every nested field
    // the way the rest of this method does for its own flat fields would duplicate
    // AnalysisCacheDiagnosticPayloadConverter's own closed-set knowledge for no integrity benefit:
    // System.Text.Json (via AnalysisCacheJson.Options, the same options used to persist the entry)
    // already writes object/collection properties in a fixed, deterministic declared-property and
    // list order, so hashing its own serialized bytes is an equally strong, drift-free integrity
    // input. This digest is recomputed from Entry.Outcome and compared byte-for-byte on every read
    // (see AnalysisCacheStore.Authorize) — any bit of the outcome changing changes this hash.
    private static string FormatOutcome(AnalysisCacheOutcomeV1 outcome) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(outcome, AnalysisCacheJson.Options)));
}
