using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Split out of MetricBudgetContractTests to keep that file under the repository's 800-line
// decomposition limit. Covers absolute-minimum breach and baseline-driven ignore suppression for
// ArchitectureMetricBudgetAnalysisService's EvaluateAbsoluteBudget/IsIgnored boundaries.
[TestFixture]
public sealed class MetricBudgetContractEvaluationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-metric-budget-eval-{Guid.NewGuid():N}");
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
    public void Validate_BreachedMinimumBudget_ReportsMinimumBoundInPayload()
    {
        string path = WriteFile("dependencies.arch.yml", """
            version: 1
            name: Metric budget minimum evaluation
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors:
                  - namespace: ArchLinterNet.Core.Model
              nodes:
                - id: model
                  mappings:
                    - namespace: ArchLinterNet.Core.Model
            metrics:
              - id: model-types
                kind: topology_type_count
                topology_node: model
            contracts:
              strict_metric_budgets:
                - id: strict-model-type-floor
                  metric: model-types
                  minimum: 1000000
            """);
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ValidationOutcome strict = engine.Validate(new ValidationRequest { PolicyPath = path, Mode = "strict" });
        var payload = (MetricBudgetPayload)strict.Violations.Single().Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(strict.Passed, Is.False);
            Assert.That(payload.BreachedBound, Is.EqualTo("minimum"));
            Assert.That(payload.ConfiguredLimit, Is.EqualTo(1000000));
            Assert.That(payload.MeasuredValue, Is.LessThan(1000000));
        });
    }

    [Test]
    public void Validate_BreachedBudgetMatchingBaselineIgnoredViolation_SuppressesFindingWithoutLeavingItUnmatched()
    {
        // Metric budgets are a closed contract shape (RawMetricBudgetValidator rejects an inline
        // `ignored_violations` key), so budget-level ignores can only reach
        // ArchitectureContractExecutionContext.IsIgnored through a loaded baseline document, unlike
        // dependency-family contracts which also accept it authored directly in the policy.
        string path = WriteFile("dependencies.arch.yml", """
            version: 1
            name: Metric budget ignore evaluation
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors:
                  - namespace: ArchLinterNet.Core.Model
              nodes:
                - id: model
                  mappings:
                    - namespace: ArchLinterNet.Core.Model
            metrics:
              - id: model-types
                kind: topology_type_count
                topology_node: model
            contracts:
              strict_metric_budgets:
                - id: strict-model-type-limit
                  metric: model-types
                  maximum: 0
            """);
        string baselinePath = WriteFile("dependencies.baseline.yml", """
            version: 1
            baseline:
              strict_metric_budgets:
                - id: strict-model-type-limit
                  ignored_violations:
                    - source_type: "*"
                      forbidden_reference: "*"
                      reason: exercised by a regression test
            """);
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ValidationOutcome strict = engine.Validate(new ValidationRequest
        {
            PolicyPath = path,
            BaselinePath = baselinePath,
            Mode = "strict",
        });

        Assert.Multiple(() =>
        {
            Assert.That(strict.Passed, Is.True);
            Assert.That(strict.Violations, Is.Empty);
            Assert.That(strict.UnmatchedIgnoredViolations, Is.Empty);
        });
    }

    private string WriteFile(string fileName, string contents)
    {
        string path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, contents);
        return path;
    }
}
