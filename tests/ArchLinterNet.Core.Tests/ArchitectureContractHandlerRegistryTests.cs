using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureContractHandlerRegistryTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static readonly Assembly _layerFixtureAssembly = typeof(HandlerRegistryLayerFixtures.Upper.UpperService).Assembly;

    private ArchitectureAnalysisContext CreateContext(Assembly assembly)
    {
        return new ArchitectureAnalysisContext(
            _tempDir,
            new[] { assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static IReadOnlyList<object> Project(IEnumerable<ArchitectureViolation> violations)
    {
        return violations
            .Select(v => (object)(v.ContractName, v.ContractId, v.SourceType, v.ForbiddenNamespace,
                ForbiddenReferences: string.Join("|", v.ForbiddenReferences)))
            .ToList();
    }

    private static ArchitectureContractDocument CreateLayerFixtureDocument(
        string dependencySource,
        string dependencyForbidden,
        List<string> layerOrder)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["layerUpper"] = new() { Namespace = "HandlerRegistryLayerFixtures.Upper" },
                ["layerLower"] = new() { Namespace = "HandlerRegistryLayerFixtures.Lower" },
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string>() },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Name = "Dependency", Id = "dep", Source = dependencySource, Forbidden = new List<string> { dependencyForbidden } },
                },
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Name = "Layer Order", Id = "order", Layers = layerOrder },
                },
            }
        };
    }

    [Test]
    public void CreateDefault_RegistersHandlersForMigratedFamilies()
    {
        ArchitectureContractHandlerRegistry registry = ArchitectureContractHandlerRegistry.CreateDefault();

        Assert.That(registry.TryGetHandler("dependency", out _), Is.True);
        Assert.That(registry.TryGetHandler("layer", out _), Is.True);
        Assert.That(registry.TryGetHandler("layer_template", out _), Is.True);
        Assert.That(registry.TryGetHandler("cycle", out _), Is.True);
        Assert.That(registry.TryGetHandler("protected", out _), Is.False);
    }

    [Test]
    public void Execute_UnknownFamily_Throws()
    {
        ArchitectureContractHandlerRegistry registry = ArchitectureContractHandlerRegistry.CreateDefault();
        var runner = new ArchitectureContractRunner(
            CreateContext(_layerFixtureAssembly),
            CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" }));
        IArchitectureContract contract = new ArchitectureDependencyContract { Name = "x", Source = "layerUpper" };

        Assert.Throws<InvalidOperationException>(() => registry.Execute("unknown_family", runner, contract));
    }

    [Test]
    public void DependencyHandler_MatchesDirectRunnerCheck()
    {
        // Upper references Lower, so forbidding Upper -> Lower is a real violation.
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" });
        var runner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureDependencyContract contract = document.Contracts.Strict[0];

        List<ArchitectureViolation> direct = runner.CheckContract(contract);
        ArchitectureHandlerResult viaHandler = ArchitectureContractHandlerRegistry.CreateDefault()
            .Execute("dependency", runner, contract);

        Assert.That(direct, Has.Count.GreaterThan(0));
        Assert.That(Project(viaHandler.Violations), Is.EqualTo(Project(direct)));
        Assert.That(viaHandler.Cycles, Is.Empty);
    }

    [Test]
    public void LayerHandler_MatchesDirectRunnerCheck()
    {
        // Lower listed before Upper: Upper (later) referencing the earlier-listed Lower violates the order.
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerLower", "layerUpper" });
        var runner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureLayerContract contract = document.Contracts.StrictLayers[0];

        List<ArchitectureViolation> direct = runner.CheckLayerContract(contract);
        ArchitectureHandlerResult viaHandler = ArchitectureContractHandlerRegistry.CreateDefault()
            .Execute("layer", runner, contract);

        Assert.That(direct, Has.Count.GreaterThan(0));
        Assert.That(Project(viaHandler.Violations), Is.EqualTo(Project(direct)));
    }

    [Test]
    public void LayerHandler_OrderedCorrectly_ProducesNoViolations()
    {
        // Upper listed before Lower: only Lower is checked, and Lower does not reference Upper.
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" });
        var runner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureLayerContract contract = document.Contracts.StrictLayers[0];

        ArchitectureHandlerResult viaHandler = ArchitectureContractHandlerRegistry.CreateDefault()
            .Execute("layer", runner, contract);

        Assert.That(viaHandler.Violations, Is.Empty);
    }

    [Test]
    public void CycleHandler_NoCycle_ReturnsEmptyNonViolationResult()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["layerA"] = new() { Namespace = "HandlerRegistryCycleFixtures.LayerA" },
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string>() },
            Contracts = new ArchitectureContractGroups
            {
                StrictCycles = new List<ArchitectureCycleContract>
                {
                    new() { Name = "Cycle", Id = "cyc", Layers = new List<string> { "layerA" } }
                }
            }
        };

        Assembly fixtureAssembly = typeof(HandlerRegistryCycleFixtures.LayerA.ServiceA).Assembly;
        var runner = new ArchitectureContractRunner(CreateContext(fixtureAssembly), document);
        ArchitectureCycleContract contract = document.Contracts.StrictCycles[0];

        ArchitectureHandlerResult result = ArchitectureContractHandlerRegistry.CreateDefault()
            .Execute("cycle", runner, contract);

        Assert.That(result.Cycles, Is.Empty);
        Assert.That(result.Violations, Is.Empty);
    }

    [Test]
    public void CycleHandler_WithCycle_PrefixesContractIdOntoEachCycle()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["layerA"] = new() { Namespace = "HandlerRegistryCycleFixtures.LayerA" },
                ["layerB"] = new() { Namespace = "HandlerRegistryCycleFixtures.LayerB" },
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string>() },
            Contracts = new ArchitectureContractGroups
            {
                StrictCycles = new List<ArchitectureCycleContract>
                {
                    new() { Name = "Cycle", Id = "cyc", Layers = new List<string> { "layerA", "layerB" } }
                }
            }
        };

        Assembly fixtureAssembly = typeof(HandlerRegistryCycleFixtures.LayerA.ServiceA).Assembly;
        var runner = new ArchitectureContractRunner(CreateContext(fixtureAssembly), document);
        ArchitectureCycleContract contract = document.Contracts.StrictCycles[0];

        List<string> direct = runner.CheckCycleContract(contract).ToList();
        ArchitectureHandlerResult viaHandler = ArchitectureContractHandlerRegistry.CreateDefault()
            .Execute("cycle", runner, contract);

        Assert.That(direct, Has.Count.GreaterThan(0));
        Assert.That(viaHandler.Cycles, Is.EqualTo(direct.Select(c => $"[cyc] {c}")));
        Assert.That(viaHandler.Violations, Is.Empty);
    }

    [Test]
    public void Executor_RoutesMigratedFamiliesThroughRegistry_MatchesDirectRunnerCalls()
    {
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerLower", "layerUpper" });

        var directRunner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        List<ArchitectureViolation> expectedViolations = new();
        expectedViolations.AddRange(directRunner.CheckContract(document.Contracts.Strict[0]));
        expectedViolations.AddRange(directRunner.CheckLayerContract(document.Contracts.StrictLayers[0]));

        var executorRunner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureContractExecutor.ExecutionResult result =
            ArchitectureContractExecutor.Execute(executorRunner, document, "strict");

        Assert.That(Project(result.Violations), Is.EqualTo(Project(expectedViolations)));
    }
}
