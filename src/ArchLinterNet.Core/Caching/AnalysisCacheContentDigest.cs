using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Caching;

// Finding #1: this is now a keyed authenticity tag (HMAC-SHA256), not an unkeyed content hash.
// The prior SHA256.HashData(...) form only proved an entry's bytes were internally consistent —
// anyone who can write the entry file can also recompute a matching unkeyed hash, so it never
// proved ArchLinterNet itself produced the entry. Keying it with AnalysisCacheHmacKeyStore's local,
// cache-root-scoped secret closes that gap: see that type's own remarks for the exact threat model
// this does (and does not) cover. The field name (AnalysisCacheEntryV1.ContentDigest) is left
// unchanged deliberately — this is a minimal, in-place strengthening of what that field means, not
// a schema/API rename.
//
// Canonicalization itself is unchanged and independent of JSON serialization/property ordering
// (which System.Text.Json does not guarantee to be a stable hashing surface across versions) —
// built the same way BuildStateCanonicalHasher/EvaluatedBuildInputManifestCollector canonicalize
// their own digests: an explicit ordinal join, now HMAC'd instead of plain-hashed.
internal static class AnalysisCacheContentDigest
{
    public static string Compute(AnalysisCacheEntryV1 entry, string cacheRootPath)
    {
        byte[] key = AnalysisCacheHmacKeyStore.GetOrCreateKey(cacheRootPath);
        byte[] canonicalBytes = Encoding.UTF8.GetBytes(BuildCanonicalForm(entry));
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, canonicalBytes));
    }

    // Constant-time comparison against a stored tag — avoids a timing side channel on the compare
    // (a plain string.Equals/Ordinal short-circuits on the first mismatched byte). Any length
    // mismatch (e.g. a tampered or foreign-format ContentDigest that isn't even 64 hex chars) is
    // treated as an immediate non-match without attempting a byte comparison — this alone is not
    // secret-dependent and does not reintroduce the timing channel FixedTimeEquals defends against.
    public static bool Verify(AnalysisCacheEntryV1 entry, string cacheRootPath, string storedTagHex)
    {
        string expectedHex = Compute(entry, cacheRootPath);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedHex);
        byte[] storedBytes = Encoding.UTF8.GetBytes(storedTagHex);
        return expectedBytes.Length == storedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, storedBytes);
    }

    // Cache persistence is optional. Key-store access, malformed-but-deserializable object graphs,
    // and serialization failures must therefore become a typed cache miss/recompute path rather
    // than escaping from a lookup and failing validation itself.
    public static bool TryVerify(
        AnalysisCacheEntryV1 entry,
        string cacheRootPath,
        string storedTagHex,
        out AnalysisCacheRejectReason failureReason)
    {
        try
        {
            bool valid = Verify(entry, cacheRootPath, storedTagHex);
            failureReason = valid ? default : AnalysisCacheRejectReason.IntegrityMismatch;
            return valid;
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            failureReason = AnalysisCacheRejectReason.PathUnsafe;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or InvalidOperationException)
        {
            failureReason = AnalysisCacheRejectReason.Corrupt;
            return false;
        }
    }

    public static bool TryCompute(
        AnalysisCacheEntryV1 entry,
        string cacheRootPath,
        out string? contentDigest,
        out AnalysisCacheRejectReason failureReason)
    {
        try
        {
            contentDigest = Compute(entry, cacheRootPath);
            failureReason = default;
            return true;
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            contentDigest = null;
            failureReason = AnalysisCacheRejectReason.PathUnsafe;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or InvalidOperationException)
        {
            contentDigest = null;
            failureReason = AnalysisCacheRejectReason.Corrupt;
            return false;
        }
    }

    private static string BuildCanonicalForm(AnalysisCacheEntryV1 entry)
    {
        IEnumerable<string> manifestLines = entry.ProjectManifests
            .OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal)
            .Select(manifest => $"{manifest.ProjectPath}|{manifest.ManifestDigest}|{manifest.Eligibility}");
        IEnumerable<string> artifactLines = entry.ArtifactManifests
            .OrderBy(manifest => manifest.ArtifactPath, StringComparer.Ordinal)
            .Select(manifest => $"{manifest.ArtifactPath}|{manifest.ContentDigest}");

        return string.Join(
            '\n',
            AnalysisCacheEnvelope.SchemaId,
            $"format:{entry.FormatVersion}",
            $"key:{entry.KeyDigest}",
            $"mode:{entry.Mode}",
            $"tool:{entry.ToolVersion}",
            $"created:{entry.CreatedAtUtc:O}",
            $"status:{entry.CompletionStatus}",
            $"manifests:{string.Join(';', manifestLines)}",
            $"artifacts:{string.Join(';', artifactLines)}",
            $"outcome:{FormatOutcome(entry.Outcome)}");
    }

    // The reusable outcome carries deeply nested findings (violations with a closed-set polymorphic
    // Payload, baseline identities, policy locations, ...). Hand-canonicalizing every nested field
    // the way the rest of this method does for its own flat fields would duplicate
    // AnalysisCacheDiagnosticPayloadConverter's own closed-set knowledge for no integrity benefit:
    // System.Text.Json (via AnalysisCacheJson.Options, the same options used to persist the entry)
    // already writes object/collection properties in a fixed, deterministic declared-property and
    // list order, so hashing its own serialized bytes is an equally strong, drift-free integrity
    // input. This digest is recomputed from Entry.Outcome and combined into the same HMAC input on
    // every read (see AnalysisCacheStore.Authorize) — any bit of the outcome changing changes the tag.
    private static string FormatOutcome(AnalysisCacheOutcomeV1 outcome) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(outcome, AnalysisCacheJson.Options)));
}
