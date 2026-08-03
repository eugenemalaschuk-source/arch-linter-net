using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AnalysisCacheKeyTests
{
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
        string a = AnalysisCacheKey.ComputeContractIdsDigest(new[] { "b", "a" });
        string b = AnalysisCacheKey.ComputeContractIdsDigest(new[] { "a", "b" });
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void NormalizeMode_IsLowercase()
    {
        Assert.That(AnalysisCacheKey.NormalizeMode("STRICT"), Is.EqualTo("strict"));
    }
}
