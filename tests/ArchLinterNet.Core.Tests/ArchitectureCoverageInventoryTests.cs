using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureCoverageInventoryTests
{
    private static readonly Assembly[] _targetAssemblies = { typeof(ArchitectureCoverageInventoryTests).Assembly };

    private static IArchitectureRunnerSetupService CreateRunnerSetupService()
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

    private static ArchitectureAnalysisSession CreateSession()
    {
        var context = new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: _targetAssemblies,
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());

        return new ArchitectureAnalysisSession(
            context, CreateDocument(), selectedContractIds: null, enableUnmatchedIgnoreTracking: true, preprocessorSymbols: null);
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
    public void Build_WithProjectDiscoveryResult_ExposesItVerbatim()
    {
        var discoveryResult = new ProjectDiscoveryResult(
            new[] { "Fixture.Assembly" },
            new[] { "bin/Debug/net10.0" },
            new[] { "src/Fixture" },
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
