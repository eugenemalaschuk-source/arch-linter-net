using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineMergeIndexTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Merge_GeneratedInputs_MatchesLegacyOrderedOutput(int version)
    {
        ArchitectureContractDocument legacyPolicy = CreateGeneratedPolicy(version);
        ArchitectureContractDocument indexedPolicy = CreateGeneratedPolicy(version);
        ArchitectureBaselineDocument baseline = CreateGeneratedBaseline(version);

        ArchitectureBaselineMergeLegacyReference.MergeStrict(legacyPolicy, baseline);
        ArchitectureBaselineMergeWorkCounter workCounter = new();
        ArchitectureBaselineLoadingService.Merge(indexedPolicy, baseline, workCounter);

        Assert.Multiple(() =>
        {
            Assert.That(
                ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(indexedPolicy),
                Is.EqualTo(ArchitectureBaselineMergeLegacyReference.StrictIgnoreDigest(legacyPolicy)));
            Assert.That(workCounter.ContractIndexEntries, Is.EqualTo(12));
            Assert.That(workCounter.ContractLookups, Is.EqualTo(24));
            Assert.That(workCounter.MembershipChecks, Is.GreaterThan(0));
            Assert.That(workCounter.LoadedIgnoresAccepted, Is.GreaterThan(0));
        });
    }

    [Test]
    public void Merge_Version1UsesOrdinalDisplayPairAndPreservesFirstReasonAndOrder()
    {
        ArchitectureDependencyContract contract = Contract("rule", new List<ArchitectureIgnoredViolation>
        {
            ToPolicyIgnore(LegacyIgnore("A.Type", "B.Type", "inline-first")),
        });
        ArchitectureContractDocument policy = Policy(contract);
        ArchitectureBaselineDocument baseline = Baseline(1,
            Entry("RULE", LegacyIgnore("A.Type", "B.Type", "loaded-duplicate"),
                LegacyIgnore("a.Type", "B.Type", "case-distinct"),
                LegacyIgnore("A.Type", "b.Type", "reference-case-distinct"),
                LegacyIgnore("C.Type", "D.Type", "loaded-first"),
                LegacyIgnore("C.Type", "D.Type", "loaded-duplicate")));

        ArchitectureBaselineLoadingService.Merge(policy, baseline);

        ArchitectureIgnoredViolation[] ignores = contract.IgnoredViolations.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(ignores.Select(ignore => ignore.SourceType),
                Is.EqualTo(new[] { "A.Type", "a.Type", "A.Type", "C.Type" }));
            Assert.That(ignores.Select(ignore => ignore.Reason),
                Is.EqualTo(new[] { "inline-first", "case-distinct", "reference-case-distinct", "loaded-first" }));
            Assert.That(ignores[0].IsFromLoadedBaseline, Is.False);
            Assert.That(ignores.Skip(1).All(ignore => ignore.IsFromLoadedBaseline), Is.True);
        });
    }

    [TestCase(2)]
    [TestCase(3)]
    public void Merge_StructuredKeyKeepsCurrentVersionRuleAndExistingFieldOmissions(int version)
    {
        ArchitectureIgnoredViolation sameStructuredIdentity = StructuredPolicyIgnore(
            sourceMember: "same-member",
            reason: "inline-reason",
            kind: "dependency",
            forbiddenReference: "Inline.Forbidden",
            identityVersion: ArchitectureViolationIdentity.CurrentVersion);
        ArchitectureIgnoredViolation legacyStructuredIdentity = StructuredPolicyIgnore(
            sourceMember: "old-version-member",
            reason: "legacy-inline",
            identityVersion: ArchitectureViolationIdentity.CurrentVersion - 1);
        ArchitectureDependencyContract contract = Contract("rule", new List<ArchitectureIgnoredViolation>
        {
            sameStructuredIdentity,
            legacyStructuredIdentity,
        });
        ArchitectureContractDocument policy = Policy(contract);
        ArchitectureBaselineIgnoredViolation duplicateByExistingKey = StructuredBaselineIgnore(
            sourceMember: "same-member",
            reason: "loaded-duplicate",
            kind: "different-kind",
            forbiddenReference: "Loaded.Forbidden");
        ArchitectureBaselineIgnoredViolation distinctMember = StructuredBaselineIgnore(
            sourceMember: "distinct-member",
            reason: "loaded-first");
        ArchitectureBaselineIgnoredViolation matchesOnlyOldInlineVersion = StructuredBaselineIgnore(
            sourceMember: "old-version-member",
            reason: "old-identity-not-a-match");
        ArchitectureBaselineDocument baseline = Baseline(version,
            Entry("rule", duplicateByExistingKey, distinctMember, matchesOnlyOldInlineVersion));

        ArchitectureBaselineLoadingService.Merge(policy, baseline);

        Assert.Multiple(() =>
        {
            Assert.That(contract.IgnoredViolations, Has.Count.EqualTo(4));
            Assert.That(contract.IgnoredViolations[0].Reason, Is.EqualTo("inline-reason"));
            Assert.That(contract.IgnoredViolations[0].Kind, Is.EqualTo("dependency"));
            Assert.That(contract.IgnoredViolations[1].Reason, Is.EqualTo("legacy-inline"));
            Assert.That(contract.IgnoredViolations[2].Reason, Is.EqualTo("loaded-first"));
            Assert.That(contract.IgnoredViolations[3].Reason, Is.EqualTo("old-identity-not-a-match"));
            Assert.That(contract.IgnoredViolations[2].IsFromLoadedBaseline, Is.True);
            Assert.That(contract.IgnoredViolations[3].IsFromLoadedBaseline, Is.True);
        });
    }

    [TestCase(1, 1)]
    [TestCase(32, 64)]
    [TestCase(128, 256)]
    [TestCase(512, 64)]
    public void Merge_ContractAndEntryWorkIsLinear(int contractCount, int entryCount)
    {
        ArchitectureDependencyContract[] contracts = Enumerable.Range(0, contractCount)
            .Select(index => Contract(ContractId(index)))
            .ToArray();
        ArchitectureContractDocument policy = Policy(contracts);
        ArchitectureBaselineDocument baseline = Baseline(1,
            Enumerable.Range(0, entryCount)
                .Select(_ => Entry(ContractId(contractCount - 1)))
                .ToArray());
        ArchitectureBaselineMergeWorkCounter workCounter = new();

        ArchitectureBaselineLoadingService.Merge(policy, baseline, workCounter);

        Assert.Multiple(() =>
        {
            Assert.That(workCounter.ContractIndexEntries, Is.EqualTo(contractCount));
            Assert.That(workCounter.ContractLookups, Is.EqualTo(entryCount));
            Assert.That(workCounter.MembershipChecks, Is.Zero);
            Assert.That(workCounter.InlineIgnoreIndexEntries, Is.Zero);
        });
    }

    [TestCase(1, 0)]
    [TestCase(100, 50)]
    [TestCase(1_000, 500)]
    public void Merge_IgnoreWorkSeedsInlineOnceAndChecksEachLoadedKey(int totalIgnoreCount, int inlineIgnoreCount)
    {
        int loadedIgnoreCount = totalIgnoreCount - inlineIgnoreCount;
        ArchitectureDependencyContract contract = Contract("rule",
            Enumerable.Range(0, inlineIgnoreCount)
                .Select(index => ToPolicyIgnore(LegacyIgnore($"Source{index}", $"Target{index}", "inline")))
                .ToArray());
        ArchitectureContractDocument policy = Policy(contract);
        ArchitectureBaselineDocument baseline = Baseline(1,
            Entry("rule", Enumerable.Range(inlineIgnoreCount, loadedIgnoreCount)
                .Select(index => LegacyIgnore($"Source{index}", $"Target{index}", "loaded"))
                .ToArray()));
        ArchitectureBaselineMergeWorkCounter workCounter = new();

        ArchitectureBaselineLoadingService.Merge(policy, baseline, workCounter);

        Assert.Multiple(() =>
        {
            Assert.That(workCounter.InlineIgnoreIndexEntries, Is.EqualTo(inlineIgnoreCount));
            Assert.That(workCounter.MembershipChecks, Is.EqualTo(loadedIgnoreCount));
            Assert.That(workCounter.LoadedIgnoresAccepted, Is.EqualTo(loadedIgnoreCount));
            Assert.That(contract.IgnoredViolations, Has.Count.EqualTo(totalIgnoreCount));
        });
    }

    private static ArchitectureContractDocument CreateGeneratedPolicy(int version)
    {
        ArchitectureDependencyContract[] contracts = Enumerable.Range(0, 12)
            .Select(index => Contract(index == 4 ? "rUlE03" : ContractId(index)))
            .ToArray();
        for (int contractIndex = 0; contractIndex < contracts.Length; contractIndex++)
        {
            int seedCount = contractIndex % 3;
            for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
            {
                ArchitectureBaselineIgnoredViolation seed = GeneratedIgnore(version, (contractIndex + seedIndex) % 7);
                contracts[contractIndex].IgnoredViolations.Add(ToPolicyIgnore(seed));
            }
        }

        return Policy(contracts);
    }

    private static ArchitectureBaselineDocument CreateGeneratedBaseline(int version)
    {
        ArchitectureBaselineContractEntry[] entries = Enumerable.Range(0, 24)
            .Select(entryIndex =>
            {
                int contractIndex = entryIndex % 12;
                string id = contractIndex == 3 && entryIndex % 2 == 0
                    ? "rUlE03"
                    : ContractId(contractIndex);
                ArchitectureBaselineIgnoredViolation[] ignores = Enumerable.Range(0, 3)
                    .Select(ignoreIndex => GeneratedIgnore(version, (entryIndex + ignoreIndex) % 7))
                    .ToArray();
                return Entry(id, ignores);
            })
            .ToArray();

        return Baseline(version, entries);
    }

    private static ArchitectureBaselineIgnoredViolation GeneratedIgnore(int version, int index)
    {
        ArchitectureBaselineIgnoredViolation ignore = LegacyIgnore(
            $"SourceType{index}",
            $"Forbidden{index % 3}",
            $"reason-{index}");
        if (version is 2 or 3)
        {
            ignore.IdentityVersion = ArchitectureViolationIdentity.CurrentVersion;
            ignore.ContractFamily = "dependency";
            ignore.Kind = $"kind-{index % 2}";
            ignore.SourceAssembly = "Source.Assembly";
            ignore.SourceMember = $"SourceMember{index % 4}";
            ignore.TargetAssembly = "Target.Assembly";
            ignore.TargetType = $"TargetType{index % 3}";
            ignore.TargetMember = $"TargetMember{index % 2}";
            ignore.Occurrence = index % 2;
            ignore.Configuration = "release";
        }

        return ignore;
    }

    private static ArchitectureDependencyContract Contract(
        string id,
        IEnumerable<ArchitectureIgnoredViolation>? ignores = null) => new()
        {
            Id = id,
            Name = id,
            Source = "Synthetic.Source",
            Forbidden = new List<string>(),
            IgnoredViolations = ignores?.ToList() ?? new List<ArchitectureIgnoredViolation>(),
        };

    private static ArchitectureContractDocument Policy(params ArchitectureDependencyContract[] contracts) => new()
    {
        Version = 1,
        Name = "Generated baseline merge policy",
        Contracts = new ArchitectureContractGroups { Strict = contracts.ToList() },
    };

    private static ArchitectureBaselineDocument Baseline(
        int version,
        params ArchitectureBaselineContractEntry[] entries) => new()
        {
            Version = version,
            Baseline = new ArchitectureBaselineContractGroups { Strict = entries.ToList() },
        };

    private static ArchitectureBaselineContractEntry Entry(
        string id,
        params ArchitectureBaselineIgnoredViolation[] ignores) => new()
        {
            Id = id,
            IgnoredViolations = ignores.ToList(),
        };

    private static ArchitectureBaselineIgnoredViolation LegacyIgnore(string source, string forbidden, string reason) => new()
    {
        SourceType = source,
        ForbiddenReference = forbidden,
        Reason = reason,
    };

    private static ArchitectureBaselineIgnoredViolation StructuredBaselineIgnore(
        string sourceMember,
        string reason,
        string kind = "dependency",
        string forbiddenReference = "Forbidden.Type") => new()
        {
            SourceType = "Source.Type",
            ForbiddenReference = forbiddenReference,
            Reason = reason,
            IdentityVersion = ArchitectureViolationIdentity.CurrentVersion,
            ContractFamily = "dependency",
            Kind = kind,
            SourceAssembly = "Source.Assembly",
            SourceMember = sourceMember,
            TargetAssembly = "Target.Assembly",
            TargetType = "Target.Type",
            TargetMember = "Target.Member",
            Occurrence = 1,
            Configuration = "release",
        };

    private static ArchitectureIgnoredViolation StructuredPolicyIgnore(
        string sourceMember,
        string reason,
        string kind = "dependency",
        string forbiddenReference = "Forbidden.Type",
        int identityVersion = ArchitectureViolationIdentity.CurrentVersion)
    {
        ArchitectureBaselineIgnoredViolation baselineIgnore = StructuredBaselineIgnore(
            sourceMember, reason, kind, forbiddenReference);
        baselineIgnore.IdentityVersion = identityVersion;
        return ToPolicyIgnore(baselineIgnore);
    }

    private static ArchitectureIgnoredViolation ToPolicyIgnore(ArchitectureBaselineIgnoredViolation ignore) => new()
    {
        SourceType = ignore.SourceType,
        ForbiddenReference = ignore.ForbiddenReference,
        Reason = ignore.Reason,
        IdentityVersion = ignore.IdentityVersion,
        ContractFamily = ignore.ContractFamily,
        Kind = ignore.Kind,
        SourceAssembly = ignore.SourceAssembly,
        SourceMember = ignore.SourceMember,
        TargetAssembly = ignore.TargetAssembly,
        TargetType = ignore.TargetType,
        TargetMember = ignore.TargetMember,
        Occurrence = ignore.Occurrence,
        Configuration = ignore.Configuration,
    };

    private static string ContractId(int index) => $"Rule{index:D2}";
}
