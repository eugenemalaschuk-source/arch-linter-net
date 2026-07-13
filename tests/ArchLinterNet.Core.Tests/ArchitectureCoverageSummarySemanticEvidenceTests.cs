using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureCoverageSummaryTests
{
    [Test]
    public void BuildCoverageSummary_SemanticRoleScope_ExplainsGovernanceAndExclusionFacts()
    {
        ArchitectureContractDocument document = CreateDomainClassificationDocument();
        document.Layers["sales"] = new ArchitectureLayer
        {
            Selector = new ArchitectureLayerSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
            }
        };
        ArchitectureCoverageContract contract = new()
        {
            Name = "semantic-role-coverage",
            Id = "semantic-role-coverage",
            Scope = "semantic_role",
            Roots =
            {
                new ArchitectureCoverageRoot { Namespace = "SemanticCoverageSampleFixtures" }
            }
        };
        contract.Exclude.Add(new ArchitectureCoverageExclusion
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object>
            {
                ["domain"] = "Inventory"
            },
            Reason = "Inventory is exempted."
        });

        ArchitectureCoverageSummary summary = RequireSummary(new ArchitectureContractRunner(
            CreateContext(typeof(ArchitectureCoverageSummaryTests)), document).BuildCoverageSummary(contract));

        Assert.That(summary.CoveredItems, Has.Some.Matches<ArchitectureCoverageSummaryEvidenceItem>(item =>
            item.Evidence.Contains("governed by layer", StringComparison.Ordinal)));
        Assert.That(summary.ExcludedItems, Has.Some.Matches<ArchitectureCoverageSummaryExcludedItem>(item =>
            item.Evidence.Contains("role=DomainLayer", StringComparison.Ordinal)
            && item.Evidence.Contains("domain=Inventory", StringComparison.Ordinal)));
    }
}
