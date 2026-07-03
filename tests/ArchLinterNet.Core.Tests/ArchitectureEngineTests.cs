using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureEngineTests
{
    private string _tempDir = null!;
    private string _policyPath = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(_policyPath, """
            version: 1
            name: Engine Test
            layers:
              core:
                namespace: ArchLinterNet.Core
            analysis:
              target_assemblies:
                - ArchLinterNet.Core
            contracts:
              strict: []
              strict_layers: []
              strict_allow_only: []
              strict_cycles: []
              strict_method_body: []
              strict_asmdef: []
              strict_independence: []
            """);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void Build_WithDefaultRegistrations_ResolvesBothApplicationServices()
    {
        ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ValidationOutcome validationOutcome = engine.Validate(new ValidationRequest
        {
            PolicyPath = _policyPath,
            Mode = "strict",
        });

        BaselineGenerationOutcome baselineOutcome = engine.GenerateBaseline(new BaselineGenerationRequest
        {
            PolicyPath = _policyPath,
            Mode = "strict",
        });

        Assert.That(validationOutcome, Is.Not.Null);
        Assert.That(baselineOutcome, Is.Not.Null);
    }

    [Test]
    public void Validate_MatchesLegacyStaticService()
    {
        ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        ValidationRequest request = new() { PolicyPath = _policyPath, Mode = "strict" };

        ValidationOutcome viaEngine = engine.Validate(request);
        ValidationOutcome viaStatic = ArchitectureValidationService.Validate(request);

        Assert.That(viaEngine.Passed, Is.EqualTo(viaStatic.Passed));
        Assert.That(viaEngine.Violations, Is.EqualTo(viaStatic.Violations));
        Assert.That(viaEngine.Cycles, Is.EqualTo(viaStatic.Cycles));
    }

    [Test]
    public void GenerateBaseline_MatchesLegacyStaticService()
    {
        ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        BaselineGenerationRequest request = new() { PolicyPath = _policyPath, Mode = "strict" };

        BaselineGenerationOutcome viaEngine = engine.GenerateBaseline(request);
        BaselineGenerationOutcome viaStatic = ArchitectureBaselineService.Generate(request);

        Assert.That(viaEngine.Succeeded, Is.EqualTo(viaStatic.Succeeded));
        Assert.That(viaEngine.Yaml, Is.EqualTo(viaStatic.Yaml));
        Assert.That(viaEngine.CandidateCount, Is.EqualTo(viaStatic.CandidateCount));
    }

    [Test]
    public void ServiceCollectionExtensions_RegistersBothApplicationServices()
    {
        ServiceCollection services = new();
        services.AddArchLinterNetCore();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<IArchitectureValidationApplicationService>(), Is.Not.Null);
        Assert.That(provider.GetService<IArchitectureBaselineApplicationService>(), Is.Not.Null);
    }

    [Test]
    public void ServiceCollectionExtensions_RegistersContractExecutorAsSingleton()
    {
        ServiceCollection services = new();
        services.AddArchLinterNetCore();

        using ServiceProvider provider = services.BuildServiceProvider();

        IArchitectureContractExecutor? first = provider.GetService<IArchitectureContractExecutor>();
        IArchitectureContractExecutor? second = provider.GetService<IArchitectureContractExecutor>();

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void ConfigureServices_ReplacesValidationApplicationService()
    {
        FakeValidationApplicationService fake = new();

        ArchitectureEngine engine = new ArchitectureEngineBuilder()
            .AddArchLinterNetCore()
            .ConfigureServices(services =>
                services.AddSingleton<IArchitectureValidationApplicationService>(fake))
            .Build();

        ValidationOutcome outcome = engine.Validate(new ValidationRequest
        {
            PolicyPath = _policyPath,
            Mode = "strict",
        });

        Assert.That(fake.WasCalled, Is.True);
        Assert.That(outcome, Is.SameAs(fake.Outcome));
    }

    [Test]
    public void Dispose_DisposesProviderOwnedServices()
    {
        DisposableValidationApplicationService? created = null;

        ArchitectureEngine engine = new ArchitectureEngineBuilder()
            .AddArchLinterNetCore()
            .ConfigureServices(services =>
                services.AddSingleton<IArchitectureValidationApplicationService>(
                    _ => created = new DisposableValidationApplicationService()))
            .Build();

        // Resolution happens lazily; force the container to construct the singleton it owns.
        engine.Validate(new ValidationRequest { PolicyPath = _policyPath, Mode = "strict" });

        engine.Dispose();

        Assert.That(created, Is.Not.Null);
        Assert.That(created!.WasDisposed, Is.True);
    }

    private sealed class DisposableValidationApplicationService : IArchitectureValidationApplicationService, IDisposable
    {
        public bool WasDisposed { get; private set; }

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
        {
            return new ValidationOutcome(
                Passed: true,
                Violations: Array.Empty<Model.ArchitectureViolation>(),
                Cycles: Array.Empty<string>(),
                CoverageFindings: Array.Empty<Model.ArchitectureViolation>(),
                CoverageConfig: "off",
                UnmatchedIgnoredViolations: Array.Empty<Model.ArchitectureUnmatchedIgnoredViolation>(),
                UnmatchedIgnoredViolationsConfig: "off",
                PolicyConsistencyFindings: Array.Empty<Model.PolicyConsistencyDiagnostic>(),
                PolicyConsistencyConfig: "off",
                CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>());
        }

        public void Dispose()
        {
            WasDisposed = true;
        }
    }

    private sealed class FakeValidationApplicationService : IArchitectureValidationApplicationService
    {
        public bool WasCalled { get; private set; }

        public ValidationOutcome Outcome { get; } = new(
            Passed: true,
            Violations: Array.Empty<Model.ArchitectureViolation>(),
            Cycles: Array.Empty<string>(),
            CoverageFindings: Array.Empty<Model.ArchitectureViolation>(),
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: Array.Empty<Model.ArchitectureUnmatchedIgnoredViolation>(),
            UnmatchedIgnoredViolationsConfig: "off",
            PolicyConsistencyFindings: Array.Empty<Model.PolicyConsistencyDiagnostic>(),
            PolicyConsistencyConfig: "off",
            CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>());

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
        {
            WasCalled = true;
            return Outcome;
        }
    }
}
