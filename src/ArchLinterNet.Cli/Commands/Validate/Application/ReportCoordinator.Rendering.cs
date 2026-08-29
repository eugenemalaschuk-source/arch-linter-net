using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Rendering helpers are kept separate from report routing so output accounting stays readable.
internal sealed partial class ReportCoordinator
{
    private string FormatHumanContent(
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken)
    {
        return isSingleMode
            ? FormatSingleHuman(outcomesByMode[0].Outcome, cancellationToken)
            : FormatCombinedHuman(outcomesByMode, cancellationToken);
    }

    private static string FormatAssessmentCompletionForHumans(
        ArchitectureAssessmentCompletionEvidence? completion)
    {
        if (completion is null)
        {
            return string.Empty;
        }

        string reasons = completion.Reasons.Count == 0
            ? "none"
            : string.Join(
                "; ",
                completion.Reasons.Select(reason =>
                {
                    ArchitectureApplicabilityProvenance provenance = reason.Provenance;
                    string policy = string.IsNullOrEmpty(provenance.PolicyIdentity)
                        ? string.Empty
                        : $", policy={provenance.PolicyIdentity}";
                    return $"{reason.Code} (family={provenance.Family}, control={provenance.ControlIdentity}{policy})";
                }));

        return $"Assessment completion: {CompletionStateToken(completion.State)}; reasons: {reasons}";
    }

    private static string CompletionStateToken(ArchitectureAssessmentCompletionState state) =>
        state.ToString().ToLowerInvariant();

    private static string AddAssessmentCompletionToJson(
        string json,
        ArchitectureAssessmentCompletionEvidence? completion)
    {
        if (completion is null)
        {
            return json;
        }

        JsonNode document = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("The validation JSON report was empty.");
        if (document is not JsonObject payload)
        {
            throw new InvalidOperationException("The validation JSON report was not an object.");
        }

        payload["assessment_completion"] = BuildAssessmentCompletionJson(completion);
        return payload.ToJsonString();
    }

    private static JsonObject BuildAssessmentCompletionJson(
        ArchitectureAssessmentCompletionEvidence completion)
    {
        JsonArray reasons = new();
        foreach (ArchitectureApplicabilityReason reason in completion.Reasons)
        {
            ArchitectureApplicabilityProvenance provenance = reason.Provenance;
            reasons.Add(new JsonObject
            {
                ["code"] = reason.Code,
                ["provenance"] = new JsonObject
                {
                    ["family"] = provenance.Family,
                    ["control_identity"] = provenance.ControlIdentity,
                    ["policy_identity"] = provenance.PolicyIdentity,
                },
            });
        }

        return new JsonObject
        {
            ["state"] = CompletionStateToken(completion.State),
            ["reasons"] = reasons,
        };
    }

    private static string AddAssessmentCompletionToSarif(
        string json,
        ArchitectureAssessmentCompletionEvidence? completion)
    {
        if (completion is null)
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
            JsonObject properties = payload["properties"] as JsonObject ?? new JsonObject();
            payload["properties"] = properties;
            properties["arch_linter_net.assessment_completion"] = BuildAssessmentCompletionJson(completion);
        }
        else
        {
            foreach (JsonNode? run in runs)
            {
                if (run is not JsonObject runObject)
                {
                    continue;
                }

                JsonObject properties = runObject["properties"] as JsonObject ?? new JsonObject();
                runObject["properties"] = properties;
                properties["arch_linter_net.assessment_completion"] = BuildAssessmentCompletionJson(completion);
            }
        }

        return payload.ToJsonString();
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

        return AddAssessmentCompletionToJson(result, outcome.AssessmentCompletionEvidence);
    }

    private string FormatSarifContent(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        string result = _runtime.FormatResultAsSarif(
            mode, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.PreflightDiagnostics,
            outcome.CoverageSummaries, outcome.SourceExpansion, outcome.SubtractiveMatcherParticipation, cancellationToken);

        return AddAssessmentCompletionToSarif(result, outcome.AssessmentCompletionEvidence);
    }

    private static string? RenderContent(
        string? needed,
        string format,
        Func<string> render,
        SinkDistributionEvidence evidence,
        ValidationTiming? timing)
    {
        if (needed is null)
        {
            return null;
        }

        string content;
        using (timing?.Measure($"render_{format}"))
            content = render();
        evidence.RecordRenderedFormat(format);
        return content;
    }

    private static string FormatStructuredContent(
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        Func<string, ValidationOutcome, CancellationToken, string> formatSingle,
        Func<IReadOnlyList<(string Mode, ValidationOutcome Outcome)>, CancellationToken, string> formatCombined,
        CancellationToken cancellationToken)
    {
        return isSingleMode
            ? formatSingle(outcomesByMode[0].Mode, outcomesByMode[0].Outcome, cancellationToken)
            : formatCombined(outcomesByMode, cancellationToken);
    }

    private static Dictionary<string, string> BuildContentByFormat(string? humanContent, string? jsonContent, string? sarifContent)
    {
        Dictionary<string, string> contentByFormat = new();
        if (humanContent is not null)
        {
            contentByFormat[FormatHuman] = humanContent;
        }
        if (jsonContent is not null)
        {
            contentByFormat[FormatJson] = jsonContent;
        }
        if (sarifContent is not null)
        {
            contentByFormat[FormatSarif] = sarifContent;
        }
        return contentByFormat;
    }

    // Re-renders a complete document from an already-computed outcome for an output-error
    // envelope; it never repeats validation or contract execution.
    public string RenderReportContent(
        string format, bool isSingleMode, IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return format switch
        {
            FormatJson => isSingleMode
                ? FormatSingleJson(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedJson(outcomesByMode),
            FormatSarif => isSingleMode
                ? FormatSingleSarif(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedSarif(outcomesByMode),
            _ => isSingleMode
                ? FormatSingleHuman(outcomesByMode[0].Outcome)
                : FormatCombinedHuman(outcomesByMode),
        };
    }
}
