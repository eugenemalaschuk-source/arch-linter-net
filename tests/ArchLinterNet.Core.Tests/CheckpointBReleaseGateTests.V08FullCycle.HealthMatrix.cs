using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static CheckpointScenarioResult AssertHealthMatrix(
        CandidatePackageFeed candidate,
        string baseRoot,
        string currentRoot,
        string validSarifPath,
        string revision,
        string primaryHealthOutputPath)
    {
        // `health` unconditionally requires --baseline (even for a clean run: "reviewed exact
        // persistent-debt baseline (required)"). `baseline generate --ensure-built` now works
        // against this fixture's target_assemblies (the missing-build-state-options gap it and
        // `measure` shared is fixed -- both now carry --ensure-built/--no-restore/--configuration/
        // --framework/--platform/--runtime), but the empty case is trivial content
        // ("version: 2\nbaseline: {}\n") that needs no --ensure-built round trip, so it stays
        // hand-authored (see V08FullCycleFragmentContent.EmptyBaseline) and is reused for HEALTHY
        // (genuinely nothing to review), FAILING (current's violations stay unreviewed against it,
        // so the failure is genuinely "current strict violation", not "unassessable"), and
        // UNASSESSABLE/DEGRADING below (neither depends on debt actually being reviewed).
        string emptyBaselinePath = Path.Combine(baseRoot, "v08-empty-baseline.arch.yml");
        File.WriteAllText(emptyBaselinePath, V08FullCycleFragmentContent.EmptyBaseline);

        // HEALTHY: the unmodified checked-in fixture, no reviewed debt to carry.
        CommandResult healthy = candidate.RunTool(baseRoot,
            "health",
            "--policy", DependenciesPath(baseRoot),
            "--baseline", emptyBaselinePath,
            "--mode", "strict",
            "--ensure-built",
            "--format", "json");
        AssertHealthState(healthy, "healthy", "pass", "v08-health-healthy");

        // FAILING: the deliberate current-state violations, unreviewed (the empty base baseline
        // covers none of them), with required evidence correctly bound so the failure is genuinely
        // "current strict violation", not "unassessable".
        // --execution-context is required for the health JSON to carry report_evidence at all
        // (ArchitectureHealthReportEvidenceWriter.Format returns the bare summary without it) --
        // report pr/badge both need that evidence, and per the docs guide it must match the change
        // report's own execution context ("v08-full-cycle", used by AssertChangeSnapshotAndReport).
        CommandResult failing = candidate.RunTool(currentRoot,
            "health",
            "--policy", DependenciesPath(currentRoot),
            "--baseline", emptyBaselinePath,
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--execution-context", "v08-full-cycle",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision={revision},scope={V08EvidenceScope}",
            "--evidence-repository", V08EvidenceRepository,
            "--evidence-revision", revision,
            "--evidence-scope", V08EvidenceScope);
        AssertHealthState(failing, "failing", "fail", "v08-health-failing");
        File.WriteAllText(primaryHealthOutputPath, failing.StandardOutput);

        // DEBT: the same current-state violations, reviewed via a baseline covering them exactly.
        // `baseline generate` cannot produce this (see the empty-baseline comment above), so the
        // baseline is instead built from `baseline verify`'s own "new" report against the same live
        // violations -- `verify` does expose --ensure-built (unlike `generate`/`diff`, the latter's
        // --ensure-built/evidence options being documented but not actually wired, a separate gap).
        //
        // The sanity check below independently proves this baseline is byte-correct: a fresh
        // `baseline verify --ensure-built` against it reports both entries "matched", zero
        // resolved/stale. `health --baseline` on the exact same file previously reported the gate
        // failing with reason "resolved_baseline_hygiene" -- a real, confirmed engine bug, now fixed:
        // ArchitectureContractExecutionContext.IsIgnored only ever recorded a baseline candidate for a
        // still-live (unmatched) occurrence, so health's debt-gate snapshot-reuse path
        // (ArchitectureAnalysisSnapshot.CollectBaselineCandidates, which reuses the main
        // baseline-loaded snapshot rather than the standalone flow's baseline-free collection pass)
        // could never see a violation the loaded baseline had already suppressed -- exactly the ones
        // it needs to classify as Frozen/matched. Fixed by recording every occurrence, matched or
        // not, when the caller has no cycle-specific observeCandidate filter of its own.
        string baselinePath = Path.Combine(currentRoot, "v08-baseline.arch.yml");
        string debtBaselineYaml = BuildDebtBaselineFromLiveViolations(candidate, currentRoot, emptyBaselinePath);
        File.WriteAllText(baselinePath, debtBaselineYaml);

        CommandResult debtBaselineSanityCheck = candidate.RunTool(currentRoot,
            "baseline", "verify",
            "--policy", DependenciesPath(currentRoot),
            "--baseline", baselinePath,
            "--mode", "strict",
            "--ensure-built",
            "--format", "json");
        Assert.That(debtBaselineSanityCheck.ExitCode, Is.EqualTo(0),
            $"v08-health-debt (baseline sanity check): {debtBaselineSanityCheck.CombinedOutput}{Environment.NewLine}--- generated baseline ---{Environment.NewLine}{debtBaselineYaml}");

        CommandResult debt = candidate.RunTool(currentRoot,
            "health",
            "--policy", DependenciesPath(currentRoot),
            "--baseline", baselinePath,
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision={revision},scope={V08EvidenceScope}",
            "--evidence-repository", V08EvidenceRepository,
            "--evidence-revision", revision,
            "--evidence-scope", V08EvidenceScope);
        AssertHealthState(debt, "debt", "pass", "v08-health-debt");

        // UNASSESSABLE: required evidence bound to a revision that does not match the assessment
        // context. Reuses the empty baseline -- a wrong-revision evidence mismatch is unassessable
        // regardless of whether debt happens to be reviewed.
        CommandResult unassessable = candidate.RunTool(currentRoot,
            "health",
            "--policy", DependenciesPath(currentRoot),
            "--baseline", emptyBaselinePath,
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision=deadbeefdeadbeefdeadbeefdeadbeefdeadbeef,scope={V08EvidenceScope}",
            "--evidence-repository", V08EvidenceRepository,
            "--evidence-revision", revision,
            "--evidence-scope", V08EvidenceScope);
        AssertHealthState(unassessable, "unassessable", "unassessable", "v08-health-unassessable");

        // DEGRADING (deliberately blocking, per issue #524): a new structured waiver is added to
        // degradingRoot's policy between the base and current policy-context snapshots.
        // docs/policy-format/structured-waivers.md ("New and broadened waivers are blocking change
        // evidence by default") documents this as the canonical deliberately-blocking Degrading
        // proof: ArchitecturePolicyWeakeningWaiverEvaluator flags a waiver present in the current
        // context but absent from the base context as "structured_waiver_added" at the current
        // policy's analysis.policy_weakening severity (default "error", left untouched here), which
        // ArchitectureDebtGateApplicationService.Evaluate folds into !debtGate.Passed -- failing
        // ArchitectureHealthProjector.ResolveGate -- while ProjectPolicyWeakening maps that same
        // finding to the policy_weakening dimension's Degrading state (never Fail), which
        // ResolveHealth reports as the overall Health category since no other dimension is
        // Fail/Unassessable. This is a distinct, dedicated blocking path from waiver *lifecycle* debt
        // (waiver_debt/ProjectWaiverDebt): verified directly against the engine that any blocking
        // lifecycle waiver state (invalid/expired/stale-when-blocking) maps straight to the Fail
        // dimension state, so waiver_debt alone can never produce "degrading" health under a failing
        // gate -- the deliberately-blocking Degrading case must come from policy weakening instead.
        string degradingRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-v08-degrading-{Guid.NewGuid():N}");
        CopyDirectoryExcludingGit(baseRoot, degradingRoot);
        try
        {
            ApplyDegradingWeakeningMutation(degradingRoot);
            string degradingBaseContext = Path.Combine(degradingRoot, "v08-degrading-base-context.json");
            string degradingCurrentContext = Path.Combine(degradingRoot, "v08-degrading-current-context.json");
            AssertPolicyContext(candidate, baseRoot, degradingBaseContext);
            AssertPolicyContext(candidate, degradingRoot, degradingCurrentContext);

            CommandResult degradingWeakening = candidate.RunTool(degradingRoot,
                "policy", "weakening",
                "--base-context", degradingBaseContext,
                "--current-context", degradingCurrentContext);
            // PolicyWeakeningCommandHandler: exit 1 exactly when the comparison found error-severity
            // weakening (result.HasErrors) -- the new structured waiver above must trip it.
            Assert.That(degradingWeakening.ExitCode, Is.EqualTo(1),
                $"v08-health-degrading (policy weakening): {degradingWeakening.CombinedOutput}");

            // No live code violations exist here (degradingRoot is an unmutated base copy plus only
            // a policy-level waiver), so the empty baseline is exactly as reviewed as any other.
            string degradingBaselinePath = Path.Combine(degradingRoot, "v08-degrading-baseline.arch.yml");
            File.WriteAllText(degradingBaselinePath, V08FullCycleFragmentContent.EmptyBaseline);

            CommandResult degrading = candidate.RunTool(degradingRoot,
                "health",
                "--policy", DependenciesPath(degradingRoot),
                "--baseline", degradingBaselinePath,
                "--base-context", degradingBaseContext,
                "--current-context", degradingCurrentContext,
                "--mode", "strict",
                "--ensure-built",
                "--format", "json");
            AssertHealthState(degrading, "degrading", "fail", "v08-health-degrading");
        }
        finally
        {
            DeleteDirectoryEventually(degradingRoot);
        }

        return Passed("v08-health-matrix");
    }

    // Advisory Degrading (issue #524's second Degrading variant, distinct from the blocking case
    // above): the same structured_waiver_added mechanism, but analysis.policy_weakening: warn keeps
    // ArchitectureDebtGateApplicationService.Evaluate's !weakening.HasErrors term satisfied (only
    // error-severity findings count), so the gate passes while
    // ArchitectureHealthProjector.ProjectPolicyWeakening still reports the policy_weakening dimension
    // Degrading -- it maps any non-empty findings regardless of severity. Registered as its own
    // required scenario so a regression collapsing both Degrading variants into one gate outcome
    // cannot hide behind the aggregate v08-health-matrix pass.
    private static CheckpointScenarioResult AssertHealthMatrixAdvisoryDegrading(CandidatePackageFeed candidate, string baseRoot)
    {
        string advisoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-v08-degrading-advisory-{Guid.NewGuid():N}");
        CopyDirectoryExcludingGit(baseRoot, advisoryRoot);
        try
        {
            ApplyWeakeningMutation(advisoryRoot, policyWeakeningSeverity: "warn");
            string advisoryBaseContext = Path.Combine(advisoryRoot, "v08-degrading-advisory-base-context.json");
            string advisoryCurrentContext = Path.Combine(advisoryRoot, "v08-degrading-advisory-current-context.json");
            AssertPolicyContext(candidate, baseRoot, advisoryBaseContext);
            AssertPolicyContext(candidate, advisoryRoot, advisoryCurrentContext);

            CommandResult advisoryWeakening = candidate.RunTool(advisoryRoot,
                "policy", "weakening",
                "--base-context", advisoryBaseContext,
                "--current-context", advisoryCurrentContext);
            // Only warn-severity findings exist here, so PolicyWeakeningCommandHandler's exit-1
            // (HasErrors) condition must NOT trip -- the command still reports the finding, just at a
            // severity that does not itself fail CI.
            Assert.That(advisoryWeakening.ExitCode, Is.EqualTo(0),
                $"v08-health-degrading-advisory (policy weakening): {advisoryWeakening.CombinedOutput}");

            string advisoryBaselinePath = Path.Combine(advisoryRoot, "v08-degrading-advisory-baseline.arch.yml");
            File.WriteAllText(advisoryBaselinePath, V08FullCycleFragmentContent.EmptyBaseline);

            CommandResult advisory = candidate.RunTool(advisoryRoot,
                "health",
                "--policy", DependenciesPath(advisoryRoot),
                "--baseline", advisoryBaselinePath,
                "--base-context", advisoryBaseContext,
                "--current-context", advisoryCurrentContext,
                "--mode", "strict",
                "--ensure-built",
                "--format", "json");
            AssertHealthState(advisory, "degrading", "pass", "v08-health-degrading-advisory");
        }
        finally
        {
            DeleteDirectoryEventually(advisoryRoot);
        }

        return Passed("v08-health-degrading-advisory");
    }

    // `baseline generate` cannot produce a baseline for this fixture (see AssertHealthMatrix), so
    // the DEBT scenario's baseline is instead assembled from `baseline verify`'s own "new" report
    // (the exact structured identity of each live, currently-unreviewed violation) run against an
    // empty baseline. This is the same content a human would get from `baseline generate` were it
    // able to run, discovered fresh from this exact candidate/build rather than hardcoded.
    private static string BuildDebtBaselineFromLiveViolations(
        CandidatePackageFeed candidate, string root, string emptyBaselinePath)
    {
        CommandResult verify = candidate.RunTool(root,
            "baseline", "verify",
            "--policy", DependenciesPath(root),
            "--baseline", emptyBaselinePath,
            "--mode", "strict",
            "--ensure-built",
            "--format", "json");
        Assert.That(verify.ExitCode, Is.EqualTo(1),
            $"v08-health-debt (baseline verify discovery): {verify.CombinedOutput}");

        using JsonDocument document = JsonDocument.Parse(verify.StandardOutput);
        JsonElement newEntries = document.RootElement.GetProperty("new");
        Assert.That(newEntries.GetArrayLength(), Is.GreaterThanOrEqualTo(2),
            $"v08-health-debt (baseline verify discovery) expected both live violations: {verify.StandardOutput}");

        var groups = new Dictionary<string, List<(string ContractId, JsonElement Entry)>>(StringComparer.Ordinal);
        foreach (JsonElement entry in newEntries.EnumerateArray())
        {
            string group = entry.GetProperty("contract_group").GetString()!;
            string contractId = entry.GetProperty("contract_id").GetString()!;
            if (!groups.TryGetValue(group, out List<(string ContractId, JsonElement Entry)>? entries))
            {
                entries = new List<(string, JsonElement)>();
                groups[group] = entries;
            }

            entries.Add((contractId, entry));
        }

        var yaml = new StringBuilder();
        yaml.AppendLine("version: 2");
        yaml.AppendLine("baseline:");
        foreach ((string group, List<(string ContractId, JsonElement Entry)> entries) in groups)
        {
            yaml.AppendLine($"  {group}:");
            foreach (IGrouping<string, JsonElement> byContract in entries
                .GroupBy(static pair => pair.ContractId, static pair => pair.Entry, StringComparer.Ordinal))
            {
                yaml.AppendLine($"    - id: {YamlScalar(byContract.Key)}");
                yaml.AppendLine("      ignored_violations:");
                foreach (JsonElement entry in byContract)
                {
                    JsonElement identity = entry.GetProperty("identity");
                    yaml.AppendLine($"        - source_type: {YamlScalar(entry.GetProperty("source_type").GetString()!)}");
                    yaml.AppendLine($"          forbidden_reference: {YamlScalar(entry.GetProperty("forbidden_reference").GetString()!)}");
                    yaml.AppendLine("          reason: v08 full-cycle reviewed debt fixture");
                    yaml.AppendLine($"          identity_version: {identity.GetProperty("identityVersion").GetInt32()}");
                    yaml.AppendLine($"          contract_family: {YamlScalar(identity.GetProperty("contractFamily").GetString()!)}");
                    yaml.AppendLine($"          kind: {YamlScalar(identity.GetProperty("kind").GetString()!)}");
                    AppendOptionalYamlField(yaml, "source_assembly", identity.GetProperty("sourceAssembly"));
                    AppendOptionalYamlField(yaml, "source_member", identity.GetProperty("sourceMember"));
                    AppendOptionalYamlField(yaml, "target_assembly", identity.GetProperty("targetAssembly"));
                    AppendOptionalYamlField(yaml, "target_type", identity.GetProperty("targetType"));
                    AppendOptionalYamlField(yaml, "target_member", identity.GetProperty("targetMember"));
                    yaml.AppendLine($"          occurrence: {identity.GetProperty("occurrence").GetInt32()}");
                    AppendOptionalYamlField(yaml, "configuration", identity.GetProperty("configuration"));
                }
            }
        }

        return yaml.ToString();
    }

    private static void AppendOptionalYamlField(StringBuilder yaml, string fieldName, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        yaml.AppendLine($"          {fieldName}: {YamlScalar(value.GetString()!)}");
    }

    private static string YamlScalar(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    // "Each contract and its ignored_violations remain one atomic item owned by one source" (see
    // docs/policy-format/imports.md) -- a second fragment cannot redeclare
    // modules-never-reference-the-host to attach a waiver (composition rejects it as a duplicate
    // contract id), so the waiver is added by editing the existing fragment's own copy in place
    // (degradingRoot is a disposable temp copy; the checked-in fixture is never touched).
    //
    // The waiver uses the full structured shape from docs/policy-format/structured-waivers.md
    // (id/target.fingerprint/owner/issue/introduced/expires), not a legacy matcher-only entry:
    // ArchitecturePolicyWeakeningWaiverEvaluator keys waivers by (mode, contract_family, contract_id,
    // waiver_id) and only a structured entry carries a waiver id to key on -- this is the mechanism
    // that makes the base/current policy-context comparison see it as "structured_waiver_added" at
    // all, independent of the fingerprint actually matching a live violation.
    private static void ApplyDegradingWeakeningMutation(string root) => ApplyWeakeningMutation(root, policyWeakeningSeverity: null);

    // policyWeakeningSeverity: null leaves analysis.policy_weakening at its documented default
    // ("error"), producing the blocking Degrading case (gate=fail). An explicit "warn" instead
    // produces the advisory Degrading case (gate=pass): ArchitecturePolicyWeakeningModels.HasErrors
    // only counts error-severity findings (feeding ArchitectureDebtGateApplicationService.Evaluate's
    // !weakening.HasErrors term), while ArchitectureHealthProjector.ProjectPolicyWeakening maps ANY
    // non-empty findings to the Degrading dimension state regardless of severity -- the same
    // structured_waiver_added mechanism, just with the gate consequence toggled by policy severity.
    private static void ApplyWeakeningMutation(string root, string? policyWeakeningSeverity)
    {
        string fragmentPath = Path.Combine(root, "fragments", "module-contracts.yml");
        List<string> lines = File.ReadAllLines(fragmentPath).ToList();
        int anchorIndex = lines.FindIndex(static line =>
            line.TrimEnd() == "      reason: Modules must not depend on the composition host that wires them together.");
        if (anchorIndex < 0)
        {
            throw new InvalidOperationException(
                $"'{fragmentPath}' no longer matches the expected modules-never-reference-the-host shape.");
        }

        string introduced = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        string expires = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        lines.InsertRange(anchorIndex + 1,
        [
            "      ignored_violations:",
            "        - id: ARCH-IGN-V08-DEGRADING",
            "          source_type: Synthetic.Modules.M20.Module",
            "          forbidden_reference: Synthetic.Composition",
            "          target:",
            "            fingerprint: sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "          reason: Temporary reviewed exception pending module extraction (synthetic, deliberately introduced for the v0.8 degrading Health proof).",
            "          owner: architecture-team",
            "          issue: ARCH-524",
            $"          introduced: {introduced}",
            $"          expires: {expires}",
        ]);
        File.WriteAllLines(fragmentPath, lines);

        // The waiver deliberately matches no live violation (M20 never actually references the
        // host), which is exactly what makes it stale waiver-lifecycle evidence. analysis
        // .unmatched_ignored_violations defaults to "error", which would otherwise turn that
        // mismatch into a hard strict-validation failure and drag the whole debt-gate down to
        // unassessable ("baseline_verification_incomplete") -- collapsing the very degrading signal
        // this scenario exists to isolate. Downgrading to "warn" here keeps that noise out of
        // current_evaluation/reviewed_finding_debt; the policy-weakening comparison that actually
        // drives this scenario is a separate mechanism (--base-context/--current-context), unaffected
        // by this setting.
        string policyPath = DependenciesPath(root);
        string policy = File.ReadAllText(policyPath);
        const string AnalysisMarker = "analysis:";
        int analysisIndex = policy.IndexOf(AnalysisMarker, StringComparison.Ordinal);
        if (analysisIndex < 0)
        {
            throw new InvalidOperationException($"'{policyPath}' has no analysis: block to extend.");
        }

        int analysisInsertAt = analysisIndex + AnalysisMarker.Length;
        string severityLine = policyWeakeningSeverity is null
            ? string.Empty
            : $"{Environment.NewLine}  policy_weakening: {policyWeakeningSeverity}";
        policy = policy.Insert(analysisInsertAt, $"{severityLine}{Environment.NewLine}  unmatched_ignored_violations: warn");
        File.WriteAllText(policyPath, policy);
    }

    private static void AssertHealthState(
        CommandResult result, string expectedHealth, string expectedGate, string scenarioId, string? extraDiagnostic = null)
    {
        Assert.That(result.ExitCode, Is.AnyOf(0, 1, 2), $"{scenarioId}: {result.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        string? health = document.RootElement.TryGetProperty("health", out JsonElement healthElement)
            ? healthElement.GetString()
            : null;
        string? gate = document.RootElement.TryGetProperty("gate", out JsonElement gateElement)
            ? gateElement.GetString()
            : null;
        string message = extraDiagnostic is null
            ? $"{scenarioId}: {result.StandardOutput}"
            : $"{scenarioId}: {result.StandardOutput}{Environment.NewLine}{extraDiagnostic}";
        Assert.Multiple(() =>
        {
            Assert.That(health, Is.EqualTo(expectedHealth), message);
            Assert.That(gate, Is.EqualTo(expectedGate), message);
        });
    }
}
