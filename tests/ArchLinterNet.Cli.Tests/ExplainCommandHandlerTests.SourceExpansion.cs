using System.Text.Json;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Source-set expansion projections for `explain`, split from ExplainCommandHandlerTests to keep
// each file under the repository file-size threshold.
public sealed partial class ExplainCommandHandlerTests
{
    [Test]
    public void Json_SourceSetExpansion_EmitsAuthoredSetAndResolvedSource()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
            {
                SourceExpansion = Expansion()
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        JsonElement expansion = document.RootElement.GetProperty("sourceSetExpansion");
        JsonElement contract = expansion.GetProperty("contracts")[0];
        JsonElement instance = contract.GetProperty("instances")[0];
        Assert.Multiple(() =>
        {
            Assert.That(expansion.GetProperty("sets")[0].GetProperty("name").GetString(), Is.EqualTo("modules"));
            Assert.That(contract.GetProperty("authoredContractId").GetString(), Is.EqualTo("modules-no-infrastructure"));
            Assert.That(contract.GetProperty("exclusions")[0].GetProperty("matched").GetBoolean(), Is.False);
            Assert.That(contract.GetProperty("inclusions")[0].GetProperty("source").GetString(),
                Is.EqualTo("Acme.Modules.Orders"));
            Assert.That(instance.GetProperty("source").GetString(), Is.EqualTo("Acme.Modules.Orders"));
            Assert.That(instance.GetProperty("sourceSet").GetString(), Is.EqualTo("modules"));
            Assert.That(instance.GetProperty("selector").GetString(), Is.EqualTo("Acme.Modules.*"));
            Assert.That(contract.GetProperty("policyLocation").GetProperty("sourcePath").GetString(),
                Is.EqualTo("architecture/parts/modules.yml"));
            Assert.That(instance.GetProperty("authoredContractPolicyLocation").GetProperty("sourcePath").GetString(),
                Is.EqualTo("architecture/parts/modules.yml"));
            Assert.That(instance.GetProperty("sourceSetReferencePolicyLocation").GetProperty("sourcePath").GetString(),
                Is.EqualTo("architecture/parts/modules.yml"));
        });
    }

    [Test]
    public void Human_SourceSetExpansion_NamesSetSourceAndFragment()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
            {
                SourceExpansion = Expansion()
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(console.OutputText, Does.Contain("[modules-no-infrastructure] set 'modules' -> Acme.Modules.Orders"));
            Assert.That(console.OutputText,
                Does.Contain("Source expansion exclusion: [modules-no-infrastructure] stale sources -> Acme.Modules.Legacy"));
            Assert.That(console.OutputText, Does.Contain("architecture/parts/modules.yml"));
        });
    }

    [Test]
    public void Explain_SelectorParticipation_ReportsEffectiveInclusionAndStaleExclusion()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
            {
                SelectorParticipation =
                [
                    new ArchitectureSubtractiveMatcherParticipation(
                        "controllers", "controllers", "types_matching", null, true)
                    {
                        Kind = ArchitectureSelectorParticipationKind.Inclusion,
                        Mode = ArchitectureSelectorParticipationMode.Strict
                    },
                    new ArchitectureSubtractiveMatcherParticipation(
                        "controllers", "controllers", "exclude_types_matching", 0, true)
                    {
                        Mode = ArchitectureSelectorParticipationMode.Audit,
                        EvaluationFailed = true
                    }
                ]
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        JsonElement participation = document.RootElement.GetProperty("selectorParticipation");
        Assert.Multiple(() =>
        {
            Assert.That(participation[0].GetProperty("kind").GetString(), Is.EqualTo("inclusion"));
            Assert.That(participation[0].GetProperty("index").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(participation[0].GetProperty("mode").GetString(), Is.EqualTo("strict"));
            Assert.That(participation[0].GetProperty("matched").GetBoolean(), Is.True);
            Assert.That(participation[1].GetProperty("kind").GetString(), Is.EqualTo("exclusion"));
            Assert.That(participation[1].GetProperty("mode").GetString(), Is.EqualTo("audit"));
            Assert.That(participation[1].GetProperty("matched").GetBoolean(), Is.True);
            Assert.That(participation[1].GetProperty("evaluationFailed").GetBoolean(), Is.True);
        });

        Handler(runtime, console).Execute(Options(format: "human"));
        Assert.That(console.OutputText,
            Does.Contain("Selector participation: [controllers] audit exclusion exclude_types_matching[0] matched; evaluation failed"));
    }

    [Test]
    public void Human_OptionalEmptyExpansion_NamesReasonWithoutInstances()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
            {
                SourceExpansion = new ArchitectureSourceExpansionInventory(
                    [],
                    [
                        new ArchitectureContractExpansion(
                            "strict_package_dependency",
                            "modules-no-infrastructure",
                            "modules avoid infrastructure",
                            ["future_modules"],
                            [])
                        {
                            OptionalEmpty = true,
                            OptionalReason = "The module slice is planned but not extracted yet."
                        }
                    ])
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(console.OutputText,
                Does.Contain("[modules-no-infrastructure] optional-empty (The module slice is planned but not extracted yet.)"));
            Assert.That(console.OutputText, Does.Not.Contain("selector:"));
        });
    }

    [Test]
    public void Human_ExplicitSourcesExpansion_WithoutSetOrLocation_NamesSourcesSelector()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
            {
                SourceExpansion = new ArchitectureSourceExpansionInventory(
                    [],
                    [
                        new ArchitectureContractExpansion(
                            "strict_package_dependency",
                            "modules-no-infrastructure",
                            "modules avoid infrastructure",
                            [],
                            [
                                new ArchitectureExpandedContractInstance(
                                    "modules-no-infrastructure/acme-modules-orders",
                                    "Acme.Modules.Orders",
                                    null,
                                    "Acme.Modules.Orders")
                            ])
                    ])
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(console.OutputText,
                Does.Contain("[modules-no-infrastructure] sources -> Acme.Modules.Orders"));
            Assert.That(console.OutputText, Does.Not.Contain("(policy:"));
        });
    }

    [Test]
    public void Json_InlineUnionExpansion_NamesSelectorFieldAndSetValue()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
            {
                SourceExpansion = new ArchitectureSourceExpansionInventory(
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
                        }
                    ])
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        JsonElement contract = document.RootElement.GetProperty("sourceSetExpansion").GetProperty("contracts")[0];
        Assert.Multiple(() =>
        {
            Assert.That(contract.GetProperty("kind").GetString(), Is.EqualTo("inline_union"));
            Assert.That(contract.GetProperty("selectorField").GetString(), Is.EqualTo("project_sets"));
            Assert.That(contract.GetProperty("instances")[0].GetProperty("selector").GetString(),
                Is.EqualTo("src/Acme.Modules/Acme.Modules.csproj"));
        });
    }

    [Test]
    public void Json_NoExpansion_EmitsEmptyExpansionSections()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        JsonElement expansion = document.RootElement.GetProperty("sourceSetExpansion");
        Assert.Multiple(() =>
        {
            Assert.That(expansion.GetProperty("sets").GetArrayLength(), Is.Zero);
            Assert.That(expansion.GetProperty("contracts").GetArrayLength(), Is.Zero);
        });
    }

    private static ArchitectureSourceExpansionInventory Expansion()
    {
        ArchitecturePolicySourceDescriptor fragment = new(
            "architecture/parts/modules.yml", "architecture/parts/modules.yml",
            ArchitecturePolicyDocumentRole.Fragment, 1, null, null,
            ["architecture/root.yml", "architecture/parts/modules.yml"]);
        ArchitecturePolicySourceLocation location = new(
            fragment, "contracts.strict_package_dependency[0]", 3, 5, null, null);

        return new ArchitectureSourceExpansionInventory(
            [
                new ArchitectureSourceSetResolution(
                    "modules", ArchitectureSourceSetKind.Assembly, ["Acme.Modules.Orders"], false, string.Empty)
                {
                    PolicyLocation = location
                }
            ],
            [
                new ArchitectureContractExpansion(
                    "strict_package_dependency",
                    "modules-no-infrastructure",
                    "modules avoid infrastructure",
                    ["modules"],
                    [
                        new ArchitectureExpandedContractInstance(
                            "modules-no-infrastructure/acme-modules-orders",
                            "Acme.Modules.Orders",
                            "modules",
                            "Acme.Modules.*")
                        {
                            PolicyLocation = location,
                            AuthoredContractPolicyLocation = location,
                            SourceSetReferencePolicyLocation = location
                        }
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
                        }
                    ]
                }
            ]);
    }
}
