using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Composes JSON and SARIF documents from immutable validation outcomes. Sink lifecycle and
// publication remain exclusively owned by ReportCoordinator.
internal sealed class StructuredReportRenderer
{
    private const string FormatJson = "json";
    private const string FormatSarif = "sarif";
    private const string ImportedRuleIdPrefix = "external-evidence:";

    private readonly ICliRuntime _runtime;
    private readonly ReportApplicabilityRenderer _applicability = new();

    public StructuredReportRenderer(ICliRuntime runtime)
    {
        _runtime = runtime;
    }

    internal string Render(
        string format,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken = default)
    {
        return format switch
        {
            FormatJson => isSingleMode
                ? FormatSingleJson(outcomesByMode[0].Mode, outcomesByMode[0].Outcome, cancellationToken)
                : FormatCombinedJson(outcomesByMode, cancellationToken),
            FormatSarif => isSingleMode
                ? FormatSingleSarif(outcomesByMode[0].Mode, outcomesByMode[0].Outcome, cancellationToken)
                : FormatCombinedSarif(outcomesByMode, cancellationToken),
            _ => throw new ArgumentException($"Unsupported structured report format: {format}", nameof(format)),
        };
    }

    private string FormatSingleJson(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default) =>
        FormatJsonContent(mode, outcome, cancellationToken);

    private string FormatCombinedJson(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken = default)
    {
        JsonArray results = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(JsonNode.Parse(FormatJsonContent(mode, outcome, cancellationToken)));
        }

        return new JsonObject { ["results"] = results }.ToJsonString();
    }

    private string FormatSingleSarif(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default) =>
        FormatSarifContent(mode, outcome, cancellationToken);

    private string FormatCombinedSarif(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken = default)
    {
        JsonArray runs = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonNode? document = JsonNode.Parse(FormatSarifContent(mode, outcome, cancellationToken));
            foreach (JsonNode? run in document?["runs"]?.AsArray() ?? new JsonArray())
            {
                runs.Add(run?.DeepClone());
            }
        }

        return new JsonObject { ["version"] = "2.1.0", ["runs"] = runs }.ToJsonString();
    }

    // cancellationToken defaults to None so RenderReportContent (which must always complete a
    // render regardless of the real cancellation state — see its own comment) keeps working
    // unchanged; every other caller passes the live token through, checked per violation inside
    // the widest FormatResultForCiArtifacts overload — the dominant contributor to a large
    // report's size, not just before/after this call.
    private string FormatJsonContent(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        string result = _runtime.FormatResultForCiArtifacts(
            mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.CoverageFindings,
            outcome.UnmatchedIgnoredViolations,
            outcome.PolicyConsistencyConfig == "off" ? Array.Empty<PolicyConsistencyDiagnostic>() : outcome.PolicyConsistencyFindings,
            outcome.CoverageSummaries, outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures,
            outcome.ClassificationRoles, outcome.ClassificationPathDeferred, outcome.PreflightDiagnostics,
            outcome.SourceExpansion, outcome.SubtractiveMatcherParticipation, cancellationToken);

        result = outcome.Waivers.Count == 0
            ? result
            : ArchitectureDiagnosticFormatter.AddWaiversToCiArtifacts(result, outcome.Waivers);
        result = ArchitectureDiagnosticFormatter.AddPolicyInventoryToCiArtifacts(result, outcome.PolicyInventory);
        result = AddImportedDiagnosticsToJson(result, outcome.ImportedDiagnosticFindings);

        return _applicability.AddAssessmentCompletionToJson(
            result, outcome.AssessmentCompletionEvidence, outcome.ApplicabilityProjection);
    }

    // Additive side-channel, mirroring how applicability_findings is already added to the JSON
    // payload above rather than merged into the native "violations" array — imported diagnostics
    // are ArchitectureFinding-normalized (like applicability), not ArchitectureViolation-shaped.
    private static string AddImportedDiagnosticsToJson(string json, IReadOnlyList<ArchitectureFinding> findings)
    {
        if (findings.Count == 0)
        {
            return json;
        }

        JsonNode document = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("The validation JSON report was empty.");
        if (document is not JsonObject payload)
        {
            throw new InvalidOperationException("The validation JSON report was not an object.");
        }

        JsonArray result = new();
        foreach (ArchitectureFinding finding in findings)
        {
            result.Add(JsonSerializer.SerializeToNode(
                ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding)));
        }

        payload["imported_diagnostics"] = result;
        return payload.ToJsonString();
    }

    private string FormatSarifContent(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        string result = _runtime.FormatResultAsSarif(
            mode, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.PreflightDiagnostics,
            outcome.CoverageSummaries, outcome.SourceExpansion, outcome.SubtractiveMatcherParticipation, cancellationToken);
        result = AddImportedDiagnosticsToSarif(result, outcome.ImportedDiagnosticFindings, cancellationToken);

        return _applicability.AddAssessmentCompletionToSarif(
            result, outcome.AssessmentCompletionEvidence, outcome.ApplicabilityProjection);
    }

    // Reuses the Core SARIF formatter (the same one ArchitectureExternalEvidenceBinder's caller
    // chain already produces trusted findings through) to build the imported-diagnostics results
    // and rules, then merges them into the existing run the same way AddApplicabilityFindingsToSarifRun
    // merges applicability results — never hand-building an imported diagnostic's SARIF shape here.
    private string AddImportedDiagnosticsToSarif(
        string json, IReadOnlyList<ArchitectureFinding> findings, CancellationToken cancellationToken)
    {
        if (findings.Count == 0)
        {
            return json;
        }

        JsonNode document = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("The validation SARIF report was empty.");
        if (document is not JsonObject payload)
        {
            throw new InvalidOperationException("The validation SARIF report was not an object.");
        }

        JsonArray runs = payload["runs"] as JsonArray ?? new JsonArray();
        if (payload["runs"] is null)
        {
            payload["runs"] = runs;
        }

        if (runs.Count == 0)
        {
            runs.Add(new JsonObject
            {
                ["tool"] = new JsonObject
                {
                    ["driver"] = new JsonObject { ["name"] = "arch-linter-net", ["rules"] = new JsonArray() },
                },
                ["results"] = new JsonArray(),
            });
        }

        string importedSarif = ArchitectureSarifFormatter.FormatFindingsAsSarif(
            findings, _runtime.Version, cancellationToken);
        JsonObject importedPayload = (JsonNode.Parse(importedSarif) as JsonObject)!;
        JsonObject importedRun = (importedPayload["runs"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault()
            ?? new JsonObject();
        JsonArray importedResults = importedRun["results"] as JsonArray ?? new JsonArray();
        JsonArray importedRules = ((importedRun["tool"] as JsonObject)?["driver"] as JsonObject)?["rules"]
            as JsonArray ?? new JsonArray();

        NamespaceImportedRuleIds(importedResults, importedRules);
        MergeImportedDiagnosticsIntoRun((JsonObject)runs[0]!, importedResults, importedRules);
        return payload.ToJsonString();
    }

    private static void NamespaceImportedRuleIds(JsonArray results, JsonArray rules)
    {
        foreach (JsonObject ruleObject in rules.OfType<JsonObject>())
        {
            string? id = ruleObject["id"]?.GetValue<string>();
            if (id is not null)
            {
                ruleObject["id"] = ImportedRuleIdPrefix + id;
            }
        }

        foreach (JsonObject resultObject in results.OfType<JsonObject>())
        {
            string? ruleId = resultObject["ruleId"]?.GetValue<string>();
            if (ruleId is not null)
            {
                resultObject["ruleId"] = ImportedRuleIdPrefix + ruleId;
            }
        }
    }

    private static void MergeImportedDiagnosticsIntoRun(
        JsonObject run, JsonArray importedResults, JsonArray importedRules)
    {
        JsonArray results = run["results"] as JsonArray ?? new JsonArray();
        run["results"] = results;
        foreach (JsonNode? result in importedResults.ToArray())
        {
            results.Add(result?.DeepClone());
        }

        JsonObject tool = run["tool"] as JsonObject ?? new JsonObject();
        run["tool"] = tool;
        JsonObject driver = tool["driver"] as JsonObject ?? new JsonObject();
        tool["driver"] = driver;
        JsonArray rules = driver["rules"] as JsonArray ?? new JsonArray();

        foreach (JsonNode? rule in importedRules)
        {
            if (rule is not JsonObject ruleObject)
            {
                continue;
            }

            string? ruleId = ruleObject["id"]?.GetValue<string>();
            bool alreadyPresent = rules.OfType<JsonObject>()
                .Any(existing => string.Equals(existing["id"]?.GetValue<string>(), ruleId, StringComparison.Ordinal));
            if (!alreadyPresent)
            {
                rules.Add(rule.DeepClone());
            }
        }

        JsonArray orderedRules = new();
        foreach (JsonNode? rule in rules
            .OfType<JsonObject>()
            .OrderBy(rule => rule["id"]?.GetValue<string>(), StringComparer.Ordinal))
        {
            orderedRules.Add(rule.DeepClone());
        }

        driver["rules"] = orderedRules;
    }



    internal string RenderReportContent(
        string format,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return Render(format, isSingleMode, outcomesByMode);
    }
}
