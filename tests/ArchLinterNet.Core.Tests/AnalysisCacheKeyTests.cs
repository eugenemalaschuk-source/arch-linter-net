using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AnalysisCacheKeyTests
{
    private static readonly string[] _value = { "b", "a" };
    private static readonly string[] _value1 = { "a", "b" };
    private static readonly string[] _value2 = { "DEBUG" };
    private static readonly string[] _value3 = { "RELEASE" };
    private static readonly string[] _value4 = { "B", "A" };
    private static readonly string[] _value5 = { "A", "B" };
    private static readonly string[] _value6 = { "A" };
    private static AnalysisCacheKey CreateKey(string configuration = "Debug") => new(
        "policy", "strict", null, "contracts", "workspace", configuration, "net10.0", null, null);

    [Test]
    public void Digest_IsStableForEquivalentKeys()
    {
        Assert.That(CreateKey().Digest, Is.EqualTo(CreateKey().Digest));
    }

    [Test]
    public void Digest_ChangesWhenConfigurationChanges()
    {
        Assert.That(CreateKey("Debug").Digest, Is.Not.EqualTo(CreateKey("Release").Digest));
    }

    [Test]
    public void Digest_ChangesWhenModeChanges()
    {
        AnalysisCacheKey strict = CreateKey() with { Mode = "strict" };
        AnalysisCacheKey audit = CreateKey() with { Mode = "audit" };
        Assert.That(strict.Digest, Is.Not.EqualTo(audit.Digest));
    }

    [Test]
    public void Digest_ChangesWhenWorkspaceDigestChanges()
    {
        AnalysisCacheKey a = CreateKey() with { WorkspaceDigest = "workspace-a" };
        AnalysisCacheKey b = CreateKey() with { WorkspaceDigest = "workspace-b" };
        Assert.That(a.Digest, Is.Not.EqualTo(b.Digest));
    }

    // Finding #5: portable identity — a cache key built from equivalent repository content must be
    // identical regardless of which absolute checkout root produced it. This is the concrete,
    // regression-guarding version of the review's "Same repository state in different checkout
    // roots" requirement: ComputePolicyDigest must never fold the absolute checkout path into the
    // digest, only the policy file's own content plus its path *relative* to repositoryRoot.
    [Test]
    public void ComputePolicyDigest_IsPortableAcrossEquivalentCheckoutRoots()
    {
        string checkoutA = Path.Combine(Path.GetTempPath(), "arch-linter-net-key-tests-a-" + Guid.NewGuid().ToString("N"));
        string checkoutB = Path.Combine(Path.GetTempPath(), "arch-linter-net-key-tests-b-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(checkoutA, "architecture"));
        Directory.CreateDirectory(Path.Combine(checkoutB, "architecture"));
        string policyContent = "schema: dependencies/v1\nanalysis: {}\n";
        string policyPathA = Path.Combine(checkoutA, "architecture", "dependencies.arch.yml");
        string policyPathB = Path.Combine(checkoutB, "architecture", "dependencies.arch.yml");
        File.WriteAllText(policyPathA, policyContent);
        File.WriteAllText(policyPathB, policyContent);

        try
        {
            string digestA = AnalysisCacheKey.ComputePolicyDigest(new[] { policyPathA }, checkoutA);
            string digestB = AnalysisCacheKey.ComputePolicyDigest(new[] { policyPathB }, checkoutB);

            Assert.That(digestA, Is.EqualTo(digestB));
        }
        finally
        {
            Directory.Delete(checkoutA, recursive: true);
            Directory.Delete(checkoutB, recursive: true);
        }
    }

    [Test]
    public void ComputeWorkspaceDigest_IsPortableAcrossEquivalentCheckoutRoots()
    {
        string checkoutA = Path.Combine(Path.GetTempPath(), "repo-a-" + Guid.NewGuid().ToString("N"));
        string checkoutB = Path.Combine(Path.GetTempPath(), "repo-b-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(checkoutA, "src", "A"));
        Directory.CreateDirectory(Path.Combine(checkoutB, "src", "A"));

        try
        {
            string digestA = AnalysisCacheKey.ComputeWorkspaceDigest(
                new[] { Path.Combine(checkoutA, "src", "A", "A.csproj") }, checkoutA);
            string digestB = AnalysisCacheKey.ComputeWorkspaceDigest(
                new[] { Path.Combine(checkoutB, "src", "A", "A.csproj") }, checkoutB);

            Assert.That(digestA, Is.EqualTo(digestB));
        }
        finally
        {
            Directory.Delete(checkoutA, recursive: true);
            Directory.Delete(checkoutB, recursive: true);
        }
    }

    [Test]
    public void ComputeContractIdsDigest_IsOrderIndependent()
    {
        string a = AnalysisCacheKey.ComputeContractIdsDigest(_value);
        string b = AnalysisCacheKey.ComputeContractIdsDigest(_value1);
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void NormalizeMode_IsLowercase()
    {
        Assert.That(AnalysisCacheKey.NormalizeMode("STRICT"), Is.EqualTo("strict"));
    }

    // Finding #2: every remaining result-affecting AnalysisSnapshotRequest/ValidationRequest
    // dimension must be folded into AnalysisCacheKey — one invalidation regression per dimension,
    // same everything else, only that one field differs.
    [Test]
    public void Digest_ChangesWhenPreprocessorSymbolsDigestChanges()
    {
        AnalysisCacheKey a = CreateKey() with { PreprocessorSymbolsDigest = AnalysisCacheKey.ComputePreprocessorSymbolsDigest(_value2) };
        AnalysisCacheKey b = CreateKey() with { PreprocessorSymbolsDigest = AnalysisCacheKey.ComputePreprocessorSymbolsDigest(_value3) };
        Assert.That(a.Digest, Is.Not.EqualTo(b.Digest));
    }

    [Test]
    public void ComputePreprocessorSymbolsDigest_IsOrderIndependentButSetSensitive()
    {
        string a = AnalysisCacheKey.ComputePreprocessorSymbolsDigest(_value4);
        string b = AnalysisCacheKey.ComputePreprocessorSymbolsDigest(_value5);
        string c = AnalysisCacheKey.ComputePreprocessorSymbolsDigest(_value6);

        Assert.That(a, Is.EqualTo(b));
        Assert.That(a, Is.Not.EqualTo(c));
    }

    [Test]
    public void ComputePreprocessorSymbolsDigest_NullAndEmpty_AreBothEmptyString()
    {
        Assert.That(AnalysisCacheKey.ComputePreprocessorSymbolsDigest(null), Is.Empty);
        Assert.That(AnalysisCacheKey.ComputePreprocessorSymbolsDigest(Array.Empty<string>()), Is.Empty);
    }

    [Test]
    public void Digest_ChangesWhenBaselineDigestChanges()
    {
        AnalysisCacheKey a = CreateKey() with { BaselineDigest = "baseline-a" };
        AnalysisCacheKey b = CreateKey() with { BaselineDigest = "baseline-b" };
        Assert.That(a.Digest, Is.Not.EqualTo(b.Digest));
    }

    // Finding #2's "content, not just path" requirement: a changed baseline file must invalidate
    // reuse even when the baseline's own path is unchanged.
    [Test]
    public void ComputeBaselineDigest_ChangesWhenBaselineContentChanges()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"arch-linter-baseline-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(baselinePath, "{\"version\":1}");
            string digestBefore = AnalysisCacheKey.ComputeBaselineDigest(baselinePath);

            File.WriteAllText(baselinePath, "{\"version\":2}");
            string digestAfter = AnalysisCacheKey.ComputeBaselineDigest(baselinePath);

            Assert.That(digestBefore, Is.Not.EqualTo(digestAfter));
        }
        finally
        {
            File.Delete(baselinePath);
        }
    }

    [Test]
    public void ComputeBaselineDigest_NoBaseline_IsEmptyString()
    {
        Assert.That(AnalysisCacheKey.ComputeBaselineDigest(null), Is.Empty);
    }

    [Test]
    public void Digest_ChangesWhenIncludeAsmdefContractsChanges()
    {
        AnalysisCacheKey a = CreateKey() with { IncludeAsmdefContracts = true };
        AnalysisCacheKey b = CreateKey() with { IncludeAsmdefContracts = false };
        Assert.That(a.Digest, Is.Not.EqualTo(b.Digest));
    }

    [Test]
    public void Digest_ChangesWhenEnforceUnmatchedIgnoredViolationsPolicyChanges()
    {
        AnalysisCacheKey a = CreateKey() with { EnforceUnmatchedIgnoredViolationsPolicy = true };
        AnalysisCacheKey b = CreateKey() with { EnforceUnmatchedIgnoredViolationsPolicy = false };
        Assert.That(a.Digest, Is.Not.EqualTo(b.Digest));
    }

    [Test]
    public void Digest_ChangesWhenWaiverEvaluationDateChanges()
    {
        AnalysisCacheKey a = CreateKey() with { WaiverEvaluationDate = "2026-08-28" };
        AnalysisCacheKey b = CreateKey() with { WaiverEvaluationDate = "2026-08-29" };

        Assert.That(a.Digest, Is.Not.EqualTo(b.Digest));
    }
}
