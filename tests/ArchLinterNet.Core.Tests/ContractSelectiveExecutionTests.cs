using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSelectiveExecutionTests
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

    private ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(ArchitectureContractDocument).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocumentWithIds()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["layerA"] = new() { Namespace = "ArchLinterNet.Core" },
                ["layerB"] = new() { Namespace = "ArchLinterNet.Core.Contracts" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(ArchitectureContractDocument).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Name = "Contract Alpha", Id = "alpha", Source = "layerA", Forbidden = new List<string> { "layerB" } },
                    new() { Name = "Contract Beta", Id = "beta", Source = "layerB", Forbidden = new List<string> { "layerA" } },
                },
                StrictCycles = new List<ArchitectureCycleContract>
                {
                    new() { Name = "Cycle Check", Id = "cycle-check", Layers = new List<string> { "layerA", "layerB" } },
                }
            }
        };
    }

    [Test]
    public void NoFilter_ExecutesAllContracts()
    {
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            CreateDocumentWithIds());

        var violations = new List<ArchitectureViolation>();
        violations.AddRange(runner.CheckContract(runner.StrictContracts().First()));
        violations.AddRange(runner.CheckContract(runner.StrictContracts().Last()));

        Assert.That(violations, Has.Count.GreaterThan(0));
    }

    [Test]
    public void SingleSelectedId_ExecutesOnlyMatchingContract()
    {
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            CreateDocumentWithIds(),
            new HashSet<string> { "alpha" });

        var alphaContract = runner.StrictContracts().First(c => c.Id == "alpha");
        var betaContract = runner.StrictContracts().First(c => c.Id == "beta");

        List<ArchitectureViolation> alphaViolations = runner.CheckContract(alphaContract);
        List<ArchitectureViolation> betaViolations = runner.CheckContract(betaContract);

        Assert.That(alphaViolations, Has.Count.GreaterThan(0));
        Assert.That(betaViolations, Is.Empty);
    }

    [Test]
    public void MultipleSelectedIds_ExecutesAllMatchingContracts()
    {
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            CreateDocumentWithIds(),
            new HashSet<string> { "alpha", "beta" });

        var violations = new List<ArchitectureViolation>();
        violations.AddRange(runner.CheckContract(runner.StrictContracts().First(c => c.Id == "alpha")));
        violations.AddRange(runner.CheckContract(runner.StrictContracts().First(c => c.Id == "beta")));

        Assert.That(violations, Has.Count.GreaterThan(0));
    }

    [Test]
    public void EmptyFilter_ExecutesAllContracts()
    {
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            CreateDocumentWithIds(),
            new HashSet<string>());

        var violations = new List<ArchitectureViolation>();
        violations.AddRange(runner.CheckContract(runner.StrictContracts().First()));
        violations.AddRange(runner.CheckContract(runner.StrictContracts().Last()));

        Assert.That(violations, Has.Count.GreaterThan(0));
    }

    [Test]
    public void NonMatchingId_SkipsAllContracts()
    {
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            CreateDocumentWithIds(),
            new HashSet<string> { "nonexistent" });

        var alphaContract = runner.StrictContracts().First(c => c.Id == "alpha");
        var betaContract = runner.StrictContracts().First(c => c.Id == "beta");

        Assert.That(runner.CheckContract(alphaContract), Is.Empty);
        Assert.That(runner.CheckContract(betaContract), Is.Empty);
    }

    [Test]
    public void ConfigurationCheck_RunsRegardlessOfFilter()
    {
        var doc = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["missing"] = new() { Namespace = "Does.Not.Exist" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "NonExistentAssembly" }
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Name = "Test", Id = "test", Source = "missing", Forbidden = new List<string>() },
                },
            }
        };

        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext(_tempDir, Array.Empty<Assembly>(), new[] { "NonExistentAssembly" }, Array.Empty<string>()),
            doc,
            new HashSet<string> { "test" });

        List<ArchitectureViolation> configViolations = runner.CheckConfiguration();

        Assert.That(configViolations, Has.Count.GreaterThan(0));
        Assert.That(configViolations[0].ContractName, Is.EqualTo("<configuration>"));
    }

    [Test]
    public void CycleContract_WithFilter_RespectsSelection()
    {
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            CreateDocumentWithIds(),
            new HashSet<string> { "cycle-check" });

        var cycleContract = runner.StrictCycleContracts().First(c => c.Id == "cycle-check");
        IReadOnlyCollection<string> cycles = runner.CheckCycleContract(cycleContract);

        Assert.That(cycles, Is.Not.Null);
    }

    [Test]
    public void Violation_IncludesContractId()
    {
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            CreateDocumentWithIds());

        var contract = runner.StrictContracts().First(c => c.Id == "alpha");
        List<ArchitectureViolation> violations = runner.CheckContract(contract);

        Assert.That(violations, Has.Count.GreaterThan(0));
        Assert.That(violations[0].ContractId, Is.EqualTo("alpha"));
    }
}
