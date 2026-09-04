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
        CommandResult result = candidate.RunTool(root,
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
        CommandResult auditResult = candidate.RunTool(root,
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

            CommandResult result = candidate.RunTool(unmappedRoot,
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
        // Ordinary-mode assembly resolution only probes project OUTPUT paths for a metric that
        // requires exact artifact binding (component_footprint_count with unit: project/assembly --
        // see ArchitectureMetricProjectOwnership.RequiresExactArtifactBinding). modules-outgoing is
        // an outgoing_component_count metric over a type-subject topology with no such binding, so
        // for a genuinely external target repository (this fixture, not ArchLinterNet analyzing its
        // own already-loaded assemblies) bare `measure` -- with no build-state option at all -- was
        // unassessable, exit 2, missing_required_input: a confirmed product gap (`measure` and
        // `baseline generate`/`update`/`prune` categorically could not resolve
        // analysis.target_assemblies for any external target), now fixed by giving all four the same
        // --ensure-built/--no-restore/--configuration/--framework/--platform/--runtime surface
        // validate/health/gate/topology/baseline verify/diff already had.
        CommandResult measureWithoutEnsureBuilt = candidate.RunTool(root,
            "measure",
            "--policy", DependenciesPath(root),
            "--format", "json");
        Assert.That(measureWithoutEnsureBuilt.ExitCode, Is.EqualTo(2),
            $"v08-measure-budget (bare): {measureWithoutEnsureBuilt.CombinedOutput}");
        using (JsonDocument bareDocument = JsonDocument.Parse(measureWithoutEnsureBuilt.StandardOutput))
        {
            Assert.That(bareDocument.RootElement.GetProperty("status").GetString(), Is.EqualTo("unassessable"),
                $"v08-measure-budget (bare): {measureWithoutEnsureBuilt.CombinedOutput}");
        }

        CommandResult measure = candidate.RunTool(root,
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
