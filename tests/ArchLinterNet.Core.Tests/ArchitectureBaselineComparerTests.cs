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
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
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

    [Test]
    public void Compare_Version2Baseline_SameNamedTypeInDifferentAssembly_IsReportedAsNewNotFrozen()
    {
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "no-multi-program", Name = "no-multi-program", Source = "app" },
                },
            },
        };

        // Baseline only ever recorded the Program-in-Host.A occurrence.
        ArchitectureBaselineDocument baseline = new()
        {
            Version = 2,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "no-multi-program",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Host.A.Program",
                                ForbiddenReference = "System.Object",
                                Reason = "known debt",
                                IdentityVersion = 2,
                                ContractFamily = "strict",
                                Kind = "dependency",
                                SourceAssembly = "Host.A",
                                TargetAssembly = "mscorlib",
                                TargetMember = "System.Object",
                                Occurrence = 0,
                            },
                        },
                    },
                },
            },
        };

        // Current candidates: the SAME (source_type, forbidden_reference) pair exists in TWO
        // different assemblies — Host.A (already baselined) and Host.B (must remain reported).
        IReadOnlyList<ArchitectureBaselineCandidate> candidates =
        [
            new("strict", "no-multi-program", "Host.A.Program", "System.Object",
                new ArchitectureViolationIdentity(2, "strict", "dependency", "no-multi-program",
                    "Host.A", "Host.A.Program", null, "mscorlib", null, "System.Object", 0)),
            new("strict", "no-multi-program", "Host.B.Program", "System.Object",
                new ArchitectureViolationIdentity(2, "strict", "dependency", "no-multi-program",
                    "Host.B", "Host.B.Program", null, "mscorlib", null, "System.Object", 0)),
        ];

        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            policy, baseline, candidates, mode: "all");

        Assert.Multiple(() =>
        {
            Assert.That(result.Frozen, Has.Count.EqualTo(1));
            Assert.That(result.Frozen[0].SourceType, Is.EqualTo("Host.A.Program"));
            Assert.That(result.New, Has.Count.EqualTo(1));
            Assert.That(result.New[0].SourceType, Is.EqualTo("Host.B.Program"));
            Assert.That(result.Resolved, Is.Empty);
        });
    }

    [Test]
    public void Compare_Version1Baseline_UnqualifiedIdentityStillMatchesByLegacyPair()
    {
        // Version 1 baselines must keep matching exactly as before — no structured-identity
        // reinterpretation of legacy entries.
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "core-rule", Name = "core-rule", Source = "core" },
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
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new() { SourceType = "Src.Type", ForbiddenReference = "Ref.Type", Reason = "legacy" },
                        },
                    },
                },
            },
        };

        // Candidate now carries a fully qualified v2 identity (assembly, member), but since the
        // baseline document is still version 1, matching must fall back to the legacy pair.
        IReadOnlyList<ArchitectureBaselineCandidate> candidates =
        [
            new("strict", "core-rule", "Src.Type", "Ref.Type",
                new ArchitectureViolationIdentity(2, "strict", "dependency", "core-rule",
                    "Some.Assembly", "Src.Type", null, "Other.Assembly", null, "Ref.Type", 0)),
        ];

        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            policy, baseline, candidates, mode: "all");

        Assert.That(result.Frozen, Has.Count.EqualTo(1));
        Assert.That(result.New, Is.Empty);
    }

    [Test]
    public void Compare_Version2Baseline_ReportsConfigurationErrorForUnknownContractId()
    {
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "core-rule", Name = "core-rule", Source = "core" },
                },
            },
        };

        ArchitectureBaselineDocument baseline = new()
        {
            Version = 2,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "no-such-contract",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Src.Type", ForbiddenReference = "Ref.Type", Reason = "orphaned",
                                IdentityVersion = 2, ContractFamily = "strict", Kind = "dependency", Occurrence = 0,
                            },
                        },
                    },
                },
            },
        };

        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            policy, baseline, Array.Empty<ArchitectureBaselineCandidate>(), mode: "all");

        Assert.That(result.ConfigurationErrors, Has.Count.EqualTo(1));
        Assert.That(result.ConfigurationErrors[0].ContractId, Is.EqualTo("no-such-contract"));
    }

    [Test]
    public void Compare_Version2Baseline_OutOfScopeEntryIsCarriedThroughWithIdentity()
    {
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Audit = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "audit-rule", Name = "audit-rule", Source = "core" },
                },
            },
        };

        ArchitectureBaselineDocument baseline = new()
        {
            Version = 2,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Audit = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "audit-rule",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Src.Type", ForbiddenReference = "Ref.Type", Reason = "audit debt",
                                IdentityVersion = 2, ContractFamily = "audit", Kind = "dependency", Occurrence = 0,
                            },
                        },
                    },
                },
            },
        };

        // mode "strict" puts every "audit"-group entry out of scope.
        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            policy, baseline, Array.Empty<ArchitectureBaselineCandidate>(), mode: "strict");

        Assert.That(result.OutOfScope, Has.Count.EqualTo(1));
        Assert.That(result.OutOfScope[0].SourceType, Is.EqualTo("Src.Type"));
    }

    [Test]
    public void Compare_Version2Baseline_CandidateWithoutIdentity_UsesFallbackAndDedupsDuplicates()
    {
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "core-rule", Name = "core-rule", Source = "core" },
                },
            },
        };

        ArchitectureBaselineDocument baseline = new()
        {
            Version = 2,
            Baseline = new ArchitectureBaselineContractGroups(),
        };

        // Candidates constructed without an explicit Identity (as some call sites still do) must
        // fall back to a derived identity rather than crashing, and duplicate candidates for the
        // exact same fallback identity must still be deduplicated into one "new" entry.
        IReadOnlyList<ArchitectureBaselineCandidate> candidates =
        [
            new("strict", "core-rule", "Src.Type", "Ref.Type"),
            new("strict", "core-rule", "Src.Type", "Ref.Type"),
        ];

        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            policy, baseline, candidates, mode: "all");

        Assert.That(result.New, Has.Count.EqualTo(1));
        Assert.That(result.New[0].SourceType, Is.EqualTo("Src.Type"));
    }
}
