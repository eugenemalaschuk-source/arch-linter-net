using System.Text.Json;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class MetricBudgetContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-metric-budget-{Guid.NewGuid():N}");
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
    public void Load_ValidStrictAndAuditBudgets_BindsBoundsAndCatalogsBothModes()
    {
        string path = WriteFile("dependencies.arch.yml", Policy("""
          strict_metric_budgets:
            - id: max-types
              metric: type-count
              maximum: 10
            - id: bounded-types
              metric: type-count
              minimum: 1
              maximum: 20
          audit_metric_budgets:
            - id: min-types
              metric: type-count
              minimum: 0
        """));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictMetricBudgets, Has.Count.EqualTo(2));
            Assert.That(document.Contracts.StrictMetricBudgets[0].Id, Is.EqualTo("max-types"));
            Assert.That(document.Contracts.StrictMetricBudgets[0].Metric, Is.EqualTo("type-count"));
            Assert.That(document.Contracts.StrictMetricBudgets[0].Minimum, Is.Null);
            Assert.That(document.Contracts.StrictMetricBudgets[0].Maximum, Is.EqualTo(10));
            Assert.That(document.Contracts.StrictMetricBudgets[1].Minimum, Is.EqualTo(1));
            Assert.That(document.Contracts.StrictMetricBudgets[1].Maximum, Is.EqualTo(20));
            Assert.That(document.Contracts.AuditMetricBudgets.Single().Id, Is.EqualTo("min-types"));
            Assert.That(document.Contracts.AllStrict.Select(contract => contract.Id),
                Does.Contain("max-types"));
            Assert.That(document.Contracts.AllAudit.Select(contract => contract.Id),
                Does.Contain("min-types"));
        });
    }

    [Test]
    public void ExportPolicyContext_MetricBudget_ProjectsTypedMetricAndBoundFacts()
    {
        string path = WriteFile("policy-context.arch.yml", Policy("""
          strict_metric_budgets:
            - id: bounded-types
              metric: type-count
              minimum: 1
              maximum: 10
        """));
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ArchitecturePolicyContextExport context = engine.ExportPolicyContext(new ArchitecturePolicyContextRequest { PolicyPath = path });
        ArchitecturePolicyContextContract contract = context.Contracts.Single(item => item.Id == "bounded-types");
        string json = ArchitecturePolicyContextFormatter.FormatAsJson(context);
        string markdown = ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);

        Assert.Multiple(() =>
        {
            Assert.That(contract.Family, Is.EqualTo("metric_budgets"));
            Assert.That(contract.Facts.Single(fact => fact.Name == "metric").Values, Is.EqualTo(["type-count"]));
            Assert.That(contract.Facts.Single(fact => fact.Name == "minimum").Values, Is.EqualTo(["1"]));
            Assert.That(contract.Facts.Single(fact => fact.Name == "maximum").Values, Is.EqualTo(["10"]));
            Assert.That(json, Does.Contain("\"metric\"").And.Contain("\"maximum\""));
            Assert.That(markdown, Does.Contain("metric: `type-count`").And.Contain("minimum: `1`").And.Contain("maximum: `10`"));
        });
    }

    [Test]
    public void ExportPolicyContext_RelativeMetricBudget_ProjectsBaselineFacts()
    {
        string path = WriteFile("relative-policy-context.arch.yml", Policy("""
          strict_metric_budgets:
            - id: ratchet-types
              metric: type-count
              baseline_mode: max_delta
              max_delta: 2
              maximum: 10
        """));
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ArchitecturePolicyContextContract contract = engine.ExportPolicyContext(
            new ArchitecturePolicyContextRequest { PolicyPath = path })
            .Contracts.Single(item => item.Id == "ratchet-types");

        Assert.Multiple(() =>
        {
            Assert.That(contract.Facts.Single(fact => fact.Name == "baseline_mode").Values, Is.EqualTo(["max_delta"]));
            Assert.That(contract.Facts.Single(fact => fact.Name == "max_delta").Values, Is.EqualTo(["2"]));
            Assert.That(contract.Facts.Single(fact => fact.Name == "maximum").Values, Is.EqualTo(["10"]));
        });
    }

    [TestCase("id: no-bound\n      metric: type-count", "at least one")]
    [TestCase("id: negative-minimum\n      metric: type-count\n      minimum: -1", "minimum must be non-negative")]
    [TestCase("id: negative-maximum\n      metric: type-count\n      maximum: -1", "maximum must be non-negative")]
    [TestCase("id: inverted\n      metric: type-count\n      minimum: 4\n      maximum: 3", "minimum must be less than or equal to maximum")]
    [TestCase("id: unknown-metric\n      metric: missing\n      maximum: 3", "references unknown metric")]
    public void Load_InvalidBoundsOrReference_RejectsBudget(string budget, string expectedMessage)
    {
        string path = WriteFile("dependencies.arch.yml", Policy(BudgetBlock(budget)));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [Test]
    public void Load_BlankAndDuplicateBudgetIds_RejectsBothGlobalInvariants()
    {
        string blankPath = WriteFile("blank-id.arch.yml", Policy("""
          strict_metric_budgets:
            - id: " "
              metric: type-count
              maximum: 3
        """));
        string duplicatePath = WriteFile("duplicate-id.arch.yml", Policy("""
          strict_metric_budgets:
            - id: shared-id
              metric: type-count
              maximum: 3
          audit_metric_budgets:
            - id: SHARED-ID
              metric: type-count
              minimum: 0
        """));

        Assert.Multiple(() =>
        {
            Assert.That(
                Assert.Throws<InvalidOperationException>(
                    () => new ArchitecturePolicyDocumentLoader().Load(blankPath))!.Message,
                Does.Contain("non-empty id"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(
                    () => new ArchitecturePolicyDocumentLoader().Load(duplicatePath))!.Message,
                Does.Contain("Duplicate metric budget id"));
        });
    }

    [Test]
    public void Load_ImportedMetricAndBudget_ComposesAndValidatesReference()
    {
        WriteFile("metric-budget.fragment.yml", """
            metrics:
              - id: imported-type-count
                kind: topology_type_count
                topology_node: all-types
            contracts:
              strict_metric_budgets:
                - id: imported-budget
                  metric: imported-type-count
                  maximum: 5
            """);
        string rootPath = WriteFile("dependencies.arch.yml", """
            version: 1
            name: Imported metric budget policy
            imports:
              - metric-budget.fragment.yml
            layers: {}
            analysis:
              target_assemblies: []
            topology:
              subject_kind: type
              scope:
                selectors:
                  - namespace: Test
              nodes:
                - id: all-types
                  mappings:
                    - namespace: Test
            contracts: {}
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(rootPath);

        Assert.Multiple(() =>
        {
            Assert.That(document.Metrics.Select(metric => metric.Id), Is.EqualTo(["imported-type-count"]));
            Assert.That(document.Contracts.StrictMetricBudgets.Single().Id, Is.EqualTo("imported-budget"));
            Assert.That(document.Contracts.StrictMetricBudgets.Single().Metric, Is.EqualTo("imported-type-count"));
        });
    }

    [Test]
    public void Load_PolicyWithoutBudgets_RemainsCompatible()
    {
        string path = WriteFile("dependencies.arch.yml", """
            version: 1
            name: No metric budgets
            layers: {}
            analysis:
              target_assemblies: []
            contracts: {}
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(document.Metrics, Is.Empty);
            Assert.That(document.Contracts.StrictMetricBudgets, Is.Empty);
            Assert.That(document.Contracts.AuditMetricBudgets, Is.Empty);
            Assert.That(document.Contracts.AllStrict, Is.Empty);
            Assert.That(document.Contracts.AllAudit, Is.Empty);
        });
    }

    [Test]
    public void Validate_BreachedStrictAndAuditBudget_UsesSharedMetricEvidenceInEveryFindingEnvelope()
    {
        string path = WriteFile("dependencies.arch.yml", """
            version: 1
            name: Metric budget evaluation
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
              audit_metric_budgets:
                - id: audit-model-type-limit
                  metric: model-types
                  maximum: 0
            """);
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ValidationOutcome strict = engine.Validate(new ValidationRequest { PolicyPath = path, Mode = "strict" });
        ValidationOutcome audit = engine.Validate(new ValidationRequest { PolicyPath = path, Mode = "audit" });
        BaselineGenerationOutcome baseline = engine.GenerateBaseline(new BaselineGenerationRequest
        {
            PolicyPath = path,
            Mode = "strict",
        });
        ArchitectureViolation violation = strict.Violations.Single();
        var payload = (MetricBudgetPayload)violation.Payload!;
        var formatter = new ArchitectureDiagnosticFormatter();
        using JsonDocument ci = JsonDocument.Parse(
            formatter.FormatResultForCiArtifacts("strict", false, strict.Violations, Array.Empty<string>()));
        using JsonDocument sarif = JsonDocument.Parse(
            new ArchitectureSarifFormatter().FormatResultAsSarif("strict", strict.Violations, Array.Empty<string>(), "test"));
        JsonElement jsonFinding = ci.RootElement.GetProperty("violations")[0];
        JsonElement sarifFinding = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("properties").GetProperty("arch_linter_net");
        ArchitectureApplicabilityRecord budgetRecord = strict.ApplicabilityRecords.Single(record =>
            record.Family == "metric_budgets");

        Assert.Multiple(() =>
        {
            Assert.That(strict.Passed, Is.False);
            Assert.That(audit.Passed, Is.False);
            Assert.That(baseline.Succeeded, Is.True);
            Assert.That(baseline.Yaml, Does.Contain("strict_metric_budgets"));
            Assert.That(audit.Violations.Single().ContractId, Is.EqualTo("audit-model-type-limit"));
            Assert.That(payload.BudgetId, Is.EqualTo("strict-model-type-limit"));
            Assert.That(payload.MeasuredValue, Is.GreaterThan(0));
            Assert.That(payload.BreachedBound, Is.EqualTo("maximum"));
            Assert.That(payload.ConfiguredLimit, Is.Zero);
            Assert.That(payload.Contributors, Is.EqualTo(payload.Contributors.OrderBy(value => value, StringComparer.Ordinal)));
            Assert.That(budgetRecord.ControlIdentity, Is.EqualTo("strict-model-type-limit"));
            Assert.That(budgetRecord.MetricEvidence!.Value, Is.EqualTo(payload.MeasuredValue));
            Assert.That(jsonFinding.GetProperty("kind").GetString(), Is.EqualTo("metric_budget"));
            Assert.That(jsonFinding.GetProperty("measured_value").GetInt32(), Is.EqualTo(payload.MeasuredValue));
            Assert.That(jsonFinding.GetProperty("details").GetProperty("configured_limit").GetInt32(), Is.Zero);
            Assert.That(sarifFinding.GetRawText(), Is.EqualTo(jsonFinding.GetRawText()));
            Assert.That(formatter.FormatViolationsForHumans(strict.Violations), Does.Contain("measured_value"));
        });
    }

    [Test]
    public void Validate_RelativeBudget_UsesReviewedMetricValueAndProjectsComparisonEvidence()
    {
        string path = WriteFile("relative.arch.yml", """
            version: 1
            name: Relative metric budget evaluation
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
                - id: ratchet
                  metric: model-types
                  baseline_mode: max_delta
                  max_delta: 0
            """);
        string baselinePath = WriteFile("relative-baseline.arch.yml", """
            version: 3
            baseline: {}
            metric_baselines:
              - metric_identity_version: 1
                metric_id: model-types
                metric_kind: topology_type_count
                native_subject: model
                effective_scope: model
                value: 0
            """);
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ValidationOutcome outcome = engine.Validate(new ValidationRequest
        {
            PolicyPath = path,
            BaselinePath = baselinePath,
            Mode = "strict",
        });

        ArchitectureViolation violation = outcome.Violations.Single(item => item.ContractId == "ratchet");
        MetricBudgetPayload payload = (MetricBudgetPayload)violation.Payload!;
        var formatter = new ArchitectureDiagnosticFormatter();
        using JsonDocument ci = JsonDocument.Parse(
            formatter.FormatResultForCiArtifacts("strict", false, outcome.Violations, Array.Empty<string>()));
        using JsonDocument sarif = JsonDocument.Parse(
            new ArchitectureSarifFormatter().FormatResultAsSarif("strict", outcome.Violations, Array.Empty<string>(), "test"));
        JsonElement jsonFinding = ci.RootElement.GetProperty("violations")[0];
        JsonElement details = jsonFinding.GetProperty("details");
        JsonElement sarifFinding = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("properties").GetProperty("arch_linter_net");

        Assert.Multiple(() =>
        {
            Assert.That(payload.BaselineMode, Is.EqualTo("max_delta"));
            Assert.That(payload.BaselineValue, Is.Zero);
            Assert.That(payload.Delta, Is.EqualTo(payload.MeasuredValue));
            Assert.That(payload.AllowedDelta, Is.Zero);
            Assert.That(payload.EffectiveThreshold, Is.Zero);
            Assert.That(payload.AbsoluteCap, Is.Null);
            Assert.That(details.GetProperty("baseline_mode").GetString(), Is.EqualTo("max_delta"));
            Assert.That(details.GetProperty("baseline_value").GetInt32(), Is.Zero);
            Assert.That(details.GetProperty("delta").GetInt32(), Is.EqualTo(payload.MeasuredValue));
            Assert.That(details.GetProperty("allowed_delta").GetInt32(), Is.Zero);
            Assert.That(details.GetProperty("effective_threshold").GetInt64(), Is.Zero);
            Assert.That(details.GetProperty("absolute_cap").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(sarifFinding.GetRawText(), Is.EqualTo(jsonFinding.GetRawText()));
        });
    }

    [Test]
    public void Validate_RelativeBudget_MissingOrStaleMetricBaselineFailsClosed()
    {
        string path = WriteFile("relative-missing.arch.yml", ExecutableMetricPolicy("""
                - id: ratchet
                  metric: model-types
                  baseline_mode: no_worse_than_baseline
            """));
        string missingBaselinePath = WriteFile("relative-missing-baseline.arch.yml", """
            version: 3
            baseline: {}
            metric_baselines: []
            """);
        string staleBaselinePath = WriteFile("relative-stale-baseline.arch.yml", """
            version: 3
            baseline: {}
            metric_baselines:
              - metric_identity_version: 1
                metric_id: model-types
                metric_kind: incoming_component_count
                native_subject: model
                effective_scope: model
                value: 0
            """);
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ValidationOutcome missing = engine.Validate(new ValidationRequest
        {
            PolicyPath = path,
            BaselinePath = missingBaselinePath,
            Mode = "strict",
        });
        ValidationOutcome stale = engine.Validate(new ValidationRequest
        {
            PolicyPath = path,
            BaselinePath = staleBaselinePath,
            Mode = "strict",
        });

        ArchitectureApplicabilityRecord missingRecord = missing.ApplicabilityRecords.Single(record =>
            record.Family == "metric_budgets" && record.ControlIdentity == "ratchet");
        ArchitectureApplicabilityRecord staleRecord = stale.ApplicabilityRecords.Single(record =>
            record.Family == "metric_budgets" && record.ControlIdentity == "ratchet");

        Assert.Multiple(() =>
        {
            Assert.That(missing.Passed, Is.False);
            Assert.That(missingRecord.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(missingRecord.Reasons.Select(reason => reason.Code),
                Is.EqualTo([ArchitectureApplicabilityReasonCodes.MissingMetricBaseline]));
            Assert.That(stale.Passed, Is.False);
            Assert.That(staleRecord.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(staleRecord.Reasons.Select(reason => reason.Code),
                Is.EqualTo([ArchitectureApplicabilityReasonCodes.StaleMetricBaseline]));
        });
    }

    [Test]
    public void Validate_RelativeBudget_RespectsNoWorseThanBaselineAndAbsoluteCap()
    {
        string noWorsePath = WriteFile("relative-pass.arch.yml", ExecutableMetricPolicy("""
                - id: ratchet
                  metric: model-types
                  baseline_mode: no_worse_than_baseline
            """));
        string cappedPath = WriteFile("relative-cap.arch.yml", ExecutableMetricPolicy("""
                - id: ratchet
                  metric: model-types
                  baseline_mode: max_delta
                  max_delta: 100000
                  maximum: 0
            """));
        string baselinePath = WriteFile("relative-cap-baseline.arch.yml", """
            version: 3
            baseline: {}
            metric_baselines:
              - metric_identity_version: 1
                metric_id: model-types
                metric_kind: topology_type_count
                native_subject: model
                effective_scope: model
                value: 100000
            """);
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ValidationOutcome noWorse = engine.Validate(new ValidationRequest
        {
            PolicyPath = noWorsePath,
            BaselinePath = baselinePath,
            Mode = "strict",
        });
        ValidationOutcome capped = engine.Validate(new ValidationRequest
        {
            PolicyPath = cappedPath,
            BaselinePath = baselinePath,
            Mode = "strict",
        });
        MetricBudgetPayload payload = (MetricBudgetPayload)capped.Violations.Single(item => item.ContractId == "ratchet").Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(noWorse.Passed, Is.True);
            Assert.That(capped.Passed, Is.False);
            Assert.That(payload.BreachedBound, Is.EqualTo("maximum"));
            Assert.That(payload.AbsoluteCap, Is.Zero);
            Assert.That(payload.EffectiveThreshold, Is.Zero);
            Assert.That(payload.AllowedDelta, Is.EqualTo(100000));
        });
    }

    [Test]
    public void GenerateBaseline_RelativeMetricBudget_EmitsVersion3WithCurrentScalarValue()
    {
        string path = WriteFile("relative-generate.arch.yml", ExecutableMetricPolicy("""
                - id: ratchet
                  metric: model-types
                  baseline_mode: max_delta
                  max_delta: 1
            """));
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        BaselineGenerationOutcome outcome = engine.GenerateBaseline(new BaselineGenerationRequest
        {
            PolicyPath = path,
            Mode = "strict",
        });

        string yaml = outcome.Yaml
            ?? throw new AssertionException("A successful relative baseline generation must produce YAML.");
        string generatedPath = WriteFile("relative-generated-baseline.arch.yml", yaml);
        ArchitectureBaselineDocument generated = new ArchitectureBaselineLoadingService().Load(generatedPath);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(generated.Version, Is.EqualTo(3));
            Assert.That(generated.MetricBaselines, Has.Count.EqualTo(1));
            Assert.That(generated.MetricBaselines.Single().Identity, Is.EqualTo(
                new ArchitectureMetricBaselineIdentity(
                    ArchitectureMetricBaselineIdentity.CurrentVersion,
                    "model-types",
                    "topology_type_count",
                    "model",
                    null,
                    "model")));
            Assert.That(generated.MetricBaselines.Single().Value, Is.GreaterThan(0));
        });
    }

    private static string ExecutableMetricPolicy(string strictBudgets) => $$"""
        version: 1
        name: Relative metric budget policy
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
        {{strictBudgets}}
        """;

    private string WriteFile(string fileName, string contents)
    {
        string path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    private static string Policy(string contractBlock) => $$"""
        version: 1
        name: Metric budget policy
        layers: {}
        analysis:
          target_assemblies: []
        topology:
          subject_kind: type
          scope:
            selectors:
              - namespace: Test
          nodes:
            - id: all-types
              mappings:
                - namespace: Test
        metrics:
          - id: type-count
            kind: topology_type_count
            topology_node: all-types
        contracts:
        {{contractBlock}}
        """;

    private static string BudgetBlock(string budget)
    {
        string normalized = string.Join(
            "\n",
            budget.Split('\n').Select(line => line.TrimStart()));
        return "  strict_metric_budgets:\n    - " + normalized.Replace("\n", "\n      ", StringComparison.Ordinal);
    }
}
