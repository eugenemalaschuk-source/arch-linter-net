using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineComparerTests
{
    [Test]
    public void Compare_ContractIdCaseDiffers_TreatsEntryAsFrozenNotResolvedOrNew()
    {
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "Core-Rule", Name = "core-rule", Source = "core" },
                },
            },
        };

        ArchitectureBaselineDocument baseline = new()
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "core-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "Src.Type", ForbiddenReference = "Ref.Type", Reason = "legacy" },
                        },
                    },
                },
            },
        };

        IReadOnlyList<ArchitectureBaselineCandidate> candidates =
        [
            new("strict", "Core-Rule", "Src.Type", "Ref.Type"),
        ];

        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            policy, baseline, candidates, mode: "all");

        Assert.That(result.Frozen, Has.Count.EqualTo(1));
        Assert.That(result.Frozen[0].ContractId, Is.EqualTo("core-rule"));
        Assert.That(result.Resolved, Is.Empty);
        Assert.That(result.New, Is.Empty);
        Assert.That(result.ConfigurationErrors, Is.Empty);
    }
}
