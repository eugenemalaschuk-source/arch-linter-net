using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AsmdefValidationEngineTests
{
    private string _tempDir = null!;
    private string _policyPath = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-asmdef-engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(_policyPath, """
            version: 1
            name: Engine Asmdef Test
            contracts:
              strict_asmdef: []
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
    public void Build_WithDefaultRegistrations_ValidatesAsmdef()
    {
        ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        AsmdefValidationOutcome outcome = engine.ValidateAsmdef(new AsmdefValidationRequest
        {
            PolicyPath = _policyPath,
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.Violations, Is.Empty);
    }

    [Test]
    public void ConfigureServices_ReplacesAsmdefValidationService()
    {
        FakeAsmdefValidationService fake = new();

        ArchitectureEngine engine = new ArchitectureEngineBuilder()
            .AddArchLinterNetCore()
            .ConfigureServices(services => services.AddSingleton<IAsmdefValidationService>(fake))
            .Build();

        AsmdefValidationOutcome outcome = engine.ValidateAsmdef(new AsmdefValidationRequest
        {
            PolicyPath = _policyPath,
        });

        Assert.That(fake.WasCalled, Is.True);
        Assert.That(outcome, Is.SameAs(fake.Outcome));
    }

    private sealed class FakeAsmdefValidationService : IAsmdefValidationService
    {
        public bool WasCalled { get; private set; }

        public AsmdefValidationOutcome Outcome { get; } = new(
            Passed: true,
            Violations: Array.Empty<Model.ArchitectureViolation>());

        public AsmdefValidationOutcome Validate(AsmdefValidationRequest request)
        {
            WasCalled = true;
            return Outcome;
        }
    }
}
