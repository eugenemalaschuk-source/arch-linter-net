using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Review finding #4: "The cache key contains the complete requested mode set, but the stored facts
// come only from outcomesByMode[0]." strict can pass while audit fails, yet the old design wrote
// Passed=true and strict-only counts under a "strict,audit"-shaped key. The fix: one
// AnalysisCacheKey/AnalysisCacheEntryV1 per requested mode (AnalysisCacheKey.Mode is a single mode
// string, never a joined set) — ValidateCommandHandler.Execution.cs's ExecuteCombinedModes now
// calls TryPopulateCache once per (mode, outcome) pair. This test proves the store-level mechanism
// that guarantees correctness: two modes evaluated from the same policy/contracts/config produce
// two independent entries, and looking each one up returns only that mode's own outcome — never the
// other mode's, and never a value derived from "the first mode only".
[TestFixture]
public sealed class AnalysisCachePerModeTests
{
    private static AnalysisCacheKey KeyForMode(string mode) => new(
        "policy-digest", mode, null, "contracts-digest", "workspace-digest", null, null, null, null);

    private static AnalysisCacheOutcomeV1 OutcomeWithPassed(bool passed) => new(
        passed, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureClassificationConflict>(), Array.Empty<ArchitectureClassificationMetadataFailure>());

    [Test]
    public void StrictAndAuditModes_PopulateIndependentEntries_NeitherOverwritesTheOther()
    {
        string root = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-per-mode-tests", Guid.NewGuid().ToString("N"));
        AnalysisCacheLocation location = new(root, AnalysisCacheMode.ExplicitPath);
        AnalysisCacheProjectManifest[] manifests =
        {
            new("src/A/A.csproj", "digest-a", CacheEligibility.VerifiedCacheEligible),
        };

        try
        {
            AnalysisCacheKey strictKey = KeyForMode("strict");
            AnalysisCacheKey auditKey = KeyForMode("audit");

            // A distinct requested mode must digest to a distinct key/file, never the same entry.
            Assert.That(strictKey.Digest, Is.Not.EqualTo(auditKey.Digest));

            // strict passes, audit (same policy/contracts/config, different mode) fails — exactly
            // the scenario the review named.
            AnalysisCacheStore.PutResult strictPut = AnalysisCacheStore.Put(location, strictKey, manifests, OutcomeWithPassed(true));
            AnalysisCacheStore.PutResult auditPut = AnalysisCacheStore.Put(location, auditKey, manifests, OutcomeWithPassed(false));
            Assert.That(strictPut.RejectReason, Is.Null);
            Assert.That(auditPut.RejectReason, Is.Null);

            AnalysisCacheLookupResult strictLookup = AnalysisCacheStore.TryGet(location, strictKey, manifests);
            AnalysisCacheLookupResult auditLookup = AnalysisCacheStore.TryGet(location, auditKey, manifests);

            Assert.That(strictLookup.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));
            Assert.That(auditLookup.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));
            Assert.That(strictLookup.Entry!.Outcome.Passed, Is.True, "strict entry must keep its own Passed=true");
            Assert.That(auditLookup.Entry!.Outcome.Passed, Is.False, "audit entry must keep its own Passed=false, never strict's true");
            Assert.That(strictLookup.Entry!.Mode, Is.EqualTo("strict"));
            Assert.That(auditLookup.Entry!.Mode, Is.EqualTo("audit"));

            // Two distinct published entries on disk — not one shared "combined" file.
            int publishedEntryCount = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Count();
            Assert.That(publishedEntryCount, Is.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
