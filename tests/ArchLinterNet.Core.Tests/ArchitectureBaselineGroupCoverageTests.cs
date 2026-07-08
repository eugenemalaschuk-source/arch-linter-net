using System.Collections;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Guards the single source of truth for baseline-capable groups. Baseline candidates are produced for
// every executable contract family except asmdef and layer_templates, so the baseline model
// (ArchitectureBaselineContractGroups) must be able to represent exactly those groups. If a new
// contract family is added to ArchitectureContractCatalog without a matching baseline group, or a
// baseline group is declared that no family can produce, these tests fail — preventing the silent
// divergence where diff/update/prune/verify would ignore real debt for a family.
[TestFixture]
public sealed class ArchitectureBaselineGroupCoverageTests
{
    // Reflectively populate one contract in every List<IArchitectureContract> group on the policy
    // model so the catalog materializes a descriptor for each family. Layer templates are excluded:
    // their family is not baseline-resolvable and LayerTemplateExpander requires real container data.
    private static ArchitectureContractDocument BuildDocumentWithEveryContractGroup()
    {
        var contracts = new ArchitectureContractGroups();

        foreach (var property in typeof(ArchitectureContractGroups).GetProperties())
        {
            if (!property.PropertyType.IsGenericType
                || property.PropertyType.GetGenericTypeDefinition() != typeof(List<>))
            {
                continue;
            }

            Type itemType = property.PropertyType.GetGenericArguments()[0];
            if (!typeof(IArchitectureContract).IsAssignableFrom(itemType))
            {
                continue;
            }

            if (itemType == typeof(ArchitectureLayerTemplateContract))
            {
                continue;
            }

            var list = (IList)Activator.CreateInstance(property.PropertyType)!;
            list.Add(Activator.CreateInstance(itemType)!);
            property.SetValue(contracts, list);
        }

        return new ArchitectureContractDocument { Version = 1, Name = "Probe", Contracts = contracts };
    }

    [Test]
    public void BaselineModelGroups_MatchEveryBaselineCapableCatalogGroup()
    {
        ArchitectureContractCatalog catalog =
            ArchitectureContractCatalog.Build(BuildDocumentWithEveryContractGroup());

        IReadOnlyCollection<string> catalogGroups = catalog.BaselineCapableGroups();

        Assert.That(
            ArchitectureBaselineContractGroups.GroupNames,
            Is.EquivalentTo(catalogGroups),
            "ArchitectureBaselineContractGroups.GroupNames must cover exactly the catalog's "
            + "baseline-capable groups. Add the missing group's property to the baseline model "
            + "(and schema), or remove the extra group name.");
    }

    [Test]
    public void GroupNames_AreRoundTrippableThroughGetGroupAndSetGroup()
    {
        var groups = new ArchitectureBaselineContractGroups();

        Assert.That(ArchitectureBaselineContractGroups.GroupNames, Is.Unique);

        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            var entries = new List<ArchitectureBaselineContractEntry>
            {
                new() { Id = $"probe-{groupName}" },
            };

            groups.SetGroup(groupName, entries);
            Assert.That(groups.GetGroup(groupName), Is.SameAs(entries), $"GetGroup/SetGroup mismatch for '{groupName}'.");
        }
    }

    [Test]
    public void GetGroup_UnknownGroup_Throws()
    {
        var groups = new ArchitectureBaselineContractGroups();

        Assert.That(() => groups.GetGroup("strict_asmdef"), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
