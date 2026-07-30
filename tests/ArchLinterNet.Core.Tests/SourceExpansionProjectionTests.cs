using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Covers the machine-readable projections of the resolved source-set expansion, so a JSON or SARIF
// consumer can prove which sources an authored contract expanded to without parsing display text.
[TestFixture]
public sealed class SourceExpansionProjectionTests
{
    private static readonly ArchitectureSarifFormatter _sarifFormatter = new();

    private static ArchitecturePolicySourceLocation FragmentLocation()
    {
        ArchitecturePolicySourceDescriptor fragment = new(
            "architecture/parts/modules.yml",
            "architecture/parts/modules.yml",
            ArchitecturePolicyDocumentRole.Fragment,
            1,
            null,
            null,
            ["architecture/root.yml", "architecture/parts/modules.yml"]);

        return new ArchitecturePolicySourceLocation(
            fragment, "contracts.strict_package_dependency[0]", 3, 5, null, null);
    }

    private static ArchitectureSourceExpansionInventory Inventory(bool withLocation = true)
    {
        ArchitecturePolicySourceLocation? location = withLocation ? FragmentLocation() : null;

        return new ArchitectureSourceExpansionInventory(
            [
                new ArchitectureSourceSetResolution(
                    "modules",
                    ArchitectureSourceSetKind.Assembly,
                    ["Acme.Modules.Billing", "Acme.Modules.Orders"],
                    false,
                    string.Empty)
                {
                    PolicyLocation = location
                },
                new ArchitectureSourceSetResolution(
                    "future_modules",
                    ArchitectureSourceSetKind.Layer,
                    [],
                    true,
                    "The module slice is planned but not extracted yet.")
            ],
            [
                new ArchitectureContractExpansion(
                    "strict_package_dependency",
                    "modules-no-infrastructure",
                    "modules avoid infrastructure",
                    ["modules"],
                    [
                        new ArchitectureExpandedContractInstance(
                            "modules-no-infrastructure/acme-modules-billing",
                            "Acme.Modules.Billing",
                            "modules",
                            "Acme.Modules.*")
                    ])
                {
                    PolicyLocation = location,
                    Exclusions =
                    [
                        new ArchitectureExpandedContractExclusion(
                            "Acme.Modules.Legacy",
                            null,
                            "Acme.Modules.Legacy",
                            false)
                        {
                            PolicyLocation = location
                        },
                        new ArchitectureExpandedContractExclusion(
                            "Acme.Modules.Billing",
                            "modules",
                            "Acme.Modules.*",
                            true)
                        {
                            PolicyLocation = location
                        }
                    ]
                },
                new ArchitectureContractExpansion(
                    "strict_external",
                    "inner-no-vendor",
                    "inner layers avoid vendor",
                    ["future_modules"],
                    [])
                {
                    OptionalEmpty = true,
                    OptionalReason = "The module slice is planned but not extracted yet."
                }
            ]);
    }

    [Test]
    public void Sarif_CarriesResolvedSetsAndInstances()
    {
        string json = _sarifFormatter.FormatResultAsSarif(
            "strict",
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            Array.Empty<ArchitectureCoverageSummary>(),
            Inventory(),
            "1.2.3");

        JsonElement expansion = JsonDocument.Parse(json).RootElement
            .GetProperty("runs")[0].GetProperty("properties").GetProperty("source_set_expansion");
        JsonElement set = expansion.GetProperty("sets")[0];
        JsonElement contract = expansion.GetProperty("contracts")[0];
        JsonElement exclusion = contract.GetProperty("exclusions")[1];
        JsonElement instance = contract.GetProperty("instances")[0];

        Assert.Multiple(() =>
        {
            Assert.That(set.GetProperty("name").GetString(), Is.EqualTo("modules"));
            Assert.That(set.GetProperty("kind").GetString(), Is.EqualTo("assembly"));
            Assert.That(set.GetProperty("resolved_sources").EnumerateArray().Select(v => v.GetString()),
                Is.EqualTo(new[] { "Acme.Modules.Billing", "Acme.Modules.Orders" }));
            Assert.That(set.GetProperty("policy_location").GetProperty("source_path").GetString(),
                Is.EqualTo("architecture/parts/modules.yml"));
            Assert.That(contract.GetProperty("group").GetString(), Is.EqualTo("strict_package_dependency"));
            Assert.That(contract.GetProperty("authored_contract_id").GetString(), Is.EqualTo("modules-no-infrastructure"));
            Assert.That(contract.GetProperty("authored_contract_name").GetString(), Is.EqualTo("modules avoid infrastructure"));
            Assert.That(contract.GetProperty("source_sets").EnumerateArray().Select(v => v.GetString()),
                Is.EqualTo(new[] { "modules" }));
            Assert.That(exclusion.GetProperty("source_set").GetString(), Is.EqualTo("modules"));
            Assert.That(exclusion.GetProperty("matched").GetBoolean(), Is.True);
            Assert.That(instance.GetProperty("contract_id").GetString(),
                Is.EqualTo("modules-no-infrastructure/acme-modules-billing"));
            Assert.That(instance.GetProperty("source").GetString(), Is.EqualTo("Acme.Modules.Billing"));
            Assert.That(instance.GetProperty("source_set").GetString(), Is.EqualTo("modules"));
            Assert.That(instance.GetProperty("selector").GetString(), Is.EqualTo("Acme.Modules.*"));
        });
    }

    [Test]
    public void Sarif_CarriesOptionalEmptyStateAndNullLocation()
    {
        string json = _sarifFormatter.FormatResultAsSarif(
            "audit",
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            Array.Empty<ArchitectureCoverageSummary>(),
            Inventory(withLocation: false),
            "1.2.3");

        JsonElement expansion = JsonDocument.Parse(json).RootElement
            .GetProperty("runs")[0].GetProperty("properties").GetProperty("source_set_expansion");
        JsonElement optionalSet = expansion.GetProperty("sets")[1];
        JsonElement optionalContract = expansion.GetProperty("contracts")[1];

        Assert.Multiple(() =>
        {
            Assert.That(optionalSet.GetProperty("kind").GetString(), Is.EqualTo("layer"));
            Assert.That(optionalSet.GetProperty("optional").GetBoolean(), Is.True);
            Assert.That(optionalSet.GetProperty("reason").GetString(), Does.Contain("planned"));
            Assert.That(optionalSet.GetProperty("resolved_sources").GetArrayLength(), Is.Zero);
            Assert.That(optionalContract.GetProperty("optional_empty").GetBoolean(), Is.True);
            Assert.That(optionalContract.GetProperty("optional_reason").GetString(), Does.Contain("planned"));
            Assert.That(optionalContract.GetProperty("instances").GetArrayLength(), Is.Zero);
            Assert.That(expansion.GetProperty("sets")[0].GetProperty("policy_location").ValueKind,
                Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public void Sarif_CycleFindingOverload_CarriesExpansion()
    {
        string json = ArchitectureSarifFormatter.FormatResultAsSarif(
            "strict",
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureCycleFinding>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            Array.Empty<ArchitectureCoverageSummary>(),
            Inventory(),
            "1.2.3");

        JsonElement expansion = JsonDocument.Parse(json).RootElement
            .GetProperty("runs")[0].GetProperty("properties").GetProperty("source_set_expansion");

        Assert.That(expansion.GetProperty("contracts")[0].GetProperty("authored_contract_id").GetString(),
            Is.EqualTo("modules-no-infrastructure"));
    }

    [Test]
    public void CiArtifactsJson_CarriesResolvedExpansion()
    {
        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            "strict",
            true,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureCycleFinding>(),
            Array.Empty<ArchitectureClassificationRoleFact>(),
            null,
            Array.Empty<BuildStatePreflightDiagnostic>(),
            Inventory());

        JsonElement expansion = JsonDocument.Parse(json).RootElement.GetProperty("source_set_expansion");

        Assert.Multiple(() =>
        {
            Assert.That(expansion.GetProperty("sets")[0].GetProperty("name").GetString(), Is.EqualTo("modules"));
            Assert.That(expansion.GetProperty("contracts")[0].GetProperty("instances")[0].GetProperty("source").GetString(),
                Is.EqualTo("Acme.Modules.Billing"));
            Assert.That(expansion.GetProperty("contracts")[0].GetProperty("exclusions")[0].GetProperty("matched").GetBoolean(),
                Is.False);
        });
    }

    [Test]
    public void Sarif_CarriesInlineUnionSelectorFieldAndValueProvenance()
    {
        ArchitectureSourceExpansionInventory inventory = new(
            [],
            [
                new ArchitectureContractExpansion(
                    "strict_project_metadata", "modules-packable", "modules are packable", ["module_projects"],
                    [new ArchitectureExpandedContractInstance(
                        "modules-packable", "src/Acme.Modules/Acme.Modules.csproj", "module_projects",
                        "src/Acme.Modules/Acme.Modules.csproj")])
                {
                    Kind = ArchitectureContractExpansionKind.InlineUnion,
                    SelectorField = "project_sets"
                },
                new ArchitectureContractExpansion(
                    "audit_composition", "host-composition", "host composition", ["hosts"],
                    [new ArchitectureExpandedContractInstance(
                        "host-composition", "Acme.Host.Api", "hosts", "Acme.Host.*")])
                {
                    Kind = ArchitectureContractExpansionKind.InlineUnion,
                    SelectorField = "allowed_only_in_assembly_sets"
                }
            ]);

        string json = _sarifFormatter.FormatResultAsSarif(
            "all", Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<BuildStatePreflightDiagnostic>(), Array.Empty<ArchitectureCoverageSummary>(), inventory, "1.2.3");
        JsonElement contracts = JsonDocument.Parse(json).RootElement
            .GetProperty("runs")[0].GetProperty("properties").GetProperty("source_set_expansion")
            .GetProperty("contracts");

        Assert.Multiple(() =>
        {
            Assert.That(contracts[0].GetProperty("kind").GetString(), Is.EqualTo("inline_union"));
            Assert.That(contracts[0].GetProperty("selector_field").GetString(), Is.EqualTo("project_sets"));
            Assert.That(contracts[0].GetProperty("instances")[0].GetProperty("selector").GetString(),
                Is.EqualTo("src/Acme.Modules/Acme.Modules.csproj"));
            Assert.That(contracts[1].GetProperty("selector_field").GetString(),
                Is.EqualTo("allowed_only_in_assembly_sets"));
            Assert.That(contracts[1].GetProperty("instances")[0].GetProperty("selector").GetString(),
                Is.EqualTo("Acme.Host.*"));
        });
    }

    [Test]
    public void CiArtifactsJson_WithoutExpansion_EmitsEmptySections()
    {
        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            "strict",
            true,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureCycleFinding>());

        JsonElement expansion = JsonDocument.Parse(json).RootElement.GetProperty("source_set_expansion");

        Assert.Multiple(() =>
        {
            Assert.That(expansion.GetProperty("sets").GetArrayLength(), Is.Zero);
            Assert.That(expansion.GetProperty("contracts").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public void InstanceIdsFor_UnknownAuthoredId_ReturnsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Inventory().InstanceIdsFor("not-an-authored-id"), Is.Empty);
            Assert.That(Inventory().InstanceIdsFor("MODULES-NO-INFRASTRUCTURE"),
                Is.EqualTo(new[] { "modules-no-infrastructure/acme-modules-billing" }));
            Assert.That(ArchitectureSourceExpansionInventory.Empty.IsEmpty, Is.True);
            Assert.That(Inventory().IsEmpty, Is.False);
        });
    }
}
