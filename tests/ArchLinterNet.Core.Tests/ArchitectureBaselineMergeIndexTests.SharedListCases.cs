using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureBaselineMergeIndexTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Merge_SharedIgnoreListAcrossContractsKeepsMembershipIndexCurrent(int version)
    {
        ArchitectureContractDocument legacyPolicy = CreateSharedIgnoreListPolicy(version);
        ArchitectureContractDocument indexedPolicy = CreateSharedIgnoreListPolicy(version);
        ArchitectureBaselineDocument baseline = CreateSharedIgnoreListBaseline(version);
        List<ArchitectureIgnoredViolation> legacyIgnores = legacyPolicy.Contracts.Strict[0].IgnoredViolations;
        List<ArchitectureIgnoredViolation> indexedIgnores = indexedPolicy.Contracts.Strict[0].IgnoredViolations;

        Assert.That(legacyPolicy.Contracts.Strict[1].IgnoredViolations, Is.SameAs(legacyIgnores));
        Assert.That(indexedPolicy.Contracts.Strict[1].IgnoredViolations, Is.SameAs(indexedIgnores));

        ArchitectureBaselineMergeLegacyReference.MergeStrict(legacyPolicy, baseline);
        ArchitectureBaselineLoadingService.Merge(indexedPolicy, baseline);

        string expectedDigest = ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(legacyPolicy);
        string actualDigest = ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(indexedPolicy);
        Assert.Multiple(() =>
        {
            Assert.That(actualDigest, Is.EqualTo(expectedDigest));
            Assert.That(legacyIgnores, Has.Count.EqualTo(11));
            Assert.That(indexedIgnores, Has.Count.EqualTo(11));
            Assert.That(indexedIgnores[^2].Reason, Is.EqualTo("loaded-x-first"));
            Assert.That(indexedIgnores[^1].Reason, Is.EqualTo("loaded-y-first"));
            Assert.That(indexedIgnores[^1].IsFromLoadedBaseline, Is.True);
        });
    }

    private static ArchitectureContractDocument CreateSharedIgnoreListPolicy(int version)
    {
        var sharedIgnores = Enumerable.Range(0, 9)
            .Select(index => ToPolicyIgnore(SharedIgnore(version, $"inline-{index}", $"inline-{index}")))
            .ToList();
        ArchitectureDependencyContract firstContract = Contract("A");
        ArchitectureDependencyContract secondContract = Contract("B");
        firstContract.IgnoredViolations = sharedIgnores;
        secondContract.IgnoredViolations = sharedIgnores;
        return Policy(firstContract, secondContract);
    }

    private static ArchitectureBaselineDocument CreateSharedIgnoreListBaseline(int version)
    {
        return Baseline(version,
            Entry("A", SharedIgnore(version, "x", "loaded-x-first")),
            Entry("B", SharedIgnore(version, "y", "loaded-y-first")),
            Entry("A", SharedIgnore(version, "y", "loaded-y-later")));
    }

    private static ArchitectureBaselineIgnoredViolation SharedIgnore(int version, string identity, string reason)
    {
        return version switch
        {
            1 => LegacyIgnore($"Source.{identity}", $"Forbidden.{identity}", reason),
            2 or 3 => StructuredBaselineIgnore(
                sourceMember: $"Member.{identity}",
                reason,
                forbiddenReference: $"Forbidden.{identity}"),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
    }
}
