using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureHealthTestingAdapterTests
{
    private static readonly string[] _expectedValidationModes = ["strict", "audit"];

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
                Is.EqualTo(_expectedValidationModes));
            Assert.That(fromEngine.ValidationOutcomes.Select(outcome => outcome.Mode),
                Is.EqualTo(_expectedValidationModes));
            Assert.That(fromBuilder.DebtGate.Evaluation.ReusedAnalysisSnapshot, Is.True);
            Assert.That(fromEngine.DebtGate.Evaluation.ReusedAnalysisSnapshot, Is.True);
            Assert.That(fromBuilder.AnalysisCounters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(fromBuilder.AnalysisCounters.ProjectGraphEvaluations, Is.EqualTo(1));
            Assert.That(fromBuilder.AnalysisCounters.SnapshotMaterializations, Is.EqualTo(1));
            Assert.That(fromBuilder.AnalysisCounters.SelectedAssemblyCount, Is.EqualTo(1));
            Assert.That(fromBuilder.AnalysisCounters.AssemblyLoads, Is.LessThanOrEqualTo(1));
            Assert.That(fromEngine.AnalysisCounters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(fromEngine.AnalysisCounters.ProjectGraphEvaluations, Is.EqualTo(1));
            Assert.That(fromEngine.AnalysisCounters.SnapshotMaterializations, Is.EqualTo(1));
            Assert.That(fromEngine.AnalysisCounters.SelectedAssemblyCount, Is.EqualTo(1));
            Assert.That(fromEngine.AnalysisCounters.AssemblyLoads, Is.LessThanOrEqualTo(1));
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

    [Test]
    public void SharedSnapshot_HealthAndChangeProjection_AreEquivalentToIndependentProjection()
    {
        string policyPath = WritePolicy();
        string baselinePath = WriteBaseline();
        using ArchitectureEngine engine = new ArchitectureEngineBuilder()
            .AddArchLinterNetCore()
            .Build();
        using ArchitectureAnalysisSnapshot snapshot = engine.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = policyPath,
            BaselinePath = baselinePath,
            PreparationMode = BuildPreparationMode.Ordinary,
        });

        ArchitectureHealthRequest healthRequest = new()
        {
            DebtGate = new ArchitectureDebtGateRequest
            {
                PolicyPath = policyPath,
                BaselinePath = baselinePath,
                Mode = "all",
            },
        };
        ArchitectureHealthOutcome health = engine.EvaluateHealth(healthRequest, snapshot);
        ValidationOutcome validation = health.ValidationOutcomes
            .Single(outcome => outcome.Mode == "strict")
            .Outcome;
        BaselineVerifyOutcome reusedBaseline = engine.VerifyBaseline(new BaselineVerifyRequest
        {
            PolicyPath = policyPath,
            BaselinePath = baselinePath,
            Mode = "strict",
        }, snapshot);
        ArchitectureChangeSnapshot shared = engine.CreateChangeSnapshot(
            snapshot,
            "strict",
            validation,
            reusedBaseline,
            conditionSetName: null);

        ArchitectureGraphOutcome legacyNamespaces = engine.BuildGraph(new ArchitectureGraphRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
            Level = ArchitectureGraphLevel.Namespace,
        });
        ArchitectureGraphOutcome legacyAssemblies = engine.BuildGraph(new ArchitectureGraphRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
            Level = ArchitectureGraphLevel.Assembly,
        });
        BaselineDiffOutcome legacyBaseline = engine.DiffBaseline(new BaselineDiffRequest
        {
            PolicyPath = policyPath,
            BaselinePath = baselinePath,
            Mode = "strict",
        });
        ArchitectureChangeSnapshot independent = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            validation,
            legacyNamespaces,
            legacyAssemblies,
            legacyBaseline.Frozen);

        Assert.Multiple(() =>
        {
            Assert.That(health.ValidationOutcomes.Select(outcome => outcome.Mode), Is.EqualTo(_expectedValidationModes));
            Assert.That(health.DebtGate.Evaluation.ReusedAnalysisSnapshot, Is.True);
            Assert.That(ArchitectureChangeReports.SerializeSnapshot(shared),
                Is.EqualTo(ArchitectureChangeReports.SerializeSnapshot(independent)));
            Assert.That(snapshot.Counters.SnapshotMaterializations, Is.EqualTo(1));
            Assert.That(snapshot.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
        });
    }

    [Test]
    public void SharedSnapshot_HealthProfile_UsesOneSnapshotAndMeasuredHealthPhases()
    {
        string policyPath = WritePolicy();
        string baselinePath = WriteBaseline();
        using ArchitectureEngine engine = new ArchitectureEngineBuilder()
            .AddArchLinterNetCore()
            .Build();
        ValidationTiming timing = new();
        ArchitectureHealthOutcome health;
        using (timing.Measure("total"))
        using (ArchitectureAnalysisSnapshot snapshot = engine.CreateSnapshot(
                   new AnalysisSnapshotRequest
                   {
                       PolicyPath = policyPath,
                       BaselinePath = baselinePath,
                       PreparationMode = BuildPreparationMode.Ordinary,
                   },
                   timing))
        {
            health = engine.EvaluateHealth(new ArchitectureHealthRequest
            {
                DebtGate = new ArchitectureDebtGateRequest
                {
                    PolicyPath = policyPath,
                    BaselinePath = baselinePath,
                    Mode = "all",
                },
            }, snapshot, timing);
        }

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            health.AnalysisCounters,
            timing,
            renderedSinkCount: 1,
            outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success,
            cancellationObserved: false,
            new AnalysisProfileBuildOptions
            {
                Measurements = new AnalysisProfileMeasurements
                {
                    AllocatedBytesTotal = 0,
                    PeakWorkingSetBytes = 1,
                },
            });
        string[] phaseNames = profile.Phases.Select(phase => phase.Name).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(phaseNames, Does.Contain("total"));
            Assert.That(phaseNames, Does.Contain("health_validation_evaluation"));
            Assert.That(phaseNames, Does.Contain("health_debt_gate"));
            Assert.That(phaseNames, Does.Contain("health_projection"));
            Assert.That(profile.Counters.ModesEvaluated, Is.EqualTo(2));
            Assert.That(profile.Counters.SnapshotMaterializations, Is.EqualTo(1));
            Assert.That(profile.Measurements, Is.Not.Null);
        });
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
