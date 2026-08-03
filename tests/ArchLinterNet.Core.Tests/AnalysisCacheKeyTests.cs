using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AnalysisCacheKeyTests
{
    private static AnalysisCacheKey CreateKey(string configuration = "Debug") => new(
        "repo", "policy", "strict", null, "contracts", configuration, "net10.0", null, null);

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
    public void ComputeRepositoryRootDigest_IsPortableAcrossEquivalentAbsolutePaths()
    {
        string a = Path.Combine(Path.GetTempPath(), "repo-a");
        Directory.CreateDirectory(a);
        try
        {
            string digest1 = AnalysisCacheKey.ComputeRepositoryRootDigest(a);
            string digest2 = AnalysisCacheKey.ComputeRepositoryRootDigest(a + Path.DirectorySeparatorChar);
            Assert.That(digest1, Is.EqualTo(digest2));
        }
        finally
        {
            Directory.Delete(a);
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
    public void ComputeModeSet_IsOrderIndependentAndLowercase()
    {
        Assert.That(AnalysisCacheKey.ComputeModeSet(new[] { "AUDIT", "strict" }),
            Is.EqualTo(AnalysisCacheKey.ComputeModeSet(new[] { "strict", "audit" })));
    }
}
