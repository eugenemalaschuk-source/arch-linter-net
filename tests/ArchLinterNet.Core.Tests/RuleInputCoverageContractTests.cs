using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class RuleInputCoverageContractTests
{
    private static readonly string[] _value = { "audio-rule" };
    private static readonly string[] _value1 = { "video-to-ghost-rule" };
    private static readonly string[] _value2 = { "ghost" };
    private static readonly string[] _value3 = { "typo-rule" };
    private static readonly string[] _value4 = { "does_not_exist_layer" };
    private static readonly string[] _value5 = { "video-to-ghost-rule", "typo-rule" };
    private static readonly string[] _value6 = { "video-to-ghost-rule", "typo-rule" };
    private static readonly string[] _value7 = { "audio-rule", "video-to-ghost-rule", "typo-rule" };
    private static readonly string[] _value8 = { "module-container-rule" };
    private const string FixtureRoot = "ArchLinterNet.Core.Tests.RuleInputCoverageFixtures";

    private static readonly Assembly[] _targetAssemblies = { typeof(RuleInputCoverageContractTests).Assembly };

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: _targetAssemblies,
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        ArchitectureContractDocument document = new();

        document.Layers["audio"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Audio" };
        document.Layers["video"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Video" };
        document.Layers["ghost"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Ghost" };

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "audio-rule",
            Id = "audio-rule",
            Source = "audio",
            Forbidden = { "video" },
            Reason = "Audio must not depend on video."
        });

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "video-to-ghost-rule",
            Id = "video-to-ghost-rule",
            Source = "video",
            Forbidden = { "ghost" },
            Reason = "Video must not depend on ghost."
        });

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "typo-rule",
            Id = "typo-rule",
            Source = "does_not_exist_layer",
            Forbidden = { "audio" },
            Reason = "Placeholder rule with a dangling source layer."
        });

        document.Contracts.StrictModuleContainers.Add(new ArchitectureModuleContainerContract
        {
            Name = "module-container-rule",
            Id = "module-container-rule",
            Container = FixtureRoot,
            Profile = "cli_command",
            Reason = "Fixture modules must remain independently owned."
        });

        return document;
    }

    private static ArchitectureCoverageContract CreateRuleInputContract(
        IEnumerable<string> contractIds, IEnumerable<ArchitectureCoverageExclusion>? exclude = null)
    {
        ArchitectureCoverageContract contract = new()
        {
            Name = "rule-input-coverage",
            Id = "rule-input-coverage",
            Scope = "rule_input",
            Reason = "Flag if referenced rules stop matching any code.",
        };

        contract.ContractIds.AddRange(contractIds);

        if (exclude != null)
        {
            contract.Exclude.AddRange(exclude);
        }

        return contract;
    }

    [Test]
    public void CheckRuleInputCoverage_ContractWithRealMatches_ProducesNoFindings()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(_value));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void CheckRuleInputCoverage_TargetLayerWithNoMatchingCode_IsReportedAsEmptyInput()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(_value1));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].SourceType, Is.EqualTo("video-to-ghost-rule"));
        Assert.That(findings[0].ForbiddenNamespace, Is.EqualTo("empty-input"));
        Assert.That(findings[0].ForbiddenReferences, Is.EqualTo(_value2));
    }

    [Test]
    public void CheckRuleInputCoverage_DanglingLayerReference_IsReportedAsUnresolved()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(_value3));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].SourceType, Is.EqualTo("typo-rule"));
        Assert.That(findings[0].ForbiddenNamespace, Is.EqualTo("unresolved"));
        Assert.That(findings[0].ForbiddenReferences, Is.EqualTo(_value4));
    }

    [Test]
    public void CheckRuleInputCoverage_ExcludedContractId_ProducesNoFindings()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(
                _value5,
                new[]
                {
                    new ArchitectureCoverageExclusion
                    {
                        ContractId = "video-to-ghost-rule",
                        Reason = "Ghost layer is intentionally unused for now."
                    },
                    new ArchitectureCoverageExclusion
                    {
                        ContractId = "typo-rule",
                        Reason = "Placeholder rule retained for documentation purposes."
                    }
                }));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void CheckRuleInputCoverage_ExactOptionalEmptyInput_SuppressesOnlyThatInput()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureCoverageContract contract = CreateRuleInputContract(
            _value6);
        contract.OptionalInputs.Add(new ArchitectureOptionalRuleInput
        {
            ContractId = "video-to-ghost-rule",
            Input = "forbidden",
            Layer = "ghost",
            Reason = "The future video integration has not been created."
        });

        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].SourceType, Is.EqualTo("typo-rule"));
        Assert.That(findings[0].ForbiddenNamespace, Is.EqualTo("unresolved"));
    }

    [Test]
    public void CheckRuleInputCoverage_RepeatedRuns_AreDeterministic()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureCoverageContract contract = CreateRuleInputContract(
            _value7);

        ArchitectureContractRunner firstRunner = new(CreateContext(), document);
        ArchitectureContractRunner secondRunner = new(CreateContext(), document);

        List<ArchitectureViolation> first = firstRunner.CheckCoverageContract(contract);
        List<ArchitectureViolation> second = secondRunner.CheckCoverageContract(contract);

        Assert.That(
            first.Select(f => (f.SourceType, f.ForbiddenNamespace, Reference: f.ForbiddenReferences.Single())),
            Is.EqualTo(second.Select(f => (f.SourceType, f.ForbiddenNamespace, Reference: f.ForbiddenReferences.Single()))));
    }

    [Test]
    public void CheckRuleInputCoverage_ModuleContainerWithDiscoveredModules_ProducesNoFindings()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(CreateRuleInputContract(_value8));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void RuleInputReferences_FieldAwareFamilies_PreserveActualInputNames()
    {
        var typePlacement = new ArchitectureTypePlacementContract
        {
            TypesMatching = new ArchitectureTypeMatcher { Layer = "selector-layer" },
            MustResideInLayers = { "placement-layer" }
        };
        var attributeUsage = new ArchitectureAttributeUsageContract
        {
            AllowedOnlyInLayers = { "attribute-allowed" },
            ForbiddenInLayers = { "attribute-forbidden" }
        };
        var interfaceImplementation = new ArchitectureInterfaceImplementationContract
        {
            AllowedOnlyInLayers = { "interface-allowed" },
            ForbiddenInLayers = { "interface-forbidden" }
        };
        var moduleContainer = new ArchitectureModuleContainerContract
        {
            Container = "Example.Cli.Commands",
            Profile = "cli_command"
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                ArchitectureRuleInputReferences.For(typePlacement).Select(reference => (reference.Input, reference.Layer)),
                Is.EquivalentTo(new[]
                {
                    ("types_matching.layer", "selector-layer"),
                    ("must_reside_in_layers", "placement-layer")
                }));
            Assert.That(
                ArchitectureRuleInputReferences.For(attributeUsage).Select(reference => (reference.Input, reference.Layer)),
                Is.EquivalentTo(new[]
                {
                    ("allowed_only_in_layers", "attribute-allowed"),
                    ("forbidden_in_layers", "attribute-forbidden")
                }));
            Assert.That(
                ArchitectureRuleInputReferences.For(interfaceImplementation).Select(reference => (reference.Input, reference.Layer)),
                Is.EquivalentTo(new[]
                {
                    ("allowed_only_in_layers", "interface-allowed"),
                    ("forbidden_in_layers", "interface-forbidden")
                }));
            Assert.That(
                ArchitectureRuleInputReferences.For(moduleContainer)
                    .Select(reference => (reference.Input, reference.Layer, reference.IsLayerReference)),
                Is.EqualTo(new[] { ("container", "Example.Cli.Commands", false) }));
        });
    }
}
