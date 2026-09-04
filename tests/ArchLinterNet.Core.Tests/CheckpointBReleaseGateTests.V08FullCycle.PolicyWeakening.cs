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

        CommandResult gate = candidate.RunTool(root,
            "gate",
            "--policy", DependenciesPath(root),
            "--base-context", baseContext,
            "--current-context", currentContext,
            "--mode", "all",
            "--ensure-built");
        // No baseline is supplied, so the deliberate exposure/budget violations are unreviewed new
        // debt: gate must fail CI on them.
        Assert.That(gate.ExitCode, Is.Not.EqualTo(0), $"v08-policy-weakening-gate (gate): {gate.CombinedOutput}");

        return Passed("v08-policy-weakening-gate");
    }

    private static CheckpointScenarioResult AssertExternalEvidenceBinding(
        CandidatePackageFeed candidate, string root, string validSarifPath, string revision)
    {
        // Wrong-revision required evidence must be unassessable, not silently ignored or treated as
        // a pass: bind the same valid SARIF artifact under a revision that does not match the
        // top-level assessment context.
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

        // Missing required evidence entirely must also fail closed rather than silently pass.
        CommandResult missing = candidate.RunTool(root,
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json");
        Assert.That(missing.ExitCode, Is.EqualTo(2),
            $"v08-external-evidence-binding (missing) expected an unassessable/fail-closed runtime exit: {missing.CombinedOutput}");

        return Passed("v08-external-evidence-binding");
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
