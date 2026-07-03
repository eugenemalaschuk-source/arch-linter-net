using System.Reflection;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using Microsoft.Extensions.DependencyInjection;
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

    private static ArchitectureContractHandlerRegistry CreateRegistry()
    {
        return new ArchitectureContractHandlerRegistry(new IArchitectureContractHandler[]
        {
            new DependencyContractHandler(),
            new LayerContractHandler(),
            new AllowOnlyContractHandler(),
            new CycleContractHandler(),
            new AcyclicSiblingContractHandler(),
            new MethodBodyContractHandler(),
            new AsmdefContractHandler(),
            new IndependenceContractHandler(),
            new ProtectedContractHandler(),
            new ExternalContractHandler(),
            new CoverageContractHandler(),
        });
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
    public void Constructor_RegistersHandlersForEveryFamily()
    {
        ArchitectureContractHandlerRegistry registry = CreateRegistry();

        Assert.That(registry.TryGetHandler("dependency", out _), Is.True);
        Assert.That(registry.TryGetHandler("layer", out _), Is.True);
        Assert.That(registry.TryGetHandler("layer_template", out _), Is.True);
        Assert.That(registry.TryGetHandler("allow_only", out _), Is.True);
        Assert.That(registry.TryGetHandler("cycle", out _), Is.True);
        Assert.That(registry.TryGetHandler("acyclic_sibling", out _), Is.True);
        Assert.That(registry.TryGetHandler("method_body", out _), Is.True);
        Assert.That(registry.TryGetHandler("asmdef", out _), Is.True);
        Assert.That(registry.TryGetHandler("independence", out _), Is.True);
        Assert.That(registry.TryGetHandler("protected", out _), Is.True);
        Assert.That(registry.TryGetHandler("external", out _), Is.True);
        Assert.That(registry.TryGetHandler("coverage", out _), Is.True);
        Assert.That(registry.TryGetHandler("unknown_family", out _), Is.False);
    }

    [Test]
    public void AddArchLinterNetCore_RegistersHandlerRegistryForEveryFamily()
    {
        using ServiceProvider provider = new ServiceCollection().AddArchLinterNetCore().BuildServiceProvider();

        ArchitectureContractHandlerRegistry concreteRegistry = provider.GetRequiredService<ArchitectureContractHandlerRegistry>();
        IArchitectureContractHandlerRegistry registry = provider.GetRequiredService<IArchitectureContractHandlerRegistry>();

        Assert.That(registry, Is.SameAs(concreteRegistry),
            "The interface registration must resolve to the same singleton as the concrete type, preserving compatibility for callers resolving ArchitectureContractHandlerRegistry directly.");
        Assert.That(registry.TryGetHandler("dependency", out _), Is.True);
        Assert.That(registry.TryGetHandler("layer", out _), Is.True);
        Assert.That(registry.TryGetHandler("layer_template", out _), Is.True);
        Assert.That(registry.TryGetHandler("allow_only", out _), Is.True);
        Assert.That(registry.TryGetHandler("cycle", out _), Is.True);
        Assert.That(registry.TryGetHandler("acyclic_sibling", out _), Is.True);
        Assert.That(registry.TryGetHandler("method_body", out _), Is.True);
        Assert.That(registry.TryGetHandler("asmdef", out _), Is.True);
        Assert.That(registry.TryGetHandler("independence", out _), Is.True);
        Assert.That(registry.TryGetHandler("protected", out _), Is.True);
        Assert.That(registry.TryGetHandler("external", out _), Is.True);
        Assert.That(registry.TryGetHandler("coverage", out _), Is.True);
    }

    [Test]
    public void Execute_UnknownFamily_Throws()
    {
        ArchitectureContractHandlerRegistry registry = CreateRegistry();
        var runner = new ArchitectureContractRunner(
            CreateContext(_layerFixtureAssembly),
            CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" }));
        IArchitectureContract contract = new ArchitectureDependencyContract { Name = "x", Source = "layerUpper" };

        Assert.Throws<InvalidOperationException>(() => registry.Execute("unknown_family", runner.Session, contract));
    }

    [Test]
    public void DependencyHandler_MatchesDirectRunnerCheck()
    {
        // Upper references Lower, so forbidding Upper -> Lower is a real violation.
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" });
        var runner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureDependencyContract contract = document.Contracts.Strict[0];

        List<ArchitectureViolation> direct = runner.CheckContract(contract);
        ArchitectureHandlerResult viaHandler = CreateRegistry()
            .Execute("dependency", runner.Session, contract);

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
        ArchitectureHandlerResult viaHandler = CreateRegistry()
            .Execute("layer", runner.Session, contract);

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

        ArchitectureHandlerResult viaHandler = CreateRegistry()
            .Execute("layer", runner.Session, contract);

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

        ArchitectureHandlerResult result = CreateRegistry()
            .Execute("cycle", runner.Session, contract);

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
        ArchitectureHandlerResult viaHandler = CreateRegistry()
            .Execute("cycle", runner.Session, contract);

        Assert.That(direct, Has.Count.GreaterThan(0));
        Assert.That(viaHandler.Cycles, Is.EqualTo(direct.Select(c => $"[cyc] {c}")));
        Assert.That(viaHandler.Violations, Is.Empty);
    }

    [Test]
    public void AllowOnlyHandler_MatchesDirectRunnerCheck()
    {
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" });
        document.Contracts.StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "AllowOnly", Id = "allow", Source = "layerUpper", Allowed = new List<string> { "layerUpper" } },
        };
        var runner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureAllowOnlyContract contract = document.Contracts.StrictAllowOnly[0];

        List<ArchitectureViolation> direct = runner.CheckAllowOnlyContract(contract);
        ArchitectureHandlerResult viaHandler = CreateRegistry()
            .Execute("allow_only", runner.Session, contract);

        Assert.That(direct, Has.Count.GreaterThan(0));
        Assert.That(Project(viaHandler.Violations), Is.EqualTo(Project(direct)));
        Assert.That(viaHandler.Cycles, Is.Empty);
    }

    [Test]
    public void AcyclicSiblingHandler_WithCycle_PrefixesContractIdOntoEachCycle()
    {
        Assembly fixtureAssembly = typeof(AcyclicSiblingFixtures.TwoNode.Auth.AuthService).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { fixtureAssembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract>
                {
                    new() { Name = "Acyclic", Id = "acyc", Ancestors = new List<string> { "AcyclicSiblingFixtures.TwoNode" } },
                },
            }
        };
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext(_tempDir, new[] { fixtureAssembly }, Array.Empty<string>(), Array.Empty<string>()),
            document);
        ArchitectureAcyclicSiblingContract contract = document.Contracts.StrictAcyclicSiblings[0];

        List<string> direct = runner.CheckAcyclicSiblingContract(contract).ToList();
        ArchitectureHandlerResult viaHandler = CreateRegistry()
            .Execute("acyclic_sibling", runner.Session, contract);

        Assert.That(direct, Has.Count.GreaterThan(0));
        Assert.That(viaHandler.Cycles, Is.EqualTo(direct.Select(c => $"[acyc] {c}")));
        Assert.That(viaHandler.Violations, Is.Empty);
    }

    [Test]
    public void Executor_AsInstanceService_TwoIndependentInstancesProduceIdenticalResults()
    {
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerLower", "layerUpper" });

        var firstRunner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        IArchitectureContractExecutor firstExecutor = new ArchitectureContractExecutor();
        ArchitectureContractExecutionResult firstResult =
            firstExecutor.Execute(firstRunner.Session, "strict", CreateRegistry());

        var secondRunner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        IArchitectureContractExecutor secondExecutor = new ArchitectureContractExecutor();
        ArchitectureContractExecutionResult secondResult =
            secondExecutor.Execute(secondRunner.Session, "strict", CreateRegistry());

        Assert.That(Project(secondResult.Violations), Is.EqualTo(Project(firstResult.Violations)),
            "The executor holds no mutable instance/global state, so two independently constructed instances run against equivalent sessions must produce identical results.");
    }

    [Test]
    public void Executor_RoutesAllFamiliesThroughRegistry_MatchesDirectRunnerCalls()
    {
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerLower", "layerUpper" });

        var directRunner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        List<ArchitectureViolation> expectedViolations = new();
        expectedViolations.AddRange(directRunner.CheckContract(document.Contracts.Strict[0]));
        expectedViolations.AddRange(directRunner.CheckLayerContract(document.Contracts.StrictLayers[0]));

        var executorRunner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureContractExecutionResult result =
            new ArchitectureContractExecutor().Execute(executorRunner.Session, "strict", CreateRegistry());

        Assert.That(Project(result.Violations), Is.EqualTo(Project(expectedViolations)));
    }

    [Test]
    public void Executor_BaselineCandidatesAndUnmatchedIgnores_VisibleThroughRunnerFacadeAfterHandlerDispatch()
    {
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" });
        document.Contracts.Strict[0].IgnoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "does.not.exist.Type", ForbiddenReference = "also.missing.Type", Reason = "fixture" },
        };

        var runner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);

        ArchitectureContractExecutionResult result =
            new ArchitectureContractExecutor().Execute(runner.Session, "strict", CreateRegistry());

        Assert.That(result.Violations, Has.Count.GreaterThan(0));
        Assert.That(runner.BaselineCandidates, Has.Count.GreaterThan(0),
            "Baseline candidates collected while a handler ran against the session should surface through the runner facade.");
        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(1),
            "The ignore entry that never matched a real violation should surface through the runner facade.");
    }

    [Test]
    public void Session_TypeIndexAndReferenceGraph_AreSharedAcrossMultipleHandlerDispatches()
    {
        var document = CreateLayerFixtureDocument("layerUpper", "layerLower", new List<string> { "layerUpper", "layerLower" });
        var runner = new ArchitectureContractRunner(CreateContext(_layerFixtureAssembly), document);
        ArchitectureContractHandlerRegistry registry = CreateRegistry();

        registry.Execute("dependency", runner.Session, document.Contracts.Strict[0]);
        ArchitectureTypeIndex firstTypeIndex = runner.Session.TypeIndex;
        ArchitectureReferenceGraph firstReferenceGraph = runner.Session.ReferenceGraph;

        registry.Execute("dependency", runner.Session, document.Contracts.Strict[0]);

        Assert.That(runner.Session.TypeIndex, Is.SameAs(firstTypeIndex),
            "The type index is a single per-session cache, not rebuilt per handler dispatch.");
        Assert.That(runner.Session.ReferenceGraph, Is.SameAs(firstReferenceGraph),
            "The reference graph is a single per-session cache, not rebuilt per handler dispatch.");
    }

    [Test]
    public void Execute_LayerTemplateIdConflict_DoesNotThrow_ReportedByPolicyConsistencyInstead()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["shared"] = new() { Namespace = "HandlerRegistryLayerFixtures.Upper" },
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string>() },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Id = "strict-layer", Name = "Strict Layer", Layers = new List<string> { "shared" } },
                },
                // Conflicts with the contract generated by AuditLayerTemplates below ("tmpl/foo").
                // Layer-template ID conflicts are detected by the policy-consistency pass
                // (configurable via analysis.policy_consistency), not by a hard throw during
                // contract execution.
                AuditLayers = new List<ArchitectureLayerContract>
                {
                    new() { Id = "tmpl/foo", Name = "Audit Layer", Layers = new List<string> { "shared" } },
                },
                AuditLayerTemplates = new List<ArchitectureLayerTemplateContract>
                {
                    new()
                    {
                        Id = "tmpl",
                        Name = "Audit Template",
                        Containers = { "Foo" },
                        Layers = { new ArchitectureTemplateLayer { Name = "Sub" } }
                    }
                },
            }
        };

        var executor = new ArchitectureContractExecutor();

        var strictRunner = new ArchitectureContractRunner(CreateContext(typeof(ArchitectureContractDocument).Assembly), document);
        Assert.DoesNotThrow(() => executor.Execute(strictRunner.Session, "strict", CreateRegistry()));

        var auditRunner = new ArchitectureContractRunner(CreateContext(typeof(ArchitectureContractDocument).Assembly), document);
        Assert.DoesNotThrow(() => executor.Execute(auditRunner.Session, "audit", CreateRegistry()));

        var policyConsistencyRunner = new ArchitectureContractRunner(CreateContext(typeof(ArchitectureContractDocument).Assembly), document);
        var findings = policyConsistencyRunner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "duplicate-id" && f.ConflictingContractIds.Contains("tmpl/foo")), Is.True);
    }
}
