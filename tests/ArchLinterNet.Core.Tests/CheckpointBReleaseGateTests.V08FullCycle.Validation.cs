using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static (CheckpointScenarioResult Scenario, string Findings, string SarifPath) AssertValidateStrictAudit(
        CandidatePackageFeed candidate,
        string root,
        string validSarifPath,
        string repository,
        string revision,
        string scope)
    {
        string outputSarifPath = Path.Combine(root, "v08-validate-strict.sarif");
        CommandResult result = candidate.RunToolWithReusedRestore(root,
            "--policy", DependenciesPath(root),
            "--mode", "strict",
            "--ensure-built",
            "--report", "json=stdout",
            "--report", $"sarif={outputSarifPath}",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={repository},revision={revision},scope={scope}",
            "--evidence-repository", repository,
            "--evidence-revision", revision,
            "--evidence-scope", scope);

        // The deliberate exposure and budget violations introduced by ApplyV08CurrentMutations make
        // this a failing strict run, not a successful one -- proving the composed pipeline observes
        // and reports real findings rather than validating an artificially clean fixture.
        Assert.That(result.ExitCode, Is.EqualTo(1), $"v08-validate-strict-audit: {result.CombinedOutput}");
        Assert.That(File.Exists(outputSarifPath), Is.True, $"v08-validate-strict-audit (sarif): {result.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement findings = document.RootElement.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : document.RootElement.GetProperty("findings");
        Assert.That(findings.GetArrayLength(), Is.GreaterThanOrEqualTo(2),
            $"v08-validate-strict-audit expected both the exposure and budget findings: {result.StandardOutput}");

        // audit is a genuinely separate execution semantic from strict, not a second strict pass:
        // V08FullCycleFragmentContent.Contracts declares audit_assembly_dependency (advisory-only,
        // never evaluated by --mode strict) forbidding the modules' one legitimate, already-allowed
        // abstractions dependency -- something --mode strict, run above, never reports. Proving
        // --mode audit surfaces exactly that finding (and none of strict's exposure/budget findings,
        // which audit_assembly_dependency's own family never evaluates) demonstrates the packed CLI
        // actually runs the distinct audit contract set, not merely strict twice.
        CommandResult auditResult = candidate.RunToolWithReusedRestore(root,
            "--policy", DependenciesPath(root),
            "--mode", "audit",
            "--ensure-built",
            "--format", "json",
            "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={repository},revision={revision},scope={scope}",
            "--evidence-repository", repository,
            "--evidence-revision", revision,
            "--evidence-scope", scope);
        Assert.That(auditResult.ExitCode, Is.EqualTo(1), $"v08-validate-strict-audit (audit): {auditResult.CombinedOutput}");
        using JsonDocument auditDocument = JsonDocument.Parse(auditResult.StandardOutput);
        JsonElement auditFindings = auditDocument.RootElement.TryGetProperty("violations", out JsonElement auditViolations)
            ? auditViolations
            : auditDocument.RootElement.GetProperty("findings");
        bool hasAuditOnlyFinding = auditFindings.EnumerateArray().Any(static finding =>
            finding.TryGetProperty("contract_id", out JsonElement contractId)
            && (contractId.GetString() ?? string.Empty).StartsWith(
                "audit-modules-reference-abstractions-under-review", StringComparison.Ordinal));
        Assert.That(hasAuditOnlyFinding, Is.True,
            $"v08-validate-strict-audit expected the audit-only finding: {auditResult.StandardOutput}");
        bool hasStrictOnlyFindingUnderAudit = auditFindings.EnumerateArray().Any(static finding =>
            finding.TryGetProperty("contract_id", out JsonElement contractId)
            && (contractId.GetString() == "m01-contracts-do-not-expose-internal-state"
                || contractId.GetString() == "modules-outgoing-limit"));
        Assert.That(hasStrictOnlyFindingUnderAudit, Is.False,
            $"v08-validate-strict-audit: --mode audit must not evaluate strict-only contracts: {auditResult.StandardOutput}");
        return (Passed("v08-validate-strict-audit"), result.StandardOutput, outputSarifPath);
    }

    private static CheckpointScenarioResult AssertRecursiveExposureEvidence(string validateJson)
    {
        using JsonDocument document = JsonDocument.Parse(validateJson);
        JsonElement findings = document.RootElement.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : document.RootElement.GetProperty("findings");
        JsonElement? exposureFinding = findings.EnumerateArray()
            .Where(static finding => finding.TryGetProperty("contract_id", out JsonElement contractId)
                && contractId.GetString() == "m01-contracts-do-not-expose-internal-state")
            .Select(static finding => (JsonElement?)finding)
            .FirstOrDefault();
        Assert.That(exposureFinding, Is.Not.Null,
            $"v08-recursive-exposure-evidence expected the deliberate contract-surface-exposure violation: {validateJson}");

        // exposure_path/canonical_exposure_path are the real structured fields
        // (ArchitectureDiagnosticFormatter.ApplyContractSurfaceExposureCiFields) -- no fallback to
        // detail/message, whose free text (the fixture's own doc comment and the policy's reason both
        // say "generic wrapper") could satisfy a coarse substring check even if the structured path
        // itself silently disappeared.
        string? exposurePath = TryFindExposurePath(exposureFinding!.Value, out string? path) ? path : null;
        Assert.That(exposurePath, Is.Not.Null.And.Not.Empty,
            $"v08-recursive-exposure-evidence expected a non-empty exposure_path or canonical_exposure_path, not a "
            + $"fallback to free-text detail/message: {exposureFinding}");

        // ModuleContracts.GetSnapshot() returns IReadOnlyList<ModuleInternalState> (see
        // V08FullCycleFragmentContent.ModuleContractsSource). The canonical exposure path encodes
        // this as ordered positional segments -- declaring type, member, return, generic-argument
        // position -- rather than the literal terminal type name; assert the concrete segment chain
        // that proves the path actually walked into the generic wrapper, not just the word "generic".
        Assert.Multiple(() =>
        {
            Assert.That(exposurePath, Does.Contain("Synthetic.Modules.M01.ModuleContracts"),
                $"v08-recursive-exposure-evidence expected the exposure path to name the declaring source type: {exposurePath}");
            Assert.That(exposurePath, Does.Contain("Method:GetSnapshot"),
                $"v08-recursive-exposure-evidence expected the exposure path to name the source member GetSnapshot: {exposurePath}");
            Assert.That(exposurePath, Does.Contain("return"),
                $"v08-recursive-exposure-evidence expected the exposure path to walk through the method's return type: {exposurePath}");
            Assert.That(exposurePath, Does.Contain("generic_argument"),
                $"v08-recursive-exposure-evidence expected the exposure path to walk into the IReadOnlyList<> generic wrapper position: {exposurePath}");
        });

        return Passed("v08-recursive-exposure-evidence");
    }

    private static bool TryFindExposurePath(JsonElement finding, out string? exposurePath)
    {
        foreach (string propertyName in new[] { "exposure_path", "canonical_exposure_path" })
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
        CommandResult capture = candidate.RunToolWithReusedRestore(root,
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
        CommandResult verify = candidate.RunToolWithReusedRestore(root,
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

        CommandResult diff = candidate.RunToolWithReusedRestore(root,
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

    // Mandatory negative proof (issue #524): a required first-party subject the declared topology
    // no longer maps must fail closed to unassessable, not silently pass. A fresh scratch copy of
    // `root` (already carrying the full v0.8 fragment) swaps in
    // V08FullCycleFragmentContent.TopologyAndMetricsWithUnmappedSubject, which drops the
    // composition-host node (and its allowed_edges) while composition_host stays in
    // scope.selectors -- every type in that layer is now in scope but genuinely unmapped.
    private static CheckpointScenarioResult AssertTopologyUnmappedSubjectFailsClosed(
        CandidatePackageFeed candidate, string root, string revision)
    {
        string unmappedRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-v08-topology-unmapped-{Guid.NewGuid():N}");
        CopyDirectoryExcludingGit(root, unmappedRoot);
        try
        {
            string fragmentPath = Path.Combine(unmappedRoot, "fragments", "v08-full-cycle.yml");
            string fragment = string.Join(
                Environment.NewLine + Environment.NewLine,
                V08FullCycleFragmentContent.TopologyAndMetricsWithUnmappedSubject,
                V08FullCycleFragmentContent.Contracts,
                V08FullCycleFragmentContent.ExternalEvidence);
            File.WriteAllText(fragmentPath, fragment);

            string baselinePath = Path.Combine(unmappedRoot, "v08-topology-unmapped-baseline.arch.yml");
            File.WriteAllText(baselinePath, V08FullCycleFragmentContent.EmptyBaseline);

            CommandResult result = candidate.RunToolWithReusedRestore(unmappedRoot,
                "health",
                "--policy", DependenciesPath(unmappedRoot),
                "--baseline", baselinePath,
                "--mode", "strict",
                "--ensure-built",
                "--format", "json",
                "--external-evidence", $"id=v08-static-analysis,path={V08EvidenceRelativePath},repository={V08EvidenceRepository},revision={revision},scope={V08EvidenceScope}",
                "--evidence-repository", V08EvidenceRepository,
                "--evidence-revision", revision,
                "--evidence-scope", V08EvidenceScope);
            AssertHealthState(result, "unassessable", "unassessable", "v08-topology-unmapped");

            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            JsonElement dimensions = document.RootElement.GetProperty("dimensions");
            bool topologyIsUnassessable = dimensions.EnumerateArray().Any(dimension =>
                dimension.GetProperty("name").GetString() == "topology"
                && dimension.GetProperty("state").GetString() == "unassessable");
            Assert.That(topologyIsUnassessable, Is.True,
                $"v08-topology-unmapped expected the topology dimension itself to be unassessable, not another dimension: {result.StandardOutput}");

            return Passed("v08-topology-unmapped");
        }
        finally
        {
            DeleteDirectoryEventually(unmappedRoot);
        }
    }

    private static CheckpointScenarioResult AssertMeasureAndBudget(
        CandidatePackageFeed candidate, string root, string validateJson)
    {
        // docs/guides/single-tool-workflow.md section 8 documents `measure --ensure-built` (fixed
        // alongside this scenario: the guide previously omitted --ensure-built from measure's snippet
        // while every other command in the same guide carried it, which is exactly the "docs
        // authorize an unassessable path" gap #524 forbids). The bare-call check below is a
        // regression guard for the confirmed product gap that omission was papering over -- bare
        // `measure` against a genuinely external target repository (this fixture, not ArchLinterNet
        // analyzing its own already-loaded assemblies) was unassessable, exit 2, missing_required_
        // input, because Ordinary-mode assembly resolution only probes project OUTPUT paths for a
        // metric that requires exact artifact binding (component_footprint_count with unit:
        // project/assembly -- see ArchitectureMetricProjectOwnership.RequiresExactArtifactBinding),
        // and modules-outgoing has no such binding. It is not itself the documented path.
        CommandResult measureWithoutEnsureBuilt = candidate.RunToolWithReusedRestore(root,
            "measure",
            "--policy", DependenciesPath(root),
            "--format", "json");
        Assert.That(measureWithoutEnsureBuilt.ExitCode, Is.EqualTo(2),
            $"v08-measure-budget (bare, regression guard): {measureWithoutEnsureBuilt.CombinedOutput}");
        using (JsonDocument bareDocument = JsonDocument.Parse(measureWithoutEnsureBuilt.StandardOutput))
        {
            Assert.That(bareDocument.RootElement.GetProperty("status").GetString(), Is.EqualTo("unassessable"),
                $"v08-measure-budget (bare, regression guard): {measureWithoutEnsureBuilt.CombinedOutput}");
        }

        // The documented command (docs/guides/single-tool-workflow.md section 8, --ensure-built
        // included) evaluated for real.
        CommandResult measure = candidate.RunToolWithReusedRestore(root,
            "measure",
            "--policy", DependenciesPath(root),
            "--ensure-built",
            "--format", "json");
        Assert.That(measure.ExitCode, Is.EqualTo(0), $"v08-measure-budget: {measure.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(measure.StandardOutput);
        Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("complete"),
            $"v08-measure-budget: {measure.CombinedOutput}");
        JsonElement modulesOutgoing = document.RootElement.GetProperty("measurements")
            .EnumerateArray()
            .Single(measurement => measurement.GetProperty("id").GetString() == "modules-outgoing");
        Assert.That(modulesOutgoing.GetProperty("state").GetString(), Is.EqualTo("evaluable"),
            $"v08-measure-budget: {measure.CombinedOutput}");
        Assert.That(modulesOutgoing.GetProperty("value").GetInt32(), Is.EqualTo(1),
            $"v08-measure-budget: {measure.CombinedOutput}");

        // The enforced budget (strict_metric_budgets: modules-outgoing-limit) is proven by the
        // earlier strict validate run already reporting it as one of the >= 2 findings.
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
}
