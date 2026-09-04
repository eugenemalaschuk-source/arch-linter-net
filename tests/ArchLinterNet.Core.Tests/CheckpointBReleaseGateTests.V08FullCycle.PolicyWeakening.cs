using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static void AssertPolicyContext(CandidatePackageFeed candidate, string root, string outputPath)
    {
        CommandResult context = candidate.RunTool(root,
            "policy", "context",
            "--policy", DependenciesPath(root),
            "--format", "json");
        Assert.That(context.ExitCode, Is.EqualTo(0), $"policy context ({root}): {context.CombinedOutput}");
        File.WriteAllText(outputPath, context.StandardOutput);
    }

    private static CheckpointScenarioResult AssertPolicyWeakeningAndGate(
        CandidatePackageFeed candidate, string root, string baseContext, string currentContext)
    {
        CommandResult weakening = candidate.RunTool(root,
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

        CommandResult gate = candidate.RunTool(root,
            "gate",
            "--policy", DependenciesPath(root),
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

        return Passed("v08-policy-weakening-gate");
    }

    private const string V08ExternalEvidenceFamily = "external_diagnostics";
    private const string V08ExternalEvidenceControlIdentity = "v08-static-analysis";

    private static CheckpointScenarioResult AssertExternalEvidenceBinding(
        CandidatePackageFeed candidate, string root, string validSarifPath, string revision)
    {
        // Wrong-revision required evidence must be unassessable, not silently ignored or treated as
        // a pass: bind the same valid SARIF artifact under a revision that does not match the
        // top-level assessment context. Assert the specific applicability reason
        // (ArchitectureExternalEvidenceApplicabilityProjector.Family="external_diagnostics",
        // SarifEvidenceReader's wire reason code "wrong_external_revision"), not just "some exit 2" --
        // any unrelated preflight/runtime failure would also exit 2 without proving this scenario at
        // all.
        CommandResult wrongRevision = candidate.RunTool(root,
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision=deadbeefdeadbeefdeadbeefdeadbeefdeadbeef,scope={V08EvidenceScope}",
            "--evidence-repository", V08EvidenceRepository,
            "--evidence-revision", revision,
            "--evidence-scope", V08EvidenceScope);
        Assert.That(wrongRevision.ExitCode, Is.EqualTo(2),
            $"v08-external-evidence-binding (wrong revision) expected an unassessable/fail-closed runtime exit: {wrongRevision.CombinedOutput}");
        AssertExternalEvidenceApplicabilityReason(wrongRevision, "wrong_external_revision",
            "v08-external-evidence-binding (wrong revision)");

        // Missing required evidence entirely must also fail closed rather than silently pass, with
        // its own distinct reason code -- proving this is genuinely "no evidence supplied" and not
        // the same wrong-revision path reached by a different means.
        CommandResult missing = candidate.RunTool(root,
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json");
        Assert.That(missing.ExitCode, Is.EqualTo(2),
            $"v08-external-evidence-binding (missing) expected an unassessable/fail-closed runtime exit: {missing.CombinedOutput}");
        AssertExternalEvidenceApplicabilityReason(missing, "missing_required_input",
            "v08-external-evidence-binding (missing)");

        // Valid, correctly bound evidence must produce a genuinely different outcome: complete
        // (evaluable), not unassessable -- proving the two failure cases above are real fail-closed
        // behavior and not simply "this control can never pass".
        CommandResult validEvidence = candidate.RunTool(root,
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision={revision},scope={V08EvidenceScope}",
            "--evidence-repository", V08EvidenceRepository,
            "--evidence-revision", revision,
            "--evidence-scope", V08EvidenceScope);
        using (JsonDocument validDocument = JsonDocument.Parse(validEvidence.StandardOutput))
        {
            JsonElement validControl = FindExternalEvidenceControl(validDocument);
            Assert.That(validControl.GetProperty("state").GetString(), Is.EqualTo("evaluable"),
                $"v08-external-evidence-binding (valid) expected the correctly bound evidence control to be evaluable, not unassessable: {validEvidence.CombinedOutput}");
        }

        return Passed("v08-external-evidence-binding");
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

    private static CheckpointScenarioResult AssertChangeSnapshotAndReport(
        CandidatePackageFeed candidate,
        string baseRoot,
        string currentRoot,
        string baseSnapshotPath,
        string currentSnapshotPath,
        string changeReportPath)
    {
        CommandResult baseSnapshot = candidate.RunTool(baseRoot,
            "change", "snapshot",
            "--policy", DependenciesPath(baseRoot),
            "--mode", "strict",
            "--ensure-built",
            "--output", baseSnapshotPath);
        Assert.That(baseSnapshot.ExitCode, Is.EqualTo(0), $"v08-change-snapshot-report (base): {baseSnapshot.CombinedOutput}");

        CommandResult currentSnapshot = candidate.RunTool(currentRoot,
            "change", "snapshot",
            "--policy", DependenciesPath(currentRoot),
            "--mode", "strict",
            "--ensure-built",
            "--output", currentSnapshotPath);
        Assert.That(currentSnapshot.ExitCode, Is.EqualTo(0), $"v08-change-snapshot-report (current): {currentSnapshot.CombinedOutput}");

        CommandResult report = candidate.RunTool(currentRoot,
            "change", "report",
            "--base", baseSnapshotPath,
            "--current", currentSnapshotPath,
            "--execution-context", "v08-full-cycle",
            "--format", "json",
            "--output", changeReportPath);
        Assert.That(report.ExitCode, Is.EqualTo(0), $"v08-change-snapshot-report (report): {report.CombinedOutput}");
        Assert.That(File.Exists(changeReportPath), Is.True, "v08-change-snapshot-report");

        return Passed("v08-change-snapshot-report");
    }
}
