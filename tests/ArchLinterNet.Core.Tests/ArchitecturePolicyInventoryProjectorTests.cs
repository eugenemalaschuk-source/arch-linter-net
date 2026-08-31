using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyInventoryProjectorTests
{
    [Test]
    public void Project_SourceSetAliasesCountOnce_AndSelectedScopeIsExact()
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Inventory",
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal =
                [
                    ExpandedExternal("boundary/one", "boundary", "one"),
                    ExpandedExternal("boundary/two", "boundary", "two"),
                ],
                Strict =
                [new ArchitectureDependencyContract { Id = "ordinary", Name = "ordinary" }],
                Audit =
                [new ArchitectureDependencyContract { Id = "audit-only", Name = "audit-only" }],
                StrictCoverage =
                [new ArchitectureCoverageContract { Id = "coverage", Name = "coverage", Scope = "namespace" }],
            },
        };

        ArchitecturePolicyInventory all = ArchitecturePolicyInventoryProjector.Project(
            document, "strict", Array.Empty<ArchitectureWaiverLifecycleRecord>());
        ArchitecturePolicyInventory selected = ArchitecturePolicyInventoryProjector.Project(
            document, "strict", Array.Empty<ArchitectureWaiverLifecycleRecord>(), ["boundary"]);

        Assert.Multiple(() =>
        {
            Assert.That(all.SchemaId, Is.EqualTo(ArchitecturePolicyInventory.CurrentSchemaId));
            Assert.That(all.EffectiveRuleCount, Is.EqualTo(4));
            Assert.That(all.Rules, Is.EqualTo(new ArchitecturePolicyInventoryRules(2, 1, 1)));
            Assert.That(selected.EffectiveRuleCount, Is.EqualTo(1));
            Assert.That(selected.Rules, Is.EqualTo(new ArchitecturePolicyInventoryRules(1, 0, 0)));
        });
    }

    [Test]
    public void Project_StrictAndAuditInvocations_ExposeTheSameRepositoryInventory()
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Inventory",
            Contracts = new ArchitectureContractGroups
            {
                Strict = [new ArchitectureDependencyContract { Id = "strict", Name = "strict" }],
                Audit = [new ArchitectureDependencyContract { Id = "audit", Name = "audit" }],
                StrictCoverage = [new ArchitectureCoverageContract { Id = "coverage", Name = "coverage", Scope = "namespace" }],
            },
        };

        ArchitecturePolicyInventory strict = ArchitecturePolicyInventoryProjector.Project(
            document, "strict", Array.Empty<ArchitectureWaiverLifecycleRecord>());
        ArchitecturePolicyInventory audit = ArchitecturePolicyInventoryProjector.Project(
            document, "audit", Array.Empty<ArchitectureWaiverLifecycleRecord>());

        Assert.Multiple(() =>
        {
            Assert.That(strict.EffectiveRuleCount, Is.EqualTo(3));
            Assert.That(strict.Rules, Is.EqualTo(new ArchitecturePolicyInventoryRules(1, 1, 1)));
            Assert.That(audit, Is.EqualTo(strict));
        });
    }

    [Test]
    public void Project_ExcludesDisabledCoverage_AndConsumesLifecycleStatesOnce()
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Inventory",
            Contracts = new ArchitectureContractGroups
            {
                Strict = [new ArchitectureDependencyContract { Id = "ordinary", Name = "ordinary" }],
                StrictCoverage =
                [new ArchitectureCoverageContract { Id = "coverage", Name = "coverage", Scope = "namespace" }],
            },
        };

        ArchitectureWaiverLifecycleRecord[] waivers =
        [
            Lifecycle("z-expired", "expired"),
            Lifecycle("a-active", "active"),
            Lifecycle("m-metadata", "metadata_incomplete"),
            Lifecycle("s-stale", "stale"),
            Lifecycle("i-invalid", "invalid"),
        ];

        ArchitecturePolicyInventory inventory = ArchitecturePolicyInventoryProjector.Project(
            document,
            "strict",
            waivers,
            includeCoverageContracts: false);

        Assert.Multiple(() =>
        {
            Assert.That(inventory.EffectiveRuleCount, Is.EqualTo(1));
            Assert.That(inventory.Rules, Is.EqualTo(new ArchitecturePolicyInventoryRules(1, 0, 0)));
            Assert.That(inventory.IgnoreDebt, Is.EqualTo(
                new ArchitecturePolicyInventoryIgnoreDebt(5, 1, 1, 1, 1, 1)));
            Assert.That(inventory.Waivers.Select(record => record.Id),
                Is.EqualTo(new[] { "a-active", "i-invalid", "m-metadata", "s-stale", "z-expired" }));
        });
    }

    [Test]
    public void Project_UnknownLifecycleStateFailsClosed()
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Inventory",
            Contracts = new ArchitectureContractGroups(),
        };

        Assert.That(
            () => ArchitecturePolicyInventoryProjector.Project(
                document,
                "strict",
                [Lifecycle("future", "future")]),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void CacheMapping_PreservesInventory_AndMissingInventoryRemainsAbsent()
    {
        ArchitecturePolicyInventory inventory = new(
            ArchitecturePolicyInventory.CurrentSchemaId,
            2,
            new ArchitecturePolicyInventoryRules(1, 0, 1),
            new ArchitecturePolicyInventoryIgnoreDebt(1, 1, 0, 0, 0, 0),
            [Lifecycle("active", "active")]);
        ValidationOutcome original = new(
            true,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            "off",
            Array.Empty<PolicyConsistencyDiagnostic>(),
            "off",
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            PolicyInventory = inventory,
        };

        AnalysisCacheOutcomeV1 cached = AnalysisCacheOutcomeMapper.ToCacheOutcome(original);
        ValidationOutcome reconstructed = AnalysisCacheOutcomeMapper.FromCacheOutcome(
            cached,
            "/repo",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            ArchitectureSourceExpansionInventory.Empty);
        AnalysisCacheOutcomeV1 oldCached = new(
            true,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            "off",
            Array.Empty<PolicyConsistencyDiagnostic>(),
            "off",
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>());
        ValidationOutcome oldReconstructed = AnalysisCacheOutcomeMapper.FromCacheOutcome(
            oldCached,
            "/repo",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            ArchitectureSourceExpansionInventory.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(reconstructed.PolicyInventory, Is.EqualTo(inventory));
            Assert.That(oldReconstructed.PolicyInventory, Is.Null);
        });
    }

    private static ArchitectureExternalDependencyContract ExpandedExternal(
        string id,
        string authoredId,
        string source) => new()
        {
            Id = id,
            Name = authoredId,
            Source = source,
            ExpansionOrigin = new ArchitectureSourceExpansionOrigin(
                authoredId, authoredId, source, "set", source),
        };

    private static ArchitectureWaiverLifecycleRecord Lifecycle(string id, string state) => new(
        id,
        state,
        "ordinary",
        "ordinary",
        "strict",
        "source",
        "forbidden",
        null,
        "reason",
        null,
        null,
        null,
        null,
        new DateOnly(2026, 8, 31),
        true);
}
