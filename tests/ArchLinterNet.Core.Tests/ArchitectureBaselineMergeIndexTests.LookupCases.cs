using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureBaselineMergeIndexTests
{
    [Test]
    public void Merge_CaseInsensitiveDuplicateContractIdsKeepFirstPolicyContract()
    {
        ArchitectureBaselineDocument lazyBaseline = Baseline(1,
            Entry("RULE", LegacyIgnore("Loaded.Lazy", "Lazy.Forbidden", "lazy")));
        ArchitectureBaselineDocument indexedBaseline = Baseline(1,
            Enumerable.Range(0, 9)
                .Select(index => Entry(
                    index == 0 ? "RULE" : "Other",
                    LegacyIgnore($"Loaded.Indexed{index}", $"Indexed.Forbidden{index}", $"indexed-{index}")))
                .ToArray());

        AssertFirstContractMatch(lazyBaseline, expectIndex: false);
        AssertFirstContractMatch(indexedBaseline, expectIndex: true);
    }

    [TestCase(4)]
    [TestCase(8)]
    public void Merge_SmallRepeatedIdGroupUsesOneLinearLookupForEarlyMatch(int entryCount)
    {
        ArchitectureDependencyContract[] contracts = Enumerable.Range(0, 512)
            .Select(index => Contract(ContractId(index)))
            .ToArray();
        ArchitectureContractDocument policy = Policy(contracts);
        ArchitectureBaselineDocument baseline = Baseline(1,
            Enumerable.Range(0, entryCount).Select(_ => Entry(ContractId(0))).ToArray());
        ArchitectureBaselineMergeWorkCounter workCounter = new();

        ArchitectureBaselineLoadingService.Merge(policy, baseline, workCounter);

        Assert.Multiple(() =>
        {
            Assert.That(workCounter.ContractLookups, Is.EqualTo(entryCount));
            Assert.That(workCounter.ContractIndexEntries, Is.Zero);
            Assert.That(workCounter.ContractLinearScanComparisons, Is.EqualTo(1));
        });
    }

    [Test]
    public void Merge_SmallContractGroupUsesLinearLookupsForMixedIds()
    {
        ArchitectureContractDocument legacyPolicy = Policy(Contract("first"), Contract("second"));
        ArchitectureContractDocument currentPolicy = Policy(Contract("first"), Contract("second"));
        ArchitectureBaselineDocument baseline = Baseline(1,
            Enumerable.Range(0, 10)
                .Select(index => Entry(index % 2 == 0 ? "first" : "second"))
                .ToArray());
        ArchitectureBaselineMergeWorkCounter workCounter = new();

        ArchitectureBaselineMergeLegacyReference.MergeStrict(legacyPolicy, baseline);
        ArchitectureBaselineLoadingService.Merge(currentPolicy, baseline, workCounter);

        Assert.Multiple(() =>
        {
            Assert.That(
                ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(currentPolicy),
                Is.EqualTo(ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(legacyPolicy)));
            Assert.That(workCounter.ContractIndexEntries, Is.Zero);
            Assert.That(workCounter.ContractLookups, Is.EqualTo(10));
            Assert.That(workCounter.ContractLinearScanComparisons, Is.EqualTo(15));
        });
    }

    private static void AssertFirstContractMatch(
        ArchitectureBaselineDocument baseline,
        bool expectIndex)
    {
        ArchitectureContractDocument legacyPolicy = DuplicateContractPolicy();
        ArchitectureContractDocument currentPolicy = DuplicateContractPolicy();
        ArchitectureBaselineMergeWorkCounter workCounter = new();

        ArchitectureBaselineMergeLegacyReference.MergeStrict(legacyPolicy, baseline);
        ArchitectureBaselineLoadingService.Merge(currentPolicy, baseline, workCounter);

        ArchitectureDependencyContract firstMatch = currentPolicy.Contracts.Strict[0];
        ArchitectureDependencyContract caseVariantDuplicate = currentPolicy.Contracts.Strict[1];
        Assert.Multiple(() =>
        {
            Assert.That(
                ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(currentPolicy),
                Is.EqualTo(ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(legacyPolicy)));
            Assert.That(firstMatch.IgnoredViolations.Any(ignore => ignore.SourceType.StartsWith("Loaded.", StringComparison.Ordinal)), Is.True);
            Assert.That(caseVariantDuplicate.IgnoredViolations.Any(ignore => ignore.SourceType.StartsWith("Loaded.", StringComparison.Ordinal)), Is.False);
            Assert.That(workCounter.ContractIndexEntries, Is.EqualTo(expectIndex ? 6 : 0));
            Assert.That(workCounter.ContractLinearScanComparisons, Is.EqualTo(expectIndex ? 0 : 1));
        });
    }

    private static ArchitectureContractDocument DuplicateContractPolicy()
    {
        return Policy(
            Contract("Rule", [ToPolicyIgnore(LegacyIgnore("First.Inline", "First.Forbidden", "first"))]),
            Contract("rUlE", [ToPolicyIgnore(LegacyIgnore("Second.Inline", "Second.Forbidden", "second"))]),
            Contract("Other"),
            Contract("Spare.One"),
            Contract("Spare.Two"),
            Contract("Spare.Three"));
    }
}
