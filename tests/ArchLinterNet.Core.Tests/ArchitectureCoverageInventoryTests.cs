using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureCoverageInventoryTests
{
    private static readonly string[] _value = { "Acme.Modules.Billing", "Acme.Modules.Orders" };
    private static readonly string[] _value1 = { "Fixture.Assembly" };
    private static readonly string[] _value2 = { "bin/Debug/net10.0" };
    private static readonly string[] _value3 = { "src/Fixture" };
    private static readonly Assembly[] _targetAssemblies = { typeof(ArchitectureCoverageInventoryTests).Assembly };

    private static ArchitectureRunnerSetupService CreateRunnerSetupService()
    {
        return new ArchitectureRunnerSetupService(
            new ArchitecturePolicyDocumentLoader(),
            new ArchitectureBaselineLoadingService(),
            new ArchitectureRepositoryRootResolver(),
            new ConditionSetResolutionService(),
            new ArchitectureProjectDiscoveryService(),
            new ArchitectureAssemblyResolutionService());
    }

    private const string AlphaNamespace = "ArchLinterNet.Core.Tests.CoverageInventoryFixtures.Alpha";
    private const string BetaNamespace = "ArchLinterNet.Core.Tests.CoverageInventoryFixtures.Beta";

    private static ArchitectureAnalysisSession CreateSession(ArchitectureContractDocument? document = null)
    {
        var context = new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: _targetAssemblies,
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());

        return new ArchitectureAnalysisSession(
            context, document ?? CreateDocument(), selectedContractIds: null, enableUnmatchedIgnoreTracking: true, preprocessorSymbols: null);
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        var document = new ArchitectureContractDocument();
        document.Layers["alpha"] = new ArchitectureLayer { Namespace = AlphaNamespace };
        document.Layers["beta"] = new ArchitectureLayer { Namespace = BetaNamespace };
        document.Contracts.StrictLayerTemplates.Add(new ArchitectureLayerTemplateContract
        {
            Name = "fixture-template",
            Containers = { AlphaNamespace },
            Layers = { new ArchitectureTemplateLayer { Name = "Inner" } },
            Exhaustive = true,
            Reason = "fixture"
        });
        return document;
    }

    [Test]
    public void Build_CollectsNamespacesSortedOrdinallyWithRepresentativeType()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        var alpha = inventory.Namespaces.Single(n => n.Namespace == AlphaNamespace);
        var beta = inventory.Namespaces.Single(n => n.Namespace == BetaNamespace);

        Assert.That(alpha.RepresentativeType, Is.EqualTo($"{AlphaNamespace}.AlphaOtherType"));
        Assert.That(beta.RepresentativeType, Is.EqualTo($"{BetaNamespace}.BetaOtherType"));

        var ordered = inventory.Namespaces.Select(n => n.Namespace).ToList();
        var expectedOrder = ordered.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.That(ordered, Is.EqualTo(expectedOrder));
    }

    [Test]
    public void Build_RepeatedBuilds_ProduceIdenticalNamespaceOrderingAndRepresentativeTypes()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureCoverageInventory first = ArchitectureCoverageInventory.Build(document, CreateSession());
        ArchitectureCoverageInventory second = ArchitectureCoverageInventory.Build(document, CreateSession());

        Assert.That(first.Namespaces, Is.EqualTo(second.Namespaces));
    }

    [Test]
    public void DependencyEdges_DeduplicatesAndExcludesSelfEdges_SortedBySourceThenTarget()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        var edges = inventory.DependencyEdges;

        Assert.That(edges.Count(e => e.SourceNamespace == AlphaNamespace && e.TargetNamespace == BetaNamespace), Is.EqualTo(1));
        Assert.That(edges.Any(e => e.SourceNamespace == AlphaNamespace && e.TargetNamespace == AlphaNamespace), Is.False);

        var orderedBySource = edges.OrderBy(e => e.SourceNamespace, StringComparer.Ordinal)
            .ThenBy(e => e.TargetNamespace, StringComparer.Ordinal)
            .ToList();
        Assert.That(edges, Is.EqualTo(orderedBySource));
    }

    [Test]
    public void Build_PreservesExhaustiveLayerTemplateExpansion()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        var expansion = inventory.ExpandedLayerTemplates.Single();

        Assert.That(expansion.Exhaustive, Is.True);
        Assert.That(expansion.ContainerNamespace, Is.EqualTo(AlphaNamespace));
    }

    [Test]
    public void Build_ExposesResolvedSourceSetExpansion()
    {
        ArchitectureContractDocument document = CreateDocument();
        document.SourceExpansion = new ArchitectureSourceExpansionInventory(
            [
                new ArchitectureSourceSetResolution(
                    "modules",
                    ArchitectureSourceSetKind.Assembly,
                    ["Acme.Modules.Billing", "Acme.Modules.Orders"],
                    false,
                    string.Empty)
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
            ]);

        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(document, CreateSession());

        Assert.Multiple(() =>
        {
            Assert.That(inventory.SourceExpansion.Sets.Single().ResolvedSources,
                Is.EqualTo(_value));
            Assert.That(inventory.SourceExpansion.Contracts.Single().AuthoredContractId,
                Is.EqualTo("modules-no-infrastructure"));
        });
    }

    [Test]
    public void Build_WithProjectDiscoveryResult_ExposesItVerbatim()
    {
        var discoveryResult = new ProjectDiscoveryResult(
            _value1,
            _value2,
            _value3,
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>());

        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(
            CreateDocument(), CreateSession(), discoveryResult);

        Assert.That(inventory.ProjectDiscovery, Is.SameAs(discoveryResult));
    }

    [Test]
    public void Build_WithoutProjectDiscoveryResult_IsAbsent()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        Assert.That(inventory.ProjectDiscovery, Is.Null);
    }

    [Test]
    public void Session_ExposesCoverageInventoryOnlyThroughExplicitAccessor()
    {
        ArchitectureAnalysisSession session = CreateSession();

        ArchitectureCoverageInventory inventory = session.BuildCoverageInventory(CreateDocument());

        Assert.That(inventory.Namespaces, Is.Not.Empty);
    }

    [Test]
    public void Build_ExposesTypedRuntimeInclusionAndStaleExclusionParticipation()
    {
        ArchitectureContractDocument document = CreateDocument();
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "alpha-types",
            Id = "alpha-types",
            TypesMatching = new ArchitectureTypeMatcher { Namespace = AlphaNamespace },
            ExcludeTypesMatching = { new ArchitectureTypeMatcher { Namespace = "No.Such.Namespace" } }
        };
        document.Contracts.StrictTypePlacement.Add(contract);
        ArchitectureAnalysisSession session = CreateSession(document);

        // Build before execution to prove the lazily cached inventory exposes the session's final
        // append-only runtime evidence without reconstructing selector matches.
        ArchitectureCoverageInventory inventory = session.BuildCoverageInventory(document);
        session.CheckTypePlacementContract(contract);

        Assert.That(
            inventory.SelectorParticipation.Select(item => (item.Kind, item.Field, item.Matched, item.IsStaleExclusion)),
            Is.EqualTo(new[]
            {
                (ArchitectureSelectorParticipationKind.Inclusion, "types_matching", true, false),
                (ArchitectureSelectorParticipationKind.Exclusion, "exclude_types_matching", false, true)
            }));
    }

    [Test]
    public void DeclaredLayers_PreservesNamespaceSuffixAndExternalFlag()
    {
        var document = new ArchitectureContractDocument();
        document.Layers["suffix-layer"] = new ArchitectureLayer
        {
            Namespace = AlphaNamespace,
            NamespaceSuffix = "Impl"
        };
        document.Layers["external-layer"] = new ArchitectureLayer
        {
            Namespace = BetaNamespace,
            External = true
        };

        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(document, CreateSession());

        var suffixLayer = inventory.DeclaredLayers.Single(l => l.Name == "suffix-layer");
        var externalLayer = inventory.DeclaredLayers.Single(l => l.Name == "external-layer");

        Assert.That(suffixLayer.Layer.NamespaceSuffix, Is.EqualTo("Impl"));
        Assert.That(externalLayer.Layer.External, Is.True);
    }

    [Test]
    public void PolicyWithoutCoverageContracts_ValidationBehaviorIsUnaffectedByInventoryExisting()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-coverage-inventory-unaffected-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
            File.WriteAllText(policyPath, """
                version: 1
                name: Test

                layers:
                  core:
                    namespace: ArchLinterNet.Core

                analysis:
                  target_assemblies: [ArchLinterNet.Core]

                contracts: {}
                """);

            ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            });

            Assert.That(outcome.Passed, Is.True);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void BuildRunner_ResolvedProjectDiscoveryResult_ReachesSessionWithoutExplicitOverride()
    {
        string repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-coverage-inventory-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);
        try
        {
            string projectDir = Path.Combine(repoRoot, "ArchLinterNet.Core");
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(Path.Combine(projectDir, "ArchLinterNet.Core.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            string policyPath = Path.Combine(repoRoot, "policy.arch.yml");
            File.WriteAllText(policyPath, "version: 1\nname: test\n");

            var document = new ArchitectureContractDocument
            {
                Version = 1,
                Name = "Test",
                Analysis = new ArchitectureAnalysisConfiguration
                {
                    TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
                    Projects = new List<string> { Path.Combine(projectDir, "ArchLinterNet.Core.csproj") }
                }
            };

            ArchitectureRunnerSetup setup = CreateRunnerSetupService().BuildRunner(document, policyPath);

            ArchitectureCoverageInventory inventory = setup.Runner.Session.BuildCoverageInventory(document);

            Assert.That(inventory.ProjectDiscovery, Is.Not.Null);
            Assert.That(inventory.ProjectDiscovery!.SourceRoots, Has.Member("ArchLinterNet.Core"));
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }

    [Test]
    public void BuildRunner_NoSolutionOrProjectsConfigured_ProjectDiscoveryIsAbsentNotEmpty()
    {
        string repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-coverage-inventory-no-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);
        try
        {
            string policyPath = Path.Combine(repoRoot, "policy.arch.yml");
            File.WriteAllText(policyPath, "version: 1\nname: test\n");

            var document = new ArchitectureContractDocument
            {
                Version = 1,
                Name = "Test",
                Analysis = new ArchitectureAnalysisConfiguration
                {
                    TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
                }
            };

            ArchitectureRunnerSetup setup = CreateRunnerSetupService().BuildRunner(document, policyPath);

            ArchitectureCoverageInventory inventory = setup.Runner.Session.BuildCoverageInventory(document);

            Assert.That(inventory.ProjectDiscovery, Is.Null,
                "no analysis.solution/analysis.projects means discovery was never attempted, " +
                "which must be distinguishable from discovery running and finding nothing");
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }

    [Test]
    public void Session_BuildCoverageInventory_RepeatedCalls_ReturnSameCachedInstance()
    {
        ArchitectureAnalysisSession session = CreateSession();
        ArchitectureContractDocument document = CreateDocument();

        ArchitectureCoverageInventory first = session.BuildCoverageInventory(document);
        ArchitectureCoverageInventory second = session.BuildCoverageInventory(document);

        Assert.That(second, Is.SameAs(first));
    }
}
