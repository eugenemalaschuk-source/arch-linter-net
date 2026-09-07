using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

using CheckpointScenarioResult = CheckpointBReleaseGateTests.CheckpointScenarioResult;
using CommandResult = CheckpointBReleaseGateTests.CommandResult;

/// <summary>
/// The v0.8 full-cycle scenario's policy-weakening/gate, external-evidence-binding, and
/// change-snapshot/report phases.
/// </summary>
internal sealed class CheckpointBV08PolicyWeakeningPhases(CheckpointBV08ToolRunner runner)
{
    private const string V08ExternalEvidenceFamily = "external_diagnostics";
    private const string V08ExternalEvidenceControlIdentity = "v08-static-analysis";

    private readonly CheckpointBV08ToolRunner _runner = runner;

    internal CheckpointScenarioResult AssertPolicyWeakeningAndGate(string root, string baseContext, string currentContext)
    {
        CommandResult weakening = _runner.RunToolWithReusedRestore(root,
            "policy", "weakening",
            "--base-context", baseContext,
            "--current-context", currentContext);
        // New contracts (topology, exposure, budget, external evidence) were added between base and
        // current: this is new declared scope, not itself a policy relaxation, so weakening exits 0.
        Assert.That(weakening.ExitCode, Is.EqualTo(0), $"v08-policy-weakening-gate (weakening): {weakening.CombinedOutput}");

        // `gate` requires --baseline; without it the handler rejects the invocation as invalid
        // arguments (exit 2) before ArchitectureDebtGateApplicationService ever runs, which would
        // make this scenario pass on a CLI usage error instead of on an actual gate decision. Supply
        // the canonical empty baseline explicitly, so the deliberate exposure/budget violations
        // introduced between base and current genuinely surface as unreviewed new persistent debt.
        string gateBaselinePath = Path.Combine(root, "v08-policy-weakening-gate-baseline.arch.yml");
        File.WriteAllText(gateBaselinePath, V08FullCycleFragmentContent.EmptyBaseline);

        CommandResult gate = _runner.RunToolWithReusedRestore(root,
            "gate",
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(root),
            "--baseline", gateBaselinePath,
            "--base-context", baseContext,
            "--current-context", currentContext,
            "--mode", "all",
            "--ensure-built",
            "--format", "json");
        // Exit 1 is specifically "new/resolved/stale/ambiguous persistent debt or error-severity
        // policy weakening" (see GateCommandHandler's documented exit codes) -- exit 2 would mean the
        // gate itself never completed a comparison, which must not count as this scenario passing.
        Assert.That(gate.ExitCode, Is.EqualTo(1), $"v08-policy-weakening-gate (gate): {gate.CombinedOutput}");

        using JsonDocument gateJson = JsonDocument.Parse(gate.StandardOutput);
        JsonElement gateRoot = gateJson.RootElement;
        Assert.That(gateRoot.GetProperty("succeeded").GetBoolean(), Is.True,
            $"v08-policy-weakening-gate expected the gate comparison itself to complete: {gate.StandardOutput}");
        Assert.That(gateRoot.GetProperty("passed").GetBoolean(), Is.False, "v08-policy-weakening-gate");

        JsonElement[] newDebtEntries = gateRoot.GetProperty("persistent_debt").GetProperty("entries")
            .EnumerateArray()
            .Where(entry => entry.GetProperty("status").GetString() == "new")
            .ToArray();
        Assert.That(newDebtEntries, Is.Not.Empty,
            $"v08-policy-weakening-gate expected the deliberate exposure/budget violations to surface as new persistent debt records: {gate.StandardOutput}");

        return CheckpointBReleaseGateTests.Passed("v08-policy-weakening-gate");
    }

    internal CheckpointScenarioResult AssertExternalEvidenceBinding(string root, string validSarifPath, string revision)
    {
        // Wrong-revision required evidence must be unassessable, not silently ignored or treated as
        // a pass: bind the same valid SARIF artifact under a revision that does not match the
        // top-level assessment context. Assert the specific applicability reason
        // (ArchitectureExternalEvidenceApplicabilityProjector.Family="external_diagnostics",
        // SarifEvidenceReader's wire reason code "wrong_external_revision"), not just "some exit 2" --
        // any unrelated preflight/runtime failure would also exit 2 without proving this scenario at
        // all.
        CommandResult wrongRevision = _runner.RunToolWithReusedRestore(root,
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={CheckpointBV08EvidenceIdentity.RelativePath},repository={CheckpointBV08EvidenceIdentity.Repository},revision=deadbeefdeadbeefdeadbeefdeadbeefdeadbeef,scope={CheckpointBV08EvidenceIdentity.Scope}",
            "--evidence-repository", CheckpointBV08EvidenceIdentity.Repository,
            "--evidence-revision", revision,
            "--evidence-scope", CheckpointBV08EvidenceIdentity.Scope);
        Assert.That(wrongRevision.ExitCode, Is.EqualTo(2),
            $"v08-external-evidence-binding (wrong revision) expected an unassessable/fail-closed runtime exit: {wrongRevision.CombinedOutput}");
        AssertExternalEvidenceApplicabilityReason(wrongRevision, "wrong_external_revision",
            "v08-external-evidence-binding (wrong revision)");

        // Missing required evidence entirely must also fail closed rather than silently pass, with
        // its own distinct reason code -- proving this is genuinely "no evidence supplied" and not
        // the same wrong-revision path reached by a different means.
        CommandResult missing = _runner.RunToolWithReusedRestore(root,
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json");
        Assert.That(missing.ExitCode, Is.EqualTo(2),
            $"v08-external-evidence-binding (missing) expected an unassessable/fail-closed runtime exit: {missing.CombinedOutput}");
        AssertExternalEvidenceApplicabilityReason(missing, "missing_required_input",
            "v08-external-evidence-binding (missing)");

        // Wrong-scope required evidence must fail closed the same way, with its own distinct reason
        // code -- otherwise scope binding could silently stop being enforced without any scenario
        // catching it.
        CommandResult wrongScope = _runner.RunToolWithReusedRestore(root,
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={CheckpointBV08EvidenceIdentity.RelativePath},repository={CheckpointBV08EvidenceIdentity.Repository},revision={revision},scope=audit",
            "--evidence-repository", CheckpointBV08EvidenceIdentity.Repository,
            "--evidence-revision", revision,
            "--evidence-scope", CheckpointBV08EvidenceIdentity.Scope);
        Assert.That(wrongScope.ExitCode, Is.EqualTo(2),
            $"v08-external-evidence-binding (wrong scope) expected an unassessable/fail-closed runtime exit: {wrongScope.CombinedOutput}");
        AssertExternalEvidenceApplicabilityReason(wrongScope, "wrong_external_scope",
            "v08-external-evidence-binding (wrong scope)");

        // Valid, correctly bound evidence must produce a genuinely different outcome: complete
        // (evaluable), not unassessable -- proving the failure cases above are real fail-closed
        // behavior and not simply "this control can never pass". Routed through `health` (with
        // --execution-context, required for report_evidence to populate at all) rather than the bare
        // root command, so the actual provenance facts #524 requires -- logical id, consumed-byte
        // SHA-256, repository/revision/scope, zero-result count, and the canonical trust receipt --
        // can be verified, not just the applicability control's state.
        string validSarifSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(validSarifPath)));
        string validBaselinePath = Path.Combine(root, "v08-external-evidence-binding-baseline.arch.yml");
        File.WriteAllText(validBaselinePath, V08FullCycleFragmentContent.EmptyBaseline);
        string validReportPath = Path.Combine(root, "v08-external-evidence-binding-health.json");

        CommandResult validEvidence = _runner.RunToolWithReusedRestore(root,
            "health",
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(root),
            "--baseline", validBaselinePath,
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--execution-context", "v08-external-evidence-binding",
            "--external-evidence", $"id=v08-static-analysis,path={CheckpointBV08EvidenceIdentity.RelativePath},repository={CheckpointBV08EvidenceIdentity.Repository},revision={revision},scope={CheckpointBV08EvidenceIdentity.Scope}",
            "--evidence-repository", CheckpointBV08EvidenceIdentity.Repository,
            "--evidence-revision", revision,
            "--evidence-scope", CheckpointBV08EvidenceIdentity.Scope);
        File.WriteAllText(validReportPath, validEvidence.StandardOutput);

        using (JsonDocument validDocument = JsonDocument.Parse(validEvidence.StandardOutput))
        {
            // Correctly bound evidence must genuinely reach "current" trust state below -- proving the
            // wrong-revision/missing/wrong-scope failure cases above are real fail-closed behavior and
            // not simply "this control can never pass". health's JSON shape carries applicability
            // under report_evidence, not the top-level assessment_completion the bare root command
            // uses (see FindExternalEvidenceControl), so the trust-receipt checks below are the
            // equivalent proof for this health-routed call.
            JsonElement receipt = validDocument.RootElement.GetProperty("report_evidence").GetProperty("validation_outcomes")
                .EnumerateArray()
                .Single(outcome => outcome.GetProperty("mode").GetString() == "strict")
                .GetProperty("external_evidence").GetProperty("trust_receipts")
                .EnumerateArray()
                .Single(item => item.GetProperty("logical_id").GetString() == "v08-static-analysis");
            JsonElement context = receipt.GetProperty("context");
            Assert.Multiple(() =>
            {
                Assert.That(receipt.GetProperty("state").GetString(), Is.EqualTo("current"),
                    $"v08-external-evidence-binding (valid) trust receipt: {validEvidence.StandardOutput}");
                Assert.That(receipt.GetProperty("artifact_sha256").GetString(), Is.EqualTo(validSarifSha256),
                    "v08-external-evidence-binding (valid) expected the trust receipt to bind the exact consumed bytes of validSarifPath.");
                Assert.That(receipt.GetProperty("result_count").GetInt32(), Is.EqualTo(0),
                    "v08-external-evidence-binding (valid) expected the zero-result SARIF artifact's result_count to be recorded as 0.");
                Assert.That(context.GetProperty("repository").GetString(), Is.EqualTo(CheckpointBV08EvidenceIdentity.Repository));
                Assert.That(context.GetProperty("revision").GetString(), Is.EqualTo(revision));
                Assert.That(context.GetProperty("scope").GetString(), Is.EqualTo(CheckpointBV08EvidenceIdentity.Scope));
            });
        }

        // The generated PR report must still be bound to this exact Health evidence, not a
        // recomputation: the same logical id and trust state must appear in the rendered Markdown.
        // `report pr` also requires --change; a trivial base==current snapshot pair (no delta of
        // interest to this scenario) satisfies that requirement without duplicating the dedicated
        // v08-change-snapshot-report scenario's own bounded-delta proof.
        string trivialSnapshotPath = Path.Combine(root, "v08-external-evidence-binding-snapshot.json");
        CommandResult trivialSnapshot = _runner.RunToolWithReusedRestore(root,
            "change", "snapshot",
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--output", trivialSnapshotPath);
        Assert.That(trivialSnapshot.ExitCode, Is.EqualTo(0), $"v08-external-evidence-binding (change snapshot): {trivialSnapshot.CombinedOutput}");

        string trivialChangeReportPath = Path.Combine(root, "v08-external-evidence-binding-change.json");
        CommandResult trivialChangeReport = _runner.RunToolWithReusedRestore(root,
            "change", "report",
            "--base", trivialSnapshotPath,
            "--current", trivialSnapshotPath,
            "--execution-context", "v08-external-evidence-binding",
            "--format", "json",
            "--output", trivialChangeReportPath);
        Assert.That(trivialChangeReport.ExitCode, Is.EqualTo(0), $"v08-external-evidence-binding (change report): {trivialChangeReport.CombinedOutput}");

        string validReportOutputPath = Path.Combine(root, "v08-external-evidence-binding-report.md");
        CommandResult reportPr = _runner.RunToolWithReusedRestore(root,
            "report", "pr",
            "--health", validReportPath,
            "--change", trivialChangeReportPath,
            "--output", validReportOutputPath);
        Assert.That(reportPr.ExitCode, Is.EqualTo(0), $"v08-external-evidence-binding (report pr): {reportPr.CombinedOutput}");
        string reportMarkdown = File.ReadAllText(validReportOutputPath);
        Assert.That(reportMarkdown, Does.Contain("logical_evidence=`v08-static-analysis` state=`current`"),
            $"v08-external-evidence-binding expected the PR report to stay bound to the same canonical trust receipt: {reportMarkdown}");

        return CheckpointBReleaseGateTests.Passed("v08-external-evidence-binding");
    }

    private static void AssertExternalEvidenceApplicabilityReason(CommandResult result, string expectedReasonCode, string scenarioLabel)
    {
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement control = FindExternalEvidenceControl(document);
        Assert.Multiple(() =>
        {
            Assert.That(control.GetProperty("state").GetString(), Is.EqualTo("unassessable"),
                $"{scenarioLabel}: {result.CombinedOutput}");
            string[] reasonCodes = control.GetProperty("record").GetProperty("reasons").EnumerateArray()
                .Select(reason => reason.GetProperty("code").GetString())
                .Where(code => !string.IsNullOrEmpty(code))
                .Select(code => code!)
                .ToArray();
            Assert.That(reasonCodes, Does.Contain(expectedReasonCode),
                $"{scenarioLabel} expected reason code '{expectedReasonCode}', got [{string.Join(",", reasonCodes)}]: {result.CombinedOutput}");
        });
    }

    private static JsonElement FindExternalEvidenceControl(JsonDocument document)
    {
        JsonElement control = document.RootElement.GetProperty("assessment_completion").GetProperty("controls")
            .EnumerateArray()
            .SingleOrDefault(candidate => candidate.GetProperty("family").GetString() == V08ExternalEvidenceFamily
                && candidate.GetProperty("control_identity").GetString() == V08ExternalEvidenceControlIdentity);
        Assert.That(control.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined),
            $"expected an assessment_completion control for family='{V08ExternalEvidenceFamily}' control_identity='{V08ExternalEvidenceControlIdentity}': {document.RootElement}");
        return control;
    }

    internal CheckpointScenarioResult AssertChangeSnapshotAndReport(
        string baseRoot,
        string currentRoot,
        string baseSnapshotPath,
        string currentSnapshotPath,
        string changeReportPath)
    {
        CommandResult baseSnapshot = _runner.RunToolWithReusedRestore(baseRoot,
            "change", "snapshot",
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(baseRoot),
            "--mode", "strict",
            "--ensure-built",
            "--output", baseSnapshotPath);
        Assert.That(baseSnapshot.ExitCode, Is.EqualTo(0), $"v08-change-snapshot-report (base): {baseSnapshot.CombinedOutput}");

        CommandResult currentSnapshot = _runner.RunToolWithReusedRestore(currentRoot,
            "change", "snapshot",
            "--policy", CheckpointBV08ToolRunner.DependenciesPath(currentRoot),
            "--mode", "strict",
            "--ensure-built",
            "--output", currentSnapshotPath);
        Assert.That(currentSnapshot.ExitCode, Is.EqualTo(0), $"v08-change-snapshot-report (current): {currentSnapshot.CombinedOutput}");

        CommandResult report = _runner.RunToolWithReusedRestore(currentRoot,
            "change", "report",
            "--base", baseSnapshotPath,
            "--current", currentSnapshotPath,
            "--execution-context", "v08-full-cycle",
            "--format", "json",
            "--output", changeReportPath);
        Assert.That(report.ExitCode, Is.EqualTo(0), $"v08-change-snapshot-report (report): {report.CombinedOutput}");
        Assert.That(File.Exists(changeReportPath), Is.True, "v08-change-snapshot-report");

        // Identical base/current snapshots, or an empty report, must not satisfy this scenario:
        // ApplyV08CurrentMutations introduces a genuinely new namespace
        // (Synthetic.Modules.M01.Internal, holding ModuleInternalState) and the exposure violation
        // that references it -- assert both survive into the change report's bounded delta, not just
        // that the command exited 0 and wrote a file.
        using JsonDocument changeDocument = JsonDocument.Parse(File.ReadAllText(changeReportPath));
        JsonElement changeRoot = changeDocument.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(changeRoot.GetProperty("execution_context").GetProperty("execution_id").GetString(),
                Is.EqualTo("v08-full-cycle"), $"v08-change-snapshot-report (report): {report.StandardOutput}");

            bool addedNewNamespace = changeRoot.GetProperty("added").EnumerateArray().Any(entry =>
                entry.GetProperty("kind").GetString() == "namespace"
                && entry.GetProperty("identity").GetString() == "Synthetic.Modules.M01.Internal");
            Assert.That(addedNewNamespace, Is.True,
                $"v08-change-snapshot-report expected the new Synthetic.Modules.M01.Internal namespace to appear in "
                + $"the report's added surfaces: {report.StandardOutput}");

            bool newExposureFinding = changeRoot.GetProperty("new_findings").EnumerateArray().Any(finding =>
            {
                using JsonDocument identity = JsonDocument.Parse(finding.GetProperty("identity").GetString()!);
                return identity.RootElement.GetProperty("contract_id").GetString() == "m01-contracts-do-not-expose-internal-state";
            });
            Assert.That(newExposureFinding, Is.True,
                $"v08-change-snapshot-report expected the deliberate contract-surface-exposure violation to appear "
                + $"in the report's new findings, bound to ModuleInternalState's exposure: {report.StandardOutput}");
        });

        return CheckpointBReleaseGateTests.Passed("v08-change-snapshot-report");
    }
}
