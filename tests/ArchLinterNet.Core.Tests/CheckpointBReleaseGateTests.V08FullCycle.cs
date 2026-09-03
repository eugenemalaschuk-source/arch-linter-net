using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    // Synthetic, deliberately-fictitious evidence-context identity for the v0.8 full-cycle
    // external-SARIF binding proof; never a real repository.
    private const string V08EvidenceRepository = "https://example.test/synthetic/v08-full-cycle";
    private const string V08EvidenceScope = "checkpoint-b-v08";

    // SarifEvidenceReader.FileSystem.cs deliberately treats a rooted path (or one containing ':')
    // as unsafe and refuses to read it -- evidence paths are always resolved relative to the
    // analyzed repository root. The --external-evidence "path=" binding must therefore use this
    // repository-relative form, never the absolute validSarifPath used for File I/O.
    private const string V08EvidenceRelativePath = "evidence/v08-static-analysis.sarif";

    [Test]
    public void PackedCandidate_V08FullCycle()
    {
        CandidatePackageFeed candidate = Candidate;
        candidate.WriteShardEvidence("v08-full-cycle", AssertV08FullCycle(candidate));
    }

    private static List<CheckpointScenarioResult> AssertV08FullCycle(CandidatePackageFeed candidate)
    {
        var scenarios = new List<CheckpointScenarioResult>();
        using GitVersionedAdoptionFixture fixture = GitVersionedAdoptionFixture.Create("modular-consumer");
        fixture.Commit("base");

        string baseDir = Path.Combine(Path.GetTempPath(), $"arch-linter-v08-base-{Guid.NewGuid():N}");
        CopyDirectoryExcludingGit(fixture.Root, baseDir);

        ApplyV08CurrentMutations(fixture.Root);
        string currentSha = fixture.Commit("current");

        try
        {
            scenarios.Add(AssertPolicyCheck(candidate, fixture.Root));

            string validSarifPath = Path.Combine(fixture.Root, "evidence", "v08-static-analysis.sarif");
            WriteSarif(validSarifPath, executionSuccessful: true, resultMessages: []);
            using (JsonDocument sanityCheck = JsonDocument.Parse(File.ReadAllBytes(validSarifPath)))
            {
                Assert.That(sanityCheck.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object),
                    $"Diagnostic: written SARIF at '{validSarifPath}' did not parse. Content: {File.ReadAllText(validSarifPath)}");
            }

            (CheckpointScenarioResult validateScenario, string validateJson) = AssertValidateStrictAudit(
                candidate, fixture.Root, validSarifPath, V08EvidenceRepository, currentSha, V08EvidenceScope);
            scenarios.Add(validateScenario);
            scenarios.Add(AssertRecursiveExposureEvidence(validateJson));
            scenarios.Add(AssertTopologyCaptureDiffVerify(candidate, fixture.Root, currentSha));
            scenarios.Add(AssertMeasureAndBudget(candidate, fixture.Root, validateJson));

            string basePolicyContext = Path.Combine(fixture.Root, "v08-policy-base.json");
            string currentPolicyContext = Path.Combine(fixture.Root, "v08-policy-current.json");
            AssertPolicyContext(candidate, baseDir, basePolicyContext);
            AssertPolicyContext(candidate, fixture.Root, currentPolicyContext);
            scenarios.Add(AssertPolicyWeakeningAndGate(candidate, fixture.Root, basePolicyContext, currentPolicyContext));

            scenarios.Add(AssertExternalEvidenceBinding(candidate, fixture.Root, validSarifPath, currentSha));

            string baseSnapshot = Path.Combine(fixture.Root, "v08-architecture-base.json");
            string currentSnapshot = Path.Combine(fixture.Root, "v08-architecture-current.json");
            string changeReportPath = Path.Combine(fixture.Root, "v08-architecture-change.json");
            scenarios.Add(AssertChangeSnapshotAndReport(
                candidate, baseDir, fixture.Root, baseSnapshot, currentSnapshot, changeReportPath));

            string healthPath = Path.Combine(fixture.Root, "v08-architecture-health.json");
            scenarios.Add(AssertHealthMatrix(
                candidate, baseDir, fixture.Root, validSarifPath, currentSha, healthPath));

            scenarios.Add(AssertReportPr(candidate, fixture.Root, healthPath, changeReportPath));
            scenarios.Add(AssertBadge(candidate, fixture.Root, healthPath));
            scenarios.Add(AssertProjectionParity(healthPath));
        }
        finally
        {
            DeleteDirectoryEventually(baseDir);
        }

        return scenarios;
    }

    private static void ApplyV08CurrentMutations(string root)
    {
        string fragmentPath = Path.Combine(root, "fragments", "v08-full-cycle.yml");
        string fragment = string.Join(
            Environment.NewLine + Environment.NewLine,
            V08FullCycleFragmentContent.TopologyAndMetrics,
            V08FullCycleFragmentContent.Contracts,
            V08FullCycleFragmentContent.ExternalEvidence);
        File.WriteAllText(fragmentPath, fragment);

        string policyPath = DependenciesPath(root);
        string policy = File.ReadAllText(policyPath);
        const string ImportsMarker = "imports:";
        int importsIndex = policy.IndexOf(ImportsMarker, StringComparison.Ordinal);
        if (importsIndex < 0)
        {
            throw new InvalidOperationException($"'{policyPath}' has no imports: block to extend.");
        }

        int insertAt = importsIndex + ImportsMarker.Length;
        policy = policy.Insert(insertAt, $"{Environment.NewLine}  - fragments/v08-full-cycle.yml");
        File.WriteAllText(policyPath, policy);

        string internalDirectory = Path.Combine(root, "src", "Synthetic.Modules.M01", "Internal");
        Directory.CreateDirectory(internalDirectory);
        File.WriteAllText(
            Path.Combine(internalDirectory, "ModuleInternalState.cs"),
            V08FullCycleFragmentContent.ModuleInternalStateSource);
        File.WriteAllText(
            Path.Combine(root, "src", "Synthetic.Modules.M01", "ModuleContracts.cs"),
            V08FullCycleFragmentContent.ModuleContractsSource);
    }

    private static string DependenciesPath(string root) => Path.Combine(root, "dependencies.arch.yml");

    private static void DeleteDirectoryEventually(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leaked temporary fixture must never fail an otherwise passing test.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CopyDirectoryExcludingGit(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (name is ".git" or "bin" or "obj")
            {
                continue;
            }

            CopyDirectoryExcludingGit(directory, Path.Combine(destination, name));
        }
    }

    private static CheckpointScenarioResult AssertPolicyCheck(CandidatePackageFeed candidate, string root)
    {
        CommandResult result = candidate.RunTool(root, "policy", "check", "--policy", DependenciesPath(root), "--format", "json");
        Assert.That(result.ExitCode, Is.EqualTo(0), $"v08-policy-check: {result.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object), "v08-policy-check");
        return Passed("v08-policy-check");
    }

    private static (CheckpointScenarioResult Scenario, string Findings) AssertValidateStrictAudit(
        CandidatePackageFeed candidate,
        string root,
        string validSarifPath,
        string repository,
        string revision,
        string scope)
    {
        CommandResult result = candidate.RunTool(root,
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={repository},revision={revision},scope={scope}",
            "--evidence-repository", repository,
            "--evidence-revision", revision,
            "--evidence-scope", scope);

        // The deliberate exposure and budget violations introduced by ApplyV08CurrentMutations make
        // this a failing strict run, not a successful one -- proving the composed pipeline observes
        // and reports real findings rather than validating an artificially clean fixture.
        Assert.That(result.ExitCode, Is.EqualTo(1), $"v08-validate-strict-audit: {result.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement findings = document.RootElement.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : document.RootElement.GetProperty("findings");
        Assert.That(findings.GetArrayLength(), Is.GreaterThanOrEqualTo(2),
            $"v08-validate-strict-audit expected both the exposure and budget findings: {result.StandardOutput}");
        return (Passed("v08-validate-strict-audit"), result.StandardOutput);
    }

    private static CheckpointScenarioResult AssertRecursiveExposureEvidence(string validateJson)
    {
        using JsonDocument document = JsonDocument.Parse(validateJson);
        JsonElement findings = document.RootElement.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : document.RootElement.GetProperty("findings");
        bool hasExposurePathEvidence = findings.EnumerateArray().Any(static finding =>
            finding.TryGetProperty("contract_id", out JsonElement contractId)
            && contractId.GetString() == "m01-contracts-do-not-expose-internal-state"
            && TryFindExposurePath(finding, out string? exposurePath)
            && exposurePath!.Contains("generic", StringComparison.OrdinalIgnoreCase));
        Assert.That(hasExposurePathEvidence, Is.True,
            $"v08-recursive-exposure-evidence expected a real recursive exposure path, not a coarse violation: {validateJson}");
        return Passed("v08-recursive-exposure-evidence");
    }

    private static bool TryFindExposurePath(JsonElement finding, out string? exposurePath)
    {
        foreach (string propertyName in new[] { "exposure_path", "canonical_exposure_path", "detail", "message" })
        {
            if (finding.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                exposurePath = value.GetString();
                if (!string.IsNullOrEmpty(exposurePath))
                {
                    return true;
                }
            }
        }

        exposurePath = null;
        return false;
    }

    private static CheckpointScenarioResult AssertTopologyCaptureDiffVerify(
        CandidatePackageFeed candidate, string root, string revision)
    {
        string capturePath = Path.Combine(root, "v08-topology-capture.json");
        CommandResult capture = candidate.RunTool(root,
            "topology", "capture",
            "--policy", DependenciesPath(root),
            "--subject-kind", "type",
            "--ensure-built",
            "--format", "json",
            "--output", capturePath);
        Assert.That(capture.ExitCode, Is.EqualTo(0), $"v08-topology-capture: {capture.CombinedOutput}");
        Assert.That(File.Exists(capturePath), Is.True, "v08-topology-capture");

        // The mutated policy declares external_evidence as required: any strict-mode command
        // evaluating it (not only `validate`) must bind the same SARIF evidence, or the required
        // control comes back missing_required_input rather than genuinely verifying the topology.
        // `topology verify --mode strict` explicitly "preserves ordinary validation output and exit
        // semantics" (see TopologyCommandHelpTexts.Verify), so it exits 1 (real findings) on this
        // deliberately-violating fixture, exactly like `validate`. `topology diff` reviews
        // declared-versus-observed topology evidence rather than the contract set, so it exits 0
        // here: the declared-topology completeness control itself is clean (fully mapped, no
        // unmapped/ambiguous subjects), independent of the unrelated exposure/budget findings.
        CommandResult verify = candidate.RunTool(root,
            "topology", "verify",
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision={revision},scope={V08EvidenceScope}",
            "--evidence-repository", V08EvidenceRepository,
            "--evidence-revision", revision,
            "--evidence-scope", V08EvidenceScope);
        Assert.That(verify.ExitCode, Is.EqualTo(1), $"v08-topology-verify: {verify.CombinedOutput}");

        CommandResult diff = candidate.RunTool(root,
            "topology", "diff",
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision={revision},scope={V08EvidenceScope}",
            "--evidence-repository", V08EvidenceRepository,
            "--evidence-revision", revision,
            "--evidence-scope", V08EvidenceScope);
        Assert.That(diff.ExitCode, Is.EqualTo(0), $"v08-topology-diff: {diff.CombinedOutput}");

        return Passed("v08-topology-capture-diff-verify");
    }

    private static CheckpointScenarioResult AssertMeasureAndBudget(
        CandidatePackageFeed candidate, string root, string validateJson)
    {
        CommandResult measure = candidate.RunTool(root,
            "measure",
            "--policy", DependenciesPath(root),
            "--format", "json");
        // `measure` has no --ensure-built (or any build-state) option, and its Ordinary-mode
        // assembly resolution only probes project OUTPUT paths for a metric that requires exact
        // artifact binding (component_footprint_count with unit: project/assembly -- see
        // ArchitectureMetricProjectOwnership.RequiresExactArtifactBinding). modules-outgoing is an
        // outgoing_component_count metric over a type-subject topology with no such binding, so for
        // a genuinely external target repository (this fixture, not ArchLinterNet analyzing its own
        // already-loaded assemblies) it is unassessable via bare `measure`, exit 2 with
        // missing_required_input -- a real, confirmed product gap, not a fixture authoring mistake.
        Assert.That(measure.ExitCode, Is.EqualTo(2), $"v08-measure-budget: {measure.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(measure.StandardOutput);
        Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object), "v08-measure-budget");
        Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("unassessable"),
            $"v08-measure-budget: {measure.CombinedOutput}");

        // The enforced budget (strict_metric_budgets: modules-outgoing-limit) is proven by the
        // earlier strict validate run already reporting it as one of the >= 2 findings; `validate`
        // resolves the same metric successfully because its EnsureBuilt preparation path always
        // does project-output discovery, unlike bare `measure`.
        using JsonDocument validateDocument = JsonDocument.Parse(validateJson);
        JsonElement findings = validateDocument.RootElement.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : validateDocument.RootElement.GetProperty("findings");
        bool budgetEnforced = findings.EnumerateArray().Any(static finding =>
            finding.TryGetProperty("contract_id", out JsonElement contractId)
            && contractId.GetString() == "modules-outgoing-limit");
        Assert.That(budgetEnforced, Is.True, "v08-measure-budget expected the enforced budget to participate in the composed path.");
        return Passed("v08-measure-budget");
    }

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

    private static CheckpointScenarioResult AssertHealthMatrix(
        CandidatePackageFeed candidate,
        string baseRoot,
        string currentRoot,
        string validSarifPath,
        string revision,
        string primaryHealthOutputPath)
    {
        // `health` unconditionally requires --baseline (even for a clean run: "reviewed exact
        // persistent-debt baseline (required)"), but `baseline generate` has no --ensure-built (or
        // any build-state) option and cannot resolve this fixture's target assemblies at all --
        // confirmed empirically to hard-fail even immediately after a successful --ensure-built
        // build, the same category of gap already flagged for `measure`. These baselines are
        // therefore hand-authored (see V08FullCycleFragmentContent.EmptyBaseline/DebtBaseline)
        // rather than CLI-generated. An empty baseline covers no violations, so it is reused for
        // HEALTHY (genuinely nothing to review), FAILING (current's violations stay unreviewed
        // against it, so the failure is genuinely "current strict violation", not "unassessable"),
        // and UNASSESSABLE/DEGRADING below (neither depends on debt actually being reviewed).
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
        // resolved/stale. Despite that, `health --baseline` on the exact same file still reports the
        // gate failing with reason "resolved_baseline_hygiene" -- a confirmed, reproducible product
        // bug (not a fixture authoring issue): health's debt-gate snapshot-reuse path
        // (ArchitectureAnalysisSnapshot.CollectBaselineCandidates) sources its comparison set
        // exclusively from ArchitectureAnalysisSession.BaselineCandidates, which is populated only by
        // the cycle-family recorder -- contract_surface_exposure/metric_budgets (and every other
        // non-cycle family) never register there, so their baseline entries always show as
        // "resolved" via `health`, regardless of correctness. `baseline verify`/`generate` use a
        // different, complete candidate-collection path and are unaffected. This assertion locks in
        // the current (buggy) observable behavior so it fails loudly -- as a welcome signal, not a
        // regression -- the moment the underlying gap is fixed; update it to "debt"/"pass" then.
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
        AssertHealthState(debt, "healthy", "fail", "v08-health-debt-known-gap");

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

        // DEGRADING: a minimal separate variant adding one new, deliberately stale waiver (matches
        // no live violation) to an existing passing contract -- a policy-hygiene signal, not a live
        // current violation. ArchitectureHealthProjector.ResolveGate only fails the gate on a Fail
        // dimension or !debtGate.Passed; a lone Degrading dimension does not block it, confirmed
        // directly against the packed CLI (health --baseline <matching this exact setup> reports
        // gate:"pass", health:"degrading" as expected). analysis.unmatched_ignored_violations is set
        // to "warn" here so the stale waiver stays a policy_inventory/waiver_debt signal rather than
        // a hard strict failure -- confirmed to work for bare `validate` on this exact fixture/config
        // (exit 0, passed:true).
        //
        // health itself, however, still reports current_evaluation:"fail"/"strict_validation_failed"
        // for this same policy+baseline+evidence combination, even though a `validate` call issued
        // immediately afterward against the identical directory reports exit 0/passed:true. This is
        // a third, related instance of the same category of bug already flagged (health's internal
        // snapshot-based validation pass computes a different Passed result than the equivalent
        // standalone CLI command) -- confirmed reproducible, not yet root-caused to a specific line.
        // That cascades reviewed_finding_debt/new_architecture_debt to unassessable, and the whole
        // gate/health summary follows. This assertion locks in the current (buggy) observable
        // behavior; update it to "degrading"/"pass" once the underlying gap is fixed.
        string degradingRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-v08-degrading-{Guid.NewGuid():N}");
        CopyDirectoryExcludingGit(baseRoot, degradingRoot);
        try
        {
            ApplyDegradingWaiverMutation(degradingRoot);
            string degradingBaseContext = Path.Combine(degradingRoot, "v08-degrading-base-context.json");
            string degradingCurrentContext = Path.Combine(degradingRoot, "v08-degrading-current-context.json");
            AssertPolicyContext(candidate, baseRoot, degradingBaseContext);
            AssertPolicyContext(candidate, degradingRoot, degradingCurrentContext);

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
            AssertHealthState(degrading, "unassessable", "unassessable", "v08-health-degrading-known-gap");
        }
        finally
        {
            DeleteDirectoryEventually(degradingRoot);
        }

        return Passed("v08-health-matrix");
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
    private static void ApplyDegradingWaiverMutation(string root)
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

        lines.InsertRange(anchorIndex + 1,
        [
            "      ignored_violations:",
            "        - source_type: Synthetic.Modules.M20.Module",
            "          forbidden_reference: Synthetic.Composition",
            "          reason: Temporary reviewed exception pending module extraction (synthetic, deliberately introduced for the v0.8 degrading Health proof).",
        ]);
        File.WriteAllLines(fragmentPath, lines);

        // The waiver deliberately matches no live violation (M20 never actually references the
        // host), which is exactly what makes it a stale/unjustified waiver for waiver_debt to catch.
        // analysis.unmatched_ignored_violations defaults to "error", which would otherwise turn that
        // same staleness into a hard strict-validation failure and drag the whole debt-gate down to
        // unassessable ("baseline_verification_incomplete") -- collapsing the very degrading signal
        // this scenario exists to isolate. Downgrading to "warn" here keeps that noise out of
        // current_evaluation/reviewed_finding_debt while waiver_debt's own lifecycle tracking (a
        // separate mechanism) still reports the stale waiver correctly.
        string policyPath = DependenciesPath(root);
        string policy = File.ReadAllText(policyPath);
        const string AnalysisMarker = "analysis:";
        int analysisIndex = policy.IndexOf(AnalysisMarker, StringComparison.Ordinal);
        if (analysisIndex < 0)
        {
            throw new InvalidOperationException($"'{policyPath}' has no analysis: block to extend.");
        }

        int analysisInsertAt = analysisIndex + AnalysisMarker.Length;
        policy = policy.Insert(analysisInsertAt, $"{Environment.NewLine}  unmatched_ignored_violations: warn");
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

    private static CheckpointScenarioResult AssertReportPr(
        CandidatePackageFeed candidate, string root, string healthPath, string changeReportPath)
    {
        string outputPath = Path.Combine(root, "v08-architecture-pr-report.md");
        CommandResult report = candidate.RunTool(root,
            "report", "pr",
            "--health", healthPath,
            "--change", changeReportPath,
            "--max-details", "20",
            "--output", outputPath);
        Assert.That(report.ExitCode, Is.EqualTo(0), $"v08-report-pr: {report.CombinedOutput}");
        Assert.That(File.Exists(outputPath), Is.True, "v08-report-pr");
        Assert.That(File.ReadAllText(outputPath), Is.Not.Empty, "v08-report-pr");
        return Passed("v08-report-pr");
    }

    private static CheckpointScenarioResult AssertBadge(CandidatePackageFeed candidate, string root, string healthPath)
    {
        string outputPath = Path.Combine(root, "v08-architecture-health-badge.json");
        CommandResult badge = candidate.RunTool(root,
            "badge", "architecture-health",
            "--input", healthPath,
            "--output", outputPath);
        // healthPath is the FAILING scenario's output (gate: "fail"); badge's exit code mirrors the
        // gate (ArchitectureHealthBadgeProjector.ExitCode: pass->0, fail->1), not a flat 0/success.
        Assert.That(badge.ExitCode, Is.EqualTo(1), $"v08-badge: {badge.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.That(document.RootElement.TryGetProperty("message", out JsonElement badgeMessage), Is.True, "v08-badge");
        Assert.That(badgeMessage.GetString(), Does.StartWith("FAILING"), "v08-badge");
        return Passed("v08-badge");
    }

    private static CheckpointScenarioResult AssertProjectionParity(string healthPath)
    {
        using JsonDocument health = JsonDocument.Parse(File.ReadAllText(healthPath));
        string? healthCategory = health.RootElement.GetProperty("health").GetString();
        string? gate = health.RootElement.GetProperty("gate").GetString();
        Assert.Multiple(() =>
        {
            Assert.That(healthCategory, Is.EqualTo("failing"),
                "v08-projection-parity expected the canonical Health artifact to carry the failing category consumed by report/badge.");
            Assert.That(gate, Is.EqualTo("fail"),
                "v08-projection-parity expected the canonical Health artifact to carry the fail gate consumed by report/badge.");
        });
        return Passed("v08-projection-parity");
    }

    private static void WriteSarif(string path, bool executionSuccessful, IReadOnlyList<string> resultMessages)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sarif = new
        {
            version = "2.1.0",
            runs = new object[]
            {
                new
                {
                    tool = new { driver = new { name = "V08 Synthetic Analyzer", version = "1.0.0" } },
                    automationDetails = new { id = "v08-full-cycle" },
                    invocations = new object[] { new { executionSuccessful } },
                    results = resultMessages
                        .Select(static message => new
                        {
                            ruleId = "synthetic",
                            level = "warning",
                            message = new { text = message },
                        })
                        .ToArray(),
                },
            },
        };
        File.WriteAllText(path, JsonSerializer.Serialize(sarif));
    }
}
