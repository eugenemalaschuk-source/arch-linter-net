using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePrReportReaderTests
{
    [Test]
    public void ReadAndProject_ParsesHealthEvidenceAndResolvedChangeFinding()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome();
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(
            Snapshot([new ArchitectureChangeFinding("resolved", "dependency", "resolved")]),
            Snapshot(),
            "run-1");

        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.ReadAndProject(
            ArchitectureHealthProjector.FormatAsJson(outcome),
            ArchitectureChangeReports.FormatJson(change));

        Assert.Multiple(() =>
        {
            Assert.That(projection.Availability, Is.EqualTo(ArchitecturePrReportAvailability.Complete));
            Assert.That(projection.Headline.Gate, Is.EqualTo(outcome.Summary.Gate));
            Assert.That(projection.Headline.Health, Is.EqualTo(outcome.Summary.Health));
            Assert.That(projection.Evidence, Is.Not.Null);
            Assert.That(projection.Evidence!.ValidationOutcomes[0].PolicyInventory, Is.Not.Null);
            Assert.That(projection.Change.ResolvedFindings.Select(finding => finding.Identity), Is.EqualTo(["resolved"]));
            Assert.That(projection.Navigation.Select(reference => reference.Authority), Does.Contain("change_finding"));
        });
    }

    [Test]
    public void Read_ProjectsLegacyHealthSummaryAsUnavailableReportEvidence()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome();
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1");

        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.ReadAndProject(
            ArchitectureHealthProjector.FormatAsJson(outcome.Summary),
            ArchitectureChangeReports.FormatJson(change));

        Assert.Multiple(() =>
        {
            Assert.That(projection.Evidence, Is.Null);
            Assert.That(projection.Availability, Is.EqualTo(ArchitecturePrReportAvailability.Unavailable));
            Assert.That(projection.Headline.Gate, Is.EqualTo(outcome.Summary.Gate));
            Assert.That(projection.Headline.Health, Is.EqualTo(outcome.Summary.Health));
        });
    }

    [Test]
    public void Read_RejectsMalformedAndUnsupportedArtifacts()
    {
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1");
        string changeJson = ArchitectureChangeReports.FormatJson(change);
        string healthJson = ArchitectureHealthProjector.FormatAsJson(CreateOutcome());

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitecturePrReportReader.Read("not-json", changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson.Replace("architecture-health/v1", "architecture-health/v9", StringComparison.Ordinal),
                changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson.Replace("architecture-health-report-evidence", "unknown-evidence", StringComparison.Ordinal),
                changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson, changeJson.Replace("architecture-change-report", "unknown-change", StringComparison.Ordinal)),
                Throws.ArgumentException);
        });
    }

    [Test]
    public void Read_RejectsHealthAndChangeArtifactsWithDifferentExecutionContextModeOrConditionSet()
    {
        string healthJson = ArchitectureHealthProjector.FormatAsJson(CreateOutcome());
        string changeJson = ArchitectureChangeReports.FormatJson(
            ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1"));

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson,
                changeJson.Replace("run-1", "run-2", StringComparison.Ordinal)), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson,
                changeJson.Replace("\"mode\": \"strict\"", "\"mode\": \"audit\"", StringComparison.Ordinal)), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson,
                changeJson.Replace("\"condition_set\": \"ci\"", "\"condition_set\": \"developer\"", StringComparison.Ordinal)), Throws.ArgumentException);
        });
    }

    [Test]
    public void Read_RejectsAvailabilityThatDoesNotMatchPayloadOrKnownWireContract()
    {
        string changeJson = ArchitectureChangeReports.FormatJson(
            ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1"));

        JsonNode missingPayload = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        missingPayload["report_evidence"]!["validation_outcomes"]![0]!["availability"]!["external_evidence"] = "available";

        JsonNode unknownKey = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        unknownKey["report_evidence"]!["validation_outcomes"]![0]!["availability"]!["future_authority"] = "available";

        JsonNode unknownValue = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        unknownValue["report_evidence"]!["validation_outcomes"]![0]!["availability"]!["policy_inventory"] = "clean";

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitecturePrReportReader.Read(missingPayload.ToJsonString(), changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(unknownKey.ToJsonString(), changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(unknownValue.ToJsonString(), changeJson), Throws.ArgumentException);
        });
    }

    [Test]
    public void Read_RejectsNullArrayElementsAsInvalidArtifacts()
    {
        string changeJson = ArchitectureChangeReports.FormatJson(
            ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1"));
        JsonNode nullFinding = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        nullFinding["report_evidence"]!["validation_outcomes"]![0]!["findings"] = new JsonArray((JsonNode?)null);

        JsonNode nullBaselineEntry = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        nullBaselineEntry["report_evidence"]!["debt_gate"]!["persistent_debt"]!["entries"] = new JsonArray((JsonNode?)null);

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitecturePrReportReader.Read(nullFinding.ToJsonString(), changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(nullBaselineEntry.ToJsonString(), changeJson), Throws.ArgumentException);
        });
    }

    [Test]
    public void Read_RejectsRequestedButIncompletePolicyWeakeningReceipt()
    {
        string changeJson = ArchitectureChangeReports.FormatJson(
            ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1"));
        JsonNode health = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        JsonNode debtGate = health["report_evidence"]!["debt_gate"]!;
        debtGate["succeeded"] = false;
        debtGate["policy_weakening"] = new JsonObject
        {
            ["requested"] = true,
            ["schema_version"] = 1,
            ["kind"] = "policy-weakening",
            ["policy_name"] = "policy",
            ["policy_version"] = 1,
            ["severity"] = "error",
            ["has_blocking_findings"] = true,
            ["findings"] = new JsonArray(),
        };

        Assert.That(() => ArchitecturePrReportReader.Read(health.ToJsonString(), changeJson), Throws.ArgumentException);
    }

    [Test]
    public void Read_ParsesCompleteDebtAndPolicyWeakeningReceipts()
    {
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1");

        ArchitecturePrReportInput input = ArchitecturePrReportReader.Read(
            ArchitectureHealthProjector.FormatAsJson(CreateOutcomeWithDebtEvidence()),
            ArchitectureChangeReports.FormatJson(change));

        ArchitecturePrReportDebtGateReceipt debtGate = input.Evidence!.DebtGate;

        Assert.Multiple(() =>
        {
            Assert.That(debtGate.Succeeded, Is.True);
            Assert.That(debtGate.Passed, Is.False);
            Assert.That(debtGate.Evaluation.ReusedAnalysisSnapshot, Is.True);
            Assert.That(debtGate.Evaluation.PreflightDiagnostics.Single().Kind, Is.EqualTo("build_state_preflight"));
            Assert.That(debtGate.PersistentDebt.InSync, Is.False);
            Assert.That(debtGate.PersistentDebt.Entries.Select(entry => entry.Status), Is.EquivalentTo(
                ["new", "matched", "resolved", "stale", "changed", "ambiguous", "configuration-error"]));
            Assert.That(debtGate.PersistentDebt.Entries.Single(entry => entry.Status == "new").Identity,
                Does.Contain("identity_version"));
            Assert.That(debtGate.PersistentDebt.ConfigurationViolations.Single().CanonicalIdentity, Is.Not.Empty);
            Assert.That(debtGate.PolicyWeakening, Is.Not.Null);
            Assert.That(debtGate.PolicyWeakening!.Findings.Single().BaseProvenance!.SourcePath,
                Is.EqualTo("/repo/base.yml"));
            Assert.That(debtGate.PolicyWeakening.Findings.Single().CurrentProvenance!.Role,
                Is.EqualTo("current"));
        });
    }

    [Test]
    public void FormatAsJson_UsesLegacyDebtBucketsWhenLifecycleEntriesAreAbsent()
    {
        using JsonDocument document = JsonDocument.Parse(
            ArchitectureHealthProjector.FormatAsJson(CreateOutcomeWithDebtEvidence(includeLifecycleEntries: false)));

        JsonElement entries = document.RootElement.GetProperty("report_evidence")
            .GetProperty("debt_gate")
            .GetProperty("persistent_debt")
            .GetProperty("entries");

        Assert.That(entries.EnumerateArray().Select(entry => entry.GetProperty("status").GetString()),
            Is.EquivalentTo(["new", "matched", "resolved", "ambiguous", "configuration-error"]));
    }

    [Test]
    public void Read_ParsesCompleteAuthorityReceiptsWithTopologyAndExternalEvidence()
    {
        JsonNode health = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        JsonObject receipt = health["report_evidence"]!["validation_outcomes"]![0]!.AsObject();
        receipt["availability"] = new JsonObject
        {
            ["applicability"] = "available",
            ["external_evidence"] = "available",
            ["findings"] = "available",
            ["policy_inventory"] = "available",
            ["topology"] = "available",
            ["waiver_lifecycle"] = "available",
        };
        receipt["policy_inventory"] = new JsonObject
        {
            ["schema"] = ArchitecturePolicyInventory.CurrentSchemaId,
            ["effective_rule_count"] = 3,
            ["rules"] = new JsonObject { ["strict"] = 1, ["audit"] = 1, ["coverage"] = 1 },
            ["ignore_debt"] = new JsonObject
            {
                ["total"] = 1,
                ["active"] = 1,
                ["stale"] = 0,
                ["expired"] = 0,
                ["metadata_incomplete"] = 0,
                ["invalid"] = 0,
            },
            ["waivers"] = new JsonArray(FullWaiver()),
        };
        receipt["waiver_lifecycle"] = new JsonObject
        {
            ["profile"] = "strict",
            ["blocking_states"] = new JsonArray("expired", "stale"),
            ["records"] = new JsonArray(FullWaiver()),
        };
        receipt["applicability"] = new JsonObject
        {
            ["state"] = "pass",
            ["summary"] = new JsonObject
            {
                ["required"] = 1,
                ["required_evaluable"] = 1,
                ["required_unassessable"] = 0,
            },
            ["reasons"] = new JsonArray(ApplicabilityReason("enforced", "topology.control")),
            ["controls"] = new JsonArray(
                new JsonObject
                {
                    ["control_identity"] = "topology.control",
                    ["membership"] = "required",
                    ["state"] = "pass",
                    ["integrity_valid"] = true,
                    ["integrity_reasons"] = new JsonArray(ApplicabilityReason("integrity", "topology.control")),
                    ["expected"] = new JsonObject
                    {
                        ["control_identity"] = "topology.control",
                        ["family"] = "topology",
                        ["membership"] = "required",
                        ["provenance"] = ApplicabilityProvenance("topology.control"),
                    },
                    ["record"] = new JsonObject
                    {
                        ["control_identity"] = "topology.control",
                        ["family"] = "topology",
                        ["state"] = "pass",
                        ["reasons"] = new JsonArray(ApplicabilityReason("mapped", "topology.control")),
                        ["provenance"] = ApplicabilityProvenance("topology.control"),
                        ["topology_evidence"] = new JsonObject
                        {
                            ["mode"] = "strict",
                            ["subject_kind"] = "namespace",
                            ["declared_component_count"] = 2,
                            ["counts"] = new JsonObject
                            {
                                ["observed"] = 2,
                                ["mapped"] = 1,
                                ["reviewed_out_of_scope"] = 1,
                                ["unmapped"] = 0,
                                ["ambiguous"] = 0,
                            },
                            ["subjects"] = new JsonArray(
                                new JsonObject
                                {
                                    ["identity"] = "App.Api",
                                    ["project"] = "App",
                                    ["assembly"] = "App",
                                    ["subject"] = "App.Api",
                                    ["disposition"] = "mapped",
                                    ["node_ids"] = new JsonArray("Api"),
                                    ["reviewed_out_of_scope_id"] = "manual-1",
                                }),
                            ["relationships"] = new JsonArray(
                                new JsonObject
                                {
                                    ["source_node"] = "Api",
                                    ["target_node"] = "Core",
                                    ["witness"] = "App.Api -> Core",
                                    ["is_allowed"] = true,
                                }),
                            ["stale_nodes"] = new JsonArray("Legacy"),
                            ["stale_edges"] = new JsonArray(
                                new JsonObject { ["source_node"] = "Api", ["target_node"] = "Legacy" }),
                        },
                        ["metric_evidence"] = new JsonObject
                        {
                            ["metric_id"] = "namespace-count",
                            ["kind"] = "count",
                            ["native_subject"] = "App.Api",
                            ["unit"] = "types",
                            ["effective_scope"] = "App",
                            ["value"] = 2,
                            ["contributors"] = new JsonArray("App.Api", "App.Core"),
                        },
                    },
                }),
        };
        receipt["external_evidence"] = new JsonObject
        {
            ["mode"] = "strict",
            ["requirements"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "sarif",
                    ["format"] = "sarif",
                    ["required"] = true,
                    ["tool"] = "scanner",
                    ["tool_version"] = "1.0",
                    ["run"] = "current",
                    ["require_repository"] = true,
                    ["require_revision"] = true,
                    ["require_scope"] = true,
                    ["diagnostic_filter"] = new JsonObject
                    {
                        ["rule_ids"] = new JsonArray("RULE-1"),
                        ["rule_tags"] = new JsonArray("security"),
                        ["projects"] = new JsonArray("App"),
                        ["path_prefixes"] = new JsonArray("src/"),
                        ["severity"] = new JsonObject { ["error"] = "high" },
                        ["require_matches"] = true,
                    },
                }),
            ["trust_receipts"] = new JsonArray(
                new JsonObject
                {
                    ["logical_id"] = "sarif",
                    ["state"] = "current",
                    ["trust_status"] = "valid",
                    ["reason_code"] = "trusted",
                    ["artifact_path"] = "evidence/current.sarif",
                    ["artifact_sha256"] = "sha256",
                    ["run_id"] = "current",
                    ["result_count"] = 0,
                    ["context"] = new JsonObject
                    {
                        ["repository"] = "repo",
                        ["revision"] = "revision",
                        ["scope"] = "scope",
                    },
                }),
            ["findings"] = new JsonArray(FullFinding("external-finding")),
        };
        receipt["findings"] = new JsonArray(FullFinding("validation-finding"));

        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1");
        ArchitecturePrReportValidationReceipt parsed = ArchitecturePrReportReader.Read(
            health.ToJsonString(), ArchitectureChangeReports.FormatJson(change)).Evidence!.ValidationOutcomes.Single();

        Assert.Multiple(() =>
        {
            Assert.That(parsed.PolicyInventory!.Waivers.Single().PolicyLocation!.YamlPath, Is.EqualTo("rules[0]"));
            Assert.That(parsed.WaiverLifecycle!.BlockingStates, Is.EqualTo(["expired", "stale"]));
            Assert.That(parsed.Applicability!.Controls.Single().Record!.Topology!.Subjects.Single().NodeIds,
                Is.EqualTo(["Api"]));
            Assert.That(parsed.Applicability.Controls.Single().Record!.Metric!.Contributors,
                Is.EqualTo(["App.Api", "App.Core"]));
            Assert.That(parsed.ExternalEvidence!.Requirements.Single().DiagnosticFilter!.Severity["error"], Is.EqualTo("high"));
            Assert.That(parsed.ExternalEvidence.TrustReceipts.Single().State,
                Is.EqualTo(ArchitecturePrReportExternalEvidenceTrustState.Current));
            Assert.That(parsed.ExternalEvidence.TrustReceipts.Single().ResultCount, Is.EqualTo(0));
            Assert.That(parsed.ExternalEvidence.TrustReceipts.Single().Context!.Revision, Is.EqualTo("revision"));
            Assert.That(parsed.ExternalEvidence.Findings.Single().Remediation!.Evidence.Single().Value,
                Is.EqualTo("runtime"));
            Assert.That(parsed.Findings.Single().PolicyIdentity, Is.EqualTo("/repo/policy.yml:rules[0]"));
        });
    }

    private static ArchitectureChangeSnapshot Snapshot(IReadOnlyList<ArchitectureChangeFinding>? findings = null) =>
        new(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            "strict",
            "ci",
            [],
            findings ?? [],
            []);

    private static ArchitectureHealthOutcome CreateOutcome()
    {
        ArchitectureApplicabilityExpectedEntry expected = new(
            "control",
            "dependencies",
            ArchitectureApplicabilityMembership.Required);
        ArchitectureApplicabilityRecord record = new(
            "control",
            "dependencies",
            ArchitectureApplicabilityRecordState.Evaluable);
        var completion = new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Pass,
            [new ArchitectureApplicabilityAssessment(expected, record, [])],
            []);
        ValidationOutcome validation = new(
            Passed: true,
            Violations: [],
            Cycles: [],
            CoverageFindings: [],
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: [],
            UnmatchedIgnoredViolationsConfig: "off",
            PolicyConsistencyFindings: [],
            PolicyConsistencyConfig: "off",
            CoverageSummaries: [],
            ClassificationConflicts: [],
            ClassificationMetadataFailures: [])
        {
            PolicyInventory = new ArchitecturePolicyInventory(
                ArchitecturePolicyInventory.CurrentSchemaId,
                0,
                new ArchitecturePolicyInventoryRules(0, 0, 0),
                new ArchitecturePolicyInventoryIgnoreDebt(0, 0, 0, 0, 0, 0),
                []),
            WaiverLifecycleAssessment = new ArchitectureWaiverLifecycleAssessment("strict", [], []),
            ApplicabilityExpectedEntries = [expected],
            ApplicabilityRecords = [record],
            AssessmentCompletionEvidence = completion,
            RepositoryRoot = "/repo",
            PolicyImportPaths = ["/repo/policy.yml"],
            ResolvedAssemblyPaths = [],
            DiscoveredProjectPaths = ["/repo/App.csproj"],
        };
        var baseline = new BaselineVerifyOutcome(true, true, [], [], [], [], []);
        var debtGate = new ArchitectureDebtGateOutcome(
            true,
            true,
            new ArchitectureDebtGateEvaluation(true, "strict", []),
            baseline);
        ArchitectureHealthSummary summary = ArchitectureHealthProjector.Project(
            [new ArchitectureHealthValidationOutcome("strict", validation)], debtGate);
        return new ArchitectureHealthOutcome(
            summary,
            [new ArchitectureHealthValidationOutcome("strict", validation)],
            debtGate)
        {
            ExecutionContext = "run-1",
            ConditionSetName = "ci",
        };
    }

    private static ArchitectureHealthOutcome CreateOutcomeWithDebtEvidence(bool includeLifecycleEntries = true)
    {
        ArchitectureHealthOutcome outcome = CreateOutcome();
        ArchitectureBaselineComparisonEntry Entry(string suffix) => new(
            "strict_dependencies",
            $"contract-{suffix}",
            "App.Source",
            $"Domain.Target.{suffix}",
            $"reason-{suffix}",
            new ArchitectureViolationIdentity(
                ArchitectureViolationIdentity.CurrentVersion,
                "strict",
                "dependency",
                $"contract-{suffix}",
                "App",
                "App.Source",
                null,
                "Domain",
                "Domain.Target",
                null,
                1))
        {
            Issue = $"#{suffix}",
            CurrentForbiddenReference = $"Domain.Current.{suffix}",
        };

        ArchitectureBaselineComparisonEntry newEntry = Entry("new");
        ArchitectureBaselineComparisonEntry matchedEntry = Entry("matched");
        ArchitectureBaselineComparisonEntry resolvedEntry = Entry("resolved");
        ArchitectureBaselineComparisonEntry ambiguousEntry = Entry("ambiguous");
        ArchitectureBaselineComparisonEntry configurationEntry = Entry("configuration");
        IReadOnlyList<ArchitectureBaselineComparisonEntry> newEntries = includeLifecycleEntries ? [] : [newEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> frozenEntries = includeLifecycleEntries ? [] : [matchedEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> resolvedEntries = includeLifecycleEntries ? [] : [resolvedEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> configurationEntries = includeLifecycleEntries ? [] : [configurationEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> ambiguousEntries = includeLifecycleEntries ? [] : [ambiguousEntry];
        var persistentDebt = new BaselineVerifyOutcome(
            Succeeded: true,
            InSync: false,
            New: newEntries,
            Frozen: frozenEntries,
            Resolved: resolvedEntries,
            ConfigurationErrors: configurationEntries,
            ConfigurationViolations:
            [
                new ArchitectureViolation(
                    "baseline-configuration",
                    "baseline-config",
                    "App.Source",
                    "Domain.Configuration",
                    ["Domain.Configuration.Reference"]),
            ])
        {
            Entries =
                includeLifecycleEntries
                    ?
                    [
                        new BaselineLifecycleEntry(newEntry, BaselineEntryLifecycle.New, BaselineEntryDisposition.Added),
                        new BaselineLifecycleEntry(matchedEntry, BaselineEntryLifecycle.Matched,
                            BaselineEntryDisposition.Retained),
                        new BaselineLifecycleEntry(resolvedEntry, BaselineEntryLifecycle.Resolved,
                            BaselineEntryDisposition.Removed),
                        new BaselineLifecycleEntry(Entry("stale"), BaselineEntryLifecycle.Stale),
                        new BaselineLifecycleEntry(Entry("changed"), BaselineEntryLifecycle.Changed),
                        new BaselineLifecycleEntry(ambiguousEntry, BaselineEntryLifecycle.Ambiguous),
                        new BaselineLifecycleEntry(configurationEntry, BaselineEntryLifecycle.ConfigurationError),
                    ]
                    : [],
            Ambiguous = ambiguousEntries,
        };
        var debtGate = new ArchitectureDebtGateOutcome(
            Succeeded: true,
            Passed: false,
            new ArchitectureDebtGateEvaluation(
                Completed: true,
                Mode: "strict",
                [
                    new BuildStatePreflightDiagnostic(
                        "build-state",
                        "build-state-id",
                        BuildStatePreflightState.MissingArtifact,
                        new BuildStatePreflightEvidence("/repo/App.csproj", "App")),
                ])
            {
                ReusedAnalysisSnapshot = true,
            },
            persistentDebt)
        {
            PolicyWeakeningRequested = true,
            PolicyWeakening = new ArchitecturePolicyWeakeningResult(
                ArchitecturePolicyWeakeningResult.CurrentSchemaVersion,
                "policy-weakening",
                "architecture",
                2,
                "error",
                [
                    new ArchitecturePolicyWeakeningFinding(
                        "weakening-1",
                        "broadened_waiver",
                        "strict_dependencies:contract",
                        "broadened",
                        "error",
                        ["old"],
                        ["new"],
                        new ArchitecturePolicyContextProvenance(
                            "/repo/base.yml", "/repo", "base", "rules[0]", 1),
                        new ArchitecturePolicyContextProvenance(
                            "/repo/current.yml", "/repo", "current", "rules[0]", 2),
                        ["App.Source"],
                        "reviewed"),
                ]),
        };

        return outcome with
        {
            DebtGate = debtGate,
            Summary = ArchitectureHealthProjector.Project(outcome.ValidationOutcomes, debtGate),
        };
    }

    private static JsonObject FullWaiver() => new()
    {
        ["id"] = "waiver-1",
        ["state"] = "active",
        ["contract"] = "dependency",
        ["contract_id"] = "contract-1",
        ["contract_group"] = "strict_dependencies",
        ["source_type"] = "App.Api",
        ["forbidden_reference"] = "Domain.Core",
        ["target_fingerprint"] = "fingerprint",
        ["reason"] = "reviewed",
        ["owner"] = "owner",
        ["issue"] = "#1",
        ["introduced"] = "2026-01-01",
        ["expires"] = "2027-01-01",
        ["evaluation_date"] = "2026-09-01",
        ["matches_governed_finding"] = true,
        ["policy_location"] = new JsonObject
        {
            ["root_path"] = "/repo",
            ["source_path"] = "/repo/policy.yml",
            ["role"] = "root",
            ["source_ordinal"] = 0,
            ["declaring_source_path"] = "/repo/policy.yml",
            ["authored_import_path"] = "policy.yml",
            ["import_chain"] = new JsonArray("policy.yml"),
            ["yaml_path"] = "rules[0]",
            ["line"] = 12,
            ["column"] = 4,
            ["contract_family"] = "dependencies",
            ["contract_id"] = "contract-1",
        },
    };

    private static JsonObject ApplicabilityProvenance(string controlIdentity) => new()
    {
        ["family"] = "topology",
        ["control_identity"] = controlIdentity,
        ["policy_identity"] = "policy:topology",
        ["evidence_identity"] = "evidence:topology",
    };

    private static JsonObject ApplicabilityReason(string code, string controlIdentity) => new()
    {
        ["code"] = code,
        ["provenance"] = ApplicabilityProvenance(controlIdentity),
    };

    private static JsonObject FullFinding(string identity) => new()
    {
        ["schema_version"] = 1,
        ["kind"] = "external_diagnostic",
        ["canonical_identity"] = identity,
        ["mode"] = "strict",
        ["severity"] = "error",
        ["message_code"] = "external-finding",
        ["contract"] = "external evidence",
        ["contract_id"] = "sarif",
        ["policy_origin"] = new JsonObject
        {
            ["source_path"] = "/repo/policy.yml",
            ["yaml_path"] = "rules[0]",
        },
        ["source_location"] = new JsonObject { ["path"] = "src/App.cs", ["line"] = 10, ["column"] = 2 },
        ["remediation_guidance"] = new JsonObject
        {
            ["category"] = "external",
            ["summary"] = "Review scanner finding.",
            ["contract_identity"] = "sarif",
            ["finding_identity"] = new JsonObject { ["id"] = identity },
            ["evidence"] = new JsonArray(new JsonObject { ["kind"] = "runtime", ["value"] = "runtime" }),
            ["expected_seam_or_direction"] = "boundary",
            ["caveat"] = "verify",
            ["requires_review"] = true,
        },
        ["details"] = new JsonObject { ["source"] = "scanner" },
    };
}
