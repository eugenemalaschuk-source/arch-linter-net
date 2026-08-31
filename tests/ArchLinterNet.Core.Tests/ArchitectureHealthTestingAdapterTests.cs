using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureHealthTestingAdapterTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-health-testing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public void EvaluateHealth_ProjectsCoreHealthOutcomeForConfiguredBaseline()
    {
        string policyPath = WritePolicy();
        string baselinePath = WriteBaseline();

        ArchitectureHealthOutcome fromBuilder = new ArchitectureValidationBuilder(policyPath)
            .WithBaseline(baselinePath)
            .EvaluateHealth();

        using ArchitectureEngine engine = new ArchitectureEngineBuilder()
            .AddArchLinterNetCore()
            .Build();
        ArchitectureHealthOutcome fromEngine = engine.EvaluateHealth(new ArchitectureHealthRequest
        {
            DebtGate = new ArchitectureDebtGateRequest
            {
                PolicyPath = policyPath,
                BaselinePath = baselinePath,
                Mode = "all",
            },
        });

        Assert.Multiple(() =>
        {
            Assert.That(fromBuilder.Summary.SchemaId, Is.EqualTo(ArchitectureHealthSummary.CurrentSchemaId));
            Assert.That(fromBuilder.Gate, Is.EqualTo(fromEngine.Gate));
            Assert.That(fromBuilder.Health, Is.EqualTo(fromEngine.Health));
            Assert.That(fromBuilder.Summary.Gate, Is.EqualTo(fromEngine.Summary.Gate));
            Assert.That(fromBuilder.Summary.Health, Is.EqualTo(fromEngine.Summary.Health));
            Assert.That(fromBuilder.ValidationOutcomes.Select(outcome => outcome.Mode),
                Is.EqualTo(new[] { "strict", "audit" }));
            Assert.That(fromEngine.ValidationOutcomes.Select(outcome => outcome.Mode),
                Is.EqualTo(new[] { "strict", "audit" }));
        });
    }

    [Test]
    public void EvaluateHealth_WithoutBaseline_ThrowsExistingClearPrecondition()
    {
        string policyPath = WritePolicy();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ArchitectureValidationBuilder(policyPath).EvaluateHealth("strict"))!;

        Assert.That(
            exception.Message,
            Is.EqualTo("A baseline path is required. Call WithBaseline(path) before requesting a baseline comparison."));
    }

    private string WritePolicy()
    {
        string architectureDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(architectureDir);
        string policyPath = Path.Combine(architectureDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Architecture health testing adapter

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution

            analysis:
              target_assemblies: [ArchLinterNet.Core]
            """);
        return policyPath;
    }

    private string WriteBaseline()
    {
        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");
        return baselinePath;
    }
}
