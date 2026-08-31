using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Rendering helpers are kept separate from report routing so output accounting stays readable.
internal sealed partial class ReportCoordinator
{
    private const string PropertiesPropertyName = "properties";

    private string FormatHumanContent(
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken)
    {
        return isSingleMode
            ? FormatSingleHuman(outcomesByMode[0].Outcome, cancellationToken)
            : FormatCombinedHuman(outcomesByMode, cancellationToken);
    }

    private static string CompletionStateToken(ArchitectureAssessmentCompletionState state) =>
        state.ToString().ToLowerInvariant();

    private static string AddAssessmentCompletionToJson(
        string json,
        ArchitectureAssessmentCompletionEvidence? completion,
        ArchitectureApplicabilityProjection? projection = null)
    {
        // Once the Core projection is present it is the sole applicability output authority. The
        // completion parameter is retained only for byte-compatible rendering of legacy,
        // hand-built outcomes that carry completion evidence without a projection.
        completion = projection?.Completion ?? completion;
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

        payload["assessment_completion"] = BuildAssessmentCompletionJson(completion, projection);
        if (projection is not null)
        {
            payload["applicability_findings"] = BuildApplicabilityFindingsJson(projection);
        }
        return payload.ToJsonString();
    }

    private static JsonObject BuildAssessmentCompletionJson(
        ArchitectureAssessmentCompletionEvidence completion,
        ArchitectureApplicabilityProjection? projection = null)
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

        var result = new JsonObject
        {
            ["state"] = CompletionStateToken(completion.State),
            ["reasons"] = reasons,
        };

        if (projection is not null)
        {
            result["summary"] = BuildApplicabilitySummaryJson(projection.Summary);
            result["controls"] = BuildApplicabilityControlsJson(projection.Controls);
        }

        return result;
    }

    private static JsonObject BuildApplicabilitySummaryJson(ArchitectureApplicabilitySummary summary)
    {
        return new JsonObject
        {
            ["interpretation"] = "completeness transparency; not an architecture quality score",
            ["required_count"] = summary.RequiredCount,
            ["required_evaluable_count"] = summary.RequiredEvaluableCount,
            ["required_unassessable_count"] = summary.RequiredUnassessableCount,
            ["evaluable_count"] = summary.EvaluableCount,
            ["unassessable_count"] = summary.UnassessableCount,
            ["optional_count"] = summary.OptionalCount,
            ["not_applicable_count"] = summary.NotApplicableCount,
        };
    }

    private static JsonArray BuildApplicabilityControlsJson(
        IReadOnlyList<ArchitectureApplicabilityAssessment> controls)
    {
        JsonArray result = new();
        foreach (ArchitectureApplicabilityAssessment control in controls)
        {
            string? family = control.Expected?.Family ?? control.Record?.Family;
            var value = new JsonObject
            {
                ["control_identity"] = control.ControlIdentity,
                ["family"] = family,
                ["membership"] = control.Membership is { } membership
                    ? ArchitectureApplicabilityWireNames.MembershipToken(membership)
                    : null,
                ["state"] = control.State is { } state
                    ? ArchitectureApplicabilityWireNames.StateToken(state)
                    : null,
                ["validated_state"] = control.State is { } validatedState
                    ? ArchitectureApplicabilityWireNames.StateToken(validatedState)
                    : null,
                ["record_state"] = control.Record?.State is { } recordState
                    ? ArchitectureApplicabilityWireNames.StateToken(recordState)
                    : null,
                ["is_integrity_valid"] = control.IsIntegrityValid,
                ["integrity_reasons"] = BuildApplicabilityReasonsJson(control.IntegrityReasons),
                ["expected"] = BuildApplicabilityExpectedEntryJson(control.Expected),
                ["record"] = BuildApplicabilityRecordJson(control.Record),
            };
            result.Add(value);
        }

        return result;
    }

    private static JsonObject? BuildApplicabilityExpectedEntryJson(
        ArchitectureApplicabilityExpectedEntry? expected)
    {
        if (expected is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["control_identity"] = expected.ControlIdentity,
            ["family"] = expected.Family,
            ["membership"] = ArchitectureApplicabilityWireNames.MembershipToken(expected.Membership),
            ["provenance"] = BuildApplicabilityProvenanceJson(expected.Provenance),
        };
    }

    private static JsonObject? BuildApplicabilityRecordJson(
        ArchitectureApplicabilityRecord? record)
    {
        if (record is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["control_identity"] = record.ControlIdentity,
            ["family"] = record.Family,
            ["state"] = ArchitectureApplicabilityWireNames.StateToken(record.State),
            ["reasons"] = BuildApplicabilityReasonsJson(record.Reasons),
            ["provenance"] = BuildApplicabilityProvenanceJson(record.Provenance),
            ["topology_evidence"] = BuildTopologyEvidenceJson(record.TopologyEvidence),
        };
    }

    private static JsonObject? BuildTopologyEvidenceJson(ArchitectureTopologyMappingEvidence? evidence)
    {
        if (evidence is null)
        {
            return null;
        }

        JsonArray subjects = new();
        foreach (ArchitectureTopologySubjectEvidence subject in evidence.Subjects)
        {
            subjects.Add(new JsonObject
            {
                ["identity"] = subject.Identity,
                ["project"] = subject.Project,
                ["assembly"] = subject.Assembly,
                ["subject"] = subject.Subject,
                ["disposition"] = subject.Disposition,
                ["node_ids"] = new JsonArray(subject.NodeIds.Select(value => JsonValue.Create(value)).ToArray()),
                ["reviewed_out_of_scope_id"] = subject.ReviewedOutOfScopeId,
            });
        }

        JsonArray relationships = new();
        foreach (ArchitectureTopologyRelationEvidence relationship in evidence.Relationships)
        {
            relationships.Add(new JsonObject
            {
                ["source_node"] = relationship.SourceNode,
                ["target_node"] = relationship.TargetNode,
                ["witness"] = relationship.Witness,
                ["is_allowed"] = relationship.IsAllowed,
            });
        }

        JsonArray staleEdges = new();
        foreach (ArchitectureTopologyStaleEdgeEvidence edge in evidence.StaleEdges)
        {
            staleEdges.Add(new JsonObject { ["source_node"] = edge.SourceNode, ["target_node"] = edge.TargetNode });
        }

        return new JsonObject
        {
            ["interpretation"] = "topology completeness evidence; not an architecture quality score",
            ["mode"] = evidence.Mode,
            ["subject_kind"] = evidence.SubjectKind,
            ["declared_component_count"] = evidence.DeclaredComponentCount,
            ["observed_subject_count"] = evidence.ObservedSubjectCount,
            ["mapped_subject_count"] = evidence.MappedSubjectCount,
            ["reviewed_out_of_scope_subject_count"] = evidence.ReviewedOutOfScopeSubjectCount,
            ["unmapped_subject_count"] = evidence.UnmappedSubjectCount,
            ["ambiguous_subject_count"] = evidence.AmbiguousSubjectCount,
            ["subjects"] = subjects,
            ["relationships"] = relationships,
            ["stale_nodes"] = new JsonArray(evidence.StaleNodes.Select(value => JsonValue.Create(value)).ToArray()),
            ["stale_edges"] = staleEdges,
        };
    }

    private static JsonArray BuildApplicabilityReasonsJson(
        IReadOnlyList<ArchitectureApplicabilityReason> reasons)
    {
        JsonArray result = new();
        foreach (ArchitectureApplicabilityReason reason in reasons)
        {
            result.Add(new JsonObject
            {
                ["code"] = reason.Code,
                ["provenance"] = BuildApplicabilityProvenanceJson(reason.Provenance),
            });
        }

        return result;
    }

    private static JsonObject BuildApplicabilityProvenanceJson(
        ArchitectureApplicabilityProvenance provenance)
    {
        return new JsonObject
        {
            ["family"] = provenance.Family,
            ["control_identity"] = provenance.ControlIdentity,
            ["policy_identity"] = provenance.PolicyIdentity,
        };
    }

    private static JsonArray BuildApplicabilityFindingsJson(
        ArchitectureApplicabilityProjection projection)
    {
        JsonArray result = new();
        foreach (ArchitectureFinding finding in projection.Findings)
        {
            result.Add(JsonSerializer.SerializeToNode(
                ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding)));
        }

        return result;
    }

    private static string AddAssessmentCompletionToSarif(
        string json,
        ArchitectureAssessmentCompletionEvidence? completion,
        ArchitectureApplicabilityProjection? projection = null)
    {
        completion = projection?.Completion ?? completion;
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
            if (projection is null)
            {
                JsonObject properties = payload[PropertiesPropertyName] as JsonObject ?? new JsonObject();
                payload[PropertiesPropertyName] = properties;
                properties["arch_linter_net.assessment_completion"] = BuildAssessmentCompletionJson(completion);
            }
            else
            {
                // A valid SARIF document normally has at least one run. Keep malformed/minimal
                // formatter fakes and future hosts useful by materializing the smallest valid run
                // when the projected findings need a result container.
                var run = new JsonObject
                {
                    ["tool"] = new JsonObject
                    {
                        ["driver"] = new JsonObject
                        {
                            ["name"] = "arch-linter-net",
                            ["rules"] = new JsonArray(),
                        },
                    },
                    ["results"] = new JsonArray(),
                    [PropertiesPropertyName] = new JsonObject
                    {
                        ["arch_linter_net.assessment_completion"] = BuildAssessmentCompletionJson(completion, projection),
                    },
                };
                runs.Add(run);
                AddApplicabilityFindingsToSarifRun(run, projection);
            }
        }
        else
        {
            foreach (JsonNode? run in runs)
            {
                if (run is not JsonObject runObject)
                {
                    continue;
                }

                JsonObject properties = runObject[PropertiesPropertyName] as JsonObject ?? new JsonObject();
                runObject[PropertiesPropertyName] = properties;
                properties["arch_linter_net.assessment_completion"] = BuildAssessmentCompletionJson(completion, projection);
                if (projection is not null)
                {
                    AddApplicabilityFindingsToSarifRun(runObject, projection);
                }
            }
        }

        return payload.ToJsonString();
    }

    private static void AddApplicabilityFindingsToSarifRun(
        JsonObject run,
        ArchitectureApplicabilityProjection projection)
    {
        JsonArray results = run["results"] as JsonArray ?? new JsonArray();
        run["results"] = results;

        JsonObject driver = ((run["tool"] as JsonObject)?["driver"] as JsonObject) ?? new JsonObject();
        JsonObject tool = run["tool"] as JsonObject ?? new JsonObject();
        tool["driver"] = driver;
        run["tool"] = tool;
        JsonArray rules = driver["rules"] as JsonArray ?? new JsonArray();

        foreach (ArchitectureFinding finding in projection.Findings)
        {
            if (finding.Details is not ArchitectureApplicabilityDiagnostic diagnostic)
            {
                throw new InvalidOperationException(
                    "The applicability projection contained a non-applicability normalized finding.");
            }

            string ruleId = finding.ContractId ?? finding.ContractName;
            if (!rules.Any(rule => rule is JsonObject ruleObject
                && string.Equals(ruleObject["id"]?.GetValue<string>(), ruleId, StringComparison.Ordinal)))
            {
                rules.Add(new JsonObject
                {
                    ["id"] = ruleId,
                    ["shortDescription"] = new JsonObject { ["text"] = diagnostic.Family },
                });
            }

            results.Add(BuildApplicabilitySarifResult(finding, diagnostic, ruleId));
        }

        // The Core SARIF formatter orders rules by rule id. Reapply that ordering after adding
        // projected rules while retaining the existing result order and its normal finding
        // property envelope.
        JsonArray orderedRules = new();
        foreach (JsonNode? rule in rules
            .OfType<JsonObject>()
            .OrderBy(rule => rule["id"]?.GetValue<string>(), StringComparer.Ordinal))
        {
            // JsonNode instances may have only one parent. Preserve their canonical contents
            // while producing the ordered replacement array.
            orderedRules.Add(rule.DeepClone());
        }

        driver["rules"] = orderedRules;
    }

    private static JsonObject BuildApplicabilitySarifResult(
        ArchitectureFinding finding,
        ArchitectureApplicabilityDiagnostic diagnostic,
        string ruleId)
    {
        string membership = diagnostic.Membership is { } membershipValue
            ? ArchitectureApplicabilityWireNames.MembershipToken(membershipValue)
            : "unknown";
        string state = diagnostic.State is { } stateValue
            ? ArchitectureApplicabilityWireNames.StateToken(stateValue)
            : "missing";

        return new JsonObject
        {
            ["ruleId"] = ruleId,
            ["level"] = finding.Severity,
            ["message"] = new JsonObject
            {
                ["text"] = $"[applicability] control={diagnostic.ControlIdentity}, family={diagnostic.Family}, "
                    + $"membership={membership}, state={state}, reason={diagnostic.ReasonCode}",
            },
            ["logicalLocations"] = new JsonArray
            {
                new JsonObject
                {
                    ["fullyQualifiedName"] = diagnostic.ControlIdentity,
                    ["kind"] = "applicability",
                },
            },
            [PropertiesPropertyName] = new JsonObject
            {
                ["arch_linter_net"] = JsonSerializer.SerializeToNode(
                    ArchitectureDiagnosticFormatter.FormatNormalizedFindingForSarif(finding)),
            },
        };
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

        return AddAssessmentCompletionToJson(
            result, outcome.AssessmentCompletionEvidence, outcome.ApplicabilityProjection);
    }

    private string FormatSarifContent(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        string result = _runtime.FormatResultAsSarif(
            mode, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.PreflightDiagnostics,
            outcome.CoverageSummaries, outcome.SourceExpansion, outcome.SubtractiveMatcherParticipation, cancellationToken);

        return AddAssessmentCompletionToSarif(
            result, outcome.AssessmentCompletionEvidence, outcome.ApplicabilityProjection);
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
